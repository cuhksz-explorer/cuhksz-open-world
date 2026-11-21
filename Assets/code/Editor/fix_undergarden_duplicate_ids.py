# -*- coding: utf-8 -*-
"""Remove PatrioticMusic YAML blocks that collide with SceneRoots ID (float precision).

Patriotic music is created at runtime by UndergardenPatrioticMusicBootstrap.
Run: py -3 Assets/code/Editor/fix_undergarden_duplicate_ids.py
"""
from __future__ import annotations

import re
from collections import Counter
from datetime import datetime
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
SCENE = ROOT / "Assets" / "scene" / "undergarden.unity"
SCENE_ROOTS_ID = 9223372036854775807
COLLISION_IDS = {
    9223372036854775907,
    9223372036854775908,
    9223372036854775909,
    9223372036854775910,
}

PATRIOTIC_BLOCK = re.compile(
    r"\n--- !u!1 &9223372036854775907\nGameObject:.*?"
    r"--- !u!4 &9223372036854775910\nTransform:.*?m_LocalEulerAnglesHint: \{x: 0, y: 0, z: 0\}\n",
    re.DOTALL,
)


def main() -> None:
    text = SCENE.read_text(encoding="utf-8")
    backup = SCENE.with_name(
        f"undergarden.unity.bak_dupfix_{datetime.now().strftime('%Y%m%d_%H%M%S')}"
    )
    backup.write_text(text, encoding="utf-8")
    print(f"Backup: {backup.name}")

    if "PatrioticMusicTrigger_我和我的祖国" not in text:
        print("Patriotic YAML block not found; nothing to remove.")
        return

    text, removed = PATRIOTIC_BLOCK.subn("\n", text, count=1)
    if removed == 0:
        raise SystemExit("Failed to remove PatrioticMusic YAML block.")

    text = text.replace("  - {fileID: 9223372036854775910}\n", "")

    # SceneRoots must be the final YAML block.
    sr_match = re.search(r"--- !u!1660057539 &9223372036854775807\nSceneRoots:", text)
    if not sr_match:
        raise SystemExit("SceneRoots block missing after edit.")
    after_sr = text[sr_match.end() :]
    if re.search(r"^--- !u!", after_sr, re.MULTILINE):
        raise SystemExit("Objects still exist after SceneRoots.")

    SCENE.write_text(text, encoding="utf-8")

    block_ids = [int(x) for x in re.findall(r"^--- !u!\d+ &(\d+)", text, re.MULTILINE)]
    target = float(SCENE_ROOTS_ID)
    collisions = [i for i in block_ids if float(i) == target and i != SCENE_ROOTS_ID]
    dups = [k for k, v in Counter(block_ids).items() if v > 1]
    print(f"Removed PatrioticMusic YAML ({removed} block group).")
    print(f"Blocks: {len(block_ids)}, header dups: {len(dups)}, float collisions: {collisions}")


if __name__ == "__main__":
    main()
