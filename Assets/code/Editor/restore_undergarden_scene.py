# -*- coding: utf-8 -*-
"""Restore undergarden + remap broken prefab/script GUIDs referenced by scenes."""
from __future__ import annotations

import re
import shutil
from datetime import datetime
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
SCENE = ROOT / "Assets" / "scene" / "undergarden.unity"
BACKUP = ROOT / "Assets" / "scene" / "undergarden.unity.bak_grass_height"
STORY_PREFAB_DIR = ROOT / "Assets" / "NPCs" / "Story"
SCENE_DIR = ROOT / "Assets" / "scene"

GUID_LINE = re.compile(r"^(guid: )(.+)$", re.M)
PREFAB_BLOCK = re.compile(r"^--- !u!1001 &", re.M)
STORY_NAME = re.compile(r'propertyPath: m_Name\n\s+value: "(StoryNPC_[^"]+)"')
SOURCE_PREFAB = re.compile(
    r"m_SourcePrefab: \{fileID: 100100000, guid: ([0-9a-f]{32}), type: 3\}"
)

# Scenes still reference these historical GUIDs; current .meta uses the values on the right.
KNOWN_ASSET_REMAPS = {
    "5942054c1e55548e9b7f7e0bcfba2499": "f2ced1e4a06f4596992cf72f2f60fe2f",  # 自机角色女 裸模9.fbx
    "250905fd6ae7caa4f8cccf1207b58aad": "5b44d84dd82a47b9a0cbafa148721d18",  # bus FBX
    "8f21cf6986fa45041b95eae071f5ddfd": "f94fcbb674c94afabfc1b0eea81973cb",  # the_girl_is_a_racing_driver.glb
    "40e830d8d311743468f37e1d99bda3f0": "db82ae01b49b4eb1a6f82ffbd2127702",  # new  scene.glb
    "38c3c073cb10ac44abb39f62cb245cd2": "cb49cc38d4a14b8a8ef5a2a9ee0d577c",  # StoryQuestRuntime.cs
    "30b71fbbcdcb3184aa5041de7bdbf504": "3b41f1118da54b8bac35490b68b477d1",  # PlayerWaterRippleEmitter.cs
}

CHAIR_SIT_META_GUIDS = {
    "Assets/Resources/ChairSit/Stand Up.fbx.meta": "074e3ad34611f8441aaa71ac9557cdd0",
    "Assets/Resources/ChairSit/Sit To Stand.fbx.meta": "ac4a40e1c4513f0469e0806a22fddaa9",
    "Assets/Resources/ChairSit/Sitting Idle.fbx.meta": "f2b13a02792d1bb4d9a73ea1a0afb597",
    "Assets/Resources/ChairSit/PlayerChairSit.controller.meta": "640cd3e81ffef074592b0d8fe796a928",
}


def decode_unity_string(value: str) -> str:
    try:
        return value.encode("utf-8").decode("unicode_escape")
    except UnicodeError:
        return value


def display_from_story_object(name: str) -> str:
    core = name[len("StoryNPC_") :]
    idx = core.find("_M")
    core = core[:idx] if idx > 0 else core
    return decode_unity_string(core)


def load_story_prefab_guids() -> dict[str, str]:
    mapping: dict[str, str] = {}
    for meta in STORY_PREFAB_DIR.glob("*.prefab.meta"):
        text = meta.read_text(encoding="utf-8", errors="replace")
        match = GUID_LINE.search(text)
        if not match:
            continue
        display_name = meta.name[: -len(".prefab.meta")]
        mapping[display_name] = match.group(2).strip()
    return mapping


def build_story_guid_remap(scene_text: str, prefab_guids: dict[str, str]) -> dict[str, str]:
    remap: dict[str, str] = {}
    blocks = PREFAB_BLOCK.split(scene_text)
    for block in blocks[1:]:
        source = SOURCE_PREFAB.search(block)
        if not source:
            continue
        old_guid = source.group(1)
        names = STORY_NAME.findall(block)
        if not names:
            continue
        display_names = {display_from_story_object(n) for n in names}
        target_guids = {prefab_guids[d] for d in display_names if d in prefab_guids}
        if len(target_guids) != 1:
            continue
        new_guid = next(iter(target_guids))
        if old_guid != new_guid:
            remap[old_guid] = new_guid
    return remap


def apply_guid_remap(text: str, remap: dict[str, str]) -> str:
    for old, new in sorted(remap.items(), key=lambda item: len(item[0]), reverse=True):
        text = text.replace(old, new)
    return text


def ensure_scene_restored() -> None:
    if not SCENE.is_file():
        if BACKUP.is_file():
            shutil.copy2(BACKUP, SCENE)
            print(f"Restored missing scene from {BACKUP.name}")
        return

    text = SCENE.read_text(encoding="utf-8", errors="replace")
    if text.count("GrassBundle1") >= 1000:
        return

    if not BACKUP.is_file():
        raise SystemExit(f"Scene truncated and backup missing: {BACKUP}")

    stamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    broken_copy = SCENE.with_suffix(f".unity.broken_{stamp}")
    shutil.copy2(SCENE, broken_copy)
    shutil.copy2(BACKUP, SCENE)
    print(f"Restored truncated scene from {BACKUP.name} (saved {broken_copy.name})")


def restore_chair_sit_meta_guids() -> None:
    for rel_path, guid in CHAIR_SIT_META_GUIDS.items():
        meta_path = ROOT / rel_path.replace("/", "\\")
        if not meta_path.is_file():
            print(f"WARN missing meta: {rel_path}")
            continue
        text = meta_path.read_text(encoding="utf-8", errors="replace")
        new_text = GUID_LINE.sub(rf"\g<1>{guid}", text, count=1)
        if new_text != text:
            meta_path.write_text(new_text, encoding="utf-8")
            print(f"Restored ChairSit GUID: {meta_path.name} -> {guid}")


def patch_scene_files(story_remap: dict[str, str]) -> None:
    combined = dict(KNOWN_ASSET_REMAPS)
    combined.update(story_remap)
    extensions = {".unity", ".scene", ".prefab"}
    touched = 0
    for path in SCENE_DIR.rglob("*"):
        if not path.is_file() or path.suffix.lower() not in extensions:
            continue
        if ".broken_" in path.name or path.name.endswith(".bak") or "DO_NOT_USE" in path.name:
            continue
        text = path.read_text(encoding="utf-8", errors="replace")
        updated = apply_guid_remap(text, combined)
        if updated != text:
            path.write_text(updated, encoding="utf-8")
            touched += 1
            print(f"Patched {path.relative_to(ROOT)}")
    print(f"Patched scene/prefab files: {touched}")


def main() -> None:
    ensure_scene_restored()
    restore_chair_sit_meta_guids()

    scene_text = SCENE.read_text(encoding="utf-8", errors="replace")
    story_remap = build_story_guid_remap(scene_text, load_story_prefab_guids())
    print(f"Story NPC GUID remaps: {len(story_remap)}")
    for old, new in sorted(story_remap.items()):
        print(f"  {old} -> {new}")

    patch_scene_files(story_remap)
    print("Done. Re-open Unity and reload undergarden.")


if __name__ == "__main__":
    main()
