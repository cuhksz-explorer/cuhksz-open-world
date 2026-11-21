#!/usr/bin/env python3
"""Inject StoryNPC prefab instances into computerroom.unity and patch Content1StoryDatabase."""

from __future__ import annotations

import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SCENE = ROOT / "Assets/scene/computerroom.unity"
DATABASE = ROOT / "Assets/code/GameQuestSystem/Content1StoryDatabase.asset"

BASE = (-3.91, 0.95, -23.67)

NPCS = [
    {
        "document_id": "M1-1-1",
        "name": r"StoryNPC_\u6C88\u9752\u79BE_M1-1-1",
        "guid": "1d6cdd74f83f4dc2ac23346f23662a0b",
        "transform": "1649459802318662993",
        "story_npc": "4263895629929928928",
        "game_object": "5290918443843561506",
        "position": BASE,
        "location_id": "computerroom_m1_1_1",
    },
    {
        "document_id": "M1-1-3",
        "name": r"StoryNPC_\u5267\u60C5\u89E6\u53D1\u70B9_M1-1-3",
        "guid": "cfcd12bec9ff4068adba39d045137584",
        "transform": "5765187333852775953",
        "story_npc": "1952164378557562317",
        "game_object": "6479814816381371715",
        "position": (BASE[0], BASE[1], BASE[2] + 1.5),
        "location_id": "computerroom_m1_1_3",
    },
    {
        "document_id": "M1-2-1",
        "name": r"StoryNPC_\u8D75\u6893\u7487_M1-2-1",
        "guid": "6d09d2b77ca645c6adf1e9358c1d668f",
        "transform": "7260227208703931299",
        "story_npc": "5127299752315620437",
        "game_object": "2302143127018740899",
        "position": (BASE[0], BASE[1], BASE[2] + 3.0),
        "location_id": "computerroom_m1_2_1",
    },
    {
        "document_id": "M1-4-1",
        "name": r"StoryNPC_\u5747\u5206\u5E7B\u5F71_M1-4-1",
        "guid": "ca82e0de665a47f899bf85370742c75f",
        "transform": "2992168752625026797",
        "story_npc": "2496777196050671962",
        "game_object": "371871554594061658",
        "position": (BASE[0], BASE[1], BASE[2] + 4.5),
        "location_id": "computerroom_m1_4_1",
    },
    {
        "document_id": "M1-4-3",
        "name": r"StoryNPC_\u5267\u60C5\u89E6\u53D1\u70B9_M1-4-3",
        "guid": "cfcd12bec9ff4068adba39d045137584",
        "transform": "5765187333852775953",
        "story_npc": "1952164378557562317",
        "game_object": "6479814816381371715",
        "position": (BASE[0], BASE[1], BASE[2] + 6.0),
        "location_id": "computerroom_m1_4_3",
    },
    {
        "document_id": "M1-5-1",
        "name": r"StoryNPC_\u5468\u781A_M1-5-1",
        "guid": "5d75e952b3004a2791a477c66e4335a6",
        "transform": "6383021646317724884",
        "story_npc": "3688019076079116469",
        "game_object": "1711528047678318162",
        "position": (BASE[0], BASE[1], BASE[2] + 7.5),
        "location_id": "computerroom_m1_5_1",
    },
]

ROOT_GO_ID = 9900010001
ROOT_TR_ID = 9900010002
PREFAB_BASE_ID = 9900010100


def rotation_mods(transform_id: str, guid: str) -> str:
    lines = []
    for prop, val in [
        ("m_LocalRotation.w", "1"),
        ("m_LocalRotation.x", "0"),
        ("m_LocalRotation.y", "0"),
        ("m_LocalRotation.z", "0"),
        ("m_LocalEulerAnglesHint.x", "0"),
        ("m_LocalEulerAnglesHint.y", "0"),
        ("m_LocalEulerAnglesHint.z", "0"),
    ]:
        lines.append(
            f"    - target: {{fileID: {transform_id}, guid: {guid}, type: 3}}\n"
            f"      propertyPath: {prop}\n"
            f"      value: {val}\n"
            f"      objectReference: {{fileID: 0}}"
        )
    return "\n".join(lines)


def prefab_instance_block(instance_id: int, npc: dict) -> str:
    x, y, z = npc["position"]
    guid = npc["guid"]
    t = npc["transform"]
    sn = npc["story_npc"]
    go = npc["game_object"]
    return f"""--- !u!1001 &{instance_id}
PrefabInstance:
  m_ObjectHideFlags: 0
  serializedVersion: 2
  m_Modification:
    serializedVersion: 3
    m_TransformParent: {{fileID: {ROOT_TR_ID}}}
    m_Modifications:
    - target: {{fileID: {t}, guid: {guid}, type: 3}}
      propertyPath: m_LocalPosition.x
      value: {x}
      objectReference: {{fileID: 0}}
    - target: {{fileID: {t}, guid: {guid}, type: 3}}
      propertyPath: m_LocalPosition.y
      value: {y}
      objectReference: {{fileID: 0}}
    - target: {{fileID: {t}, guid: {guid}, type: 3}}
      propertyPath: m_LocalPosition.z
      value: {z}
      objectReference: {{fileID: 0}}
{rotation_mods(t, guid)}
    - target: {{fileID: {sn}, guid: {guid}, type: 3}}
      propertyPath: documentId
      value: {npc["document_id"]}
      objectReference: {{fileID: 0}}
    - target: {{fileID: {go}, guid: {guid}, type: 3}}
      propertyPath: m_Name
      value: "{npc["name"]}"
      objectReference: {{fileID: 0}}
    m_RemovedComponents: []
    m_RemovedGameObjects: []
    m_AddedGameObjects: []
    m_AddedComponents: []
  m_SourcePrefab: {{fileID: 100100000, guid: {guid}, type: 3}}
"""


def story_root_block() -> str:
    return f"""--- !u!1 &{ROOT_GO_ID}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 7
  m_Component:
  - component: {{fileID: {ROOT_TR_ID}}}
  m_Layer: 0
  m_HasEditorInfo: 1
  m_Name: StoryNPCs
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &{ROOT_TR_ID}
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {ROOT_GO_ID}}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
"""


def patch_scene(text: str) -> str:
    if "m_Name: StoryNPCs" in text:
        print("Scene already has StoryNPCs root; skipping scene injection.")
        return text

    blocks = [story_root_block()]
    for i, npc in enumerate(NPCS):
        blocks.append(prefab_instance_block(PREFAB_BASE_ID + i * 100, npc))

    injection = "\n".join(blocks) + "\n"

    marker = "--- !u!1660057539 &9223372036854775807"
    if marker not in text:
        raise RuntimeError("Could not find SceneRoots in computerroom.unity")

    text = text.replace(marker, injection + marker, 1)

    root_entry = f"  - {{fileID: {ROOT_TR_ID}}}\n"
    if root_entry not in text:
        text = text.replace(
            "  m_Roots:\n",
            f"  m_Roots:\n{root_entry}",
            1,
        )

    return text


def patch_document_block(doc_id: str, pos: tuple[float, float, float], location_id: str) -> None:
    global DATABASE_TEXT
    pattern = rf"(  - documentId: {re.escape(doc_id)}\n(?:.*?\n)*?    locationId: ).*?\n(    targetSceneName: ).*?\n(    fallbackTargetPosition: ).*?\n(    rewardGold:.*?\n    finishLocationName: ).*?\n"

    def replacer(match: re.Match[str]) -> str:
        finish = r"\u673A\u623F/\u9009\u8BFE\u7CFB\u7EDF"
        return (
            f"{match.group(1)}{location_id}\n"
            f"{match.group(2)}computerroom\n"
            f"{match.group(3)}{{x: {pos[0]}, y: {pos[1]}, z: {pos[2]}}}\n"
            f"{match.group(4)}{finish}\n"
        )

    new_text, n = re.subn(pattern, replacer, DATABASE_TEXT, count=1, flags=re.DOTALL)
    if n != 1:
        print(f"Warning: could not patch document {doc_id} (matches={n})")
    else:
        DATABASE_TEXT = new_text


def patch_database(text: str) -> str:
    global DATABASE_TEXT
    DATABASE_TEXT = text

    for npc in NPCS:
        patch_document_block(npc["document_id"], npc["position"], npc["location_id"])

    location_entries = []
    for npc in NPCS:
        x, y, z = npc["position"]
        loc_id = npc["location_id"]
        if f"locationId: {loc_id}" in DATABASE_TEXT:
            continue
        location_entries.append(
            f"  - locationId: {loc_id}\n"
            f"    displayName: \"\\u673A\\u623F/\\u9009\\u8BFE\\u7CFB\\u7EDF\"\n"
            f"    sceneName: computerroom\n"
            f"    position: {{x: {x}, y: {y}, z: {z}}}\n"
            f"    needsConfirmation: 0"
        )

    if location_entries:
        DATABASE_TEXT = DATABASE_TEXT.replace(
            "  npcs:\n",
            "\n".join(location_entries) + "\n  npcs:\n",
            1,
        )

    return DATABASE_TEXT


def main() -> None:
    scene_text = SCENE.read_text(encoding="utf-8")
    scene_text = patch_scene(scene_text)
    SCENE.write_text(scene_text, encoding="utf-8", newline="\n")
    print(f"Patched scene: {SCENE}")

    db_text = DATABASE.read_text(encoding="utf-8")
    db_text = patch_database(db_text)
    DATABASE.write_text(db_text, encoding="utf-8", newline="\n")
    print(f"Patched database: {DATABASE}")


if __name__ == "__main__":
    main()
