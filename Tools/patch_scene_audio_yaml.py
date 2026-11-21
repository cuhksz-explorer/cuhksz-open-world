# -*- coding: utf-8 -*-
"""Patch scene YAML: bind MapSystem.teleportSound and fix SceneAmbientAudio missing clips.

Run: py -3 Tools/patch_scene_audio_yaml.py
"""
from __future__ import annotations

import re
from datetime import datetime
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SCENE_DIR = ROOT / "Assets" / "scene"
BACKUP = SCENE_DIR / "Backups"

TELEPORT_GUID = "a97f9a59a7b00474cb590d5145580890"
NORMAL_TELEPORT_GUID = "d624aded2295aad4dbf62e770bd1a183"
TELEPORT_REF = f"{{fileID: 8300000, guid: {TELEPORT_GUID}, type: 3}}"

# Scene stem -> ambient clip guid (from Assets/Music/*.meta after download)
AMBIENT_BY_SCENE = {
    "uppergarden": "296f47e453ee5f046b8542d6d4d71270",  # 鸟叫
    "middlegarden": "296f47e453ee5f046b8542d6d4d71270",
    "undergarden": "e82a37286e8dc5a4da41a12dd3d0e91f",  # 风
    "minival": "296f47e453ee5f046b8542d6d4d71270",
    "lingcollegea": "296f47e453ee5f046b8542d6d4d71270",
    "lingcollegeb": "296f47e453ee5f046b8542d6d4d71270",
    "lingcollegec": "296f47e453ee5f046b8542d6d4d71270",
    "dlilgentiac": "296f47e453ee5f046b8542d6d4d71270",
    "lingdrom": "e82a37286e8dc5a4da41a12dd3d0e91f",
    "dorm": "171b473d672a3c84cb2226c15ebb3b0e",
    "library": "171b473d672a3c84cb2226c15ebb3b0e",
    "libraryfloor1": "171b473d672a3c84cb2226c15ebb3b0e",
    "libraryfloor2": "171b473d672a3c84cb2226c15ebb3b0e",
    "libraryfloor4": "171b473d672a3c84cb2226c15ebb3b0e",
    "classroom1": "171b473d672a3c84cb2226c15ebb3b0e",
    "teachingrooma": "171b473d672a3c84cb2226c15ebb3b0e",
    "computerroom": "171b473d672a3c84cb2226c15ebb3b0e",
    "learning room": "171b473d672a3c84cb2226c15ebb3b0e",
    "discussion room (td一楼）": "171b473d672a3c84cb2226c15ebb3b0e",
    "lecture hall(tb)": "171b473d672a3c84cb2226c15ebb3b0e",
    "corridor": "171b473d672a3c84cb2226c15ebb3b0e",
    "cec": "171b473d672a3c84cb2226c15ebb3b0e",
    "eighta": "171b473d672a3c84cb2226c15ebb3b0e",
    "eigthb": "171b473d672a3c84cb2226c15ebb3b0e",
    "shawa": "171b473d672a3c84cb2226c15ebb3b0e",
    "musec": "171b473d672a3c84cb2226c15ebb3b0e",
    "bus": "e82a37286e8dc5a4da41a12dd3d0e91f",
    "loadingscene": "88102760a005a904d9332bf770d794c3",
}

BROKEN_AMBIENT_GUIDS = {
    "003b76d76c85d264486529892cc5be48",
}


def amb_ref(guid: str) -> str:
    return f"{{fileID: 8300000, guid: {guid}, type: 3}}"


def patch_file(path: Path) -> tuple[int, int]:
    text = path.read_text(encoding="utf-8")
    original = text
    teleport_n = 0
    ambient_n = 0

    new_text, n = re.subn(
        rf"teleportSound: {{fileID: 8300000, guid: {NORMAL_TELEPORT_GUID}, type: 3}}",
        f"teleportSound: {TELEPORT_REF}",
        text,
    )
    teleport_n += n
    text = new_text
    new_text, n = re.subn(
        r"teleportSound: \{fileID: 0\}",
        f"teleportSound: {TELEPORT_REF}",
        text,
    )
    teleport_n += n
    text = new_text

    stem = path.stem.lower()
    amb_guid = AMBIENT_BY_SCENE.get(stem)
    if amb_guid:
        ref = amb_ref(amb_guid)
        for broken in BROKEN_AMBIENT_GUIDS:
            new_text, n = re.subn(
                rf"clip: {{fileID: 8300000, guid: {broken}, type: 3}}",
                f"clip: {ref}",
                text,
            )
            ambient_n += n
            text = new_text
            new_text, n = re.subn(
                rf"m_audioClip: {{fileID: 8300000, guid: {broken}, type: 3}}",
                f"m_audioClip: {ref}",
                text,
            )
            ambient_n += n
            text = new_text

        if "4974513788d0476f82f090320cfe7841" in text:
            new_text, n = re.subn(
                r"(m_Script: \{fileID: 11500000, guid: 4974513788d0476f82f090320cfe7841, type: 3\}[\s\S]*?clip: )\{fileID: 0\}",
                rf"\g<1>{ref}",
                text,
                count=1,
            )
            ambient_n += n
            text = new_text

            new_text, n = re.subn(
                r"(m_Script: \{fileID: 11500000, guid: 4974513788d0476f82f090320cfe7841, type: 3\}[\s\S]*?m_audioClip: )\{fileID: 0\}",
                rf"\g<1>{ref}",
                text,
                count=1,
            )
            ambient_n += n
            text = new_text

    if text != original:
        BACKUP.mkdir(parents=True, exist_ok=True)
        stamp = datetime.now().strftime("%Y%m%d_%H%M%S")
        ext = path.suffix
        backup = BACKUP / f"{path.stem}_audio_{stamp}{ext}"
        backup.write_text(original, encoding="utf-8")
        path.write_text(text, encoding="utf-8")

    return teleport_n, ambient_n


def main() -> None:
    total_t = total_a = 0
    files = sorted(SCENE_DIR.glob("*.unity")) + sorted(SCENE_DIR.glob("*.scene"))
    for path in files:
        if "Backups" in path.parts:
            continue
        t, a = patch_file(path)
        if t or a:
            print(f"{path.name}: teleport={t}, ambient={a}")
        total_t += t
        total_a += a
    print(f"Done. teleport patches={total_t}, ambient patches={total_a}")


if __name__ == "__main__":
    main()
