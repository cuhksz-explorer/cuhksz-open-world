# -*- coding: utf-8 -*-
"""Import teammate narration zip into Assets/Resources/Voice/Narration.

Mapping uses xlsx 序号 (seq) as the authoritative key, then resolves each clip to
(documentId, pageIndex) inside Content1StoryDatabase by matching page text.

Run:
  py -3 Tools/import_narration_voice.py
  py -3 Tools/import_narration_voice.py "path/to/generated.zip"
"""
from __future__ import annotations

import re
import shutil
import sys
import zipfile
from datetime import datetime
from pathlib import Path

try:
    import openpyxl
except ImportError:
    import subprocess

    subprocess.check_call([sys.executable, "-m", "pip", "install", "openpyxl", "-q"])
    import openpyxl

ROOT = Path(__file__).resolve().parent.parent
ASSET_PATH = ROOT / "Assets" / "code" / "GameQuestSystem" / "Content1StoryDatabase.asset"


def resolve_story_workbook_path() -> Path:
    for candidate in (
        ROOT / "egg" / "workbooks" / "剧情配音文案.xlsx",
        ROOT / "剧情配音文案.xlsx",
    ):
        if candidate.is_file():
            return candidate
    return ROOT / "剧情配音文案.xlsx"


XLSX_PATH = resolve_story_workbook_path()
VOICE_ROOT = ROOT / "Assets" / "Resources" / "Voice" / "Narration"
CACHE = ROOT / "Tools" / "_audio_cache" / "narration_import"
DEFAULT_ZIP = Path(
    r"z:\微信\xwechat_files\wxid_iex8clusq8ee32_2373\msg\file\2026-06\generated(1).zip"
)
SEQ_RE = re.compile(r"_p\d+_(?P<seq>\d+)\.mp3$", re.IGNORECASE)


def log(msg: str) -> None:
    print(msg, flush=True)


def decode_yaml_string(value: str) -> str:
    value = (value or "").strip()
    if not value:
        return ""
    if (value.startswith('"') and value.endswith('"')) or (
        value.startswith("'") and value.endswith("'")
    ):
        value = value[1:-1]
    try:
        return value.encode("utf-8").decode("unicode_escape")
    except UnicodeDecodeError:
        return value


def normalize_text(text: str) -> str:
    text = (text or "").replace("\r\n", "\n").replace("\r", "\n")
    return re.sub(r"\s+", " ", text).strip()


def parse_asset(path: Path) -> list[dict]:
    text = path.read_text(encoding="utf-8")
    lines = text.splitlines()

    in_documents = False
    doc = None
    section = None
    page = None
    choice = None
    documents: list[dict] = []

    doc_field_re = re.compile(r"^    (\w+): (.*)$")
    page_field_re = re.compile(r"^      (\w+): (.*)$")
    list_item_re = re.compile(r"^  - (\w+): (.*)$")
    page_start_re = re.compile(r"^    - pageNumber: (\d+)$")
    choice_start_re = re.compile(r"^    - label: (.*)$")

    for line in lines:
        if line.strip() == "documents:":
            in_documents = True
            continue
        if not in_documents:
            continue
        if line and not line.startswith("  "):
            break

        if line.startswith("  - documentId:"):
            if doc:
                documents.append(doc)
            doc = {"pages": [], "choices": []}
            section = None
            doc["documentId"] = decode_yaml_string(line.split(":", 1)[1])
            continue

        if doc is None:
            continue

        m = list_item_re.match(line)
        if m and m.group(1) not in ("pageNumber", "label"):
            key, val = m.group(1), m.group(2)
            if key != "documentId":
                doc[key] = decode_yaml_string(val)
            continue

        if line.strip() == "pages:":
            section = "pages"
            continue
        if line.strip() == "choices:":
            section = "choices"
            continue

        m = doc_field_re.match(line)
        if m and section is None:
            key, val = m.group(1), m.group(2)
            doc[key] = decode_yaml_string(val)
            continue

        if section == "pages":
            m = page_start_re.match(line)
            if m:
                page = {"pageNumber": int(m.group(1)), "speakerName": "", "text": ""}
                doc["pages"].append(page)
                continue
            m = page_field_re.match(line)
            if m and page is not None:
                key, val = m.group(1), m.group(2)
                page[key] = decode_yaml_string(val)
                continue

        if section == "choices":
            m = choice_start_re.match(line)
            if m:
                choice = {"label": decode_yaml_string(m.group(1)), "targetDocumentId": ""}
                doc["choices"].append(choice)
                continue
            m = page_field_re.match(line)
            if m and choice is not None:
                key, val = m.group(1), m.group(2)
                choice[key] = decode_yaml_string(val)
                continue

    if doc:
        documents.append(doc)

    return documents


def load_narration_rows() -> dict[str, dict]:
    wb = openpyxl.load_workbook(XLSX_PATH, read_only=True, data_only=True)
    ws = wb["旁白"]
    headers = [c.value for c in next(ws.iter_rows(min_row=1, max_row=1))]
    idx = {h: i for i, h in enumerate(headers)}
    rows: dict[str, dict] = {}
    for r in ws.iter_rows(min_row=2, values_only=True):
        if not r or not r[idx["章节ID"]]:
            continue
        seq = str(r[idx["序号"]]).zfill(4)
        rows[seq] = {
            "seq": seq,
            "chapterId": r[idx["章节ID"]],
            "documentId": r[idx["任务ID"]],
            "pageNumber": int(r[idx["页码"]]),
            "text": r[idx["文案"]] or "",
        }
    return rows


def find_page_match(
    documents: list[dict], document_id: str, page_number: int, text: str
) -> tuple[str, int] | None:
    target = normalize_text(text)
    matches: list[tuple[str, int, int]] = []

    for doc in documents:
        if doc.get("documentId") != document_id:
            continue
        pages = doc.get("pages") or []
        for i, page in enumerate(pages):
            if int(page.get("pageNumber") or 0) != page_number:
                continue
            if normalize_text(page.get("text", "")) != target:
                continue
            matches.append((document_id, i, len(pages)))

    if not matches:
        return None
    if len(matches) == 1:
        return matches[0][0], matches[0][1]

    matches.sort(key=lambda item: item[2], reverse=True)
    return matches[0][0], matches[0][1]


def extract_zip(zip_path: Path) -> Path:
    stamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    out = CACHE / stamp
    if out.is_dir():
        shutil.rmtree(out)
    out.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(zip_path) as zf:
        zf.extractall(out)
    for candidate in (out / "generated", out):
        if candidate.is_dir() and any(candidate.rglob("*.mp3")):
            return candidate
    raise RuntimeError(f"No mp3 found in {zip_path}")


def main() -> int:
    zip_path = Path(sys.argv[1]) if len(sys.argv) > 1 else DEFAULT_ZIP
    if not zip_path.is_file():
        log(f"Zip not found: {zip_path}")
        return 1
    if not XLSX_PATH.is_file() or not ASSET_PATH.is_file():
        log("Missing 剧情配音文案.xlsx or Content1StoryDatabase.asset")
        return 1

    log(f"=== Import narration voice ===\nZip: {zip_path}")
    src_root = extract_zip(zip_path)
    narration_rows = load_narration_rows()
    documents = parse_asset(ASSET_PATH)

    mp3_by_seq: dict[str, Path] = {}
    for src in sorted(src_root.rglob("*.mp3")):
        m = SEQ_RE.search(src.name)
        if not m:
            log(f"  skip (bad name): {src.name}")
            continue
        mp3_by_seq[m.group("seq")] = src

    if len(mp3_by_seq) != len(narration_rows):
        log(f"Warning: mp3={len(mp3_by_seq)} xlsx={len(narration_rows)}")

    if VOICE_ROOT.is_dir():
        shutil.rmtree(VOICE_ROOT)
    VOICE_ROOT.mkdir(parents=True, exist_ok=True)

    copied = 0
    unresolved: list[str] = []
    used_targets: set[str] = set()

    for seq in sorted(narration_rows.keys()):
        row = narration_rows[seq]
        src = mp3_by_seq.get(seq)
        if src is None:
            unresolved.append(f"missing mp3 for seq {seq}")
            continue

        match = find_page_match(documents, row["documentId"], row["pageNumber"], row["text"])
        if match is None:
            unresolved.append(
                f"seq {seq}: no page match doc={row['documentId']} page={row['pageNumber']}"
            )
            continue

        document_id, page_index = match
        dest_dir = VOICE_ROOT / document_id
        dest_dir.mkdir(parents=True, exist_ok=True)
        dest = dest_dir / f"{page_index:03d}.mp3"
        target_key = f"{document_id}|{page_index:03d}"
        if target_key in used_targets:
            unresolved.append(f"seq {seq}: duplicate target {target_key}")
            continue
        used_targets.add(target_key)
        shutil.copy2(src, dest)
        copied += 1
        log(f"  {document_id} [{page_index:03d}] <- {src.name}")

    manifest_lines = [
        "# Narration voice import",
        f"# {datetime.now().isoformat(timespec='seconds')}",
        f"copied={copied}",
        f"unresolved={len(unresolved)}",
        "",
    ]
    manifest_lines.extend(unresolved)
    report = ROOT / "Tools" / "narration_import_report.txt"
    report.write_text("\n".join(manifest_lines), encoding="utf-8")

    log(f"\nDone. Copied {copied}/{len(narration_rows)} clips to {VOICE_ROOT.relative_to(ROOT)}")
    if unresolved:
        log(f"Unresolved: {len(unresolved)} (see {report.relative_to(ROOT)})")
        return 1
    log("All narration clips mapped. In Unity: Assets -> Refresh.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
