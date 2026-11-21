# -*- coding: utf-8 -*-
"""Targeted GUID repair for Lingdrom / dorm / libraryfloor2/4 only.

Run: py -3 Assets/code/Editor/targeted_scene_guid_repair.py
"""
from __future__ import annotations

import importlib.util
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
MODULE = ROOT / "Assets" / "code" / "Editor" / "fix_project_guid_references.py"

spec = importlib.util.spec_from_file_location("fix_project_guid_references", MODULE)
mod = importlib.util.module_from_spec(spec)
spec.loader.exec_module(mod)

TARGET_SCENES = {
    "Assets/scene/Lingdrom.unity",
    "Assets/scene/dorm.scene",
    "Assets/scene/libraryfloor2.scene",
    "Assets/scene/libraryfloor4.scene",
}


def main() -> None:
    print("=== Targeted meta restore (library + dorm + daoyang) ===")
    mod.restore_meta_guids()

    print("\n=== Shell materials ===")
    print(f"Shell materials fixed: {mod.fix_shell_materials()}")

    print("\n=== Library floor4 legacy materials ===")
    print(f"Legacy mats: {mod.ensure_library_floor_legacy_materials()}")

    print("\n=== Material texture GUID remap ===")
    meta_index = mod.load_meta_index()
    print(f"Texture remaps: {mod.fix_broken_material_texture_guids(meta_index)}")

    print("\n=== Re-apply meta restore ===")
    mod.restore_meta_guids()

    print("\n=== Scan (scene subset) ===")
    meta_index = mod.load_meta_index()
    missing = mod.scan_missing(meta_index)
    scene_missing = {
        g: f
        for g, f in missing.items()
        if any(
            p in TARGET_SCENES or p.startswith("Assets/scene/道扬") or p.startswith("Assets/Meshes/第八书院")
            for p in f
        )
    }
    report = mod.write_report(scene_missing if scene_missing else missing)
    print(f"Report: {report.relative_to(ROOT)}")
    print(f"Missing GUIDs in target scope: {len(scene_missing)}")


if __name__ == "__main__":
    main()
