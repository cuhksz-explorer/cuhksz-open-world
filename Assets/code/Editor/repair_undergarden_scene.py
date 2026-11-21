# -*- coding: utf-8 -*-
"""Analyze undergarden.unity campus MeshCollider count (read-only).

Do NOT strip YAML blocks offline: removing MeshCollider objects leaves thousands of
dangling fileID references. Use Unity menu:
  Tools/场景配置/undergarden 修复场景（模型引用+碰撞体）

Run: py -3 Assets/code/Editor/repair_undergarden_scene.py
"""
from __future__ import annotations

import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
SCENE = ROOT / "Assets" / "scene" / "undergarden.unity"
CAMPUS_GUID = "e11e17c455d4e1e488139f047c137e3b"
BLOCK_START = re.compile(r"^--- !u!(\d+) &(\d+)\s*$", re.MULTILINE)
MESH_GUID_RE = re.compile(
    rf"m_Mesh: \{{fileID: -?\d+, guid: {re.escape(CAMPUS_GUID)}, type: 3\}}"
)


def parse_blocks(text: str) -> list[tuple[str, int, str]]:
    matches = list(BLOCK_START.finditer(text))
    blocks: list[tuple[str, int, str]] = []
    for i, m in enumerate(matches):
        end = matches[i + 1].start() if i + 1 < len(matches) else len(text)
        blocks.append((m.group(1), int(m.group(2)), text[m.start() : end]))
    return blocks


def main() -> None:
    if not SCENE.is_file():
        raise SystemExit(f"Scene not found: {SCENE}")

    text = SCENE.read_text(encoding="utf-8")
    blocks = parse_blocks(text)

    mesh_colliders = sum(
        1 for typ, _, block in blocks if typ == "64" and MESH_GUID_RE.search(block)
    )
    print(f"Scene: {SCENE}")
    print(f"YAML blocks: {len(blocks)}")
    print(f"Campus (down.fbx) MeshCollider blocks: {mesh_colliders}")
    print("Use UndergardenSceneRepairMenu in Unity Editor to clean invalid colliders safely.")


if __name__ == "__main__":
    main()
