# -*- coding: utf-8 -*-
"""Move objects appended after SceneRoots back before SceneRoots in undergarden.unity."""
from __future__ import annotations

import re
from collections import Counter
from datetime import datetime
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
SCENE = ROOT / "Assets" / "scene" / "undergarden.unity"

PATRIOTIC_BLOCK = re.compile(
    r"(--- !u!1 &9223372036854775907\nGameObject:.*?"
    r"--- !u!4 &9223372036854775910\nTransform:.*?m_LocalEulerAnglesHint: \{x: 0, y: 0, z: 0\}\n)",
    re.DOTALL,
)
SCENE_ROOTS = re.compile(r"--- !u!1660057539 &9223372036854775807\nSceneRoots:")
TRANSFORM_ROOT = "  - {fileID: 9223372036854775910}"


def main() -> None:
    text = SCENE.read_text(encoding="utf-8")
    backup = SCENE.with_name(
        f"undergarden.unity.bak_sceneroots_{datetime.now().strftime('%Y%m%d_%H%M%S')}"
    )
    backup.write_text(text, encoding="utf-8")
    print(f"Backup: {backup}")

    match = PATRIOTIC_BLOCK.search(text)
    if not match:
        raise SystemExit("PatrioticMusic blocks not found; scene may already be fixed.")

    patriotic = match.group(1)
    text = text[: match.start()] + text[match.end() :]

    scene_roots = SCENE_ROOTS.search(text)
    if not scene_roots:
        raise SystemExit("SceneRoots block not found.")

    text = text[: scene_roots.start()] + patriotic + text[scene_roots.start() :]

    if TRANSFORM_ROOT not in text:
        text = text.replace(
            "  - {fileID: 2000517010}\n",
            "  - {fileID: 2000517010}\n" + TRANSFORM_ROOT + "\n",
            1,
        )
        print("Added PatrioticMusic Transform to SceneRoots.")

    SCENE.write_text(text, encoding="utf-8")

    ids = re.findall(r"^--- !u!\d+ &(\d+)", text, re.MULTILINE)
    dups = [k for k, v in Counter(ids).items() if v > 1]
    sr_pos = text.find("--- !u!1660057539 &9223372036854775807")
    after = len(re.findall(r"^--- !u!", text[sr_pos + 1 :], re.MULTILINE))
    print(f"Blocks: {len(ids)}, duplicate IDs: {len(dups)}, blocks after SceneRoots: {after}")


if __name__ == "__main__":
    main()
