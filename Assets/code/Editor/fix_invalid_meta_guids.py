# -*- coding: utf-8 -*-
"""Replace invalid Unity .meta GUIDs (non 32-hex) with valid ones and update references.

Run from project root:
  py -3 Assets/code/Editor/fix_invalid_meta_guids.py

Safe to re-run: already-valid GUIDs are skipped.
"""
from __future__ import annotations

import re
import uuid
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
ASSETS = ROOT / "Assets"
PROJECT_SETTINGS = ROOT / "ProjectSettings"
VALID_GUID = re.compile(r"^[0-9a-f]{32}$")
GUID_LINE = re.compile(r"^(guid: )(.+)$", re.MULTILINE)
REF_EXTENSIONS = {
    ".unity",
    ".prefab",
    ".asset",
    ".mat",
    ".controller",
    ".overrideController",
    ".mask",
    ".anim",
    ".playable",
    ".signal",
    ".guiskin",
    ".fontsettings",
    ".spriteatlas",
    ".shadervariants",
    ".scene",
    ".meta",
}


def is_valid_guid(value: str) -> bool:
    return bool(VALID_GUID.fullmatch(value.strip()))


# Paths restored by fix_project_guid_references.py — never auto-regenerate these.
SKIP_META_PATHS = frozenset(
    {
        "Assets/scene/lib2f.fbx.meta",
        "Assets/scene/图书馆1楼.fbx.meta",
        "Assets/scene/图书馆1楼.blend.meta",
        "Assets/scene/lib2f.blend.meta",
        "Assets/scene/lib2f.fbx.meta",
        "Assets/scene/图书馆四楼.blend.meta",
        "Assets/scene/图书馆四楼.fbx.meta",
        "Assets/scene/Materials/11月25日.mp4.meta",
        "Assets/scene/Materials/11月25日(1).mp4.meta",
        "Assets/scene/Materials/11月25日(2).mp4.meta",
        "Assets/scene/Materials/11月25日(3).mp4.meta",
    }
)


def collect_invalid_meta_guids() -> dict[str, str]:
    mapping: dict[str, str] = {}
    for meta_path in ASSETS.rglob("*.meta"):
        rel = str(meta_path.relative_to(ROOT)).replace("\\", "/")
        if rel in SKIP_META_PATHS:
            continue
        try:
            text = meta_path.read_text(encoding="utf-8", errors="replace")
        except OSError:
            continue
        match = GUID_LINE.search(text)
        if not match:
            continue
        old = match.group(2).strip()
        if is_valid_guid(old):
            continue
        mapping[old] = uuid.uuid4().hex
    return mapping


def patch_meta_files(mapping: dict[str, str]) -> int:
    changed = 0
    for meta_path in ASSETS.rglob("*.meta"):
        try:
            text = meta_path.read_text(encoding="utf-8", errors="replace")
        except OSError:
            continue
        match = GUID_LINE.search(text)
        if not match:
            continue
        old = match.group(2).strip()
        if old not in mapping:
            continue
        new_text = GUID_LINE.sub(rf"\g<1>{mapping[old]}", text, count=1)
        if new_text != text:
            meta_path.write_text(new_text, encoding="utf-8")
            changed += 1
    return changed


def iter_reference_files():
    for folder in (ASSETS, PROJECT_SETTINGS):
        if not folder.is_dir():
            continue
        for path in folder.rglob("*"):
            if not path.is_file():
                continue
            if path.suffix.lower() in REF_EXTENSIONS or path.name.endswith(".asmdef"):
                yield path


def patch_references(mapping: dict[str, str]) -> int:
    touched = 0
    for path in iter_reference_files():
        try:
            text = path.read_text(encoding="utf-8", errors="replace")
        except (OSError, UnicodeDecodeError):
            continue
        updated = text
        for old, new in mapping.items():
            if old in updated:
                updated = updated.replace(old, new)
        if updated != text:
            path.write_text(updated, encoding="utf-8")
            touched += 1
    return touched


def main() -> None:
    mapping = collect_invalid_meta_guids()
    print(f"Invalid .meta GUIDs found: {len(mapping)}")
    if not mapping:
        print("Nothing to fix.")
        return

    meta_changed = patch_meta_files(mapping)
    refs_changed = patch_references(mapping)
    print(f"Updated .meta files: {meta_changed}")
    print(f"Updated reference files: {refs_changed}")
    print("Re-open Unity and let it reimport assets.")


if __name__ == "__main__":
    main()
