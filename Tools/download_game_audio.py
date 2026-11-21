# -*- coding: utf-8 -*-
"""Download CC0 game audio (Kenney) and map into Unity Assets/Music.

Licenses: Kenney packs CC0 (https://kenney.nl)

Run: py -3 Tools/download_game_audio.py
"""
from __future__ import annotations

import re
import shutil
import subprocess
import sys
import urllib.request
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
MUSIC = ROOT / "Assets" / "Music"
CACHE = ROOT / "Tools" / "_audio_cache"

KENNEY_PACKS = {
    "interface": "https://opengameart.org/sites/default/files/kenney_interfaceSounds.zip",
    "impact": "https://kenney.nl/media/pages/assets/impact-sounds/87b4ddecda-1677589768/kenney_impact-sounds.zip",
    "scifi": "https://kenney.nl/media/pages/assets/sci-fi-sounds/6b296f9ecf-1677589334/kenney_sci-fi-sounds.zip",
    "digital": "https://kenney.nl/media/pages/assets/digital-audio/216eac4753-1677590265/kenney_digital-audio.zip",
    "jingles": "https://kenney.nl/media/pages/assets/music-jingles/f37e530b9e-1677590399/kenney_music-jingles.zip",
    "rpg": "https://opengameart.org/sites/default/files/RPGsounds_Kenney.zip",
}

LEGACY_RESTORE = {
    "1(3).mp3": ("jingles", "jingles_NES00.ogg"),
    "普通传送.mp3": ("scifi", "spaceEngineSmall_002.ogg"),
    "科幻传送.mp3": ("scifi", "spaceEngineLarge_004.ogg"),
    "水泥地走路1.mp3": ("impact", "footstep_concrete_000.ogg"),
    "水泥地走路2.mp3": ("impact", "footstep_concrete_002.ogg"),
    "草地走路1.mp3": ("impact", "footstep_grass_000.ogg"),
    "草地走路2.mp3": ("impact", "footstep_grass_002.ogg"),
    "楼梯1.mp3": ("impact", "footstep_wood_000.ogg"),
    "楼梯2.mp3": ("impact", "footstep_wood_002.ogg"),
    "跑步1.mp3": ("impact", "footstep_concrete_003.ogg"),
    "跑步2.mp3": ("impact", "footstep_concrete_004.ogg"),
    "喘气.mp3": ("rpg", "clothBelt2.ogg"),
    "鸟叫.mp3": ("jingles", "jingles_PIZZI00.ogg"),
    "风.mp3": ("scifi", "lowFrequency_explosion_000.ogg"),
    "小雨.mp3": ("digital", "powerUp1.ogg"),
}

NAMED_OUTPUT = {
    "BGM/BGM_uppergarden.mp3": ("jingles", "jingles_NES00.ogg"),
    "BGM/BGM_undergarden_day.mp3": ("jingles", "jingles_NES01.ogg"),
    "BGM/BGM_Loadingscene.mp3": ("jingles", "jingles_PIZZI01.ogg"),
    "BGM/BGM_library.mp3": ("jingles", "jingles_PIZZI02.ogg"),
    "BGM/BGM_dorm.mp3": ("jingles", "jingles_SAX00.ogg"),
    "BGM/BGM_middlegarden.mp3": ("jingles", "jingles_NES02.ogg"),
    "BGM/BGM_lingcollege.mp3": ("jingles", "jingles_PIZZI03.ogg"),
    "BGM/BGM_classroom.mp3": ("jingles", "jingles_SAX01.ogg"),
    "BGM/BGM_lecture_hall.mp3": ("jingles", "jingles_SAX02.ogg"),
    "BGM/BGM_lingdrom.mp3": ("jingles", "jingles_NES03.ogg"),
    "AMB/AMB_wind.mp3": ("scifi", "lowFrequency_explosion_001.ogg"),
    "AMB/AMB_rain_light.mp3": ("digital", "powerUp2.ogg"),
    "AMB/AMB_birds_day.mp3": ("jingles", "jingles_PIZZI00.ogg"),
    "AMB/AMB_indoor_corridor.mp3": ("rpg", "creak1.ogg"),
    "AMB/AMB_library_hvac.mp3": ("digital", "powerUp5.ogg"),
    "AMB/AMB_dorm_night.mp3": ("rpg", "creak2.ogg"),
    "SFX/UI/SFX_UI_click_default.mp3": ("interface", "click_001.ogg"),
    "SFX/UI/SFX_UI_click_primary.mp3": ("interface", "confirmation_001.ogg"),
    "SFX/UI/SFX_UI_click_secondary.mp3": ("interface", "back_001.ogg"),
    "SFX/UI/SFX_UI_panel_open.mp3": ("interface", "open_001.ogg"),
    "SFX/UI/SFX_UI_panel_close.mp3": ("interface", "close_001.ogg"),
    "SFX/UI/SFX_UI_success.mp3": ("interface", "confirmation_002.ogg"),
    "SFX/UI/SFX_UI_error.mp3": ("interface", "error_001.ogg"),
    "SFX/UI/SFX_UI_notification.mp3": ("interface", "question_001.ogg"),
    "SFX/UI/SFX_UI_map_open.mp3": ("interface", "open_002.ogg"),
    "SFX/UI/SFX_UI_map_close.mp3": ("interface", "close_002.ogg"),
    "SFX/UI/SFX_UI_login_success.mp3": ("interface", "confirmation_003.ogg"),
    "SFX/UI/SFX_UI_login_fail.mp3": ("interface", "error_002.ogg"),
    "SFX/Player/SFX_Player_walk_concrete_01.mp3": ("impact", "footstep_concrete_000.ogg"),
    "SFX/Player/SFX_Player_walk_concrete_02.mp3": ("impact", "footstep_concrete_002.ogg"),
    "SFX/Player/SFX_Player_walk_grass_01.mp3": ("impact", "footstep_grass_000.ogg"),
    "SFX/Player/SFX_Player_walk_grass_02.mp3": ("impact", "footstep_grass_002.ogg"),
    "SFX/Player/SFX_Player_walk_stair_01.mp3": ("impact", "footstep_wood_000.ogg"),
    "SFX/Player/SFX_Player_walk_stair_02.mp3": ("impact", "footstep_wood_002.ogg"),
    "SFX/Player/SFX_Player_run_01.mp3": ("impact", "footstep_concrete_003.ogg"),
    "SFX/Player/SFX_Player_run_02.mp3": ("impact", "footstep_concrete_004.ogg"),
    "SFX/Player/SFX_Player_pant_low_stamina.mp3": ("rpg", "clothBelt2.ogg"),
    "SFX/Player/SFX_Player_jump_takeoff.mp3": ("impact", "footstep_grass_004.ogg"),
    "SFX/Player/SFX_Player_jump_land_light.mp3": ("impact", "footstep_concrete_001.ogg"),
    "SFX/Player/SFX_Player_interact_prompt.mp3": ("interface", "select_001.ogg"),
    "SFX/Player/SFX_Player_interact_confirm.mp3": ("interface", "confirmation_004.ogg"),
    "SFX/System/SFX_System_teleport_normal.mp3": ("scifi", "spaceEngineSmall_002.ogg"),
    "SFX/System/SFX_System_teleport_scifi.mp3": ("scifi", "spaceEngineLarge_004.ogg"),
    "SFX/System/SFX_System_quest_sub_complete.mp3": ("interface", "confirmation_001.ogg"),
    "SFX/System/SFX_System_quest_chain_complete.mp3": ("interface", "confirmation_003.ogg"),
    "SFX/System/SFX_System_coin_gain.mp3": ("rpg", "handleCoins.ogg"),
    "SFX/System/SFX_System_dialogue_page.mp3": ("interface", "click_002.ogg"),
    "SFX/System/SFX_System_dialogue_close.mp3": ("interface", "close_001.ogg"),
    "SFX/System/SFX_System_mall_buy_success.mp3": ("interface", "confirmation_002.ogg"),
    "SFX/System/SFX_System_mall_buy_fail.mp3": ("interface", "error_003.ogg"),
    "SFX/System/SFX_System_death_stinger.mp3": ("rpg", "metalPot3.ogg"),
    "SFX/System/SFX_System_revive.mp3": ("interface", "confirmation_004.ogg"),
    "SFX/System/SFX_System_teleport_loading_end.mp3": ("digital", "powerUp12.ogg"),
}


def log(msg: str) -> None:
    print(msg, flush=True)


def download(url: str, dest: Path) -> None:
    dest.parent.mkdir(parents=True, exist_ok=True)
    if dest.is_file() and dest.stat().st_size > 500:
        return
    log(f"  download {url}")
    req = urllib.request.Request(url, headers={"User-Agent": "Mozilla/5.0"})
    with urllib.request.urlopen(req, timeout=180) as resp:
        dest.write_bytes(resp.read())


def extract_pack(name: str, url: str) -> Path:
    zip_path = CACHE / f"{name}.zip"
    out_dir = CACHE / name
    download(url, zip_path)
    marker = out_dir / ".extracted"
    if marker.is_file():
        return out_dir
    out_dir.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(zip_path) as zf:
        zf.extractall(out_dir)
    marker.write_text("ok", encoding="utf-8")
    return out_dir


def find_file(root: Path, filename: str) -> Path | None:
    target = filename.lower()
    for p in root.rglob("*"):
        if p.is_file() and p.name.lower() == target:
            return p
    return None


def ensure_ffmpeg() -> str | None:
    try:
        import imageio_ffmpeg
        return imageio_ffmpeg.get_ffmpeg_exe()
    except Exception:
        return None


def convert_to_mp3(src: Path, dest: Path, ffmpeg: str | None) -> None:
    dest.parent.mkdir(parents=True, exist_ok=True)
    if dest.is_file() and dest.stat().st_size > 500:
        return

    if ffmpeg:
        subprocess.run(
            [ffmpeg, "-y", "-i", str(src), "-codec:a", "libmp3lame", "-q:a", "4", str(dest)],
            check=True,
            capture_output=True,
        )
        return

    shutil.copy2(src, dest.with_suffix(src.suffix))
    shutil.copy2(src, dest)


def resolve_source(pack_dirs: dict[str, Path], spec: tuple[str, str]) -> Path:
    kind, key = spec
    root = pack_dirs.get(kind)
    if root is None:
        raise KeyError(kind)
    found = find_file(root, key)
    if not found:
        raise FileNotFoundError(f"{key} not found in {kind}")
    return found


def main() -> int:
    log("=== Download Kenney CC0 packs ===")
    CACHE.mkdir(parents=True, exist_ok=True)
    MUSIC.mkdir(parents=True, exist_ok=True)

    pack_dirs: dict[str, Path] = {}
    for name, url in KENNEY_PACKS.items():
        try:
            pack_dirs[name] = extract_pack(name, url)
            log(f"  ok: {name}")
        except Exception as ex:
            log(f"  WARN pack {name}: {ex}")

    ffmpeg = ensure_ffmpeg()
    log(f"ffmpeg: {ffmpeg or 'none'}")

    log("\n=== Restore legacy Chinese filenames ===")
    for out_name, spec in LEGACY_RESTORE.items():
        try:
            src = resolve_source(pack_dirs, spec)
            convert_to_mp3(src, MUSIC / out_name, ffmpeg)
            log(f"  + {out_name}")
        except Exception as ex:
            log(f"  FAIL {out_name}: {ex}")

    log("\n=== Write checklist-named assets ===")
    for rel, spec in NAMED_OUTPUT.items():
        try:
            src = resolve_source(pack_dirs, spec)
            convert_to_mp3(src, MUSIC / rel, ffmpeg)
            log(f"  + {rel}")
        except Exception as ex:
            log(f"  FAIL {rel}: {ex}")

    res = ROOT / "Assets" / "Resources" / "Music"
    res.mkdir(parents=True, exist_ok=True)
    mirrors = [
        ("SFX/UI/SFX_UI_click_default.mp3", "SFX_UI_click_default"),
        ("SFX/System/SFX_System_teleport_scifi.mp3", "SFX_System_teleport_scifi"),
        ("SFX/System/SFX_System_coin_gain.mp3", "SFX_System_coin_gain"),
        ("BGM/BGM_uppergarden.mp3", "BGM_uppergarden"),
        ("SFX/UI/SFX_UI_panel_open.mp3", "SFX_UI_panel_open"),
        ("SFX/UI/SFX_UI_panel_close.mp3", "SFX_UI_panel_close"),
        ("SFX/UI/SFX_UI_success.mp3", "SFX_UI_success"),
        ("SFX/UI/SFX_UI_error.mp3", "SFX_UI_error"),
        ("SFX/System/SFX_System_quest_chain_complete.mp3", "SFX_System_quest_chain_complete"),
        ("SFX/System/SFX_System_death_stinger.mp3", "SFX_System_death_stinger"),
        ("SFX/System/SFX_System_revive.mp3", "SFX_System_revive"),
    ]
    for rel, res_name in mirrors:
        src = MUSIC / rel
        if src.is_file():
            shutil.copy2(src, res / f"{res_name}.mp3")

    log("\nDone.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
