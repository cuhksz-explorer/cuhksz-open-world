# -*- coding: utf-8 -*-
"""Targeted repair for Lingdrom / dorm / libraryfloor2 / libraryfloor4.

Run from project root:
  py -3 Assets/code/Editor/scene_texture_guid_repair.py
"""
from __future__ import annotations

import re
import shutil
from datetime import datetime
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
SCENE = ROOT / "Assets" / "scene"
MESHES = ROOT / "Assets" / "Meshes"
BACKUP = SCENE / "Backups"
PREFAB_BACKUP = ROOT / "Assets" / "code" / "Editor" / "library_reimport_backup"

IMPORTER = """  materials:
    materialImportMode: 1
    materialName: 1
    materialSearch: 2
    materialLocation: 1
"""

LIB2F_GUID = "7aa5c8e04a22f6c47918ec4d53021da8"
LIB4_GUID = "0fc0c835acb8857428e7673f5c1b0a62"
DORM_FBX_GUID = "905a1df5f37c2d544a127974b46bb788"
DAOYANG_FBX_GUID = "45a559bbd50c46a0a8badcc38a2a9ab4"
DAOYANG_OBJ_GUID = "7633db74a0c54f34da254b09a516d052"
ORPHAN_LIB2F_GUID = "b37695b446eb6454bb5b946ea36c32d2"

LEGACY_DORM_GUIDS = (
    "4c1ae1eec102bc54ea9b8dc29f1c9d1c",
    "ad8f009f4b9a87942be2e6a3b0a67ee7",
    "03bcd8a1124fca6449cd1afcc12d2b53",
)

SOURCE_DAOYANG_FBX = Path(
    r"z:\微信\xwechat_files\wxid_iex8clusq8ee32_2373\msg\file\2025-11\道扬.fbx"
)
SOURCE_LIB4_BLEND = Path(r"z:\PHY1001 FINAL\图书馆四楼.blend")


def fix_meta(meta_path: Path, guid: str) -> None:
    if meta_path.is_file():
        text = meta_path.read_text(encoding="utf-8", errors="replace")
    else:
        text = (
            f"fileFormatVersion: 2\nguid: {guid}\nModelImporter:\n"
            "  serializedVersion: 22361\n  externalObjects: {}\n"
        )
    text = re.sub(r"^guid: .+$", f"guid: {guid}", text, count=1, flags=re.M)
    text = re.sub(
        r"  materials:\n    materialImportMode: \d+\n    materialName: \d+\n    materialSearch: \d+\n    materialLocation: \d+\n",
        IMPORTER,
        text,
        count=1,
    )
    meta_path.write_text(text, encoding="utf-8")
    print(f"  meta fixed: {meta_path.relative_to(ROOT)} -> {guid[:8]}...")


def restore_prefab_block(scene_name: str, prefab_id: str) -> None:
    backup = PREFAB_BACKUP / f"{Path(scene_name).stem}_prefab_{prefab_id}.yaml"
    scene_path = SCENE / scene_name
    if not backup.is_file():
        print(f"  WARN no prefab backup: {backup.name}")
        return
    block = backup.read_text(encoding="utf-8")
    text = scene_path.read_text(encoding="utf-8")
    pattern = rf"(--- !u!1001 &{prefab_id}\nPrefabInstance:.*?)(?=\n--- !u!)"
    match = re.search(pattern, text, re.S)
    if not match:
        print(f"  WARN prefab &{prefab_id} not found in {scene_name}")
        return
    scene_path.write_text(text[: match.start()] + block + text[match.end() :], encoding="utf-8")
    print(f"  prefab &{prefab_id} restored in {scene_name}")


def replace_guid_in_file(path: Path, old: str, new: str) -> int:
    text = path.read_text(encoding="utf-8", errors="replace")
    count = text.count(old)
    if count:
        path.write_text(text.replace(old, new), encoding="utf-8")
    return count


def remove_dorm_legacy_overrides() -> int:
    path = SCENE / "dorm.scene"
    text = path.read_text(encoding="utf-8", errors="replace")
    pattern = re.compile(
        r"    - target: \{fileID: -?\d+, guid: 905a1df5f37c2d544a127974b46bb788, type: 3\}\n"
        r"      propertyPath: m_Materials\.Array\.data\[\d+\]\n"
        r"      value: \n"
        r"      objectReference: \{fileID: 2100000, guid: (?:"
        + "|".join(LEGACY_DORM_GUIDS)
        + r"), type: 2\}\n"
    )
    new_text, count = pattern.subn("", text)
    if count:
        path.write_text(new_text, encoding="utf-8")
    return count


def install_daoyang_fbx() -> None:
    dest = SCENE / "道扬.fbx"
    src_existing = SCENE / "道扬(1).fbx"
    if SOURCE_DAOYANG_FBX.is_file():
        shutil.copy2(SOURCE_DAOYANG_FBX, dest)
        print(f"  copied source -> {dest.relative_to(ROOT)}")
    elif src_existing.is_file():
        shutil.copy2(src_existing, dest)
        print(f"  copied 道扬(1).fbx -> 道扬.fbx")
    else:
        raise FileNotFoundError("No 道扬.fbx source found")

    meta_src = SCENE / "道扬(1).fbx.meta"
    meta_dest = SCENE / "道扬.fbx.meta"
    if meta_src.is_file():
        shutil.copy2(meta_src, meta_dest)
    fix_meta(meta_dest, DAOYANG_FBX_GUID)


def relink_lingdrom_to_fbx() -> int:
    path = SCENE / "Lingdrom.unity"
    return replace_guid_in_file(path, DAOYANG_OBJ_GUID, DAOYANG_FBX_GUID)


def ensure_lib4_blend_meta() -> None:
    blend_meta = SCENE / "图书馆四楼.blend.meta"
    if not blend_meta.is_file():
        template = SCENE / "lib2f.blend.meta"
        if template.is_file():
            shutil.copy2(template, blend_meta)
    fix_meta(blend_meta, LIB4_GUID)


def copy_lib4_blend_if_missing() -> None:
    dest = SCENE / "图书馆四楼.blend"
    if dest.is_file():
        print(f"  blend already exists ({dest.stat().st_size // 1024 // 1024} MB)")
        return
    if not SOURCE_LIB4_BLEND.is_file():
        print("  WARN source 图书馆四楼.blend not found; keeping existing FBX")
        return
    print("  copying 图书馆四楼.blend (~10GB, may take several minutes)...")
    shutil.copy2(SOURCE_LIB4_BLEND, dest)
    print(f"  blend copied ({dest.stat().st_size // 1024 // 1024} MB)")


def main() -> None:
    stamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    BACKUP.mkdir(parents=True, exist_ok=True)
    for name in ("Lingdrom.unity", "dorm.scene", "libraryfloor2.scene", "libraryfloor4.scene"):
        src = SCENE / name
        if src.is_file():
            ext = ".unity" if name.endswith(".unity") else ".scene"
            dst = BACKUP / f"{src.stem}_{stamp}{ext}"
            shutil.copy2(src, dst)
            print(f"backup: {dst.name}")

    print("\n=== Fix library FBX meta GUIDs ===")
    fix_meta(SCENE / "lib2f.fbx.meta", LIB2F_GUID)
    fix_meta(SCENE / "图书馆四楼.fbx.meta", LIB4_GUID)
    fix_meta(MESHES / "第八书院宿舍.fbx.meta", DORM_FBX_GUID)
    ensure_lib4_blend_meta()

    print("\n=== Library floor 2 scene GUID + prefab ===")
    n = replace_guid_in_file(SCENE / "libraryfloor2.scene", ORPHAN_LIB2F_GUID, LIB2F_GUID)
    print(f"  replaced orphan guid {n} times")
    restore_prefab_block("libraryfloor2.scene", "2099878117")

    print("\n=== Library floor 4 prefab ===")
    restore_prefab_block("libraryfloor4.scene", "20395746")

    print("\n=== Install 道扬.fbx + relink Lingdrom ===")
    install_daoyang_fbx()
    n = relink_lingdrom_to_fbx()
    print(f"  Lingdrom guid refs updated: {n}")

    print("\n=== Remove dorm LegacyDorm material overrides ===")
    removed = remove_dorm_legacy_overrides()
    print(f"  removed {removed} legacy overrides")

    print("\n=== Optional: copy floor4 blend ===")
    copy_lib4_blend_if_missing()

    print("\nDone. Open Unity and run:")
    print("  Tools/场景/一键修复 贴图与GUID（Lingdrom+dorm+图书馆2/4楼）")
    print("Or batch: -executeMethod SceneTextureGuidRepairBatch.ExecuteBatch")


if __name__ == "__main__":
    main()
