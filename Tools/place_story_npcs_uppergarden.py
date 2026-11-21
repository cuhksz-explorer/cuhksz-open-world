#!/usr/bin/env python3
"""Place missing Content1 story NPC prefabs into uppergarden.unity."""

from __future__ import annotations

import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SCENE = ROOT / "Assets" / "scene" / "uppergarden.unity"

GUID_SENIOR = "066ee33f648549199c14dfe15eedc6f8"  # 迎新学长
GUID_TRIGGER = "cfcd12bec9ff4068adba39d045137584"  # 剧情触发点

NPC_SENIOR = {
    "instance_id": 2321224135719450375,
    "guid": GUID_SENIOR,
    "document_id": "M0-1-3",
    "name": "StoryNPC_迎新学长_M0-1-3",
    "position": (352.13, 44.51, -625.67),
    "script_file_id": 910758710620181623,
    "transform_file_id": 3518245316141033746,
    "root_go_file_id": 1681118213843356613,
    "show_visual": True,
}

NPC_TRIGGER = {
    "instance_id": 2321224135719450376,
    "guid": GUID_TRIGGER,
    "document_id": "M0-3-1",
    "name": "StoryNPC_剧情触发点_M0-3-1",
    "position": (352.13, 44.51, -625.67),
    "script_file_id": 1952164378557562317,
    "transform_file_id": 5765187333852775953,
    "root_go_file_id": 6479814816381371715,
    "show_visual": False,
}


def already_placed(text: str, document_id: str) -> bool:
    return f"documentId\n      value: {document_id}" in text or f"value: {document_id}" in text and document_id in text


def build_prefab_instance(npc: dict) -> str:
    x, y, z = npc["position"]
    guid = npc["guid"]
    tf = npc["transform_file_id"]
    script = npc["script_file_id"]
    root_go = npc["root_go_file_id"]
    lines = [
        f"--- !u!1001 &{npc['instance_id']}",
        "PrefabInstance:",
        "  m_ObjectHideFlags: 0",
        "  serializedVersion: 2",
        "  m_Modification:",
        "    serializedVersion: 3",
        "    m_TransformParent: {fileID: 0}",
        "    m_Modifications:",
        f"    - target: {{fileID: {script}, guid: {guid}, type: 3}}",
        "      propertyPath: documentId",
        f"      value: {npc['document_id']}",
        "      objectReference: {fileID: 0}",
    ]
    if npc["show_visual"]:
        lines.extend(
            [
                f"    - target: {{fileID: {script}, guid: {guid}, type: 3}}",
                "      propertyPath: hidePrefabVisualInPlayMode",
                "      value: 0",
                "      objectReference: {fileID: 0}",
            ]
        )
    for prop, val in (
        ("m_LocalPosition.x", x),
        ("m_LocalPosition.y", y),
        ("m_LocalPosition.z", z),
        ("m_LocalRotation.w", 1),
        ("m_LocalRotation.x", 0),
        ("m_LocalRotation.y", 0),
        ("m_LocalRotation.z", 0),
        ("m_LocalEulerAnglesHint.x", 0),
        ("m_LocalEulerAnglesHint.y", 0),
        ("m_LocalEulerAnglesHint.z", 0),
    ):
        lines.extend(
            [
                f"    - target: {{fileID: {tf}, guid: {guid}, type: 3}}",
                f"      propertyPath: {prop}",
                f"      value: {val}",
                "      objectReference: {fileID: 0}",
            ]
        )
    lines.extend(
        [
            f"    - target: {{fileID: {root_go}, guid: {guid}, type: 3}}",
            "      propertyPath: m_Name",
            f"      value: {npc['name']}",
            "      objectReference: {fileID: 0}",
            "    m_RemovedComponents: []",
            "    m_RemovedGameObjects: []",
            "    m_AddedGameObjects: []",
            "    m_AddedComponents: []",
            f"  m_SourcePrefab: {{fileID: 100100000, guid: {guid}, type: 3}}",
            "",
        ]
    )
    return "\n".join(lines)


def main() -> None:
    text = SCENE.read_text(encoding="utf-8")
    to_add: list[dict] = []
    for npc in (NPC_SENIOR, NPC_TRIGGER):
        if already_placed(text, npc["document_id"]):
            print(f"skip {npc['document_id']} (already in scene)")
            continue
        to_add.append(npc)

    if not to_add:
        print("nothing to do")
        return

    blocks = "\n".join(build_prefab_instance(npc) for npc in to_add)
    marker = "--- !u!1660057539 &9223372036854775807\nSceneRoots:"
    if marker not in text:
        raise SystemExit("SceneRoots block not found in uppergarden.unity")
    text = text.replace(marker, blocks + marker, 1)

    for npc in to_add:
        root_line = f"  - {{fileID: {npc['instance_id']}}}\n"
        if root_line.strip() not in text:
            text = re.sub(
                r"(SceneRoots:\n  m_ObjectHideFlags: 0\n  m_Roots:\n)",
                r"\1" + root_line,
                text,
                count=1,
            )

    SCENE.write_text(text, encoding="utf-8")
    print(f"placed {len(to_add)} story NPC(s) in {SCENE}")


if __name__ == "__main__":
    main()
