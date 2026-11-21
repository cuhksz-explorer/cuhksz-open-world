# -*- coding: utf-8 -*-
"""
Floor 2/4 only: install FBX (preserve GUID), fix importer meta, restore scene backups.
Does NOT touch libraryfloor1.

Run after Blender export:
  py -3 Assets/code/Editor/library_floor24_fbx_restore.py
"""
from __future__ import annotations

import re
import shutil
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
SCENE = ROOT / "Assets" / "scene"
BACKUP_SCENE = SCENE / "Backups"
PREFAB_BACKUP = ROOT / "Assets" / "code" / "Editor" / "library_reimport_backup"

IMPORTER = """  materials:
    materialImportMode: 1
    materialName: 1
    materialSearch: 2
    materialLocation: 1
"""

FLOORS = {
    "floor2": {
        "fbx": "lib2f.fbx",
        "blend": "lib2f.blend",
        "guid": "7aa5c8e04a22f6c47918ec4d53021da8",
        "scene": "libraryfloor2.scene",
        "prefab_id": "2099878117",
        "backup_scene": "libraryfloor2_20260526_150306.scene",
    },
    "floor4": {
        "fbx": "图书馆四楼.fbx",
        "blend": "图书馆四楼.blend",
        "guid": "0fc0c835acb8857428e7673f5c1b0a62",
        "scene": "libraryfloor4.scene",
        "prefab_id": "20395746",
        "backup_scene": "libraryfloor4_20260526_150306.scene",
    },
}


def fix_meta(meta_path: Path, guid: str) -> None:
    if meta_path.is_file():
        t = meta_path.read_text(encoding="utf-8", errors="replace")
    else:
        t = (
            f"fileFormatVersion: 2\nguid: {guid}\nModelImporter:\n"
            "  serializedVersion: 22361\n  externalObjects: {}\n"
        )
    t = re.sub(r"^guid: .+$", f"guid: {guid}", t, count=1, flags=re.M)
    if "ModelImporter:" not in t:
        tmpl = (SCENE / "lib2f.blend.meta").read_text(encoding="utf-8", errors="replace")
        t = re.sub(r"^guid: .+$", f"guid: {guid}", tmpl, count=1, flags=re.M)
    t = re.sub(
        r"  materials:\n    materialImportMode: \d+\n    materialName: \d+\n    materialSearch: \d+\n    materialLocation: \d+\n",
        IMPORTER,
        t,
        count=1,
    )
    meta_path.write_text(t, encoding="utf-8")


def rm(path: Path) -> None:
    if not path.exists():
        return
    try:
        if path.is_dir():
            shutil.rmtree(path)
        else:
            path.unlink()
    except OSError as e:
        print(f"  WARN could not delete {path.name}: {e}")
        return
    m = Path(str(path) + ".meta")
    if m.is_file():
        try:
            m.unlink()
        except OSError:
            pass


def install_fbx(cfg: dict) -> None:
    fbx = SCENE / cfg["fbx"]
    blend = SCENE / cfg["blend"]
    guid = cfg["guid"]
    if not fbx.is_file():
        raise FileNotFoundError(f"Missing FBX (run Blender export first): {fbx}")

    blend_meta = SCENE / f"{cfg['blend']}.meta"
    fbx_meta = SCENE / f"{cfg['fbx']}.meta"
    if blend_meta.is_file() and not fbx_meta.is_file():
        shutil.copy2(blend_meta, fbx_meta)
    rm(blend)
    fix_meta(fbx_meta, guid)
    mb = fbx.stat().st_size // 1024 // 1024
    print(f"  {cfg['fbx']}: {mb} MB, guid {guid[:8]}...")


def restore_full_scene(cfg: dict) -> None:
    src = BACKUP_SCENE / cfg["backup_scene"]
    dst = SCENE / cfg["scene"]
    if not src.is_file():
        backups = sorted(BACKUP_SCENE.glob(f"{Path(cfg['scene']).stem}_*.scene"))
        if not backups:
            raise FileNotFoundError(f"No scene backup for {cfg['scene']}")
        src = backups[-1]
    shutil.copy2(src, dst)
    print(f"  scene restored: {dst.name} <- {src.name}")


def restore_prefab_block(cfg: dict) -> None:
    backup = PREFAB_BACKUP / f"{Path(cfg['scene']).stem}_prefab_{cfg['prefab_id']}.yaml"
    if not backup.is_file():
        print(f"  WARN no prefab yaml: {backup.name}")
        return
    scene_path = SCENE / cfg["scene"]
    block = backup.read_text(encoding="utf-8")
    text = scene_path.read_text(encoding="utf-8")
    pid = cfg["prefab_id"]
    pattern = rf"(--- !u!1001 &{pid}\nPrefabInstance:.*?)(?=\n--- !u!)"
    m = re.search(pattern, text, re.S)
    if not m:
        print(f"  WARN prefab &{pid} not in {scene_path.name}")
        return
    scene_path.write_text(text[: m.start()] + block + text[m.end() :], encoding="utf-8")
    print(f"  prefab &{pid} restored in {scene_path.name}")


def main() -> None:
    print("=== Install FBX (floor2 + floor4), remove .blend ===")
    for cfg in FLOORS.values():
        install_fbx(cfg)

    print("\n=== Restore full scenes from Backups (NOT floor1) ===")
    for cfg in FLOORS.values():
        restore_full_scene(cfg)

    print("\n=== Restore PrefabInstance blocks from yaml ===")
    for cfg in FLOORS.values():
        restore_prefab_block(cfg)

    print("\nDone. In Unity: Reimport lib2f.fbx + 图书馆四楼.fbx")
    print("Then: Tools -> Mesh -> lib2f / 四楼 Reimport 并重新匹配材质")
    print("libraryfloor1 was NOT modified.")


if __name__ == "__main__":
    main()
