# -*- coding: utf-8 -*-
"""Inject PatrioticMusicTrigger into undergarden.unity (editor-visible setup).

Run: py -3 Tools/setup_patriotic_trigger.py
"""
from __future__ import annotations

import re
from datetime import datetime
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SCENE = ROOT / "Assets" / "scene" / "undergarden.unity"
BACKUP = ROOT / "Assets" / "scene" / "Backups"
TRIGGER_NAME = "PatrioticMusicTrigger_我和我的祖国"
PROXIMITY_SCRIPT_GUID = "a8f3c2e14b9d4e6f8a1b0c2d3e4f5a6b"
POS = (-1360.78003, 13.6599998, 1100.43005)


SAFE_ID_LIMIT = 9_007_199_254_740_992  # 2^53; larger IDs collide in YAML float parsing
SCENE_ROOTS_ID = 9_223_372_036_854_775_807


def max_file_id(text: str) -> int:
    ids = [int(m) for m in re.findall(r"^--- !u!\d+ &(\d+)", text, re.M)]
    safe = [
        i
        for i in ids
        if i < SAFE_ID_LIMIT and float(i) != float(SCENE_ROOTS_ID)
    ]
    return max(safe) if safe else 2_000_000_000


def build_block(base: int) -> str:
    go, mono, audio, trans = base, base + 1, base + 2, base + 3
    x, y, z = POS
    return f"""
--- !u!1 &{go}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 7
  m_Component:
  - component: {{fileID: {trans}}}
  - component: {{fileID: {audio}}}
  - component: {{fileID: {mono}}}
  m_Layer: 0
  m_HasEditorInfo: 1
  m_Name: {TRIGGER_NAME}
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!114 &{mono}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {PROXIMITY_SCRIPT_GUID}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  triggerWorldPosition: {{x: {x}, y: {y}, z: {z}}}
  triggerRadius: 20
  useHorizontalDistance: 1
  musicClip: {{fileID: 0}}
  loadClipFromResources: 1
  resourcesClipPath: Music/我和我的祖国
  volume: 0.22
  loop: 1
  fadeInDuration: 2.5
  fadeOutDuration: 2
  checkInterval: 0.15
  playerMovement: {{fileID: 0}}
--- !u!82 &{audio}
AudioSource:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go}}}
  m_Enabled: 1
  serializedVersion: 4
  OutputAudioMixerGroup: {{fileID: 0}}
  m_audioClip: {{fileID: 0}}
  m_PlayOnAwake: 0
  m_Volume: 0
  m_Pitch: 1
  Loop: 1
  Mute: 0
  Spatialize: 0
  SpatializePostEffects: 0
  Priority: 128
  DopplerLevel: 1
  MinDistance: 1
  MaxDistance: 500
  Pan2D: 0
  rolloffMode: 0
  BypassEffects: 0
  BypassListenerEffects: 0
  BypassReverbZones: 0
--- !u!4 &{trans}
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go}}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: {x}, y: {y}, z: {z}}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
"""


def main() -> int:
    if not SCENE.is_file():
        print("undergarden.unity not found")
        return 1

    text = SCENE.read_text(encoding="utf-8")
    if TRIGGER_NAME in text or PROXIMITY_SCRIPT_GUID in text and "PatrioticMusic" in text:
        print("Patriotic trigger already present")
        return 0

    base = max_file_id(text) + 100
    BACKUP.mkdir(parents=True, exist_ok=True)
    stamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    (BACKUP / f"undergarden_patriotic_{stamp}.unity").write_text(text, encoding="utf-8")

  # SceneRoots must remain the final YAML block; insert before it.
    block = build_block(base)
    sr = re.search(r"--- !u!1660057539 &9223372036854775807\nSceneRoots:", text)
    if not sr:
        print("SceneRoots block not found; use Unity menu instead.")
        return 1

    text = text[: sr.start()] + block + text[sr.start() :]
    trans_ref = f"  - {{fileID: {base + 3}}}\n"
    if trans_ref.strip() not in text:
        text = text.replace(
            "  - {fileID: 2000517010}\n",
            "  - {fileID: 2000517010}\n" + trans_ref,
            1,
        )

    SCENE.write_text(text, encoding="utf-8")
    print(f"Injected {TRIGGER_NAME} at {POS} (IDs {base}-{base + 3})")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
