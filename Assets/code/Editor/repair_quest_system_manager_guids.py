# -*- coding: utf-8 -*-
"""Repair QuestSystemManager script GUIDs in scene/prefab YAML (batch, no Unity required)."""
from __future__ import annotations

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
ASSETS = ROOT / "Assets"

STORY_RUNTIME_GUID = "cb49cc38d4a14b8a8ef5a2a9ee0d577c"
QUEST_MANAGER_GUID = "ec84147cd91d44b0b07ebe99bc45f0d7"
MANAGER_OBJECT_NAME = "QuestSystemManager"

QUEST_MANAGER_FIELD_HINTS = (
    "questChains:",
    "gameServerSync:",
    "autoCreateProgressWhenEmpty:",
    "autoCheckDistanceCompletion:",
    "enableLocalProgressSave:",
    "initialGold:",
    "verboseLog:",
)


def build_game_object_names(yaml: str) -> dict[str, str]:
    names: dict[str, str] = {}
    current_id: str | None = None
    for line in yaml.splitlines():
        if line.startswith("--- !u!1 &"):
            current_id = line.split("&", 1)[1].strip()
            continue
        if current_id and line.lstrip().startswith("m_Name:"):
            names[current_id] = line.split("m_Name:", 1)[1].strip()
            current_id = None
    return names


def repair_yaml(yaml: str) -> tuple[str, int]:
    go_name_by_id = build_game_object_names(yaml)

    fixed_blocks = 0

    def repl(match: re.Match[str]) -> str:
        nonlocal fixed_blocks
        block = match.group(0)
        if STORY_RUNTIME_GUID not in block:
            return block

        has_manager_fields = any(h in block for h in QUEST_MANAGER_FIELD_HINTS)
        named_manager = False
        go_m = re.search(r"m_GameObject: \{fileID: (\d+)\}", block)
        if go_m and go_name_by_id.get(go_m.group(1)) == MANAGER_OBJECT_NAME:
            named_manager = True

        if not has_manager_fields and not named_manager:
            return block

        fixed_blocks += 1
        return block.replace(STORY_RUNTIME_GUID, QUEST_MANAGER_GUID, 1)

    pattern = re.compile(r"--- !u!114 &\d+\nMonoBehaviour:.*?(?=\n--- |\Z)", re.S)
    repaired = pattern.sub(repl, yaml)
    return repaired, fixed_blocks


def main() -> int:
    fixed_files = 0
    total_blocks = 0
    for path in ASSETS.rglob("*"):
        if path.suffix not in {".unity", ".scene", ".prefab"}:
            continue
        if "/Backups/" in path.as_posix():
            continue
        try:
            text = path.read_text(encoding="utf-8")
        except UnicodeDecodeError:
            continue
        repaired, blocks = repair_yaml(text)
        if blocks <= 0:
            continue
        if repaired != text:
            path.write_text(repaired, encoding="utf-8")
            fixed_files += 1
            total_blocks += blocks
            print(f"fixed {path.relative_to(ROOT)} ({blocks} blocks)")

    print(f"done: {fixed_files} files, {total_blocks} blocks")
    return 0


if __name__ == "__main__":
    sys.exit(main())
