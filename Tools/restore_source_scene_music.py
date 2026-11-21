# -*- coding: utf-8 -*-
"""Restore Loadingscene / undergarden / uppergarden music bindings.

Scene audio mapping (Assets/Music):
  Loadingscene  -> Legacy/Loadingscene_ambient.mp3  (运动场音效.mp3)  guid 6eef7c45...
  undergarden   -> Legacy/undergarden_ambient.mp3   (欢快的小曲.mp3)   guid 87af8026...
  uppergarden   -> 1(3).mp3                          guid 88102760...

Run: py -3 Tools/restore_source_scene_music.py
"""
from __future__ import annotations

import re
import shutil
from datetime import datetime
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SRC_ROOT = Path(r"z:\刘厚安 Unity实机2\刘厚安 Unity实机\My project (2)")
SCENE_DIR = ROOT / "Assets" / "scene"
LEGACY_DIR = ROOT / "Assets" / "Music" / "Legacy"
BACKUP = SCENE_DIR / "Backups"

CLIP_REF = r"{fileID: 8300000, guid: %s, type: 3}"

SCENES = {
    "Loadingscene.scene": {
        "clip_guid": "6eef7c45b78f87540a285aad1d61a1ce",
        "src_mp3": SRC_ROOT / "Assets" / "Music" / "小雨.mp3",
        "legacy_name": "Loadingscene_ambient.mp3",
        "ambient_volume": 0.546,
        "deactivate_names": {"SceneAmbientAudio"},
        "keep_active_names": {"GameObject"},
        "bgm": None,
    },
    "undergarden.unity": {
        "clip_guid": "87af80262673cf442856391af6ee7e13",
        "src_mp3": SRC_ROOT / "Assets" / "Music" / "风.mp3",
        "legacy_name": "undergarden_ambient.mp3",
        "ambient_volume": 0.302,
        "deactivate_names": {"BackgroundMusic", "SceneAmbientAudio"},
        "keep_active_names": {"GameObject"},
        "bgm": None,
    },
    "uppergarden.unity": {
        "clip_guid": "88102760a005a904d9332bf770d794c3",
        "src_mp3": SRC_ROOT / "Assets" / "Music" / "1(3).mp3",
        "legacy_name": None,
        "ambient_volume": None,
        "deactivate_names": {"BackgroundMusic", "SceneAmbientAudio"},
        "keep_active_names": {"BGM"},
        "bgm": {
            "guid": "88102760a005a904d9332bf770d794c3",
            "volume": 0.15,
            "dont_destroy": 1,
        },
    },
}

META_TEMPLATE = """fileFormatVersion: 2
guid: {guid}
AudioImporter:
  externalObjects: {{}}
  serializedVersion: 7
  defaultSettings:
    serializedVersion: 2
    loadType: 0
    sampleRateSetting: 0
    sampleRateOverride: 44100
    compressionFormat: 1
    quality: 1
    conversionMode: 0
    preloadAudioData: 0
  platformSettingOverrides: {{}}
  forceToMono: 0
  normalize: 1
  loadInBackground: 0
  ambisonic: 0
  3D: 1
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""


def log(msg: str) -> None:
    print(msg, flush=True)


def ensure_legacy_audio(guid: str, src_mp3: Path, legacy_name: str) -> None:
    if not src_mp3.is_file():
        raise FileNotFoundError(f"Source audio missing: {src_mp3}")
    LEGACY_DIR.mkdir(parents=True, exist_ok=True)
    dst = LEGACY_DIR / legacy_name
    meta = Path(str(dst) + ".meta")
    shutil.copy2(src_mp3, dst)
    meta.write_text(META_TEMPLATE.format(guid=guid), encoding="utf-8")
    log(f"  audio: {dst.relative_to(ROOT)}  guid={guid}")


def ensure_uppergarden_bgm(src_mp3: Path) -> None:
    dst = ROOT / "Assets" / "Music" / "1(3).mp3"
    meta = Path(str(dst) + ".meta")
    if not dst.is_file() or dst.stat().st_size != src_mp3.stat().st_size:
        shutil.copy2(src_mp3, dst)
        log(f"  refreshed: {dst.relative_to(ROOT)}")
    if meta.is_file():
        text = meta.read_text(encoding="utf-8")
        if "88102760a005a904d9332bf770d794c3" not in text:
            text = re.sub(r"^guid: .+$", "guid: 88102760a005a904d9332bf770d794c3", text, count=1, flags=re.M)
            meta.write_text(text, encoding="utf-8")


def set_gameobject_active(block: str, active: int) -> str:
    return re.sub(r"(\n  m_IsActive: )[01]", rf"\g<1>{active}", block, count=1)


def patch_scene_ambient_on_gameobject(text: str, guid: str, volume: float) -> tuple[str, int]:
    ref = CLIP_REF % guid
    count = 0
    pattern = (
        r"(--- !u!114 &\d+\nMonoBehaviour:\n"
        r"[\s\S]*?m_Script: \{fileID: 11500000, guid: 4974513788d0476f82f090320cfe7841, type: 3\}\n"
        r"[\s\S]*?m_EditorClassIdentifier: \n)"
        r"  clip: \{fileID: [^}]+\}\n"
        r"  volume: [0-9.]+\n"
    )
    repl = rf"\g<1>  clip: {ref}\n  volume: {volume}\n"
    text, n = re.subn(pattern, repl, text)
    count += n

    pattern2 = (
        r"(--- !u!82 &\d+\nAudioSource:\n[\s\S]*?OutputAudioMixerGroup: \{fileID: 0\}\n)"
        r"  m_audioClip: \{fileID: [^}]+\}\n"
    )
    # Only patch AudioSource blocks that follow SceneAmbientAudio script in same GameObject section is hard;
    # replace all ambient clip refs for this scene's target guid blocks near GameObject name.
    return text, count


def patch_scene_file(path: Path, cfg: dict) -> None:
    original = path.read_text(encoding="utf-8")
    text = original
    guid = cfg["clip_guid"]
    ref = CLIP_REF % guid

    # Split into GameObject blocks and toggle active state by m_Name
    parts = re.split(r"(--- !u!1 &\d+\nGameObject:\n)", text)
    rebuilt: list[str] = []
    changed_active = 0
    for i, part in enumerate(parts):
        if not part.startswith("--- !u!1 &"):
            rebuilt.append(part)
            continue
        name_m = re.search(r"\n  m_Name: (.+)\n", part)
        name = name_m.group(1).strip() if name_m else ""
        if name in cfg["deactivate_names"]:
            new_part = set_gameobject_active(part, 0)
            if new_part != part:
                changed_active += 1
            rebuilt.append(new_part)
        elif name in cfg["keep_active_names"]:
            new_part = set_gameobject_active(part, 1)
            if new_part != part:
                changed_active += 1
            rebuilt.append(new_part)
        else:
            rebuilt.append(part)
    text = "".join(rebuilt)

    if cfg.get("ambient_volume") is not None:
        vol = cfg["ambient_volume"]
        # SceneAmbientAudio on kept GameObject
        text, _ = patch_scene_ambient_on_gameobject(text, guid, vol)
        # AudioSource m_audioClip on same scene for legacy GameObject blocks
        text = re.sub(
            r"(m_Name: GameObject\n[\s\S]*?m_audioClip: )\{fileID: [^}]+\}",
            rf"\g<1>{ref}",
            text,
            count=1,
        )
        text = re.sub(
            r"(m_Name: SceneAmbientAudio\n[\s\S]*?m_audioClip: )\{fileID: [^}]+\}",
            rf"\g<1>{ref}",
            text,
            count=1,
        )

    if cfg.get("bgm"):
        bgm = cfg["bgm"]
        bgm_ref = CLIP_REF % bgm["guid"]
        text = re.sub(
            r"(m_Name: BGM\n[\s\S]*?bgmClip: )\{fileID: [^}]+\}",
            rf"\g<1>{bgm_ref}",
            text,
            count=1,
        )
        text = re.sub(
            r"(m_Name: BGM\n[\s\S]*?bgmVolume: )[0-9.]+",
            rf"\g<1>{bgm['volume']}",
            text,
            count=1,
        )
        text = re.sub(
            r"(m_Name: BGM\n[\s\S]*?dontDestroyOnLoad: )[01]",
            rf"\g<1>{bgm['dont_destroy']}",
            text,
            count=1,
        )

    if text == original:
        log(f"  {path.name}: no YAML changes")
        return

    BACKUP.mkdir(parents=True, exist_ok=True)
    stamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    backup = BACKUP / f"{path.stem}_source_music_{stamp}{path.suffix}"
    backup.write_text(original, encoding="utf-8")
    path.write_text(text, encoding="utf-8")
    log(f"  {path.name}: patched (backup {backup.name}, deactivated={changed_active})")


def main() -> None:
    if not SRC_ROOT.is_dir():
        raise SystemExit(f"Source project not found: {SRC_ROOT}")

    log("Copying legacy audio from source...")
    for scene_file, cfg in SCENES.items():
        if cfg.get("legacy_name"):
            ensure_legacy_audio(cfg["clip_guid"], cfg["src_mp3"], cfg["legacy_name"])
        if scene_file == "uppergarden.unity":
            ensure_uppergarden_bgm(cfg["src_mp3"])

    log("Patching scenes...")
    for scene_file, cfg in SCENES.items():
        path = SCENE_DIR / scene_file
        if not path.is_file():
            raise FileNotFoundError(path)
        patch_scene_file(path, cfg)

    log("Done.")


if __name__ == "__main__":
    main()
