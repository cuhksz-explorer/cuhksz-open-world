# -*- coding: utf-8 -*-
"""Fix corrupted library .blend.meta GUIDs + materialLocation; snapshot full scenes.

Run (Unity closed preferred for scene copy if locked):
  py -3 Assets/code/Editor/library_fix_meta_and_backup.py
"""
from __future__ import annotations

import re
import shutil
from datetime import datetime
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
SCENE = ROOT / "Assets" / "scene"
BACKUP_SCENE = SCENE / "Backups"
PREFAB_BACKUP = ROOT / "Assets" / "code" / "Editor" / "library_reimport_backup"

MODELS = {
    "图书馆1楼.blend.meta": "5e198423aa994ca4bace71cb259a7a4f",
    "lib2f.blend.meta": "7aa5c8e04a22f6c47918ec4d53021da8",
    "图书馆四楼.blend.meta": "0fc0c835acb8857428e7673f5c1b0a62",
}

IMPORTER = """  materials:
    materialImportMode: 1
    materialName: 1
    materialSearch: 2
    materialLocation: 1
"""

SCENES = ("libraryfloor1.scene", "libraryfloor2.scene", "libraryfloor4.scene")


def fix_meta(name: str, guid: str) -> None:
    p = SCENE / name
    t = p.read_text(encoding="utf-8", errors="replace")
    t = re.sub(r"^guid: .+$", f"guid: {guid}", t, count=1, flags=re.M)
    t = re.sub(
        r"  materials:\n    materialImportMode: \d+\n    materialName: \d+\n    materialSearch: \d+\n    materialLocation: \d+\n",
        IMPORTER,
        t,
        count=1,
    )
    p.write_text(t, encoding="utf-8")
    g = re.search(r"^guid: (.+)$", t, re.M)
    assert g and g.group(1) == guid
    assert "materialLocation: 1" in t
    print(f"  meta OK: {name} -> {guid[:8]}...")


def backup_scenes() -> None:
    BACKUP_SCENE.mkdir(parents=True, exist_ok=True)
    stamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    for name in SCENES:
        src = SCENE / name
        if not src.is_file():
            print(f"  WARN missing {name}")
            continue
        dst = BACKUP_SCENE / f"{src.stem}_{stamp}.scene"
        try:
            shutil.copy2(src, dst)
            mb = dst.stat().st_size // 1024 // 1024
            print(f"  full scene backup: {dst.name} ({mb} MB)")
        except OSError as e:
            print(f"  WARN could not copy {name}: {e}")


def main() -> None:
    print("=== Fix .blend.meta (GUID + External materials) ===")
    for name, guid in MODELS.items():
        fix_meta(name, guid)

    print("\n=== Full scene backups -> Assets/scene/Backups/ ===")
    backup_scenes()

    print("\n=== PrefabInstance backups (partial) ===")
    if PREFAB_BACKUP.is_dir():
        for f in PREFAB_BACKUP.glob("libraryfloor*_prefab_*.yaml"):
            print(f"  {f.name} ({f.stat().st_size // 1024} KB)")
    else:
        print("  (none)")

    print(
        "\n说明：\n"
        "  • 完整摆放 = Assets/scene/Backups/*.scene（整文件）\n"
        "  • 仅建模 prefab 覆盖 = library_reimport_backup/*_prefab_*.yaml\n"
        "  • 修复 meta 后请在团结中：Tools -> 图书馆 -> 仅 Reimport+材质+恢复布置"
    )


if __name__ == "__main__":
    main()
