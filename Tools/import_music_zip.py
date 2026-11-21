# -*- coding: utf-8 -*-
"""Import teammate Music.zip (UI + vehicle clips) into Assets/Music.

Preserves existing .meta GUIDs when overwriting checklist slots.
Also copies full source library under Assets/Music/Imports/MusicZip/.

Run:
  py -3 Tools/import_music_zip.py
  py -3 Tools/import_music_zip.py "path/to/Music.zip"
"""
from __future__ import annotations

import re
import shutil
import sys
import zipfile
from datetime import datetime
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
MUSIC = ROOT / "Assets" / "Music"
RES = ROOT / "Assets" / "Resources" / "Music"
CACHE = ROOT / "Tools" / "_audio_cache" / "music_zip_import"
DEFAULT_ZIP = Path(
    r"z:\微信\xwechat_files\wxid_iex8clusq8ee32_2373\msg\file\2026-06\Music.zip"
)
IMPORT_ROOT = MUSIC / "Imports" / "MusicZip"

# zip inner path -> Assets/Music relative checklist path
VEHICLE_MAP = {
    "audios-converted-20260613-171811/Car_open_door-converted.mp3": "SFX/System/SFX_System_vehicle_enter.mp3",
    "audios-converted-20260613-171811/Car_close_door.mp3": "SFX/System/SFX_System_vehicle_exit.mp3",
    "audios-converted-20260613-171811/Car_start_engine-converted.mp3": "SFX/System/SFX_System_vehicle_summon.mp3",
    "audios-converted-20260613-171811/Car_driving_0.mp3": "SFX/System/SFX_System_vehicle_engine_idle.mp3",
    "audios-converted-20260613-171811/Car_driving_1-converted.mp3": "SFX/System/SFX_System_vehicle_engine_accel.mp3",
    "audios-converted-20260613-171811/Car_stopping.mp3": "SFX/System/SFX_System_vehicle_brake.mp3",
    "audios-converted-20260613-171811/Car_mingdi-converted.mp3": "SFX/System/SFX_System_vehicle_horn.mp3",
    "audios-converted-20260613-171811/Car_crash_0.mp3": "SFX/System/SFX_System_vehicle_collision.mp3",
    "audios-converted-20260613-171811/Car_crash_1-converted.mp3": "SFX/System/SFX_System_bus_door.mp3",
}

VEHICLE_MAP_EXTRA = {
    "audios-converted-20260613-171811/Car_open_door-converted.mp3": "SFX/System/SFX_System_vehicle_shop_open.mp3",
    "audios-converted-20260613-171811/Car_close_door.mp3": "SFX/System/SFX_System_vehicle_shop_close.mp3",
    "audios-converted-20260613-171811/Car_start_engine-converted.mp3": "SFX/System/SFX_System_vehicle_recall.mp3",
    "UI_audio/click2-converted.mp3": "SFX/System/SFX_System_vehicle_select.mp3",
}

UI_MAP = {
    "UI_audio/click1-converted.mp3": "SFX/UI/SFX_UI_click_default.mp3",
    "UI_audio/click2-converted.mp3": "SFX/UI/SFX_UI_click_primary.mp3",
    "UI_audio/click3-converted.mp3": "SFX/UI/SFX_UI_click_secondary.mp3",
    "UI_audio/click4-converted.mp3": "SFX/UI/SFX_UI_click_tab.mp3",
    "UI_audio/click5-converted.mp3": "SFX/UI/SFX_UI_click_slider.mp3",
    "UI_audio/mouseclick1-converted.mp3": "SFX/UI/SFX_UI_login_click_enter.mp3",
    "UI_audio/mouserelease1-converted.mp3": "SFX/UI/SFX_UI_esc_back.mp3",
    "UI_audio/rollover1-converted.mp3": "SFX/UI/SFX_UI_hover.mp3",
    "UI_audio/rollover2-converted.mp3": "SFX/UI/SFX_UI_notification.mp3",
    "UI_audio/rollover3-converted.mp3": "SFX/UI/SFX_UI_map_select_point.mp3",
    "UI_audio/switch1-converted.mp3": "SFX/UI/SFX_UI_click_toggle_on.mp3",
    "UI_audio/switch2-converted.mp3": "SFX/UI/SFX_UI_click_toggle_off.mp3",
    "UI_audio/switch3-converted.mp3": "SFX/UI/SFX_UI_panel_open.mp3",
    "UI_audio/switch4-converted.mp3": "SFX/UI/SFX_UI_panel_close.mp3",
    "UI_audio/switch5-converted.mp3": "SFX/UI/SFX_UI_success.mp3",
    "UI_audio/switch6-converted.mp3": "SFX/UI/SFX_UI_error.mp3",
    "UI_audio/switch7-converted.mp3": "SFX/UI/SFX_UI_settings_open.mp3",
    "UI_audio/switch8-converted.mp3": "SFX/UI/SFX_UI_settings_close.mp3",
    "UI_audio/switch9-converted.mp3": "SFX/UI/SFX_UI_quest_open.mp3",
    "UI_audio/switch10-converted.mp3": "SFX/UI/SFX_UI_quest_close.mp3",
    "UI_audio/switch11-converted.mp3": "SFX/UI/SFX_UI_map_open.mp3",
    "UI_audio/switch12-converted.mp3": "SFX/UI/SFX_UI_map_close.mp3",
    "UI_audio/switch13-converted.mp3": "SFX/UI/SFX_UI_phone_open.mp3",
    "UI_audio/switch14-converted.mp3": "SFX/UI/SFX_UI_phone_close.mp3",
    "UI_audio/switch15-converted.mp3": "SFX/UI/SFX_UI_login_success.mp3",
    "UI_audio/switch16-converted.mp3": "SFX/UI/SFX_UI_login_fail.mp3",
    "UI_audio/switch17-converted.mp3": "SFX/UI/SFX_UI_register_confirm.mp3",
    "UI_audio/switch18-converted.mp3": "SFX/UI/SFX_UI_keybind_open.mp3",
    "UI_audio/switch19-converted.mp3": "SFX/UI/SFX_UI_keybind_rebind.mp3",
    "UI_audio/switch20-converted.mp3": "SFX/UI/SFX_UI_keybind_conflict.mp3",
    "UI_audio/switch21-converted.mp3": "SFX/UI/SFX_UI_legal_doc_open.mp3",
    "UI_audio/switch22-converted.mp3": "SFX/UI/SFX_UI_legal_doc_close.mp3",
    "UI_audio/switch23-converted.mp3": "SFX/UI/SFX_UI_curve_panel_open.mp3",
    "UI_audio/switch24-converted.mp3": "SFX/UI/SFX_UI_curve_panel_close.mp3",
    "UI_audio/switch25-converted.mp3": "SFX/UI/SFX_UI_death_panel_open.mp3",
    "UI_audio/switch26-converted.mp3": "SFX/UI/SFX_UI_revive_click.mp3",
    "UI_audio/switch27-converted.mp3": "SFX/UI/SFX_UI_logout.mp3",
    "UI_audio/switch28-converted.mp3": "SFX/UI/SFX_UI_register_random_name.mp3",
    "UI_audio/switch29-converted.mp3": "SFX/UI/SFX_UI_character_select.mp3",
    "UI_audio/switch30-converted.mp3": "SFX/UI/SFX_UI_map_confirm_teleport.mp3",
    "UI_audio/switch31-converted.mp3": "SFX/UI/SFX_UI_phone_app_open.mp3",
    "UI_audio/switch32-converted.mp3": "SFX/UI/SFX_UI_phone_app_close.mp3",
    "UI_audio/switch33-converted.mp3": "SFX/UI/SFX_UI_quality_change.mp3",
    "UI_audio/switch34-converted.mp3": "SFX/UI/SFX_UI_time_pause.mp3",
    "UI_audio/switch35-converted.mp3": "SFX/UI/SFX_UI_time_resume.mp3",
    "UI_audio/switch36-converted.mp3": "SFX/UI/SFX_UI_time_reset.mp3",
    "UI_audio/switch37-converted.mp3": "SFX/UI/SFX_UI_quest_page_turn.mp3",
    "UI_audio/rollover4-converted.mp3": "SFX/UI/SFX_UI_credits_trigger.mp3",
}

LEGACY_ALIASES = {
    "科幻传送.mp3": "SFX/System/SFX_System_teleport_scifi.mp3",
    "普通传送.mp3": "SFX/System/SFX_System_teleport_normal.mp3",
}

RESOURCES_MIRRORS = [
    ("SFX/UI/SFX_UI_click_default.mp3", "SFX_UI_click_default"),
    ("SFX/UI/SFX_UI_panel_open.mp3", "SFX_UI_panel_open"),
    ("SFX/UI/SFX_UI_panel_close.mp3", "SFX_UI_panel_close"),
    ("SFX/UI/SFX_UI_success.mp3", "SFX_UI_success"),
    ("SFX/UI/SFX_UI_error.mp3", "SFX_UI_error"),
    ("SFX/UI/SFX_UI_notification.mp3", "SFX_UI_notification"),
    ("SFX/System/SFX_System_teleport_scifi.mp3", "SFX_System_teleport_scifi"),
    ("SFX/System/SFX_System_teleport_normal.mp3", "SFX_System_teleport_normal"),
]


def log(msg: str) -> None:
    print(msg, flush=True)


def norm_zip_path(name: str) -> str:
    return name.replace("\\", "/")


def clean_name(name: str) -> str:
    base = Path(name).name
    base = re.sub(r"-converted(?=\.mp3$)", "", base, flags=re.I)
    return base


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
    return out


def copy_mapped(extract_root: Path, mapping: dict[str, str]) -> tuple[int, int]:
    copied = 0
    skipped = 0
    for src_rel, dest_rel in mapping.items():
        src = extract_root / Path(src_rel)
        if not src.is_file():
            log(f"  skip missing: {src_rel}")
            continue
        dest = MUSIC / dest_rel
        dest.parent.mkdir(parents=True, exist_ok=True)
        if dest.is_file() and dest.stat().st_size == src.stat().st_size:
            skipped += 1
            continue
        shutil.copy2(src, dest)
        copied += 1
        log(f"  map {src_rel} -> {dest_rel}")
    return copied, skipped


def archive_all(extract_root: Path) -> int:
    n = 0
    for src in sorted(extract_root.rglob("*")):
        if not src.is_file():
            continue
        if src.suffix.lower() not in {".mp3", ".wav", ".ogg"}:
            continue
        rel = src.relative_to(extract_root)
        parts = list(rel.parts)
        if parts and parts[0].startswith("audios-converted"):
            bucket = "Vehicle"
            fname = clean_name(parts[-1])
            dest = IMPORT_ROOT / bucket / fname
        elif parts and parts[0] == "UI_audio":
            bucket = "UI"
            fname = clean_name(parts[-1])
            dest = IMPORT_ROOT / bucket / fname
        else:
            dest = IMPORT_ROOT / "Other" / rel.name
        dest.parent.mkdir(parents=True, exist_ok=True)
        if dest.is_file() and dest.stat().st_size == src.stat().st_size:
            continue
        shutil.copy2(src, dest)
        n += 1
    return n


def mirror_resources() -> int:
    RES.mkdir(parents=True, exist_ok=True)
    n = 0
    for rel, res_name in RESOURCES_MIRRORS:
        src = MUSIC / rel
        if not src.is_file():
            continue
        shutil.copy2(src, RES / f"{res_name}.mp3")
        n += 1
    return n


def copy_alias(src_rel: str, dest_name: str) -> bool:
    src = MUSIC / src_rel
    dest = MUSIC / dest_name
    if not src.is_file():
        return False
    shutil.copy2(src, dest)
    return True


def main() -> int:
    zip_path = Path(sys.argv[1]) if len(sys.argv) > 1 else DEFAULT_ZIP
    log(f"=== Import Music.zip ===\nZip: {zip_path}")

    extract_root = extract_zip(zip_path)
    log(f"Extracted to: {extract_root}")

    MUSIC.mkdir(parents=True, exist_ok=True)

    log("\n=== Vehicle -> checklist SFX ===")
    v_copied, v_skipped = copy_mapped(extract_root, VEHICLE_MAP)
    log(f"Vehicle mapped: {v_copied} updated, {v_skipped} unchanged")

    log("\n=== Vehicle extra slots ===")
    ve_copied, _ = copy_mapped(extract_root, VEHICLE_MAP_EXTRA)
    log(f"Vehicle extra: {ve_copied} updated")

    log("\n=== UI -> checklist SFX ===")
    u_copied, u_skipped = copy_mapped(extract_root, UI_MAP)
    log(f"UI mapped: {u_copied} updated, {u_skipped} unchanged")

    log("\n=== Archive full library ===")
    archived = archive_all(extract_root)
    log(f"Archived clips: {archived}")

    log("\n=== Resources mirrors ===")
    res_n = mirror_resources()
    log(f"Resources mirrors: {res_n}")

    log(
        f"\nDone. vehicle={v_copied + ve_copied}, ui={u_copied}, archived={archived}, resources={res_n}\n"
        "In Unity: Assets -> Refresh"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
