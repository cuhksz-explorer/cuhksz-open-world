# -*- coding: utf-8 -*-
"""
仅恢复 GrassBundle1.prefab 内嵌网格（从同包 GrassBundle2 复制 Mesh 数据块）。
不修改 .meta（保留 guid fde66c56408f4264cab296b1abd2da58），不修改任何场景文件。
场景里各 GrassBundle1 实例的 Transform 覆盖（位置/旋转/缩放）保持不变。
"""
from __future__ import annotations

import shutil
from datetime import datetime
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
FOLIAGE = ROOT / "Assets" / "草坪" / "Plants and Foliage" / "Ground Foliage" / "Plants and Foliage"
GB1 = FOLIAGE / "GrassBundle1.prefab"
GB2 = FOLIAGE / "GrassBundle2.prefab"
MESH_MARKER = "--- !u!43 &4300000"


def extract_yaml_block(text: str, marker: str) -> str:
    start = text.find(marker)
    if start < 0:
        raise SystemExit(f"marker not found: {marker}")
    next_block = text.find("\n--- !u!", start + len(marker))
    if next_block < 0:
        raise SystemExit("next YAML block not found after mesh")
    return text[start:next_block]


def mesh_is_empty(mesh_block: str) -> bool:
    return "indexCount: 0" in mesh_block and "vertexCount: 0" in mesh_block


def main() -> None:
    if not GB1.is_file() or not GB2.is_file():
        raise SystemExit(f"missing prefab: {GB1} or {GB2}")

    gb1_text = GB1.read_text(encoding="utf-8")
    gb2_text = GB2.read_text(encoding="utf-8")

    mesh1 = extract_yaml_block(gb1_text, MESH_MARKER)
    mesh2 = extract_yaml_block(gb2_text, MESH_MARKER)

    if not mesh_is_empty(mesh1):
        print("GrassBundle1 mesh already has geometry; no change.")
        return

    if mesh_is_empty(mesh2):
        raise SystemExit("GrassBundle2 mesh is also empty; cannot restore.")

    stamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    backup = GB1.with_name(f"GrassBundle1.prefab.bak_reimport_{stamp}")
    shutil.copy2(GB1, backup)
    print(f"Backup: {backup}")

    restored = gb1_text.replace(mesh1, mesh2, 1)
    GB1.write_text(restored, encoding="utf-8")

    restored_mesh = extract_yaml_block(restored, MESH_MARKER)
    if mesh_is_empty(restored_mesh):
        shutil.copy2(backup, GB1)
        raise SystemExit("restore failed; reverted from backup")

    print("Restored GrassBundle1 embedded mesh from GrassBundle2.")
    print("Prefab GUID unchanged. Scene instances untouched.")
    print("In Unity: select GrassBundle1.prefab -> Reimport, or focus Project window to refresh.")


if __name__ == "__main__":
    main()
