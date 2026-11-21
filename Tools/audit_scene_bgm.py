# -*- coding: utf-8 -*-
"""Audit and optionally fix duplicate scene BGM/ambient audio objects."""
from __future__ import annotations

import re
from datetime import datetime
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SCENE_DIR = ROOT / "Assets" / "scene"
BACKUP = SCENE_DIR / "Backups"

SCRIPT_BGM = "d1e30f8c09cf5e445a5c44b5744f52bc"
SCRIPT_AMB = "4974513788d0476f82f090320cfe7841"

# Scenes that intentionally use SceneAmbientAudio as sole music (not BackgroundMusic).
PREFER_AMBIENT = {"loadingscene.scene", "undergarden.unity"}

# Scenes that intentionally use root BGM object.
PREFER_BGM_OBJECT = {"uppergarden.unity"}


def parse_scene(path: Path) -> list[dict]:
    text = path.read_text(encoding="utf-8", errors="replace")
    blocks = re.split(r"(?=--- !u!1 &)", text)
    objs: list[dict] = []
    for block in blocks:
        if not block.startswith("--- !u!1 &"):
            continue
        m_id = re.search(r"^--- !u!1 &(\d+)", block, re.M)
        m_name = re.search(r"\n  m_Name: (.+)\n", block)
        m_active = re.search(r"\n  m_IsActive: ([01])\n", block)
        if not m_name:
            continue
        name = m_name.group(1).strip()
        active = m_active.group(1) == "1" if m_active else True
        oid = m_id.group(1) if m_id else "?"

        bgm_ref = amb_ref = None
        has_bgm_script = has_amb_script = False
        for m in re.finditer(
            r"m_Script: \{fileID: 11500000, guid: ([a-f0-9]+), type: 3\}[\s\S]*?"
            r"(?:bgmClip: \{fileID: ([^}]+)\}|clip: \{fileID: ([^}]+)\})",
            block,
        ):
            script = m.group(1)
            ref = m.group(2) or m.group(3)
            if script == SCRIPT_BGM:
                has_bgm_script = True
                bgm_ref = ref
            if script == SCRIPT_AMB:
                has_amb_script = True
                amb_ref = ref

        kind = None
        if has_bgm_script or name in ("BackgroundMusic", "BGM"):
            kind = "BGM"
        elif has_amb_script or name == "SceneAmbientAudio" or (name == "GameObject" and amb_ref):
            kind = "Ambient"

        if kind:
            objs.append(
                {
                    "name": name,
                    "kind": kind,
                    "active": active,
                    "bgm": bgm_ref,
                    "amb": amb_ref,
                    "id": oid,
                    "block": block,
                }
            )
    return objs


def choose_winner(path: Path, objs: list[dict]) -> dict | None:
    active = [o for o in objs if o["active"]]
    if not active:
        active = objs[:]
    if not active:
        return None

    fname = path.name.lower()
    if path.name in PREFER_AMBIENT:
        for o in active:
            if o["kind"] == "Ambient":
                return o
    if path.name in PREFER_BGM_OBJECT:
        for o in active:
            if o["name"] == "BGM":
                return o

    # Default: prefer BackgroundMusic, then BGM, then SceneAmbientAudio, then GameObject ambient.
    priority = {"BackgroundMusic": 0, "BGM": 1, "SceneAmbientAudio": 2, "GameObject": 3}
    active.sort(key=lambda o: (priority.get(o["name"], 9), 0 if o["kind"] == "BGM" else 1))
    return active[0]


def set_active_in_block(block: str, active: int) -> str:
    return re.sub(r"(\n  m_IsActive: )[01]", rf"\g<1>{active}", block, count=1)


def fix_scene(path: Path) -> tuple[int, str]:
    text = path.read_text(encoding="utf-8", errors="replace")
    original = text
    objs = parse_scene(path)
    if not objs:
        return 0, "no-audio"

    winner = choose_winner(path, objs)
    if winner is None:
        return 0, "no-winner"

    changed = 0
    for o in objs:
        should_active = 1 if o["id"] == winner["id"] else 0
        current_active = 1 if o["active"] else 0
        if current_active == should_active:
            continue
        new_block = set_active_in_block(o["block"], should_active)
        if new_block != o["block"]:
            text = text.replace(o["block"], new_block, 1)
            changed += 1

    active_count = sum(1 for o in objs if o["id"] == winner["id"])
    issue = ""
    active_bgm = [o for o in objs if o["kind"] == "BGM" and o["active"]]
    active_amb = [o for o in objs if o["kind"] == "Ambient" and o["active"]]
    if len(active_bgm) > 1:
        issue += " multi-bgm"
    if len(active_amb) > 1:
        issue += " multi-amb"
    if active_bgm and active_amb:
        issue += " bgm+amb"

    if text != original:
        BACKUP.mkdir(parents=True, exist_ok=True)
        stamp = datetime.now().strftime("%Y%m%d_%H%M%S")
        backup = BACKUP / f"{path.stem}_bgm_single_{stamp}{path.suffix}"
        backup.write_text(original, encoding="utf-8")
        path.write_text(text, encoding="utf-8")

    return changed, f"keep={winner['kind']}:{winner['name']}{issue}"


def audit() -> None:
    print("SCENE BGM AUDIT")
    print("=" * 80)
    need_fix = []
    for path in sorted(list(SCENE_DIR.glob("*.unity")) + list(SCENE_DIR.glob("*.scene"))):
        if any(x in path.name.lower() for x in (".bak", "broken", "truncated", "do_not_use")):
            continue
        objs = parse_scene(path)
        active = [o for o in objs if o["active"]]
        active_bgm = [o for o in active if o["kind"] == "BGM"]
        active_amb = [o for o in active if o["kind"] == "Ambient"]
        flags = []
        if len(active_bgm) + len(active_amb) == 0 and objs:
            flags.append("ALL_INACTIVE")
        if len(active_bgm) > 1:
            flags.append("MULTI_BGM")
        if len(active_amb) > 1:
            flags.append("MULTI_AMB")
        if active_bgm and active_amb:
            flags.append("BGM+AMB")
        if len(active_bgm) + len(active_amb) > 1:
            need_fix.append(path.name)
        flag = (" " + ",".join(flags)) if flags else ""
        print(
            f"{path.name}: active={len(active_bgm)} BGM + {len(active_amb)} Ambient"
            f" | inactive={len(objs)-len(active)}{flag}"
        )
        for o in active:
            ref = o.get("bgm") or o.get("amb") or "none"
            print(f"  [ON] {o['kind']} {o['name']} ref={ref}")
        for o in objs:
            if o["active"]:
                continue
            ref = o.get("bgm") or o.get("amb") or "none"
            print(f"  [off] {o['kind']} {o['name']} ref={ref}")
    print("=" * 80)
    print(f"Scenes with multiple active music sources: {len(need_fix)}")
    for n in need_fix:
        print(f"  - {n}")


def main() -> None:
    audit()
    print("\nApplying fixes...")
    total = 0
    for path in sorted(list(SCENE_DIR.glob("*.unity")) + list(SCENE_DIR.glob("*.scene"))):
        if any(x in path.name.lower() for x in (".bak", "broken", "truncated", "do_not_use")):
            continue
        changed, info = fix_scene(path)
        if changed:
            print(f"  fixed {path.name}: {changed} toggles ({info})")
            total += changed
    print(f"Done. {total} object toggles.")
    print("\nPost-fix audit:")
    audit()


if __name__ == "__main__":
    main()
