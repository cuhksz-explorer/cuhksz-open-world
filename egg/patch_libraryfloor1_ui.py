#!/usr/bin/env python3
"""DEPRECATED: YAML patch flattens UI hierarchy. Use LibraryFloor1InteractionSetup in Unity Editor instead."""

from __future__ import annotations

import re
import sys
from collections import deque
from pathlib import Path

PROJECT = Path(__file__).resolve().parents[1]
TARGET = PROJECT / "Assets/scene/libraryfloor1.scene"
SOURCE = PROJECT / "Assets/scene/libraryfloor2.scene"

# GameObject fileIDs in libraryfloor2 (by m_Name).
SEED_GAME_OBJECTS = {
    "Phone": 259488039,
    "QuestSystemUI": 728617899,
    "QuestSystemManager": 480048029,
    "EventSystem": 510563300,
    "InteractPrompt_Shared": 1435299961,
}

DOC_HEADER_RE = re.compile(r"^--- !u!(\d+) &(\d+)\s*$", re.M)
FILEID_RE = re.compile(r"\{fileID: (-?\d+)")
NAME_RE = re.compile(r"^\s+m_Name: (.+)$", re.M)
SCENE_ROOTS_RE = re.compile(
    r"(--- !u!1660057539 &9223372036854775807\nSceneRoots:\n  m_ObjectHideFlags: 0\n  m_Roots:\n)((?:  - \{fileID: \d+\}\n)*)"
)
SENTINEL_ID = 9223372036854775807
ID_BASE = 3_000_000_000


def parse_docs(text: str) -> dict[int, str]:
    matches = list(DOC_HEADER_RE.finditer(text))
    docs: dict[int, str] = {}
    for i, match in enumerate(matches):
        start = match.start()
        end = matches[i + 1].start() if i + 1 < len(matches) else len(text)
        file_id = int(match.group(2))
        docs[file_id] = text[start:end]
    return docs


def collect_referenced_ids(block: str, known: set[int]) -> set[int]:
    found: set[int] = set()
    for match in FILEID_RE.finditer(block):
        file_id = int(match.group(1))
        if file_id > 0 and file_id in known:
            found.add(file_id)
    return found


def game_object_root_transform(go_block: str) -> int | None:
    for match in re.finditer(r"^\s*- component: \{fileID: (\d+)\}", go_block, re.M):
        return int(match.group(1))
    return None


PLAYER_TAG = "Player"
PLAYER_MOVEMENT_GUID = "72f441009abc49a48c6d3bc7c9a64279"


def is_player_game_object(go_id: int, docs: dict[int, str]) -> bool:
    block = docs.get(go_id)
    if not block or not block.startswith("--- !u!1 "):
        return False
    if re.search(rf"^\s+m_TagString: {re.escape(PLAYER_TAG)}\s*$", block, re.M):
        return True
    for match in re.finditer(r"^\s*- component: \{fileID: (\d+)\}", block, re.M):
        comp_id = int(match.group(1))
        comp = docs.get(comp_id, "")
        if PLAYER_MOVEMENT_GUID in comp:
            return True
    return False


def collect_player_exclusions(docs: dict[int, str], collected: set[int]) -> set[int]:
    exclude: set[int] = set()
    for file_id in collected:
        block = docs.get(file_id, "")
        if not block.startswith("--- !u!1 ") or not is_player_game_object(file_id, docs):
            continue
        exclude_player_hierarchy(file_id, docs, collected, exclude)
    return exclude


def exclude_player_hierarchy(go_id: int, docs: dict[int, str], collected: set[int], exclude: set[int]) -> None:
    if go_id in exclude:
        return

    go_block = docs.get(go_id, "")
    if not go_block.startswith("--- !u!1 "):
        return

    exclude.add(go_id)
    component_ids = [int(m.group(1)) for m in re.finditer(r"^\s*- component: \{fileID: (\d+)\}", go_block, re.M)]
    for comp_id in component_ids:
        if comp_id in collected:
            exclude.add(comp_id)

    transform_id = component_ids[0] if component_ids else None
    if transform_id is None:
        return

    transform_block = docs.get(transform_id, "")
    for match in re.finditer(r"^\s*- \{fileID: (\d+)\}", transform_block, re.M):
        child_transform_id = int(match.group(1))
        if child_transform_id not in collected or child_transform_id in exclude:
            continue
        child_block = docs.get(child_transform_id, "")
        child_go_match = re.search(r"^\s+m_GameObject: \{fileID: (\d+)\}", child_block, re.M)
        if child_go_match:
            child_go_id = int(child_go_match.group(1))
            exclude_player_hierarchy(child_go_id, docs, collected, exclude)


def strip_excluded_references(block: str, excluded: set[int]) -> str:
    def repl(match: re.Match[str]) -> str:
        file_id = int(match.group(1))
        if file_id in excluded:
            return "{fileID: 0}"
        return match.group(0)

    return FILEID_RE.sub(repl, block)


def collect_subtree(seed_ids: set[int], docs: dict[int, str]) -> set[int]:
    known = set(docs.keys())
    queue = deque(seed_ids)
    collected: set[int] = set()

    while queue:
        file_id = queue.popleft()
        if file_id in collected or file_id not in docs:
            continue
        collected.add(file_id)
        block = docs[file_id]
        for ref in collect_referenced_ids(block, known):
            if ref not in collected:
                queue.append(ref)

    return collected


def remap_block(block: str, mapping: dict[int, int]) -> str:
    header = DOC_HEADER_RE.match(block)
    if header:
        old_header_id = int(header.group(2))
        if old_header_id in mapping:
            block = (
                f"--- !u!{header.group(1)} &{mapping[old_header_id]}\n"
                + block[header.end() :]
            )

    def repl(match: re.Match[str]) -> str:
        old = int(match.group(1))
        if old in mapping:
            return "{fileID: " + str(mapping[old]) + "}"
        return match.group(0)

    return FILEID_RE.sub(repl, block)


def main() -> int:
    if not TARGET.exists() or not SOURCE.exists():
        print("Missing scene file(s).", file=sys.stderr)
        return 1

    target_text = TARGET.read_text(encoding="utf-8")
    source_text = SOURCE.read_text(encoding="utf-8")

    target_docs = parse_docs(target_text)
    source_docs = parse_docs(source_text)

    target_ids = {fid for fid in target_docs if fid != SENTINEL_ID}
    max_target = max(target_ids) if target_ids else 0
    next_id = max(ID_BASE, max_target + 1)

    seeds = set(SEED_GAME_OBJECTS.values())
    missing = [name for name, fid in SEED_GAME_OBJECTS.items() if fid not in source_docs]
    if missing:
        print("Missing seeds in source:", ", ".join(missing), file=sys.stderr)
        return 1

    subtree = collect_subtree(seeds, source_docs)
    excluded = collect_player_exclusions(source_docs, subtree)
    if excluded:
        subtree -= excluded
        print(f"  excluded player objects: {len(excluded)}")

    mapping: dict[int, int] = {}
    for old_id in sorted(subtree):
        mapping[old_id] = next_id
        next_id += 1

    copied_blocks: list[str] = []
    for old_id in sorted(subtree):
        block = strip_excluded_references(source_docs[old_id], excluded)
        copied_blocks.append(remap_block(block, mapping))

    root_transforms: list[int] = []
    for name, go_id in SEED_GAME_OBJECTS.items():
        go_block = source_docs[go_id]
        root = game_object_root_transform(go_block)
        if root is None or root not in mapping:
            print(f"Warning: no root transform for {name}", file=sys.stderr)
            continue
        root_transforms.append(mapping[root])

    roots_match = SCENE_ROOTS_RE.search(target_text)
    if not roots_match:
        print("SceneRoots block not found in target.", file=sys.stderr)
        return 1

    existing_roots = roots_match.group(2)
    new_root_lines = "".join(f"  - {{fileID: {rid}}}\n" for rid in root_transforms)
    patched_roots = roots_match.group(1) + existing_roots + new_root_lines

    insert_pos = roots_match.start()
    patched = target_text[:insert_pos] + "\n".join(copied_blocks) + "\n" + patched_roots + target_text[roots_match.end():]

    backup = TARGET.with_suffix(".scene.bak_ui_patch")
    backup.write_text(target_text, encoding="utf-8")
    TARGET.write_text(patched, encoding="utf-8")

    print(f"Patched {TARGET.name}")
    print(f"  copied objects: {len(subtree)}")
    print(f"  new roots: {len(root_transforms)}")
    print(f"  backup: {backup}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
