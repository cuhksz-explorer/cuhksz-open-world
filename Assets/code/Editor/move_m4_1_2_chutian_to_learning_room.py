# -*- coding: utf-8 -*-
"""Move StoryNPC_楚天_M4-1-2 from undergarden to learning room at built-in seminar position."""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
UNDER = ROOT / "Assets" / "scene" / "undergarden.unity"
LEARN = ROOT / "Assets" / "scene" / "learning room.unity"

MARKER_START = "--- !u!1001 &554764110"
NEW_ID = "2890100102"
POS = ("26.4084282", "0.656000018", "-35.5740013")

PREFAB_BLOCK = f'''--- !u!1001 &{NEW_ID}
PrefabInstance:
  m_ObjectHideFlags: 0
  serializedVersion: 2
  m_Modification:
    serializedVersion: 3
    m_TransformParent: {{fileID: 0}}
    m_Modifications:
    - target: {{fileID: 2939173051680558632, guid: 6aa287a8c9914b89a26c3b04f6d2a2c8, type: 3}}
      propertyPath: m_Name
      value: "StoryNPC_\\u695A\\u5929_M4-1-2"
      objectReference: {{fileID: 0}}
    - target: {{fileID: 5814528171238271604, guid: 6aa287a8c9914b89a26c3b04f6d2a2c8, type: 3}}
      propertyPath: documentId
      value: M4-1-2
      objectReference: {{fileID: 0}}
    - target: {{fileID: 6592311978029514272, guid: 6aa287a8c9914b89a26c3b04f6d2a2c8, type: 3}}
      propertyPath: m_LocalPosition.x
      value: {POS[0]}
      objectReference: {{fileID: 0}}
    - target: {{fileID: 6592311978029514272, guid: 6aa287a8c9914b89a26c3b04f6d2a2c8, type: 3}}
      propertyPath: m_LocalPosition.y
      value: {POS[1]}
      objectReference: {{fileID: 0}}
    - target: {{fileID: 6592311978029514272, guid: 6aa287a8c9914b89a26c3b04f6d2a2c8, type: 3}}
      propertyPath: m_LocalPosition.z
      value: {POS[2]}
      objectReference: {{fileID: 0}}
    - target: {{fileID: 6592311978029514272, guid: 6aa287a8c9914b89a26c3b04f6d2a2c8, type: 3}}
      propertyPath: m_LocalRotation.w
      value: 1
      objectReference: {{fileID: 0}}
    - target: {{fileID: 6592311978029514272, guid: 6aa287a8c9914b89a26c3b04f6d2a2c8, type: 3}}
      propertyPath: m_LocalRotation.x
      value: 0
      objectReference: {{fileID: 0}}
    - target: {{fileID: 6592311978029514272, guid: 6aa287a8c9914b89a26c3b04f6d2a2c8, type: 3}}
      propertyPath: m_LocalRotation.y
      value: 0
      objectReference: {{fileID: 0}}
    - target: {{fileID: 6592311978029514272, guid: 6aa287a8c9914b89a26c3b04f6d2a2c8, type: 3}}
      propertyPath: m_LocalRotation.z
      value: 0
      objectReference: {{fileID: 0}}
    - target: {{fileID: 6592311978029514272, guid: 6aa287a8c9914b89a26c3b04f6d2a2c8, type: 3}}
      propertyPath: m_LocalEulerAnglesHint.x
      value: 0
      objectReference: {{fileID: 0}}
    - target: {{fileID: 6592311978029514272, guid: 6aa287a8c9914b89a26c3b04f6d2a2c8, type: 3}}
      propertyPath: m_LocalEulerAnglesHint.y
      value: 0
      objectReference: {{fileID: 0}}
    - target: {{fileID: 6592311978029514272, guid: 6aa287a8c9914b89a26c3b04f6d2a2c8, type: 3}}
      propertyPath: m_LocalEulerAnglesHint.z
      value: 0
      objectReference: {{fileID: 0}}
    m_RemovedComponents: []
    m_RemovedGameObjects: []
    m_AddedGameObjects: []
    m_AddedComponents: []
  m_SourcePrefab: {{fileID: 100100000, guid: 6aa287a8c9914b89a26c3b04f6d2a2c8, type: 3}}
'''


def remove_from_undergarden(text: str) -> str:
    if MARKER_START not in text:
        raise SystemExit("M4-1-2 block not found in undergarden")
    start = text.index(MARKER_START)
    next_block = text.find("\n--- !u!", start + len(MARKER_START))
    if next_block < 0:
        raise SystemExit("end of M4-1-2 block not found")
    block = text[start:next_block]
    if "M4-1-2" not in block:
        raise SystemExit("block is not M4-1-2")
    text = text[:start] + text[next_block:]
    text = text.replace("  - {fileID: 554764110}\n", "")
    return text


def add_to_learning_room(text: str) -> str:
    if f"&{NEW_ID}" in text:
        raise SystemExit("M4-1-2 already in learning room")
    anchor = "--- !u!1660057539 &9223372036854775807\nSceneRoots:"
    if anchor not in text:
        raise SystemExit("SceneRoots not found in learning room")
    text = text.replace(anchor, PREFAB_BLOCK + anchor, 1)
    text = text.replace(
        "  m_Roots:\n  - {fileID: 2890100101}",
        f"  m_Roots:\n  - {{fileID: {NEW_ID}}}\n  - {{fileID: 2890100101}}",
        1,
    )
    return text


def main() -> None:
    under = remove_from_undergarden(UNDER.read_text(encoding="utf-8"))
    UNDER.write_text(under, encoding="utf-8")
    learn = add_to_learning_room(LEARN.read_text(encoding="utf-8"))
    LEARN.write_text(learn, encoding="utf-8")
    print("Moved StoryNPC_楚天_M4-1-2 to learning room at", POS)


if __name__ == "__main__":
    main()
