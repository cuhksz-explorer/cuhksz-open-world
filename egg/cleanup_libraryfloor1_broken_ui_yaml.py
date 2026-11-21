#!/usr/bin/env python3
"""Remove broken YAML-patched UI objects (fileID >= 3000000000) from libraryfloor1.scene."""

from __future__ import annotations

import re
import sys
from pathlib import Path

PROJECT = Path(__file__).resolve().parents[1]
TARGET = PROJECT / "Assets/scene/libraryfloor1.scene"
ID_THRESHOLD = 3_000_000_000
SENTINEL_ID = 9223372036854775807

DOC_HEADER_RE = re.compile(r"^--- !u!\d+ &(\d+)\s*$", re.M)
SCENE_ROOTS_RE = re.compile(
    r"(--- !u!1660057539 &9223372036854775807\nSceneRoots:\n  m_ObjectHideFlags: 0\n  m_Roots:\n)((?:  - \{fileID: \d+\}\n)*)"
)


def main() -> int:
    if not TARGET.exists():
        print("Scene not found", file=sys.stderr)
        return 1

    text = TARGET.read_text(encoding="utf-8")
    matches = list(DOC_HEADER_RE.finditer(text))
    remove_ids = {
        int(m.group(1))
        for m in matches
        if int(m.group(1)) >= ID_THRESHOLD and int(m.group(1)) != SENTINEL_ID
    }

    if not remove_ids:
        print("No broken UI patch blocks found.")
        return 0

    kept: list[str] = []
    for i, match in enumerate(matches):
        file_id = int(match.group(1))
        if file_id in remove_ids:
            continue
        start = match.start()
        end = matches[i + 1].start() if i + 1 < len(matches) else len(text)
        kept.append(text[start:end])

    patched = "".join(kept)
    roots_match = SCENE_ROOTS_RE.search(patched)
    if roots_match:
        lines = roots_match.group(2).splitlines(keepends=True)
        filtered = []
        for line in lines:
            m = re.search(r"\{fileID: (\d+)\}", line)
            if m and int(m.group(1)) in remove_ids:
                continue
            filtered.append(line)
        patched = (
            patched[: roots_match.start()]
            + roots_match.group(1)
            + "".join(filtered)
            + patched[roots_match.end() :]
        )

    backup = TARGET.with_suffix(".scene.bak_before_yaml_cleanup")
    backup.write_text(text, encoding="utf-8")
    TARGET.write_text(patched, encoding="utf-8")
    print(f"Removed {len(remove_ids)} broken UI objects from {TARGET.name}")
    print(f"Backup: {backup}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
