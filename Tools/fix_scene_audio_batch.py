# -*- coding: utf-8 -*-
"""Fix broken audio GUID refs and apply SceneAudioBatchSetup bindings offline.

1. Normalize non-hex .meta GUIDs under Assets/Music to standard 32-char hex.
2. Patch scene YAML: BGM, ambient, teleport, footstep clips per scene profile.

Run:
  py -3 Tools/fix_scene_audio_batch.py
"""
from __future__ import annotations

import re
import shutil
import uuid
from dataclasses import dataclass
from datetime import datetime
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
MUSIC = ROOT / "Assets" / "Music"
RES = ROOT / "Assets" / "Resources" / "Music"
SCENE_DIR = ROOT / "Assets" / "scene"
SETUP = ROOT / "Assets" / "code" / "Editor" / "SceneAudioBatchSetup.cs"

GUID_LINE = re.compile(r"^guid: .+$", re.M)
HEX_GUID = re.compile(r"^[0-9a-f]{32}$")
PROFILE_RE = re.compile(
    r'\["([^"]+)"\] = Profile\("([^"]+)", "([^"]+)", ([0-9.]+)f, (true|false)\)'
)
CLIP_REF = re.compile(r"(\{fileID: \d+, guid: )([^,]+)(, type: \d+\})")

SCRIPT_BGM = "d1e30f8c09cf5e445a5c44b5744f52bc"
SCRIPT_AMB = "4974513788d0476f82f090320cfe7841"
SCRIPT_STEP = "5cfc4a772ea1f2a458a2afbfe066fc15"
SCRIPT_MAP = "aba485093a1392547a6ed57bbcb40d81"

LEGACY_CLIPS = {
    "teleport": ("科幻传送.mp3", "SFX/System/SFX_System_teleport_scifi.mp3"),
    "walk": ("水泥地走路1.mp3", "SFX/Player/SFX_Player_walk_concrete_01.mp3"),
    "run": ("跑步2.mp3", "SFX/Player/SFX_Player_run_02.mp3"),
    "pant": ("喘气.mp3", "SFX/Player/SFX_Player_pant_low_stamina.mp3"),
    "bgm_default": ("1(3).mp3", "BGM/BGM_uppergarden.mp3"),
}


@dataclass
class Profile:
    bgm: str
    amb: str
    amb_vol: float
    outdoor: bool


def log(msg: str) -> None:
    print(msg, flush=True)


def new_hex_guid() -> str:
    return uuid.uuid4().hex


def read_guid(meta: Path) -> str | None:
    if not meta.is_file():
        return None
    m = re.search(r"^guid: (.+)$", meta.read_text(encoding="utf-8", errors="replace"), re.M)
    return m.group(1).strip() if m else None


def write_guid(meta: Path, guid: str) -> None:
    text = meta.read_text(encoding="utf-8", errors="replace")
    text = GUID_LINE.sub(f"guid: {guid}", text, count=1)
    meta.write_text(text, encoding="utf-8")


def normalize_music_meta() -> dict[str, str]:
    """Return relative Music path -> hex guid."""
    path_to_guid: dict[str, str] = {}
    fixed = 0
    for mp3 in sorted(MUSIC.rglob("*.mp3")):
        rel = mp3.relative_to(MUSIC).as_posix()
        meta = Path(str(mp3) + ".meta")
        if not meta.is_file():
            guid = new_hex_guid()
            shutil.copy2(MUSIC / "风.mp3.meta", meta) if (MUSIC / "风.mp3.meta").is_file() else None
            if meta.is_file():
                write_guid(meta, guid)
            else:
                meta.write_text(
                    f"fileFormatVersion: 2\nguid: {guid}\nDefaultImporter:\n  externalObjects: {{}}\n",
                    encoding="utf-8",
                )
            fixed += 1
        else:
            guid = read_guid(meta) or new_hex_guid()
            if not HEX_GUID.fullmatch(guid):
                guid = new_hex_guid()
                write_guid(meta, guid)
                fixed += 1
        path_to_guid[rel] = guid
    log(f"Meta GUIDs: {len(path_to_guid)} paths, {fixed} normalized/created")
    return path_to_guid


def parse_profiles() -> dict[str, Profile]:
    text = SETUP.read_text(encoding="utf-8")
    profiles: dict[str, Profile] = {}
    for m in PROFILE_RE.finditer(text):
        profiles[m.group(1).lower()] = Profile(
            bgm=m.group(2),
            amb=m.group(3),
            amb_vol=float(m.group(4)),
            outdoor=m.group(5) == "true",
        )
    return profiles


def resolve_profile(scene_path: Path, profiles: dict[str, Profile]) -> Profile:
    stem = scene_path.stem.lower()
    if stem in profiles:
        return profiles[stem]
    if any(k in stem for k in ("garden", "lingdrom", "minival", "bus")):
        return Profile("BGM/BGM_undergarden_day.mp3", "AMB/AMB_birds_day.mp3", 0.25, True)
    return Profile("BGM/BGM_classroom.mp3", "AMB/AMB_indoor_corridor.mp3", 0.16, False)


def clip_ref(guid: str) -> str:
    return f"{{fileID: 8300000, guid: {guid}, type: 3}}"


def pick_guid(path_to_guid: dict[str, str], *candidates: str) -> str | None:
    for c in candidates:
        if c in path_to_guid:
            return path_to_guid[c]
        root = Path(c).name
        if root in path_to_guid:
            return path_to_guid[root]
    return None


def replace_field(block: str, field: str, value: str) -> str:
    pat = rf"^  {re.escape(field)}: \{{fileID: \d+, guid: [^,]+, type: \d+\}}"
    repl = f"  {field}: {value}"
    new_block, n = re.subn(pat, repl, block, count=1, flags=re.M)
    return new_block if n else block


def replace_float_field(block: str, field: str, value: float) -> str:
    pat = rf"^  {re.escape(field)}: [0-9.]+"
    repl = f"  {field}: {value}"
    new_block, n = re.subn(pat, repl, block, count=1, flags=re.M)
    return new_block if n else block


def patch_script_blocks(content: str, script_guid: str, mutator) -> tuple[str, int]:
    parts = re.split(r"(--- !u!114 &\d+\nMonoBehaviour:)", content)
    if len(parts) < 2:
        return content, 0
    out = [parts[0]]
    changed = 0
    for i in range(1, len(parts), 2):
        header = parts[i]
        body = parts[i + 1] if i + 1 < len(parts) else ""
        block = header + body
        if f"guid: {script_guid}, type: 3" in block:
            new_block = mutator(block)
            if new_block != block:
                changed += 1
            block = new_block
        out.append(block)
    return "".join(out), changed


def patch_audio_source_clip(content: str, game_object_id: str, guid: str) -> str:
    parts = re.split(r"(--- !u!82 &\d+\nAudioSource:)", content)
    if len(parts) < 2:
        return content
    out = [parts[0]]
    ref = clip_ref(guid)
    for i in range(1, len(parts), 2):
        header = parts[i]
        body = parts[i + 1] if i + 1 < len(parts) else ""
        block = header + body
        if re.search(rf"^  m_GameObject: \{{fileID: {game_object_id}\}}", block, re.M):
            block = replace_field(block, "m_audioClip", ref)
        out.append(block)
    return "".join(out)


def extract_game_object_id(mono_block: str) -> str | None:
    m = re.search(r"^  m_GameObject: \{fileID: (\d+)\}", mono_block, re.M)
    return m.group(1) if m else None


def patch_scene(path: Path, profile: Profile, path_to_guid: dict[str, str]) -> dict[str, int]:
    stats = {"bgm": 0, "amb": 0, "teleport": 0, "step": 0}
    text = path.read_text(encoding="utf-8", errors="replace")

    bgm_guid = pick_guid(path_to_guid, profile.bgm, *LEGACY_CLIPS["bgm_default"])
    amb_guid = pick_guid(path_to_guid, profile.amb)
    tel_guid = pick_guid(path_to_guid, *LEGACY_CLIPS["teleport"])
    walk_guid = pick_guid(path_to_guid, *LEGACY_CLIPS["walk"])
    run_guid = pick_guid(path_to_guid, *LEGACY_CLIPS["run"])
    pant_guid = pick_guid(path_to_guid, *LEGACY_CLIPS["pant"])

    amb_go_ids: list[str] = []

    if bgm_guid:

        def mut_bgm(block: str) -> str:
            return replace_field(block, "bgmClip", clip_ref(bgm_guid))

        text, n = patch_script_blocks(text, SCRIPT_BGM, mut_bgm)
        stats["bgm"] += n

    if amb_guid:

        def mut_amb(block: str) -> str:
            nonlocal amb_go_ids
            go_id = extract_game_object_id(block)
            if go_id:
                amb_go_ids.append(go_id)
            block = replace_field(block, "clip", clip_ref(amb_guid))
            block = replace_float_field(block, "volume", profile.amb_vol)
            return block

        text, n = patch_script_blocks(text, SCRIPT_AMB, mut_amb)
        stats["amb"] += n
        for go_id in amb_go_ids:
            text = patch_audio_source_clip(text, go_id, amb_guid)

    if tel_guid:

        def mut_map(block: str) -> str:
            return replace_field(block, "teleportSound", clip_ref(tel_guid))

        text, n = patch_script_blocks(text, SCRIPT_MAP, mut_map)
        stats["teleport"] += n

    if walk_guid and run_guid and pant_guid:

        def mut_step(block: str) -> str:
            block = replace_field(block, "walkClip", clip_ref(walk_guid))
            block = replace_field(block, "runClip", clip_ref(run_guid))
            block = replace_field(block, "pantClip", clip_ref(pant_guid))
            return block

        text, n = patch_script_blocks(text, SCRIPT_STEP, mut_step)
        stats["step"] += n

    path.write_text(text, encoding="utf-8")
    return stats


def collect_scene_paths() -> list[Path]:
    paths: list[Path] = []
    for pattern in ("*.unity", "*.scene"):
        for p in SCENE_DIR.glob(pattern):
            if "Backups" in p.parts:
                continue
            paths.append(p)
    return sorted(paths, key=lambda p: p.name.lower())


def main() -> int:
    log("=== Fix audio meta GUIDs ===")
    path_to_guid = normalize_music_meta()

    log("\n=== Apply scene audio batch bindings ===")
    profiles = parse_profiles()
    totals = {"bgm": 0, "amb": 0, "teleport": 0, "step": 0, "scenes": 0}
    for scene_path in collect_scene_paths():
        profile = resolve_profile(scene_path, profiles)
        stats = patch_scene(scene_path, profile, path_to_guid)
        totals["scenes"] += 1
        totals["bgm"] += stats["bgm"]
        totals["amb"] += stats["amb"]
        totals["teleport"] += stats["teleport"]
        totals["step"] += stats["step"]
        log(
            f"  {scene_path.name}: bgm={stats['bgm']} amb={stats['amb']} "
            f"teleport={stats['teleport']} step={stats['step']} "
            f"<- {Path(profile.bgm).name} + {Path(profile.amb).name}"
        )

    log(
        f"\nDone. Scenes={totals['scenes']}, "
        f"BGM={totals['bgm']}, ambient={totals['amb']}, "
        f"teleport={totals['teleport']}, footsteps={totals['step']}\n"
        "In Unity: Assets -> Refresh, then enter Play Mode to verify."
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
