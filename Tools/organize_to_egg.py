# -*- coding: utf-8 -*-
"""Move non-runtime planning/backup artifacts into egg/ (one-time organizer)."""
from __future__ import annotations

import shutil
from datetime import datetime
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
EGG = ROOT / "egg"

# (source relative to ROOT, destination relative to EGG)
MOVES: list[tuple[str, str]] = []

# Root workbooks & misc
for name in ROOT.glob("*.xlsx"):
    MOVES.append((name.relative_to(ROOT).as_posix(), f"workbooks/{name.name}"))
for name in ("chair_sit_binding_status.txt", "_fbx_mats.py"):
    p = ROOT / name
    if p.is_file():
        MOVES.append((name, f"root_misc/{name}"))

# Tools generated artifacts (keep .py scripts in Tools/)
TOOLS_ARTIFACTS = [
    "dialogue_import_report.txt",
    "narration_import_report.txt",
    "_quest_doc_index.txt",
    "_story_roots_list.txt",
    "main_story_zh.json",
    "side_story_zh.json",
    "story_main_en.partial.json",
    "story_side_en.partial.json",
]
for name in TOOLS_ARTIFACTS:
    p = ROOT / "Tools" / name
    if p.exists():
        MOVES.append((f"Tools/{name}", f"tools_reports/{name}"))

if (ROOT / "Tools" / "_audio_cache").is_dir():
    MOVES.append(("Tools/_audio_cache", "tools_audio_cache"))

# Design / planning docs (no runtime load)
DESIGN_DOCS = [
    "Assets/code/1.txt",
    "Assets/code/1.md",
    "Assets/code/README.md",
    "Assets/code/联机与服务器配置指南.md",
    "Assets/Resources/Music/README_我和我的祖国.txt",
    "Assets/Animations/Locomotion/README.txt",
    "Assets/PhoneUI/README.md",
    "Assets/PhoneUI/ChangeBoard/README.md",
    "Assets/PhoneUI/ChangeBoard/_Preserved_Deps/README.md",
    "Assets/Carsystem/carreadme.md",
    "Assets/Carsystem/CarShopSetupGuide.md",
    "Assets/Carsystem/车辆系统替换重构清单.md",
    "Assets/code/DormMall/搭建步骤.md",
    "Assets/Imports/Sketchfab/剧情人物模型对应表.md",
    "ExportedScripts/HealthSystemBundle/README.md",
]
for rel in DESIGN_DOCS:
    p = ROOT / rel
    if p.is_file():
        MOVES.append((rel.replace("\\", "/"), f"design_docs/{Path(rel).name}"))

# Editor repair logs
for p in (ROOT / "Assets" / "code" / "Editor").glob("guid_repair_report_*.txt"):
    rel = p.relative_to(ROOT).as_posix()
    MOVES.append((rel, f"editor_reports/{p.name}"))

# Scene / prefab backups (disabled in builds; editor may recreate paths)
if (ROOT / "Assets" / "scene" / "Backups").is_dir():
    MOVES.append(("Assets/scene/Backups", "scene_backups"))
if (ROOT / "Assets" / "code" / "Editor" / "library_reimport_backup").is_dir():
    MOVES.append(
        ("Assets/code/Editor/library_reimport_backup", "library_reimport_backup")
    )
if (ROOT / "Assets" / "scene" / "middlegarden.unity.bak").is_file():
    MOVES.append(
        ("Assets/scene/middlegarden.unity.bak", "scene_orphans/middlegarden.unity.bak")
    )
if (ROOT / "_import_temp").is_dir():
    MOVES.append(("_import_temp", "import_temp"))


def move_item(src_rel: str, dst_rel: str, log: list[str]) -> None:
    src = ROOT / src_rel
    dst = EGG / dst_rel
    if not src.exists():
        log.append(f"SKIP missing: {src_rel}")
        return
    if dst.exists():
        log.append(f"SKIP exists: {dst_rel}")
        return
    dst.parent.mkdir(parents=True, exist_ok=True)
    shutil.move(str(src), str(dst))
    log.append(f"MOVED {src_rel} -> egg/{dst_rel}")


def main() -> int:
    EGG.mkdir(exist_ok=True)
    log: list[str] = [
        f"# egg organize {datetime.now().isoformat(timespec='seconds')}",
        "",
        "## Kept in project (referenced by game/editor runtime)",
        "- Assets/content1/ (story docx + database sourceAssetPath)",
        "- Assets/Imports/Sketchfab/RecommendedModels.md (Editor menu)",
        "- MirrorExamplesPipelineConverted.txt (Mirror package flag)",
        "- Assets/Localization/*.json, Content1StoryDatabase.asset",
        "- Tools/*.py (dev pipelines; paths updated for egg/workbooks)",
        "",
        "## Moved",
    ]
    for src_rel, dst_rel in MOVES:
        move_item(src_rel, dst_rel, log)

    readme = EGG / "README.txt"
    readme.write_text(
        "egg/ — 与游戏运行无关的策划表、备份、报告与文档归档。\n"
        "不影响 Unity 构建与运行时资源加载（content1 剧情源文件仍留在 Assets/）。\n"
        "详见 manifest.txt\n",
        encoding="utf-8",
    )
    (EGG / "manifest.txt").write_text("\n".join(log), encoding="utf-8")
    print("\n".join(log))
    print(f"\nDone. See {readme}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
