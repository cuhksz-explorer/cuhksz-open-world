# -*- coding: utf-8 -*-
"""Rebuild library floor materials from scene/Materials sources + fix LegacyMat shaders.

Run: py -3 Assets/code/Editor/relink_library_materials.py
"""
from __future__ import annotations

import re
import shutil
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
ASSETS = ROOT / "Assets"
MATERIALS_DIR = ASSETS / "scene" / "Materials"
LEGACY_F4 = ASSETS / "scene" / "LegacyLibraryFloor4"

FLOOR4_LEGACY_GUIDS = [
    "0f3db298c9436c44388d5381e72782f5",
    "a2bd9bb494c4865499bbbc4520a763d9",
    "a0e49c15890d2fa4394e051a07209688",
    "b359ff48644d3fd458104f527ce3df1b",
    "d62fef8d8c5e9ea41ae23fdfd3c47745",
    "a198cbf268b051a45827374c18bbbbcb",
    "9fb2bf44b7e29ec40953849db21c5496",
    "1e21e25df9560bb488e51736f5f86664",
    "31a655bcafb8cde4b96eebc3ab36b212",
    "b2f45ec83f4945646a30bec55deb72ba",
]

FLOOR2_LEGACY = {
    "9dc94fc59eb3a964cb761af3f4e4cda8": "lib2f Shell/LibraryFloor2Shell.mat",
}

FLOOR4_SHELL = {
    "02c884b724cb98148bbcfd071d8efb91": "图书馆四楼 Shell/LibraryFloor4Shell.mat",
}

META_GUID = re.compile(r"^guid: ([0-9a-f]{32})", re.M)
MAIN_TEX = re.compile(
    r"_MainTex:\s*\n\s+m_Texture: \{fileID: (\d+), guid: ([0-9a-f]{32}), type: 3\}"
)


def mat_has_albedo(path: Path) -> bool:
    try:
        text = path.read_text(encoding="utf-8", errors="replace")
    except OSError:
        return False
    if "m_Shader:" not in text:
        return False
    m = MAIN_TEX.search(text)
    return bool(m and m.group(1) != "0")


def collect_textured_mats() -> list[Path]:
    mats: list[Path] = []
    for mat in MATERIALS_DIR.glob("*.mat"):
        if mat_has_albedo(mat):
            mats.append(mat)
    return mats


def write_meta(mat_path: Path, guid: str) -> None:
    meta = mat_path.with_suffix(".mat.meta")
    meta.write_text(
        f"fileFormatVersion: 2\nguid: {guid}\n"
        "NativeFormatImporter:\n"
        "  externalObjects: {}\n"
        "  mainObjectFileID: 2100000\n"
        "  userData: \n"
        "  assetBundleName: \n"
        "  assetBundleVariant: \n",
        encoding="utf-8",
    )


def copy_mat_as_guid(source: Path, dest: Path, guid: str, display_name: str) -> None:
    text = source.read_text(encoding="utf-8", errors="replace")
    text = re.sub(r"m_Name: [^\n]+", f"m_Name: {display_name}", text, count=1)
    # Drop broken normal-map keyword if no valid bump assigned
    if "_NORMALMAP" in text and "_BumpMap:" in text:
        bump = re.search(
            r"_BumpMap:\s*\n\s+m_Texture: \{fileID: (\d+)",
            text,
        )
        if bump and bump.group(1) == "0":
            text = text.replace("  m_InvalidKeywords:\n  - _NORMALMAP\n", "  m_InvalidKeywords: []\n")
    dest.parent.mkdir(parents=True, exist_ok=True)
    dest.write_text(text, encoding="utf-8")
    write_meta(dest, guid)
    print(f"  {dest.relative_to(ROOT)} <- {source.name} (guid {guid[:8]}...)")


def fix_shell_mat(path: Path) -> None:
    if not path.is_file():
        return
    text = path.read_text(encoding="utf-8", errors="replace")
    changed = False
    if "m_InvalidKeywords:\n  - _NORMALMAP" in text:
        text = text.replace(
            "  m_InvalidKeywords:\n  - _NORMALMAP\n",
            "  m_InvalidKeywords: []\n",
        )
        changed = True
    # White plaster template wrongly puts roughness in _MainTex
    if "50ca0243fc42eac4b832ea5ff8c3fb1e" in text and "_MainTex:" in text:
        text = re.sub(
            r"(_MainTex:\s*\n\s+m_Texture: \{fileID: 2800000, guid: )50ca0243fc42eac4b832ea5ff8c3fb1e",
            r"\g<1>9931b8b347ac1eb43899aac06cb287b4",
            text,
        )
        text = re.sub(
            r"(_BumpMap:\s*\n\s+m_Texture: \{fileID: 2800000, guid: )9931b8b347ac1eb43899aac06cb287b4",
            r"\g<1>0",
            text,
            count=1,
        )
        if "m_Texture: {fileID: 0}" in text:
            text = re.sub(
                r"- _BumpMap:\s*\n\s+m_Texture: \{fileID: 0[^\n]+\n\s+m_Scale:[^\n]+\n\s+m_Offset:[^\n]+\n",
                "",
                text,
                count=1,
            )
        changed = True
    if changed:
        path.write_text(text, encoding="utf-8")
        print(f"  fixed shader/textures: {path.relative_to(ROOT)}")


def main() -> None:
    textured = collect_textured_mats()
    print(f"Textured materials in scene/Materials: {len(textured)}")
    if len(textured) < len(FLOOR4_LEGACY_GUIDS):
        print("WARN: not enough textured mats; reusing cycle")

    print("\n=== Floor 4 legacy materials (from real scene/Materials) ===")
    LEGACY_F4.mkdir(parents=True, exist_ok=True)
    for idx, guid in enumerate(FLOOR4_LEGACY_GUIDS):
        src = textured[idx % len(textured)]
        dest = LEGACY_F4 / f"LegacyMat_{guid[:8]}.mat"
        copy_mat_as_guid(src, dest, guid, f"LegacyMat_{guid[:8]}")

    print("\n=== Fix shell / legacy plaster materials ===")
    for rel in list(FLOOR2_LEGACY.values()) + list(FLOOR4_SHELL.values()):
        fix_shell_mat(ASSETS / "scene" / rel.replace("/", "\\"))
    for mat in LEGACY_F4.glob("*.mat"):
        fix_shell_mat(mat)

    print("\n=== Floor 2 shell mat guid restore (copy from LibraryFloor2Shell content) ===")
    shell2 = ASSETS / "scene" / "lib2f Shell" / "LibraryFloor2Shell.mat"
    if shell2.is_file():
        dest = ASSETS / "scene" / "lib2f Shell" / "LibraryFloor2Shell.mat"
        fix_shell_mat(dest)
        write_meta(dest, "9dc94fc59eb3a964cb761af3f4e4cda8")

    print("\nDone. In Unity: Reimport Assets/scene/图书馆1楼.fbx and 图书馆四楼.blend")
    print("(Right-click -> Reimport) to rebuild embedded materials after GUID restore.")


if __name__ == "__main__":
    main()
