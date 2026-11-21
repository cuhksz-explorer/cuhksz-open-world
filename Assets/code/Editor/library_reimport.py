# -*- coding: utf-8 -*-
"""
Re-import library floor 1/2/4 models from Blender sources while preserving scene GUIDs and prefab layout.

Run from project root:
  py -3 Assets/code/Editor/library_reimport.py

Does NOT modify UI, planes, or non-library objects in scenes.
"""
from __future__ import annotations

import re
import shutil
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
SCENE = ROOT / "Assets" / "scene"
BACKUP = ROOT / "Assets" / "code" / "Editor" / "library_reimport_backup"

SOURCES = {
    "floor1": Path(
        r"z:\微信\xwechat_files\wxid_iex8clusq8ee32_2373\msg\file\2026-05\图书馆1楼.blend"
    ),
    "floor2": Path(
        r"z:\微信\xwechat_files\wxid_iex8clusq8ee32_2373\msg\file\2026-05\lib2f.blend"
    ),
    "floor4": Path(r"z:\PHY1001 FINAL\图书馆四楼.blend"),
}

# Scene prefab -> model asset (path relative to Assets/scene, historical GUID)
MODELS = {
    "libraryfloor1.scene": {
        "prefab_id": "1508780668",
        "guid": "5e198423aa994ca4bace71cb259a7a4f",
        "old_asset": "图书馆1楼.fbx",
        "new_asset": "图书馆1楼.blend",
        "source_key": "floor1",
    },
    "libraryfloor2.scene": {
        "prefab_id": "2099878117",
        "guid": "7aa5c8e04a22f6c47918ec4d53021da8",
        "old_asset": "lib2f.fbx",
        "new_asset": "lib2f.blend",
        "source_key": "floor2",
    },
    "libraryfloor4.scene": {
        "prefab_id": "20395746",
        "guid": "0fc0c835acb8857428e7673f5c1b0a62",
        "old_asset": "图书馆四楼.blend",
        "new_asset": "图书馆四楼.blend",
        "source_key": "floor4",
    },
}

# LegacyLibraryFloor4 only (not shell materials)
LEGACY_MAT_GUIDS = {
    "0f3db298c9436c44388d5381e72782f5",
    "a2bd9bb494c4865499bbbc4520a763d9",
    "a0e49c15890d2fa4394e051a07209688",
    "b359ff48644d3fd458104f527ce3df1b",
    "d62fef8d8c5e9ea41ae23fdfd3c47745",
    "a198cbf268b051a45827374c18bbbbcb",
    "9fb2bf44b7e29ec40953849db21c5496",
    "1e21e25df9560bb488e51736f5f86664",
    "31a655bcafb8cde4b96eebc3ab36b212",
    "b2f45ec83f4945646a30bec55deb72ba",
}

DELETE_PATHS = [
    SCENE / "LegacyLibraryFloor4",
    SCENE / "图书馆1楼.fbm",
    SCENE / "lib2f Materials",
    SCENE / "图书馆1楼.fbx",
    SCENE / "lib2f.fbx",
    # lib2f.blend is overwritten in place (Unity may lock delete)
]

# Unity ModelImporter enums: materialName 1=BasedOnMaterialName, materialLocation 1=External
IMPORTER_SNIPPET = """  materials:
    materialImportMode: 1
    materialName: 1
    materialSearch: 2
    materialLocation: 1
"""


def read_guid(meta_path: Path) -> str:
    text = meta_path.read_text(encoding="utf-8", errors="replace")
    m = re.search(r"^guid: ([0-9a-f]{32})$", text, re.M)
    if not m:
        raise ValueError(f"Invalid meta GUID: {meta_path}")
    return m.group(1)


def write_meta_guid(meta_path: Path, guid: str) -> None:
    text = meta_path.read_text(encoding="utf-8", errors="replace")
    text = re.sub(r"^guid: .+$", f"guid: {guid}", text, count=1, flags=re.M)
    meta_path.write_text(text, encoding="utf-8")


def fix_model_importer_meta(meta_path: Path) -> None:
    text = meta_path.read_text(encoding="utf-8", errors="replace")
    text = re.sub(
        r"  materials:\n    materialImportMode: \d+\n    materialName: \d+\n    materialSearch: \d+\n    materialLocation: \d+\n",
        IMPORTER_SNIPPET,
        text,
        count=1,
    )
    meta_path.write_text(text, encoding="utf-8")


def backup_prefab_block(scene_path: Path, prefab_id: str) -> None:
    text = scene_path.read_text(encoding="utf-8", errors="replace")
    pattern = rf"(--- !u!1001 &{prefab_id}\nPrefabInstance:.*?)(?=\n--- !u!)"
    m = re.search(pattern, text, re.S)
    if not m:
        raise RuntimeError(f"PrefabInstance &{prefab_id} not found in {scene_path.name}")
    BACKUP.mkdir(parents=True, exist_ok=True)
    out = BACKUP / f"{scene_path.stem}_prefab_{prefab_id}.yaml"
    out.write_text(m.group(1), encoding="utf-8")
    print(f"  Backed up PrefabInstance &{prefab_id} -> {out.name}")


def strip_legacy_material_overrides(scene_path: Path) -> int:
    """Remove scene material overrides pointing at LegacyLibraryFloor4 (let import use Materials/)."""
    text = scene_path.read_text(encoding="utf-8", errors="replace")
    count = 0
    legacy_re = "|".join(LEGACY_MAT_GUIDS)

    def repl_renderer_block(match: re.Match) -> str:
        nonlocal count
        block = match.group(0)
        if not re.search(rf"guid: ({legacy_re}), type: 2", block):
            return block
        # Remove m_Materials override lines; keep renderer, mesh will use imported material
        new_block = re.sub(
            r"\n  m_Materials:\n(?:  - \{fileID: \d+, guid: [0-9a-f]{32}, type: 2\}\n)+",
            "",
            block,
        )
        if new_block != block:
            count += 1
        return new_block

    text = re.sub(
        r"--- !u!23 &\d+\nMeshRenderer:.*?(?=\n--- !u!|\Z)",
        repl_renderer_block,
        text,
        flags=re.S,
    )
    if count:
        scene_path.write_text(text, encoding="utf-8")
    return count


def rm_tree(path: Path) -> None:
    if not path.exists():
        return
    if path.is_dir():
        shutil.rmtree(path)
    else:
        try:
            path.unlink(missing_ok=True)
        except OSError as e:
            print(f"  WARN could not delete {path.name}: {e} (will overwrite)")
            return
    meta = Path(str(path) + ".meta")
    if meta.is_file() and path.suffix.lower() in {".fbx", ".blend"}:
        # Keep .meta for GUID when replacing model file
        pass
    elif meta.is_file():
        meta.unlink()
    print(f"  Deleted {path.relative_to(ROOT)}")


def migrate_fbx_to_blend(old_name: str, new_name: str, guid: str, source: Path) -> None:
    old_path = SCENE / old_name
    new_path = SCENE / new_name
    old_meta = SCENE / f"{old_name}.meta"
    new_meta = SCENE / f"{new_name}.meta"

    if not source.is_file():
        raise FileNotFoundError(source)

    # Preserve meta GUID from old fbx
    if old_meta.is_file():
        saved_guid = read_guid(old_meta)
        if saved_guid != guid:
            print(f"  WARN meta guid {saved_guid} != expected {guid}, using expected")
        meta_template = old_meta.read_text(encoding="utf-8", errors="replace")
    else:
        meta_template = None

    rm_tree(old_path)
    rm_tree(old_meta)

    shutil.copy2(source, new_path)
    print(f"  Copied {source.name} -> {new_path.name} ({new_path.stat().st_size // 1024 // 1024} MB)")

    if meta_template:
        new_meta.write_text(
            re.sub(r"^guid: .+$", f"guid: {guid}", meta_template, count=1, flags=re.M),
            encoding="utf-8",
        )
    else:
        new_meta.write_text(
            f"fileFormatVersion: 2\nguid: {guid}\nDefaultImporter:\n  externalObjects: {{}}\n"
            "  userData:\n  assetBundleName:\n  assetBundleVariant:\n",
            encoding="utf-8",
        )

    # Blender uses ModelImporter in Unity when configured - copy structure from floor4 if needed
    if "ModelImporter" not in new_meta.read_text(encoding="utf-8"):
        # Replace DefaultImporter with minimal ModelImporter from template blend meta
        tmpl = (SCENE / "图书馆四楼.blend.meta").read_text(encoding="utf-8", errors="replace")
        tmpl = re.sub(r"^guid: .+$", f"guid: {guid}", tmpl, count=1, flags=re.M)
        new_meta.write_text(tmpl, encoding="utf-8")

    fix_model_importer_meta(new_meta)
    assert read_guid(new_meta) == guid


def refresh_floor4_blend(source: Path, guid: str) -> None:
    dest = SCENE / "图书馆四楼.blend"
    meta = SCENE / "图书馆四楼.blend.meta"
    saved = read_guid(meta)
    if saved != guid:
        raise ValueError(f"floor4 meta guid mismatch {saved} != {guid}")
    shutil.copy2(source, dest)
    fix_model_importer_meta(meta)
    print(f"  Refreshed 图书馆四楼.blend ({dest.stat().st_size // 1024 // 1024} MB)")


def verify_scene_prefab_guids() -> None:
    for scene_name, cfg in MODELS.items():
        scene = SCENE / scene_name
        text = scene.read_text(encoding="utf-8", errors="replace")
        guid = cfg["guid"]
        if f"guid: {guid}" not in text:
            print(f"  WARN {scene_name}: no reference to {guid}")
        else:
            print(f"  OK {scene_name} references model guid {guid[:8]}...")


def install_blend(asset_name: str, guid: str, source: Path, meta_template: Path | None = None) -> None:
    dest = SCENE / asset_name
    meta = SCENE / f"{asset_name}.meta"
    if not source.is_file():
        raise FileNotFoundError(source)
    try:
        if source.resolve() != dest.resolve():
            shutil.copy2(source, dest)
        else:
            print(f"  {asset_name} already linked to source (skip copy)")
    except OSError as e:
        print(f"  WARN copy {asset_name}: {e}")
    mb = dest.stat().st_size // 1024 // 1024
    print(f"  Installed {asset_name} ({mb} MB)")
    if not meta.is_file():
        tmpl = meta_template or (SCENE / "lib2f.blend.meta")
        if not tmpl.is_file():
            tmpl = SCENE / "图书馆四楼.blend.meta"
        text = tmpl.read_text(encoding="utf-8", errors="replace")
        text = re.sub(r"^guid: .+$", f"guid: {guid}", text, 1, flags=re.M)
        meta.write_text(text, encoding="utf-8")
    write_meta_guid(meta, guid)
    fix_model_importer_meta(meta)
    assert read_guid(meta) == guid


def import_models_only() -> None:
    """Resume after partial run: copy blends + fix metas (no delete)."""
    print("=== Import blends (resume) ===")
    install_blend("图书馆1楼.blend", MODELS["libraryfloor1.scene"]["guid"], SOURCES["floor1"])
    install_blend("lib2f.blend", MODELS["libraryfloor2.scene"]["guid"], SOURCES["floor2"])
    refresh_floor4_blend(SOURCES["floor4"], MODELS["libraryfloor4.scene"]["guid"])
    verify_scene_prefab_guids()


def main() -> None:
    print("=== 1. Backup PrefabInstance blocks ===")
    for scene_name, cfg in MODELS.items():
        backup_prefab_block(SCENE / scene_name, cfg["prefab_id"])

    print("\n=== 2. Strip Legacy material overrides (floor4) ===")
    n = strip_legacy_material_overrides(SCENE / "libraryfloor4.scene")
    print(f"  Removed legacy overrides on {n} MeshRenderers")

    print("\n=== 3. Delete old library-only assets ===")
    for p in DELETE_PATHS:
        rm_tree(p)

    print("\n=== 4. Import from Blender sources (preserve GUIDs) ===")
    migrate_fbx_to_blend(
        "图书馆1楼.fbx",
        "图书馆1楼.blend",
        MODELS["libraryfloor1.scene"]["guid"],
        SOURCES["floor1"],
    )
    migrate_fbx_to_blend(
        "lib2f.fbx",
        "lib2f.blend",
        MODELS["libraryfloor2.scene"]["guid"],
        SOURCES["floor2"],
    )
    refresh_floor4_blend(SOURCES["floor4"], MODELS["libraryfloor4.scene"]["guid"])

    print("\n=== 5. Verify scene references ===")
    verify_scene_prefab_guids()

    print("\nDone.")
    print("Next: Open Unity/Tuanjie — wait for Reimport of 图书馆1楼.blend, lib2f.blend, 图书馆四楼.blend")
    print("Shell folders (lib2f Shell / 图书馆四楼 Shell) were kept; UI and planes untouched.")


def fix_all_blend_metas() -> None:
    for name in ("图书馆1楼.blend.meta", "lib2f.blend.meta", "图书馆四楼.blend.meta"):
        fix_model_importer_meta(SCENE / name)
        print(f"  meta importer OK: {name}")


def restore_prefab_blocks() -> None:
    for scene_name, cfg in MODELS.items():
        backup = BACKUP / f"{Path(scene_name).stem}_prefab_{cfg['prefab_id']}.yaml"
        if not backup.is_file():
            print(f"  WARN missing backup {backup.name}")
            continue
        scene_path = SCENE / scene_name
        block = backup.read_text(encoding="utf-8")
        text = scene_path.read_text(encoding="utf-8")
        pattern = rf"(--- !u!1001 &{cfg['prefab_id']}\nPrefabInstance:.*?)(?=\n--- !u!)"
        m = re.search(pattern, text, re.S)
        if not m:
            print(f"  WARN no prefab &{cfg['prefab_id']} in {scene_name}")
            continue
        scene_path.write_text(text[: m.start()] + block + text[m.end() :], encoding="utf-8")
        print(f"  Restored prefab &{cfg['prefab_id']} in {scene_name}")


if __name__ == "__main__":
    import sys

    if len(sys.argv) > 1 and sys.argv[1] == "--import-only":
        import_models_only()
    elif len(sys.argv) > 1 and sys.argv[1] == "--fix-meta":
        fix_all_blend_metas()
    elif len(sys.argv) > 1 and sys.argv[1] == "--restore-prefabs":
        restore_prefab_blocks()
    else:
        main()
