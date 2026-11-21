# -*- coding: utf-8 -*-
"""Move library StoryNPC prefab instances to undergarden / libraryfloor4."""
from __future__ import annotations

import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
SCENE = ROOT / "Assets" / "scene"
LIBRARY = SCENE / "library.unity"
UNDER = SCENE / "undergarden.unity"
FLOOR4 = SCENE / "libraryfloor4.scene"

# Prefab instance + stripped transform fileIDs to remove from library.unity
LIBRARY_REMOVE_IDS = {
    539583123,
    539583124,
    600301422,
    600301423,
    994338077,
    994338078,
    1114974641,
    1114974642,
    1217465471,
    1217465472,
    1821632166,
    1821632167,
}

PREFAB_ZHOU = {
    "guid": "1363c27cfd430124a9a3f9dcde79ecc1",
    "transform": "6383021646317724884",
    "document": "3688019076079116469",
    "name": "1711528047678318162",
    "label": "\u5468\u781A",
}
PREFAB_SHEN = {
    "guid": "02f65ca32754cdd499a5a246d8e73fd2",
    "transform": "1649459802318662993",
    "document": "4263895629929928928",
    "name": "5290918443843561506",
    "label": "\u6C88\u9752\u79BE",
}
PREFAB_CHU = {
    "guid": "578547abaeaba994c8c9e95a17fb9ca4",
    "transform": "6592311978029514272",
    "document": "5814528171238271604",
    "name": "2939173051680558632",
    "label": "\u695A\u5929",
}
PREFAB_TRIGGER = {
    "guid": "28f21bec5ba70de4388c3340beb92c01",
    "transform": "5765187333852775953",
    "document": "1952164378557562317",
    "name": "6479814816381371715",
    "label": "\u5267\u60C5\u89E6\u53D1\u70B9",
}

UNDER_NPCS = [
    (PREFAB_TRIGGER, "M1-3-3", (-1183.68, 16.69, 878.35)),
    (PREFAB_ZHOU, "M2-5-1", (-1103.2, 13.73, 944.96)),
    (PREFAB_CHU, "M4-3-1", (-1033.55, 29.97, 842.42)),
]

FLOOR4_NPCS = [
    (PREFAB_ZHOU, "M2-3-1", (-68.19, 1.64, 439.16)),
    (PREFAB_SHEN, "M2-2-1\u6C88", (-145.48, 1.64, 88.0)),
    (PREFAB_SHEN, "M2-2-3\u6C88", (-145.48, 1.64, 88.0)),
]

BLOCK_START = re.compile(r"^--- !u!(\d+) &(\d+)\s*$", re.MULTILINE)


def parse_blocks(text: str) -> list[tuple[str, int, str]]:
    matches = list(BLOCK_START.finditer(text))
    blocks = []
    for i, m in enumerate(matches):
        end = matches[i + 1].start() if i + 1 < len(matches) else len(text)
        blocks.append((m.group(1), int(m.group(2)), text[m.start() : end]))
    return blocks


def remove_blocks_by_id(text: str, remove_ids: set[int]) -> str:
    kept = []
    for typ, fid, block in parse_blocks(text):
        if fid in remove_ids:
            continue
        kept.append(block)
    return "".join(kept)


def collect_existing_ids(text: str) -> set[int]:
    return {int(m.group(2)) for m in BLOCK_START.finditer(text)}


def next_free_id(existing: set[int], start: int = 390_000_000) -> int:
    n = start
    while n in existing or (n + 1) in existing:
        n += 2
    return n


def scene_has_document(text: str, document_id: str) -> bool:
    esc = re.escape(document_id)
    return bool(
        re.search(
            rf"propertyPath: documentId\s*\n\s*value: {esc}\b",
            text,
        )
    )


def _ascii_escape(s: str) -> str:
    return s.encode("ascii", "backslashreplace").decode("ascii").replace("\\\\", "\\")


def build_prefab_yaml(
    inst_id: int,
    strip_id: int,
    prefab: dict,
    document_id: str,
    position: tuple[float, float, float],
    parent_id: int = 0,
) -> str:
    x, y, z = position
    name_value = f"StoryNPC_{prefab['label']}_{_ascii_escape(document_id)}"
    doc_value = _ascii_escape(document_id)

    lines = [
        f"--- !u!1001 &{inst_id}",
        "PrefabInstance:",
        "  m_ObjectHideFlags: 0",
        "  serializedVersion: 2",
        "  m_Modification:",
        "    serializedVersion: 3",
        f"    m_TransformParent: {{fileID: {parent_id}}}",
        "    m_Modifications:",
        f"    - target: {{fileID: {prefab['name']}, guid: {prefab['guid']}, type: 3}}",
        "      propertyPath: m_Name",
        f'      value: "{name_value}"',
        "      objectReference: {fileID: 0}",
        f"    - target: {{fileID: {prefab['document']}, guid: {prefab['guid']}, type: 3}}",
        "      propertyPath: documentId",
        f'      value: {doc_value}',
        "      objectReference: {fileID: 0}",
        f"    - target: {{fileID: {prefab['transform']}, guid: {prefab['guid']}, type: 3}}",
        "      propertyPath: m_LocalPosition.x",
        f"      value: {x}",
        "      objectReference: {fileID: 0}",
        f"    - target: {{fileID: {prefab['transform']}, guid: {prefab['guid']}, type: 3}}",
        "      propertyPath: m_LocalPosition.y",
        f"      value: {y}",
        "      objectReference: {fileID: 0}",
        f"    - target: {{fileID: {prefab['transform']}, guid: {prefab['guid']}, type: 3}}",
        "      propertyPath: m_LocalPosition.z",
        f"      value: {z}",
        "      objectReference: {fileID: 0}",
        f"    - target: {{fileID: {prefab['transform']}, guid: {prefab['guid']}, type: 3}}",
        "      propertyPath: m_LocalRotation.w",
        "      value: 1",
        "      objectReference: {fileID: 0}",
        f"    - target: {{fileID: {prefab['transform']}, guid: {prefab['guid']}, type: 3}}",
        "      propertyPath: m_LocalRotation.x",
        "      value: 0",
        "      objectReference: {fileID: 0}",
        f"    - target: {{fileID: {prefab['transform']}, guid: {prefab['guid']}, type: 3}}",
        "      propertyPath: m_LocalRotation.y",
        "      value: 0",
        "      objectReference: {fileID: 0}",
        f"    - target: {{fileID: {prefab['transform']}, guid: {prefab['guid']}, type: 3}}",
        "      propertyPath: m_LocalRotation.z",
        "      value: 0",
        "      objectReference: {fileID: 0}",
        f"    - target: {{fileID: {prefab['transform']}, guid: {prefab['guid']}, type: 3}}",
        "      propertyPath: m_LocalEulerAnglesHint.x",
        "      value: 0",
        "      objectReference: {fileID: 0}",
        f"    - target: {{fileID: {prefab['transform']}, guid: {prefab['guid']}, type: 3}}",
        "      propertyPath: m_LocalEulerAnglesHint.y",
        "      value: 0",
        "      objectReference: {fileID: 0}",
        f"    - target: {{fileID: {prefab['transform']}, guid: {prefab['guid']}, type: 3}}",
        "      propertyPath: m_LocalEulerAnglesHint.z",
        "      value: 0",
        "      objectReference: {fileID: 0}",
        "    m_RemovedComponents: []",
        "    m_RemovedGameObjects: []",
        "    m_AddedGameObjects: []",
        "    m_AddedComponents: []",
        f"  m_SourcePrefab: {{fileID: 100100000, guid: {prefab['guid']}, type: 3}}",
        f"--- !u!4 &{strip_id}",
        "Transform:",
        f"  m_CorrespondingSourceObject: {{fileID: {prefab['transform']}, guid: {prefab['guid']}, type: 3}}",
        f"  m_PrefabInstance: {{fileID: {inst_id}}}",
        "  m_PrefabAsset: {fileID: 0}",
        "",
    ]
    return "\n".join(lines)


def patch_library_story_npcs_children(text: str) -> str:
    """Clear LibraryStoryNpcs children and drop empty parent from SceneRoots."""
    text = re.sub(
        r"(--- !u!4 &1998751429\nTransform:.*?m_Children:\n)(  - \{fileID: \d+\}\n)+",
        r"\1",
        text,
        count=1,
        flags=re.DOTALL,
    )
    # Remove LibraryStoryNpcs root from scene if no children left
    text = re.sub(
        r"  - \{fileID: 1998751429\}\n",
        "",
        text,
        count=1,
    )
    # Remove empty LibraryStoryNpcs GameObject blocks
    for block_id in (1998751428, 1998751429):
        text = remove_blocks_by_id(text, {block_id})
    return text


def insert_before_scene_roots(text: str, insertion: str) -> str:
    marker = "--- !u!1660057539 &9223372036854775807\nSceneRoots:"
    idx = text.rfind(marker)
    if idx < 0:
        raise RuntimeError("SceneRoots not found")
    return text[:idx] + insertion + text[idx:]


def add_npcs_to_scene(path: Path, npc_list: list) -> list[str]:
    text = path.read_text(encoding="utf-8")
    existing = collect_existing_ids(text)
    added = []
    chunks = []

    for prefab, doc_id, pos in npc_list:
        if scene_has_document(text, doc_id):
            added.append(f"skip {path.name} {doc_id} (exists)")
            continue
        inst_id = next_free_id(existing)
        strip_id = inst_id + 1
        existing.add(inst_id)
        existing.add(strip_id)
        chunks.append(build_prefab_yaml(inst_id, strip_id, prefab, doc_id, pos))
        added.append(f"add {path.name} {doc_id} @ {pos}")

    if chunks:
        text = insert_before_scene_roots(text, "".join(chunks))
        path.write_text(text, encoding="utf-8", newline="\n")

    return added


def fix_m5_pool_position(text: str) -> bool:
    """Update M5-5-1-A world position if present."""
    pattern = (
        r"(--- !u!1001 &1072105269\nPrefabInstance:.*?propertyPath: documentId\n"
        r"      value: M5-5-1-A\n.*?propertyPath: m_LocalPosition\.x\n"
        r"      value: )[-0-9.]+(\n.*?propertyPath: m_LocalPosition\.y\n"
        r"      value: )[-0-9.]+(\n.*?propertyPath: m_LocalPosition\.z\n"
        r"      value: )[-0-9.]+"
    )
    repl = r"\g<1>-888.5\g<2>14.25\g<3>898.02"
    new_text, n = re.subn(pattern, repl, text, count=1, flags=re.DOTALL)
    return n > 0


def main():
    lib = LIBRARY.read_text(encoding="utf-8")
    lib = remove_blocks_by_id(lib, LIBRARY_REMOVE_IDS)
    lib = patch_library_story_npcs_children(lib)
    LIBRARY.write_text(lib, encoding="utf-8", newline="\n")
    print(f"library.unity: removed {len(LIBRARY_REMOVE_IDS)} object ids")

    under_results = add_npcs_to_scene(UNDER, UNDER_NPCS)
    floor4_results = add_npcs_to_scene(FLOOR4, FLOOR4_NPCS)
    for line in under_results + floor4_results:
        print(line)

    under = UNDER.read_text(encoding="utf-8")
    if fix_m5_pool_position(under):
        UNDER.write_text(under, encoding="utf-8", newline="\n")
        print("undergarden.unity: fixed M5-5-1-A position")


if __name__ == "__main__":
    main()
