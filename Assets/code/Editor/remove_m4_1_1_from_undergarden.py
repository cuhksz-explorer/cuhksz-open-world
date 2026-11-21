# -*- coding: utf-8 -*-
"""Remove only StoryNPC_楚天_M4-1-1 from undergarden.unity (keep M4-1-2 and all other NPCs)."""
from pathlib import Path

SCENE = Path(__file__).resolve().parents[3] / "Assets" / "scene" / "undergarden.unity"
MARKER_START = "--- !u!1001 &1569327320"
MARKER_NAME = 'value: "StoryNPC_\\u695A\\u5929_M4-1-1"'


def main() -> None:
    text = SCENE.read_text(encoding="utf-8")
    if MARKER_START not in text:
        raise SystemExit("M4-1-1 prefab block not found (maybe already removed)")

    start = text.index(MARKER_START)
    next_block = text.find("\n--- !u!", start + len(MARKER_START))
    if next_block < 0:
        raise SystemExit("Could not find end of prefab block")

    block = text[start:next_block]
    if MARKER_NAME not in block:
        raise SystemExit("Block at 1569327320 is not M4-1-1")

    text = text[:start] + text[next_block:]
    text = text.replace("  - {fileID: 1569327320}\n", "")

    SCENE.write_text(text, encoding="utf-8")
    print("Removed StoryNPC_楚天_M4-1-1 from undergarden.unity only.")


if __name__ == "__main__":
    main()
