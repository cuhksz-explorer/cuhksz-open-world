# -*- coding: utf-8 -*-
"""Project-wide GUID repair: restore meta, remap broken references, match scripts by fields.

Run from project root:
  py -3 Assets/code/Editor/fix_project_guid_references.py

Does NOT blindly regenerate meta GUIDs without updating references.
"""
from __future__ import annotations

import re
import uuid
from collections import defaultdict
from datetime import datetime
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
ASSETS = ROOT / "Assets"
PACKAGES = ROOT / "Packages"
PACKAGE_CACHE = ROOT / "Library" / "PackageCache"
PROJECT_SETTINGS = ROOT / "ProjectSettings"
STORY_PREFAB_DIR = ASSETS / "NPCs" / "Story"

VALID_GUID = re.compile(r"^[0-9a-f]{32}$")
GUID_LINE = re.compile(r"^(guid: )(.+)$", re.MULTILINE)
GUID_IN_REF = re.compile(r"guid: ([0-9a-f]{32})")
META_GUID = re.compile(r"^guid: ([0-9a-f]{32})", re.M)
FIELD = re.compile(r"^  ([A-Za-z_][A-Za-z0-9_]*):", re.M)
PREFAB_BLOCK = re.compile(r"^--- !u!1001 &", re.M)
STORY_NAME = re.compile(r'propertyPath: m_Name\n\s+value: "(StoryNPC_[^"]+)"')
SOURCE_PREFAB = re.compile(
    r"m_SourcePrefab: \{fileID: 100100000, guid: ([0-9a-f]{32}), type: 3\}"
)
MONO_SCRIPT = re.compile(
    r"m_Script: \{fileID: 11500000, guid: ([0-9a-f]{32}), type: 3\}"
)
SKIP_YAML_FIELDS = frozenset(
    {
        "m_ObjectHideFlags",
        "m_CorrespondingSourceObject",
        "m_PrefabInstance",
        "m_PrefabAsset",
        "m_GameObject",
        "m_Enabled",
        "m_EditorHideFlags",
        "m_Script",
        "m_Name",
        "m_EditorClassIdentifier",
    }
)
BUILTIN = {
    "0000000000000000e000000000000000",
    "0000000000000000f000000000000000",
}
REF_EXTENSIONS = {
    ".unity",
    ".prefab",
    ".asset",
    ".mat",
    ".controller",
    ".overrideController",
    ".mask",
    ".anim",
    ".playable",
    ".signal",
    ".guiskin",
    ".fontsettings",
    ".spriteatlas",
    ".shadervariants",
    ".scene",
    ".meta",
}

# Scenes still reference these historical GUIDs; restore .meta instead of patching scenes.
RESTORE_META_GUIDS: dict[str, str] = {
    "Assets/Resources/ChairSit/Stand Up.fbx.meta": "074e3ad34611f8441aaa71ac9557cdd0",
    "Assets/Resources/ChairSit/Sit To Stand.fbx.meta": "ac4a40e1c4513f0469e0806a22fddaa9",
    "Assets/Resources/ChairSit/Sitting Idle.fbx.meta": "f2b13a02792d1bb4d9a73ea1a0afb597",
    "Assets/Resources/ChairSit/PlayerChairSit.controller.meta": "640cd3e81ffef074592b0d8fe796a928",
    "Assets/Animations/ChairSit/PlayerChairSit.controller.meta": "640cd3e81ffef074592b0d8fe796a928",
    # PhoneUI prefab (scenes reference zip-era GUID)
    "Assets/PhoneUI/Prefabs/PhoneUI.prefab.meta": "32a27aa3840ed434f9f98948ff474b70",
    # Teleport / map loading videos (scene/Materials 11月25日 series)
    "Assets/scene/Materials/11月25日.mp4.meta": "787ffb4fe87f11b47867c2e90ee2bdf7",
    "Assets/scene/Materials/11月25日(1).mp4.meta": "a44ef7e562f81cf449a3484b1fc2ff35",
    "Assets/scene/Materials/11月25日(2).mp4.meta": "a460a4af0313bae4c843323b60f90a49",
    "Assets/scene/Materials/11月25日(3).mp4.meta": "bd0b623fbc80b9b4782daf5d145537b8",
    # Large scene meshes (thousands of stripped refs)
    "Assets/scene/图书馆1楼.fbx.meta": "5e198423aa994ca4bace71cb259a7a4f",
    "Assets/scene/图书馆1楼.blend.meta": "5e198423aa994ca4bace71cb259a7a4f",
    "Assets/scene/图书馆四楼.blend.meta": "0fc0c835acb8857428e7673f5c1b0a62",
    "Assets/Meshes/第八书院宿舍.fbx.meta": "905a1df5f37c2d544a127974b46bb788",
    # Story NPC body (周砚 prefab internal meshes)
    "Assets/Imports/Sketchfab/Characters/周砚.glb.meta": "b370b6743c287e844a0aee32ea1965e8",
    # Animations/ChairSit clips referenced by PlayerChairSit.controller
    "Assets/Animations/ChairSit/Stand Up.fbx.meta": "f74f6746993442c9b0e699727f37bcbb",
    "Assets/Animations/ChairSit/Sit To Stand.fbx.meta": "f4d7ca84c5cc42d6bdc0eed978f2d498",
    "Assets/Animations/ChairSit/PlayerChairSit.controller.meta": "640cd3e81ffef074592b0d8fe796a928",
    # Library floor 2 (lib2f root + shell)
    "Assets/scene/lib2f.fbx.meta": "7aa5c8e04a22f6c47918ec4d53021da8",
    "Assets/scene/lib2f.blend.meta": "7aa5c8e04a22f6c47918ec4d53021da8",
    "Assets/scene/图书馆四楼.fbx.meta": "0fc0c835acb8857428e7673f5c1b0a62",
    "Assets/scene/lib2f Shell/Meshes/Shell_Floor.asset.meta": "e323337e958d25043b5d2397fc6ff446",
    "Assets/scene/lib2f Shell/LibraryFloor2Shell.mat.meta": "9dc94fc59eb3a964cb761af3f4e4cda8",
    "Assets/scene/道扬.fbx.meta": "45a559bbd50c46a0a8badcc38a2a9ab4",
    # Library floor 4 shell (Shell_Floor mesh + shell material)
    "Assets/scene/图书馆四楼 Shell/Meshes/Shell_Floor.asset.meta": "9bd7f3cc94fe41d488a42bec59823413",
    "Assets/scene/图书馆四楼 Shell/LibraryFloor4Shell.mat.meta": "02c884b724cb98148bbcfd071d8efb91",
}

# 图书馆四楼.blend sub-materials referenced by libraryfloor4.scene (meta was regenerated).
LIBRARY_FLOOR4_LEGACY_MATERIAL_GUIDS: tuple[str, ...] = (
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
)

# Historical asset GUID -> current asset .meta GUID (when meta was regenerated).
KNOWN_ASSET_REMAPS: dict[str, str] = {
    "5942054c1e55548e9b7f7e0bcfba2499": "f2ced1e4a06f4596992cf72f2f60fe2f",  # player FBX
    "250905fd6ae7caa4f8cccf1207b58aad": "5b44d84dd82a47b9a0cbafa148721d18",  # bus
    "8f21cf6986fa45041b95eae071f5ddfd": "f94fcbb674c94afabfc1b0eea81973cb",  # racing driver glb
    "40e830d8d311743468f37e1d99bda3f0": "db82ae01b49b4eb1a6f82ffbd2127702",  # new scene glb
    "38c3c073cb10ac44abb39f62cb245cd2": "cb49cc38d4a14b8a8ef5a2a9ee0d577c",  # StoryQuestRuntime
    "30b71fbbcdcb3184aa5041de7bdbf504": "3b41f1118da54b8bac35490b68b477d1",  # PlayerWaterRippleEmitter
    # Built-in Standard shader (Tuanjie lost custom shader GUID)
    "3d167e20e9b48b9488c80c1ec085ec7d": "0000000000000000f000000000000000",
}

# Verified script remaps (fingerprint + manual).
KNOWN_SCRIPT_REMAPS: dict[str, str] = {
    "d5fb635624d13574bb3bc4cbeb95ba2e": "84b8667d373f40a390bfb62768b268b3",  # StoryQuestNpc
    "942ea79d22bf31e448a763880b0a0453": "26312a6942fa438297f80576690ff83f",  # StoryNpcVisualProfile
    "5aa60c89e114bd348b72661cd3871338": "9e47f04462734c428ee151397c63a585",  # VehicleSummonByHoldF2
    "ae6cc68d289928e40808f51a2abf299b": "72f441009abc49a48c6d3bc7c9a64279",  # TargetGuideIcon
    "d701927bfe71c964799dc4ecf8a46c90": "4e8c85c6ec964439a091f11847c3b075",  # ChairSitInteract
    "26b7df61ef935de419bb219fc1c2186a": "32ad4c15964044af85bddf919340326a",  # GameServerAuthUI
    "473dda7b6c58fc442b566cc8d2ee0786": "85f478e3e3f04d3588e22a8c9d76342f",  # QuestSystemAutoUIBuilder
    "b32af03b983a4524c881e86c6fba9699": "7beccae9371a480c849f4000174cb9e6",  # QuestSystemUI
    "b536511e050ef27489f9272cb74e1262": "c7213cc225b242858daa3606720020fa",  # PlayerSitSequence
    "2caa4b47bb0317c49b8a6f37ba765a56": "f5f9a1482bcd48218239012094b682bc",  # GameServerSync
    "bc277964f1c338c40931fcf55cf4d152": "d22fec4ebf2b47e880443febd0e3a951",  # GameServerLoginPanelToggle
    "6932e6283a2742346b3f469fd75b1f37": "453373a08e7042f4baf9d2b2f7c5fce8",  # MultiplayerFriendChatSidebarWorldChannel
    "d0b047bf9f0375142ac3a6013b56b5b7": "8fb8a15c7fd04bc5bd6191bd9fe0083c",  # MultiplayerEntryToggle
    "593c0883ec044cd46ad5954e5a4a05c6": "fa8de0d8872346519b8c00eb44ee27da",  # FlagAuctionStatePoller
    "9c54e1c9ff7d65f4994a0835d7a49091": "5827fa31c2db48f3a6a9623677ee4a71",  # LibraryFloor1PhysicsBootstrap
    "dfe4d39fe1735d54ab2c35acfda05124": "1f8a081f83c249d18de3eca603021e9f",  # IntroDollyCamera
    "17ba8c61ef931f64290b8a1de7ed4575": "8c794fb1e3964197b21b6dc432586015",  # MenuLoginPortalFlow
    "2427f89932f39aa4bab7af8e0c5d789b": "9695ccf1b0744ba2829f2db68fe1cc9a",  # LiquidWaterInteractiveRipples
}

# Asset GUID remaps (textures, databases, PhoneUI sprites, etc.)
KNOWN_ASSET_EXTRA: dict[str, str] = {
    "2b18e9a4010594b489039771efae18ab": "40a88f3da5904afab89d1fd6d99e9963",  # Content1StoryDatabase
    "2ab682e908d237041bf61256508eef1d": "40a88f3da5904afab89d1fd6d99e9963",
    "6b1ad22874c842a4bb6b936615925729": "c0cde60642974890b1386f91ccee512c",  # Phoneee.png frame
    "6f240ab3cd0a2ba4a983cd486627e33b": "0fc68cd5f62d4c5f8b5c62e1da88eeaf",  # setting.png (SlotFace bg)
    "413e089495909c94395c1d2a04373595": "05bbc0412bc841f087aaccf6a839482a",  # quest.png
    "d691e4434276a1b4e9033d8e4c7cbbd4": "66bcb2ae33694710911000a2283c0202",  # map.png
    "4d85ad33411ced74ca494e0a25e93f7a": "8815e176cdcc4047a3d60334d46b9286",  # shop.png
    "5568e65aa76261a4a8eb0c7d32a1af76": "6ab1d20e2f9c4661b916624d469cc87a",  # online.png
    "cc340ac7df2c3b146a8aceaa95a97f64": "aadbebdadfab4fc1baff39d8e2aca320",  # account.png
    "7da42c6a7e0db7143a557d5b9985a131": "5f1c9b62e4ef4a3c95f37e0563be05ee",  # inventory.png
    "67f8c4c7074301941ae2bdf886552d37": "bb4213b3b26244e5bda847034de540ce",  # 17.png
    "2fe52842f418faf4981d17361ee73484": "9931b8b347ac1eb43899aac06cb287b4",  # white_plaster BaseColor
    "f8b970ebc3a559b4494d751032df3ba8": "50ca0243fc42eac4b832ea5ff8c3fb1e",  # white_plaster Roughness
    "de58b0af1bd479f48bd9975d20b70e89": "db82ae01b49b4eb1a6f82ffbd2127702",  # new scene.glb
    "40e830d8d311743468f37e1d99bda3f0": "db82ae01b49b4eb1a6f82ffbd2127702",  # (duplicate historical)
    "10a31ef48dd548f45ae5ca460c45c16f": "4974513788d0476f82f090320cfe7841",  # SceneAmbientAudio
    "9cb6c3f3c9d50f6428efd8632ba81ee6": "b8523306e5be19545b0a8d955f9a9bb9",  # PhonePresentationController
    "c8eedd85c5ed03d4588cb6809b982249": "903bd195e04a3814395e8dde424c4b89",  # PhoneSessionController (library scenes)
    "e9da4d02b03ed624db09615f99f1e004": "e827e2b12dd849aeaeef67c35a189582",  # phone.fbx (PhoneProp mesh)
    "ee04844eb8760d74db83d97297f241f7": "65fd8a8fe28548d8947328032bf197e5",  # hero_female prefab
    "dca6fecd05baaec4db763a3872189079": "b7e21484555b42f0976c301ebb3906b9",  # hero_male_a prefab
    "adc51f3a10db2494f85b95527afe5c04": "c668d3fb751c4e789caaf7b71f9a2343",  # hero_male_b prefab
    "5ebb7d679feefb34b92d8216e1d301b0": "11343ffd5e139e14b9e3b848d1bcbed3",  # nested Up.fbx meshes (uppergarden)
}

PREFAB_NAME_HINTS: dict[str, str] = {
    "PhoneUI": "Assets/PhoneUI/Prefabs/PhoneUI.prefab",
    "new scene": "Assets/scene/new  scene.glb",
}


def is_valid_guid(value: str) -> bool:
    return bool(VALID_GUID.fullmatch(value.strip()))


def decode_unity_string(value: str) -> str:
    try:
        return value.encode("utf-8").decode("unicode_escape")
    except UnicodeError:
        return value


def display_from_story_object(name: str) -> str:
    core = name[len("StoryNPC_") :]
    idx = core.find("_M")
    core = core[:idx] if idx > 0 else core
    return decode_unity_string(core)


def load_meta_index() -> dict[str, str]:
    index: dict[str, str] = {}
    for base in (ASSETS, PACKAGES, PACKAGE_CACHE):
        if not base.is_dir():
            continue
        for meta_path in base.rglob("*.meta"):
            try:
                text = meta_path.read_text(encoding="utf-8", errors="replace")
            except OSError:
                continue
            match = META_GUID.search(text)
            if match:
                index[match.group(1)] = str(meta_path.relative_to(ROOT)).replace("\\", "/")
    return index


def iter_reference_files():
    for folder in (ASSETS, PROJECT_SETTINGS):
        if not folder.is_dir():
            continue
        for path in folder.rglob("*"):
            if not path.is_file():
                continue
            if path.suffix.lower() in REF_EXTENSIONS or path.name.endswith(".asmdef"):
                if ".broken_" in path.name or path.name.endswith(".bak") or "DO_NOT_USE" in path.name:
                    continue
                yield path


def apply_guid_remap(text: str, remap: dict[str, str]) -> str:
    for old, new in sorted(remap.items(), key=lambda item: len(item[0]), reverse=True):
        if old in text:
            text = text.replace(old, new)
    return text


def _mat_has_albedo(mat_path: Path) -> bool:
    try:
        text = mat_path.read_text(encoding="utf-8", errors="replace")
    except OSError:
        return False
    match = re.search(
        r"_MainTex:\s*\n\s+m_Texture: \{fileID: (\d+), guid: [0-9a-f]{32}, type: 3\}",
        text,
    )
    return bool(match and match.group(1) != "0")


def _norm_texture_key(name: str) -> str:
    import unicodedata

    s = unicodedata.normalize("NFKC", name).lower()
    return re.sub(r"[^a-z0-9]+", "", s)


def _build_texture_name_index() -> dict[str, list[tuple[str, str]]]:
    index: dict[str, list[tuple[str, str]]] = {}
    search_roots = [
        ASSETS / "scene" / "textures",
        ASSETS / "scene",
        ASSETS,
    ]
    image_ext = {".png", ".jpg", ".jpeg", ".tga", ".tif", ".tiff", ".exr", ".psd", ".bmp"}
    for root in search_roots:
        if not root.is_dir():
            continue
        for path in root.rglob("*"):
            if path.suffix.lower() not in image_ext:
                continue
            meta = path.with_suffix(path.suffix + ".meta")
            if not meta.is_file():
                continue
            match = META_GUID.search(meta.read_text(encoding="utf-8", errors="replace"))
            if not match:
                continue
            guid = match.group(1)
            rel = str(path.relative_to(ASSETS)).replace("\\", "/")
            index.setdefault(_norm_texture_key(path.stem), []).append((guid, rel))
    return index


def _score_texture_match(mat_name: str, tex_stem: str) -> int:
    a, b = _norm_texture_key(mat_name), _norm_texture_key(tex_stem)
    if not a or not b:
        return 0
    if a == b:
        return 100
    if a in b or b in a:
        return 80
    ta = set(re.findall(r"[a-z0-9]{3,}", a))
    tb = set(re.findall(r"[a-z0-9]{3,}", b))
    if not ta or not tb:
        return 0
    return int(70 * len(ta & tb) / max(len(ta), len(tb)))


def _find_texture_guid_for_material(
    mat_name: str, tex_index: dict[str, list[tuple[str, str]]]
) -> str | None:
    direct = tex_index.get(_norm_texture_key(mat_name))
    if direct:
        return direct[0][0]
    base = re.sub(r"\.\d{3}$", "", mat_name)
    direct = tex_index.get(_norm_texture_key(base))
    if direct:
        return direct[0][0]
    best_guid = None
    best_score = 0
    for stem, entries in tex_index.items():
        score = _score_texture_match(mat_name, stem)
        if score > best_score:
            best_score = score
            best_guid = entries[0][0]
    return best_guid if best_score >= 50 else None


def fix_broken_material_texture_guids(meta_index: dict[str, str]) -> int:
    """Remap missing texture GUIDs in scene/Materials by filename similarity."""
    tex_index = _build_texture_name_index()
    tex_ref = re.compile(r"m_Texture: \{fileID: (\d+), guid: ([0-9a-f]{32}), type: 3\}")
    materials_dir = ASSETS / "scene" / "Materials"
    fixed = 0
    for mat_path in materials_dir.glob("*.mat"):
        text = mat_path.read_text(encoding="utf-8", errors="replace")
        name_m = re.search(r"m_Name: (.+)", text)
        mat_name = name_m.group(1).strip() if name_m else mat_path.stem
        changed = False
        for fid, guid in tex_ref.findall(text):
            if guid in meta_index:
                continue
            new_guid = _find_texture_guid_for_material(mat_name, tex_index)
            if not new_guid:
                continue
            old = f"m_Texture: {{fileID: {fid}, guid: {guid}, type: 3}}"
            new = f"m_Texture: {{fileID: 2800000, guid: {new_guid}, type: 3}}"
            if old in text:
                text = text.replace(old, new)
                changed = True
        # Invalid Unity refs (guid: 0) break pink materials in Tuanjie.
        if "guid: 0," in text:
            text = text.replace(
                "m_Texture: {fileID: 2800000, guid: 0, type: 3}",
                "m_Texture: {fileID: 0}",
            )
            changed = True
        if changed:
            mat_path.write_text(text, encoding="utf-8")
            fixed += 1
    return fixed


def fix_shell_materials() -> int:
    count = 0
    for rel in (
        "scene/lib2f Shell/LibraryFloor2Shell.mat",
        "scene/图书馆四楼 Shell/LibraryFloor4Shell.mat",
    ):
        mat_path = ASSETS / rel.replace("/", "\\")
        if not mat_path.is_file():
            continue
        text = _fix_plaster_style_mat(
            mat_path.read_text(encoding="utf-8", errors="replace")
        )
        if "guid: 0," in text:
            text = text.replace(
                "m_Texture: {fileID: 2800000, guid: 0, type: 3}",
                "m_Texture: {fileID: 0}",
            )
        mat_path.write_text(text, encoding="utf-8")
        count += 1
        print(f"Fixed shell material: {rel}")
    return count


def _fix_plaster_style_mat(text: str) -> str:
    if "50ca0243fc42eac4b832ea5ff8c3fb1e" in text and "_MainTex:" in text:
        text = re.sub(
            r"(_MainTex:\s*\n\s+m_Texture: \{fileID: 2800000, guid: )50ca0243fc42eac4b832ea5ff8c3fb1e",
            r"\g<1>9931b8b347ac1eb43899aac06cb287b4",
            text,
        )
    text = text.replace(
        "  m_InvalidKeywords:\n  - _NORMALMAP\n",
        "  m_InvalidKeywords: []\n",
    )
    return text


def ensure_library_floor_legacy_materials() -> int:
    """Rebuild floor4 legacy mats from real scene/Materials (not empty White plaster copy)."""
    materials_dir = ASSETS / "scene" / "Materials"
    out_dir = ASSETS / "scene" / "LegacyLibraryFloor4"
    textured = [p for p in materials_dir.glob("*.mat") if _mat_has_albedo(p)]
    if not textured:
        print("WARN no textured materials in Assets/scene/Materials")
        return 0
    out_dir.mkdir(parents=True, exist_ok=True)
    created = 0
    for idx, guid in enumerate(LIBRARY_FLOOR4_LEGACY_MATERIAL_GUIDS):
        src = textured[idx % len(textured)]
        dest = out_dir / f"LegacyMat_{guid[:8]}.mat"
        body = src.read_text(encoding="utf-8", errors="replace")
        body = re.sub(r"m_Name: [^\n]+", f"m_Name: LegacyMat_{guid[:8]}", body, count=1)
        body = _fix_plaster_style_mat(body)
        dest.write_text(body, encoding="utf-8")
        meta_path = dest.with_suffix(".mat.meta")
        meta_path.write_text(
            f"fileFormatVersion: 2\nguid: {guid}\n"
            "NativeFormatImporter:\n"
            "  externalObjects: {}\n"
            "  mainObjectFileID: 2100000\n"
            "  userData: \n"
            "  assetBundleName: \n"
            "  assetBundleVariant: \n",
            encoding="utf-8",
        )
        created += 1
        print(f"Legacy library floor4 material: {dest.relative_to(ROOT)} <- {src.name}")
    fix_shell_materials()
    return created


def restore_meta_guids() -> int:
    changed = 0
    for rel_path, guid in RESTORE_META_GUIDS.items():
        meta_path = ROOT / rel_path.replace("/", "\\")
        if not meta_path.is_file():
            print(f"WARN missing meta: {rel_path}")
            continue
        text = meta_path.read_text(encoding="utf-8", errors="replace")
        match = GUID_LINE.search(text)
        if not match:
            print(f"WARN no guid line: {rel_path}")
            continue
        current = match.group(2).strip()
        if current == guid:
            continue
        new_text = GUID_LINE.sub(rf"\g<1>{guid}", text, count=1)
        meta_path.write_text(new_text, encoding="utf-8")
        changed += 1
        print(f"Restored meta GUID: {rel_path} ({current[:12]}...) -> {guid}")
    return changed


def fix_remaining_invalid_meta_guids() -> tuple[int, int]:
    """Assign valid hex GUIDs to .meta with invalid GUIDs; patch any references to old invalid strings.

    Skips RESTORE_META_GUIDS paths — those must keep scene-stable GUIDs from restore_meta_guids().
    """
    invalid_to_new: dict[str, str] = {}
    for meta_path in ASSETS.rglob("*.meta"):
        rel = str(meta_path.relative_to(ROOT)).replace("\\", "/")
        if rel in RESTORE_META_GUIDS:
            continue
        try:
            text = meta_path.read_text(encoding="utf-8", errors="replace")
        except OSError:
            continue
        match = GUID_LINE.search(text)
        if not match:
            continue
        old = match.group(2).strip()
        if is_valid_guid(old):
            continue
        invalid_to_new[old] = uuid.uuid4().hex

    meta_changed = 0
    for meta_path in ASSETS.rglob("*.meta"):
        try:
            text = meta_path.read_text(encoding="utf-8", errors="replace")
        except OSError:
            continue
        match = GUID_LINE.search(text)
        if not match:
            continue
        old = match.group(2).strip()
        if old not in invalid_to_new:
            continue
        new_text = GUID_LINE.sub(rf"\g<1>{invalid_to_new[old]}", text, count=1)
        if new_text != text:
            meta_path.write_text(new_text, encoding="utf-8")
            meta_changed += 1

    refs_changed = 0
    if invalid_to_new:
        for path in iter_reference_files():
            try:
                text = path.read_text(encoding="utf-8", errors="replace")
            except (OSError, UnicodeDecodeError):
                continue
            updated = apply_guid_remap(text, invalid_to_new)
            if updated != text:
                path.write_text(updated, encoding="utf-8")
                refs_changed += 1

    return meta_changed, refs_changed


def build_script_fingerprints() -> tuple[dict[str, frozenset[str]], dict[str, str]]:
    fingerprints: dict[str, frozenset[str]] = {}
    paths: dict[str, str] = {}
    for cs_path in ASSETS.rglob("*.cs"):
        try:
            src = cs_path.read_text(encoding="utf-8", errors="replace")
        except OSError:
            continue
        fields = set(
            re.findall(
                r"(?:public|private|protected)\s+(?:static\s+)?(?:readonly\s+)?[\w<>,\[\]\.?]+\s+(\w+)\s*(?:=|;|\{)",
                src,
            )
        )
        fields |= set(
            re.findall(
                r"\[SerializeField\][^\n]*\n\s*(?:public|private|protected)?\s*[\w<>,\[\]\.?]+\s+(\w+)",
                src,
            )
        )
        meta_path = cs_path.with_suffix(".cs.meta")
        if not meta_path.is_file():
            continue
        match = META_GUID.search(meta_path.read_text(encoding="utf-8", errors="replace"))
        if not match:
            continue
        guid = match.group(1)
        fingerprints[guid] = frozenset(fields)
        paths[guid] = str(cs_path.relative_to(ASSETS)).replace("\\", "/")
    return fingerprints, paths


def discover_script_remaps(meta_index: dict[str, str]) -> dict[str, str]:
    fingerprints, script_paths = build_script_fingerprints()
    missing_blocks: dict[str, list[frozenset[str]]] = defaultdict(list)

    for path in iter_reference_files():
        try:
            text = path.read_text(encoding="utf-8", errors="replace")
        except (OSError, UnicodeDecodeError):
            continue
        for block in re.split(r"^--- !u!114 ", text, flags=re.M)[1:]:
            script_match = MONO_SCRIPT.search(block)
            if not script_match:
                continue
            guid = script_match.group(1)
            if guid in BUILTIN or guid in meta_index or guid in KNOWN_SCRIPT_REMAPS:
                continue
            body = block.split("--- !u!", 1)[0]
            fields = frozenset(FIELD.findall(body)) - SKIP_YAML_FIELDS
            missing_blocks[guid].append(fields)

    discovered: dict[str, str] = {}
    for guid, blocks in missing_blocks.items():
        union: set[str] = set()
        for fields in blocks:
            union |= set(fields)
        if not union:
            continue
        best_guid = None
        best_score = 0
        for script_guid, script_fields in fingerprints.items():
            score = len(union & script_fields)
            if score > best_score:
                best_score = score
                best_guid = script_guid
        if best_guid and best_score >= 2:
            discovered[guid] = best_guid
            print(
                f"  script {guid} -> {best_guid} ({script_paths.get(best_guid, '?')}) score={best_score}"
            )
        elif best_guid and best_score >= 1:
            # Single-field scripts (e.g. toggles) often only match one field.
            runners = [
                g
                for g, fields in fingerprints.items()
                if len(union & fields) == best_score and best_score > 0
            ]
            if len(runners) == 1 and runners[0] == best_guid:
                discovered[guid] = best_guid
                print(
                    f"  script {guid} -> {best_guid} ({script_paths.get(best_guid, '?')}) score={best_score} (unique)"
                )
    return discovered


def build_prefab_remap_by_instance_name(meta_index: dict[str, str]) -> dict[str, str]:
    """Map old prefab GUIDs using PrefabInstance m_Name -> known prefab path."""
    path_to_guid: dict[str, str] = {}
    for guid, rel in meta_index.items():
        if rel.endswith(".prefab.meta"):
            path_to_guid[rel.replace(".meta", "")] = guid

    remap: dict[str, str] = {}
    scene_dir = ASSETS / "scene"
    for path in scene_dir.rglob("*"):
        if not path.is_file() or path.suffix.lower() not in {".unity", ".scene"}:
            continue
        if ".broken_" in path.name or path.name.endswith(".bak") or "DO_NOT_USE" in path.name:
            continue
        text = path.read_text(encoding="utf-8", errors="replace")
        blocks = PREFAB_BLOCK.split(text)
        for block in blocks[1:]:
            source = SOURCE_PREFAB.search(block)
            if not source:
                continue
            old_guid = source.group(1)
            if old_guid in meta_index or old_guid in remap:
                continue
            name_match = re.search(
                r"propertyPath: m_Name\n\s+value: (.+)",
                block,
            )
            if not name_match:
                continue
            name = name_match.group(1).strip()
            hint = PREFAB_NAME_HINTS.get(name)
            if not hint:
                continue
            new_guid = path_to_guid.get(hint)
            if new_guid and old_guid != new_guid:
                remap[old_guid] = new_guid
    return remap


def load_story_prefab_guids() -> dict[str, str]:
    mapping: dict[str, str] = {}
    for meta in STORY_PREFAB_DIR.glob("*.prefab.meta"):
        text = meta.read_text(encoding="utf-8", errors="replace")
        match = GUID_LINE.search(text)
        if not match:
            continue
        display_name = meta.name[: -len(".prefab.meta")]
        mapping[display_name] = match.group(2).strip()
    return mapping


def build_story_guid_remap(prefab_guids: dict[str, str]) -> dict[str, str]:
    remap: dict[str, str] = {}
    scene_dir = ASSETS / "scene"
    for path in scene_dir.rglob("*"):
        if not path.is_file() or path.suffix.lower() not in {".unity", ".scene"}:
            continue
        if ".broken_" in path.name or path.name.endswith(".bak") or "DO_NOT_USE" in path.name:
            continue
        text = path.read_text(encoding="utf-8", errors="replace")
        blocks = PREFAB_BLOCK.split(text)
        for block in blocks[1:]:
            source = SOURCE_PREFAB.search(block)
            if not source:
                continue
            old_guid = source.group(1)
            names = STORY_NAME.findall(block)
            if not names:
                continue
            display_names = {display_from_story_object(n) for n in names}
            target_guids = {prefab_guids[d] for d in display_names if d in prefab_guids}
            if len(target_guids) != 1:
                continue
            new_guid = next(iter(target_guids))
            if old_guid != new_guid:
                remap[old_guid] = new_guid
    return remap


def patch_all_references(combined_remap: dict[str, str]) -> int:
    touched = 0
    for path in iter_reference_files():
        try:
            text = path.read_text(encoding="utf-8", errors="replace")
        except (OSError, UnicodeDecodeError):
            continue
        updated = apply_guid_remap(text, combined_remap)
        if updated != text:
            path.write_text(updated, encoding="utf-8")
            touched += 1
    return touched


def scan_missing(meta_index: dict[str, str]) -> dict[str, set[str]]:
    missing: dict[str, set[str]] = defaultdict(set)
    for path in iter_reference_files():
        try:
            text = path.read_text(encoding="utf-8", errors="replace")
        except (OSError, UnicodeDecodeError):
            continue
        rel = str(path.relative_to(ROOT)).replace("\\", "/")
        for match in GUID_IN_REF.finditer(text):
            guid = match.group(1)
            if guid in BUILTIN or guid in meta_index:
                continue
            missing[guid].add(rel)
    return missing


def write_report(missing: dict[str, set[str]]) -> Path:
    stamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    report_path = ROOT / "Assets" / "code" / "Editor" / f"guid_repair_report_{stamp}.txt"
    lines = [
        f"GUID repair report {stamp}",
        f"Remaining missing unique GUIDs: {len(missing)}",
        "",
    ]
    for guid, files in sorted(missing.items(), key=lambda item: -len(item[1])):
        sample = ", ".join(sorted(files)[:3])
        lines.append(f"{len(files):4d}  {guid}  [{sample}]")
    report_path.write_text("\n".join(lines), encoding="utf-8")
    return report_path


def main() -> None:
    print("=== Step 0: Restore known .meta GUIDs (library shells, videos, lib2f) ===")
    restore_meta_guids()

    print("\n=== Step 0b: Fix Materials texture GUIDs + shell mats ===")
    meta_index = load_meta_index()
    tex_fixed = fix_broken_material_texture_guids(meta_index)
    print(f"Materials with remapped textures: {tex_fixed}")
    shell_fixed = fix_shell_materials()
    print(f"Shell materials fixed: {shell_fixed}")

    print("\n=== Step 0c: Library floor legacy materials ===")
    legacy_mats = ensure_library_floor_legacy_materials()
    print(f"Library floor4 legacy materials created/verified: {legacy_mats}")

    print("\n=== Step 1: Restore meta GUIDs again (post material pass) ===")
    restore_meta_guids()

    print("\n=== Step 2: Fix invalid .meta GUIDs (with reference patch) ===")
    meta_fixed, refs_fixed = fix_remaining_invalid_meta_guids()
    print(f"Fixed invalid meta: {meta_fixed}, reference files: {refs_fixed}")

    print("\n=== Step 2b: Re-apply restored meta (Unity may have corrupted video/controller metas) ===")
    restore_meta_guids()

    print("\n=== Step 3: Build remap table ===")
    meta_index = load_meta_index()
    print(f"Meta index size: {len(meta_index)}")

    combined = dict(KNOWN_ASSET_REMAPS)
    combined.update(KNOWN_ASSET_EXTRA)
    combined.update(KNOWN_SCRIPT_REMAPS)

    print("Discovering script remaps by field fingerprint...")
    combined.update(discover_script_remaps(meta_index))

    prefab_name_remap = build_prefab_remap_by_instance_name(meta_index)
    print(f"Prefab instance name remaps: {len(prefab_name_remap)}")
    combined.update(prefab_name_remap)

    story_remap = build_story_guid_remap(load_story_prefab_guids())
    print(f"Story NPC prefab remaps: {len(story_remap)}")
    combined.update(story_remap)

    combined = {old: new for old, new in combined.items() if old != new}

    print(f"\n=== Step 4: Patch references ({len(combined)} remaps) ===")
    touched = patch_all_references(combined)
    print(f"Patched reference files: {touched}")

    print("\n=== Step 5: Post-repair scan ===")
    meta_index = load_meta_index()
    missing = scan_missing(meta_index)
    report = write_report(missing)
    print(f"Remaining missing GUIDs: {len(missing)}")
    for guid, files in sorted(missing.items(), key=lambda item: -len(item[1]))[:15]:
        print(f"  {len(files):4d}  {guid}  {list(files)[:2]}")
    scene_missing = {
        g: f
        for g, f in missing.items()
        if any(p.startswith("Assets/scene/") for p in f)
    }
    print(f"\nMissing GUIDs touching Assets/scene: {len(scene_missing)}")
    for guid, files in sorted(scene_missing.items(), key=lambda item: -len(item[1]))[:12]:
        print(f"  {len(files):4d}  {guid}  {list(files)[:2]}")
    print(f"\nFull report: {report.relative_to(ROOT)}")
    print("Done. Re-open Unity and let assets reimport.")


if __name__ == "__main__":
    main()
