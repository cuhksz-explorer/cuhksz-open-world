# -*- coding: utf-8 -*-
"""Audit configured audio assets and scene bindings. Run: py -3 Tools/audit_audio_inventory.py"""
from __future__ import annotations

import re
from collections import Counter, defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
MUSIC = ROOT / "Assets" / "Music"
RES = ROOT / "Resources" / "Music"
SCENE_DIR = ROOT / "Assets" / "scene"
SETUP = ROOT / "Assets" / "code" / "Editor" / "SceneAudioBatchSetup.cs"

GUID_RE = re.compile(r"^guid: (.+)$", re.M)
PROFILE_RE = re.compile(
    r'\["([^"]+)"\] = Profile\("([^"]+)", "([^"]+)", ([0-9.]+)f, (true|false)\)'
)


def read_guid(meta: Path) -> str | None:
    if not meta.is_file():
        return None
    m = GUID_RE.search(meta.read_text(encoding="utf-8", errors="replace"))
    return m.group(1).strip() if m else None


def is_valid_unity_guid(guid: str) -> bool:
    return bool(re.fullmatch(r"[0-9a-f]{32}", guid or ""))


def scan_mp3(base: Path) -> list[dict]:
    out = []
    if not base.is_dir():
        return out
    for p in sorted(base.rglob("*.mp3")):
        rel = p.relative_to(base).as_posix()
        meta = Path(str(p) + ".meta")
        guid = read_guid(meta)
        out.append(
            {
                "rel": rel,
                "kb": p.stat().st_size // 1024,
                "guid": guid,
                "guid_ok": is_valid_unity_guid(guid or ""),
            }
        )
    return out


def meta_only_missing_mp3(base: Path) -> list[str]:
    missing = []
    for meta in base.rglob("*.mp3.meta"):
        mp3 = Path(str(meta)[:-5])
        if not mp3.is_file():
            missing.append(meta.relative_to(base).as_posix())
    return sorted(missing)


def parse_profiles() -> list[dict]:
    text = SETUP.read_text(encoding="utf-8")
    rows = []
    for m in PROFILE_RE.finditer(text):
        rows.append(
            {
                "scene": m.group(1),
                "bgm": m.group(2),
                "amb": m.group(3),
                "amb_vol": float(m.group(4)),
                "outdoor": m.group(5) == "true",
            }
        )
    return rows


def scan_scene_bindings() -> dict:
    stats = Counter()
    by_scene = defaultdict(lambda: {"bgm": 0, "amb": 0, "teleport": 0, "bgm_empty": 0, "amb_empty": 0})
    for path in list(SCENE_DIR.glob("*.unity")) + list(SCENE_DIR.glob("*.scene")):
        if "Backups" in path.parts:
            continue
        text = path.read_text(encoding="utf-8", errors="replace")
        name = path.stem
        bgm = len(re.findall(r"bgmClip:", text))
        amb = len(re.findall(r"m_Name: SceneAmbientAudio", text))
        tp = len(re.findall(r"teleportSound:", text))
        bgm_empty = len(re.findall(r"bgmClip: \{fileID: 0\}", text))
        amb_empty = len(re.findall(r"clip: \{fileID: 0\}", text))
        by_scene[name] = {
            "bgm": bgm,
            "amb": amb,
            "teleport": tp,
            "bgm_empty": bgm_empty,
            "amb_empty": amb_empty,
        }
        stats["bgm_refs"] += bgm
        stats["amb_nodes"] += amb
        stats["teleport_refs"] += tp
        stats["bgm_empty"] += bgm_empty
    return {"stats": stats, "by_scene": dict(by_scene)}


def main() -> None:
    music = scan_mp3(MUSIC)
    res = scan_mp3(RES)
    missing = meta_only_missing_mp3(MUSIC)
    bad_guid = [x for x in music if not x["guid_ok"]]
    profiles = parse_profiles()
    scene = scan_scene_bindings()

    print("AUDIO INVENTORY REPORT")
    print("=" * 60)
    print(f"Assets/Music mp3 files: {len(music)}")
    print(f"Resources/Music mp3 files: {len(res)}")
    print(f"Meta without mp3: {len(missing)}")
    print(f"Invalid GUID meta: {len(bad_guid)}")
    print()

    groups = {
        "BGM": [x for x in music if x["rel"].startswith("BGM/")],
        "AMB": [x for x in music if x["rel"].startswith("AMB/")],
        "SFX/UI": [x for x in music if x["rel"].startswith("SFX/UI/")],
        "SFX/Player": [x for x in music if x["rel"].startswith("SFX/Player/")],
        "SFX/System": [x for x in music if x["rel"].startswith("SFX/System/")],
        "SFX/World": [x for x in music if x["rel"].startswith("SFX/World/")],
        "Legacy(root)": [x for x in music if "/" not in x["rel"]],
    }

    for title, items in groups.items():
        print(f"[{title}] {len(items)} files")
        for it in items:
            flag = " GUID!" if not it["guid_ok"] else ""
            print(f"  - {it['rel']} ({it['kb']} KB){flag}")
        print()

    print("[SceneAudioBatchSetup profiles]")
    for p in profiles:
        print(
            f"  - {p['scene']}: BGM={p['bgm']}, AMB={p['amb']} vol={p['amb_vol']} outdoor={p['outdoor']}"
        )
    print()

    print("[Resources runtime mirrors]")
    for it in res:
        flag = " GUID!" if not it["guid_ok"] else ""
        print(f"  - {it['rel']} ({it['kb']} KB){flag}")
    print()

    print("[Scene binding summary]")
    st = scene["stats"]
    print(f"  bgmClip refs: {st['bgm_refs']} (empty {st['bgm_empty']})")
    print(f"  SceneAmbientAudio nodes: {st['amb_nodes']}")
    print(f"  teleportSound refs: {st['teleport_refs']}")
    print()

    print("[Per-scene quick view]")
    for name, info in sorted(scene["by_scene"].items()):
        if info["bgm"] or info["amb"] or info["teleport"]:
            print(
                f"  - {name}: bgm={info['bgm']} empty={info['bgm_empty']}, "
                f"amb={info['amb']}, teleport={info['teleport']}"
            )

    if bad_guid:
        print("\n[Invalid GUID samples]")
        for it in bad_guid[:15]:
            print(f"  - {it['rel']}: {it['guid']}")

    if missing:
        print("\n[Missing mp3 samples]")
        for m in missing[:15]:
            print(f"  - {m}")


if __name__ == "__main__":
    main()
