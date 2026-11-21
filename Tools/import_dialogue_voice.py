# -*- coding: utf-8 -*-
"""Import teammate character dialogue zip into Assets/Resources/Voice/Dialogue.

Mapping uses xlsx 序号 (seq) from VO_M*_dialogue_{seq}_{speaker}.mp3 filenames,
then resolves each clip to (documentId, pageIndex) or (documentId, choiceIndex).

Run:
  py -3 Tools/import_dialogue_voice.py
  py -3 Tools/import_dialogue_voice.py "path/to/unity_audio_mp3.zip"
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

from import_narration_voice import (
    ASSET_PATH,
    XLSX_PATH,
    find_page_match,
    normalize_text,
    parse_asset,
)

ROOT = Path(__file__).resolve().parent.parent
VOICE_ROOT = ROOT / "Assets" / "Resources" / "Voice" / "Dialogue"
CACHE = ROOT / "Tools" / "_audio_cache" / "dialogue_import"
DEFAULT_ZIP = Path(
    r"z:\微信\xwechat_files\wxid_iex8clusq8ee32_2373\msg\file\2026-06\generated(2).zip"
)
VO_OLD_RE = re.compile(
    r"VO_(?P<ch>M\d+)_dialogue_(?P<seq>\d+)_(?P<speaker>[a-z0-9_]+)\.mp3$",
    re.IGNORECASE,
)
VO_GEN_RE = re.compile(
    r"VO_[A-Z]+_(?P<chapter>M\d+)_(?P<doc>.+?)_(?P<kind>[pc])(?P<page>\d+)_(?P<seq>\d+)\.mp3$",
    re.IGNORECASE,
)
DIALOGUE_VOICE_CATEGORIES = frozenset({"角色台词", "林渡", "玩家选项"})


def log(msg: str) -> None:
    print(msg, flush=True)


def parse_vo_filename(name: str) -> str | None:
    m = VO_GEN_RE.search(name)
    if m:
        return m.group("seq").zfill(4)
    m = VO_OLD_RE.search(name)
    if m:
        return m.group("seq").zfill(4)
    return None


def load_dialogue_rows() -> dict[str, dict]:
    wb = openpyxl.load_workbook(XLSX_PATH, read_only=True, data_only=True)
    ws = wb["全部配音文案"]
    headers = [c.value for c in next(ws.iter_rows(min_row=1, max_row=1))]
    idx = {h: i for i, h in enumerate(headers)}
    rows: dict[str, dict] = {}
    for r in ws.iter_rows(min_row=2, values_only=True):
        if not r or not r[idx["序号"]]:
            continue
        content_type = r[idx["内容类型"]] or ""
        category = r[idx["配音分类"]] or ""
        if content_type not in ("台词/旁白", "分支选项"):
            continue
        if category not in DIALOGUE_VOICE_CATEGORIES:
            continue
        seq = str(int(r[idx["序号"]])).zfill(4)
        page = r[idx["页码"]]
        rows[seq] = {
            "seq": seq,
            "chapterId": r[idx["章节ID"]],
            "documentId": r[idx["任务ID"]],
            "pageNumber": int(page) if page is not None else None,
            "text": r[idx["文案"]] or "",
            "contentType": content_type,
            "category": category,
            "speaker": r[idx["角色"]] or "",
        }
    return rows


def find_choice_match(
    documents: list[dict], document_id: str, label_text: str
) -> tuple[str, int] | None:
    target = normalize_text(label_text)
    for doc in documents:
        if doc.get("documentId") != document_id:
            continue
        for i, choice in enumerate(doc.get("choices") or []):
            if normalize_text(choice.get("label", "")) == target:
                return document_id, i
    return None


def extract_zip(zip_path: Path) -> Path:
    stamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    out = CACHE / stamp
    if out.is_dir():
        shutil.rmtree(out)
    out.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(zip_path) as zf:
        zf.extractall(out)
    for candidate in (out / "unity_audio_mp3", out / "generated", out):
        if candidate.is_dir() and any(candidate.rglob("*.mp3")):
            return candidate
    if any(out.rglob("*.mp3")):
        return out
    raise RuntimeError(f"No mp3 found in {zip_path}")


def main() -> int:
    zip_path = Path(sys.argv[1]) if len(sys.argv) > 1 else DEFAULT_ZIP
    if not zip_path.is_file():
        log(f"Zip not found: {zip_path}")
        return 1
    if not XLSX_PATH.is_file() or not ASSET_PATH.is_file():
        log("Missing 剧情配音文案.xlsx or Content1StoryDatabase.asset")
        return 1

    log(f"=== Import dialogue voice ===\nZip: {zip_path}")
    src_root = extract_zip(zip_path)
    dialogue_rows = load_dialogue_rows()
    documents = parse_asset(ASSET_PATH)

    mp3_by_seq: dict[str, Path] = {}
    for src in sorted(src_root.rglob("*.mp3")):
        seq = parse_vo_filename(src.name)
        if seq is None:
            log(f"  skip (bad name): {src.name}")
            continue
        if seq in mp3_by_seq:
            log(f"  warning: duplicate seq {seq}, keeping {mp3_by_seq[seq].name}")
            continue
        mp3_by_seq[seq] = src

    log(f"Zip clips: {len(mp3_by_seq)} (xlsx dialogue rows: {len(dialogue_rows)})")

    if VOICE_ROOT.is_dir():
        shutil.rmtree(VOICE_ROOT)
    VOICE_ROOT.mkdir(parents=True, exist_ok=True)

    copied_pages = 0
    copied_choices = 0
    unresolved: list[str] = []
    used_targets: set[str] = set()
    skipped_missing: list[str] = []

    for seq in sorted(dialogue_rows.keys()):
        row = dialogue_rows[seq]
        src = mp3_by_seq.get(seq)
        if src is None:
            skipped_missing.append(
                f"seq {seq}: missing mp3 doc={row['documentId']} cat={row['category']}"
            )
            continue

        if row["contentType"] == "分支选项":
            match = find_choice_match(documents, row["documentId"], row["text"])
            if match is None:
                unresolved.append(
                    f"seq {seq}: no choice match doc={row['documentId']} label={row['text'][:30]}"
                )
                continue
            document_id, choice_index = match
            dest_dir = VOICE_ROOT / document_id
            dest_dir.mkdir(parents=True, exist_ok=True)
            dest = dest_dir / f"choice_{choice_index:03d}.mp3"
            target_key = f"{document_id}|choice_{choice_index:03d}"
            if target_key in used_targets:
                unresolved.append(f"seq {seq}: duplicate target {target_key}")
                continue
            used_targets.add(target_key)
            shutil.copy2(src, dest)
            copied_choices += 1
            log(f"  {document_id} [choice {choice_index:03d}] <- {src.name}")
            continue

        if row["pageNumber"] is None:
            unresolved.append(f"seq {seq}: missing page number")
            continue

        match = find_page_match(
            documents, row["documentId"], row["pageNumber"], row["text"]
        )
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
        copied_pages += 1
        log(f"  {document_id} [{page_index:03d}] <- {src.name}")

    manifest_lines = [
        "# Dialogue voice import",
        f"# {datetime.now().isoformat(timespec='seconds')}",
        f"zip_clips={len(mp3_by_seq)}",
        f"xlsx_dialogue_rows={len(dialogue_rows)}",
        f"copied_pages={copied_pages}",
        f"copied_choices={copied_choices}",
        f"missing_in_zip={len(skipped_missing)}",
        f"unresolved={len(unresolved)}",
        "",
        "## Missing in zip",
    ]
    manifest_lines.extend(skipped_missing)
    manifest_lines.append("")
    manifest_lines.append("## Unresolved mapping")
    manifest_lines.extend(unresolved)

    extra_seqs = sorted(set(mp3_by_seq.keys()) - set(dialogue_rows.keys()))
    if extra_seqs:
        manifest_lines.append("")
        manifest_lines.append("## Zip clips without dialogue xlsx row")
        manifest_lines.extend(f"seq {seq}: {mp3_by_seq[seq].name}" for seq in extra_seqs)
    report = ROOT / "Tools" / "dialogue_import_report.txt"
    report.write_text("\n".join(manifest_lines), encoding="utf-8")

    log(
        f"\nDone. Copied {copied_pages} page clips + {copied_choices} choice clips "
        f"to {VOICE_ROOT.relative_to(ROOT)}"
    )
    if skipped_missing:
        log(f"Missing in zip: {len(skipped_missing)} dialogue rows (see report)")
    if unresolved:
        log(f"Unresolved: {len(unresolved)} (see {report.relative_to(ROOT)})")
        return 1
    if skipped_missing:
        log("Import finished with gaps. In Unity: Assets -> Refresh.")
        return 0
    log("All dialogue clips mapped. In Unity: Assets -> Refresh.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
