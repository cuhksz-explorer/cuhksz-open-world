# -*- coding: utf-8 -*-
"""Import teammate-generated audio zip into Assets/Music (checklist layout).

Preserves existing .meta GUIDs by overwriting .mp3 only.
Also mirrors runtime clips to Assets/Resources/Music.

Run:
  py -3 Tools/import_generated_audio.py
  py -3 Tools/import_generated_audio.py "path/to/generated.zip"
"""
from __future__ import annotations

import shutil
import sys
import zipfile
from datetime import datetime
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
MUSIC = ROOT / "Assets" / "Music"
RES = ROOT / "Assets" / "Resources" / "Music"
CACHE = ROOT / "Tools" / "_audio_cache" / "generated_import"
DEFAULT_ZIP = Path(
    r"z:\微信\xwechat_files\wxid_iex8clusq8ee32_2373\msg\file\2026-06\generated.zip"
)

# Legacy Chinese filenames still referenced by scenes / PlayerStepSound.
LEGACY_ALIASES = {
    "1(3).mp3": "BGM/BGM_uppergarden.mp3",
    "科幻传送.mp3": "SFX/System/SFX_System_teleport_scifi.mp3",
    "普通传送.mp3": "SFX/System/SFX_System_teleport_normal.mp3",
    "水泥地走路1.mp3": "SFX/Player/SFX_Player_walk_concrete_01.mp3",
    "水泥地走路2.mp3": "SFX/Player/SFX_Player_walk_concrete_02.mp3",
    "草地走路1.mp3": "SFX/Player/SFX_Player_walk_grass_01.mp3",
    "草地走路2.mp3": "SFX/Player/SFX_Player_walk_grass_02.mp3",
    "楼梯1.mp3": "SFX/Player/SFX_Player_walk_stair_01.mp3",
    "楼梯2.mp3": "SFX/Player/SFX_Player_walk_stair_02.mp3",
    "跑步1.mp3": "SFX/Player/SFX_Player_run_01.mp3",
    "跑步2.mp3": "SFX/Player/SFX_Player_run_02.mp3",
    "喘气.mp3": "SFX/Player/SFX_Player_pant_low_stamina.mp3",
    "鸟叫.mp3": "AMB/AMB_birds_day.mp3",
    "风.mp3": "AMB/AMB_wind.mp3",
    "小雨.mp3": "AMB/AMB_rain_light.mp3",
}

# Resources.Load paths (no extension) for runtime systems.
RESOURCES_MIRRORS = [
    ("SFX/UI/SFX_UI_click_default.mp3", "SFX_UI_click_default"),
    ("SFX/UI/SFX_UI_panel_open.mp3", "SFX_UI_panel_open"),
    ("SFX/UI/SFX_UI_panel_close.mp3", "SFX_UI_panel_close"),
    ("SFX/UI/SFX_UI_success.mp3", "SFX_UI_success"),
    ("SFX/UI/SFX_UI_error.mp3", "SFX_UI_error"),
    ("SFX/UI/SFX_UI_notification.mp3", "SFX_UI_notification"),
    ("SFX/System/SFX_System_teleport_scifi.mp3", "SFX_System_teleport_scifi"),
    ("SFX/System/SFX_System_teleport_normal.mp3", "SFX_System_teleport_normal"),
    ("SFX/System/SFX_System_coin_gain.mp3", "SFX_System_coin_gain"),
    ("SFX/System/SFX_System_quest_chain_complete.mp3", "SFX_System_quest_chain_complete"),
    ("SFX/System/SFX_System_death_stinger.mp3", "SFX_System_death_stinger"),
    ("SFX/System/SFX_System_revive.mp3", "SFX_System_revive"),
    ("SFX/System/SFX_System_dialogue_page.mp3", "SFX_System_dialogue_page"),
    ("BGM/BGM_uppergarden.mp3", "BGM_uppergarden"),
    ("BGM/BGM_undergarden_patriotic.mp3", "BGM_undergarden_patriotic"),
]


def log(msg: str) -> None:
    print(msg, flush=True)


def extract_zip(zip_path: Path) -> Path:
    if not zip_path.is_file():
        raise FileNotFoundError(f"Zip not found: {zip_path}")

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
    raise RuntimeError(f"No mp3 found after extracting {zip_path}")


def copy_tree(src_root: Path, dest_root: Path) -> tuple[int, int]:
    """Copy mp3 files; return (copied, skipped)."""
    copied = 0
    skipped = 0
    for src in sorted(src_root.rglob("*.mp3")):
        rel = src.relative_to(src_root)
        if rel.parts and rel.parts[0].lower() == "generated":
            rel = Path(*rel.parts[1:])
        dest = dest_root / rel
        dest.parent.mkdir(parents=True, exist_ok=True)
        if dest.is_file() and dest.stat().st_size == src.stat().st_size:
            skipped += 1
            continue
        shutil.copy2(src, dest)
        copied += 1
        log(f"  + {rel.as_posix()}")
    return copied, skipped


def copy_alias(src_rel: str, dest_name: str) -> bool:
    src = MUSIC / src_rel
    dest = MUSIC / dest_name
    if not src.is_file():
        log(f"  skip alias (missing src): {dest_name} <- {src_rel}")
        return False
    shutil.copy2(src, dest)
    log(f"  alias {dest_name} <- {src_rel}")
    return True


def mirror_resources() -> int:
    RES.mkdir(parents=True, exist_ok=True)
    n = 0
    for rel, res_name in RESOURCES_MIRRORS:
        src = MUSIC / rel
        if not src.is_file():
            continue
        dest = RES / f"{res_name}.mp3"
        shutil.copy2(src, dest)
        n += 1
        log(f"  resources {res_name}.mp3")
    return n


def main() -> int:
    zip_path = Path(sys.argv[1]) if len(sys.argv) > 1 else DEFAULT_ZIP
    log(f"=== Import generated audio ===\nZip: {zip_path}")

    src_root = extract_zip(zip_path)
    log(f"Extracted to: {src_root}")

    MUSIC.mkdir(parents=True, exist_ok=True)
    log("\n=== Copy checklist audio to Assets/Music ===")
    copied, skipped = copy_tree(src_root, MUSIC)
    log(f"Copied: {copied}, unchanged: {skipped}")

    log("\n=== Legacy filename aliases ===")
    alias_ok = 0
    for dest_name, src_rel in LEGACY_ALIASES.items():
        if copy_alias(src_rel, dest_name):
            alias_ok += 1

    log("\n=== Resources/Music mirrors ===")
    res_n = mirror_resources()

    log(
        f"\nDone. {copied} mp3 updated, {alias_ok} legacy aliases, {res_n} Resources mirrors.\n"
        "In Unity: Assets -> Refresh, then run:\n"
        "  Tools/场景配置/一键配置所有场景音效（uppergarden 标准）"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
