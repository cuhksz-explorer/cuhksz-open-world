"""GameServer 运行时数据目录（位于 Unity 项目根目录，避免 Build 产物被 Unity 导入）。"""
from __future__ import annotations

import os
import shutil
from pathlib import Path

BACKEND_ROOT = Path(__file__).resolve().parent
LEGACY_DATA_DIR = BACKEND_ROOT / "data"


def find_unity_project_root(start: Path | None = None) -> Path:
    start = start or BACKEND_ROOT
    for candidate in [start, *start.parents]:
        if (candidate / "Assets").is_dir() and (candidate / "ProjectSettings").is_dir():
            return candidate
    return start.parents[3] if len(start.parents) > 3 else start.parent


PROJECT_ROOT = find_unity_project_root()
RUNTIME_ROOT = Path(
    os.environ.get("GAME_SERVER_RUNTIME_DIR", str(PROJECT_ROOT / "GameServerRuntime"))
).resolve()

CLIENT_RELEASE_DIR = RUNTIME_ROOT / "client_releases"
DB_PATH = RUNTIME_ROOT / "game.db"
DORM_MALL_CATALOG_PATH = RUNTIME_ROOT / "dorm_mall_catalog.json"
CONTENT_DIR = RUNTIME_ROOT / "content"
HUB_STATS_PATH = RUNTIME_ROOT / "hub_stats.json"
LOG_FILE_PATH = RUNTIME_ROOT / "server.log"


def ensure_runtime_dirs() -> None:
    RUNTIME_ROOT.mkdir(parents=True, exist_ok=True)
    CLIENT_RELEASE_DIR.mkdir(parents=True, exist_ok=True)
    (CLIENT_RELEASE_DIR / "files").mkdir(parents=True, exist_ok=True)
    CONTENT_DIR.mkdir(parents=True, exist_ok=True)
    (CONTENT_DIR / "bundles").mkdir(parents=True, exist_ok=True)


def _migrate_path(src: Path, dst: Path) -> None:
    if not src.exists() or dst.exists():
        return
    dst.parent.mkdir(parents=True, exist_ok=True)
    try:
        shutil.move(str(src), str(dst))
    except OSError:
        if src.is_dir():
            shutil.copytree(src, dst, dirs_exist_ok=True)
            shutil.rmtree(src, ignore_errors=True)
        else:
            shutil.copy2(src, dst)
            src.unlink(missing_ok=True)


def migrate_legacy_data_dir() -> None:
    """首次启动时将 backend/data 下的运行时文件迁到 GameServerRuntime/。"""
    if not LEGACY_DATA_DIR.is_dir():
        return

    mappings = [
        (LEGACY_DATA_DIR / "client_releases", CLIENT_RELEASE_DIR),
        (LEGACY_DATA_DIR / "game.db", DB_PATH),
        (LEGACY_DATA_DIR / "dorm_mall_catalog.json", DORM_MALL_CATALOG_PATH),
        (LEGACY_DATA_DIR / "content", CONTENT_DIR),
        (LEGACY_DATA_DIR / "hub_stats.json", HUB_STATS_PATH),
        (LEGACY_DATA_DIR / "server.log", LOG_FILE_PATH),
    ]
    for src, dst in mappings:
        _migrate_path(src, dst)

    # 清理已迁移的 client_releases，避免 Unity 继续扫描其中的 .dll
    legacy_releases = LEGACY_DATA_DIR / "client_releases"
    if legacy_releases.is_dir() and CLIENT_RELEASE_DIR.exists():
        shutil.rmtree(legacy_releases, ignore_errors=True)
