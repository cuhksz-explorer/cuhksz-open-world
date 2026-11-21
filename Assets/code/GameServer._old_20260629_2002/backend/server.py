"""
游戏账号与角色位置同步服务（HTTP JSON API）。
默认监听 0.0.0.0:8765；运行时数据目录：项目根/GameServerRuntime/（不在 Assets 内）
"""
from __future__ import annotations

import json
import os
import secrets
import socket
import sqlite3
import time
from contextlib import contextmanager
from pathlib import Path

import bcrypt
from fastapi import FastAPI, HTTPException, Query, Request, status
from fastapi.responses import FileResponse
from fastapi.middleware.cors import CORSMiddleware
from fastapi.staticfiles import StaticFiles
from typing import Any, Optional

from pydantic import BaseModel, Field

from client_release import register_client_release_routes
from runtime_paths import (
    CLIENT_RELEASE_DIR,
    CONTENT_DIR,
    DB_PATH,
    DORM_MALL_CATALOG_PATH,
    ensure_runtime_dirs,
    migrate_legacy_data_dir,
)
from server_hub import register_server_hub
from lguhub_auth_bridge import lguhub_auth_configured, verify_lguhub_login
from lguhub_bridge import issue_lguhub_unity_token, lguhub_base_url, provision_lguhub_user
from sso_support import (
    change_password as sso_change_password,
    consume_sso_ticket,
    ensure_sso_tables,
    issue_sso_ticket,
    register_user as sso_register_user,
    reset_password as sso_reset_password,
    verify_credentials as sso_verify_credentials,
    verify_sso_auth,
)

ROOT = Path(__file__).resolve().parent

migrate_legacy_data_dir()
ensure_runtime_dirs()

DORM_MALL_STATIC_DIR = ROOT / "static" / "dorm_mall"
STORY_DATABASE_PATH = CONTENT_DIR / "story_database.json"
CONTENT_MANIFEST_PATH = CONTENT_DIR / "manifest.json"
CONTENT_BUNDLE_DIR = CONTENT_DIR / "bundles"
ADMIN_STATIC_DIR = ROOT / "static" / "admin"
CONTROL_STATIC_DIR = ROOT / "static" / "control"
DEFAULT_SPAWN_SCENE = "undergarden"
DEFAULT_SPAWN_X = -1357.48
DEFAULT_SPAWN_Y = 3.82
DEFAULT_SPAWN_Z = 1314.31
DEFAULT_SPAWN_ROT_Y = 0.0
MAX_DORM_ROOMS = 64

app = FastAPI(title="Game Account & Position Server", version="1.0.0")
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# 可选：把导出的商品图放到 backend/static/dorm_mall 后，可通过 /static/dorm_mall/... 访问
DORM_MALL_STATIC_DIR.mkdir(parents=True, exist_ok=True)
app.mount(
    "/static/dorm_mall",
    StaticFiles(directory=str(DORM_MALL_STATIC_DIR)),
    name="dorm_mall_static",
)

ADMIN_STATIC_DIR.mkdir(parents=True, exist_ok=True)
CLIENT_RELEASE_DIR.mkdir(parents=True, exist_ok=True)
app.mount(
    "/admin",
    StaticFiles(directory=str(ADMIN_STATIC_DIR), html=True),
    name="admin_static",
)
CONTROL_STATIC_DIR.mkdir(parents=True, exist_ok=True)
app.mount(
    "/control",
    StaticFiles(directory=str(CONTROL_STATIC_DIR), html=True),
    name="control_static",
)
register_client_release_routes(app)

# 按客户端 IP 记录最近一次 HTTP 活动时间（用于 Unity 冷启动是否进 Loading 校验）
_ip_last_seen: dict[str, float] = {}
COLD_SESSION_IDLE_SEC = 60.0


def _peer_ip(request: Request) -> str:
    xff = request.headers.get("x-forwarded-for") or request.headers.get("X-Forwarded-For")
    if xff:
        return xff.split(",")[0].strip()
    if request.client and request.client.host:
        return request.client.host
    return "127.0.0.1"


def _local_ipv4_candidates() -> list[str]:
    ips: set[str] = set()
    # 1) 主机名解析（常见于局域网网卡）
    try:
        for info in socket.getaddrinfo(socket.gethostname(), None, socket.AF_INET):
            ip = info[4][0]
            if ip and ip != "127.0.0.1":
                ips.add(ip)
    except Exception:
        pass

    # 2) UDP 探测默认出口网卡（不真正发包）
    try:
        s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        s.connect(("8.8.8.8", 80))
        ip = s.getsockname()[0]
        s.close()
        if ip and ip != "127.0.0.1":
            ips.add(ip)
    except Exception:
        pass

    return sorted(ips)


@app.middleware("http")
async def touch_peer_ip_last_seen(request: Request, call_next):
    response = await call_next(request)
    try:
        _ip_last_seen[_peer_ip(request)] = time.time()
    except Exception:
        pass
    return response


def _ensure_player_positions_view_mode_column(conn: sqlite3.Connection) -> None:
    if not conn.execute(
        "SELECT name FROM sqlite_master WHERE type='table' AND name='player_positions'"
    ).fetchone():
        return
    cols = {row[1] for row in conn.execute("PRAGMA table_info(player_positions)")}
    if "view_mode" not in cols:
        conn.execute(
            "ALTER TABLE player_positions ADD COLUMN view_mode TEXT NOT NULL DEFAULT 'third'"
        )


def _is_legacy_single_user_pk_player_positions(ddl: str) -> bool:
    """旧表：user_id INTEGER PRIMARY KEY。新表：PRIMARY KEY (user_id, scene_name)。"""
    if not ddl or "player_positions" not in ddl.lower():
        return False
    compact = "".join(ddl.lower().split())
    if "primarykey(user_id,scene_name)" in compact:
        return False
    return "user_idintegerprimarykey" in compact


def _ensure_player_positions_table(conn: sqlite3.Connection) -> None:
    row = conn.execute(
        "SELECT sql FROM sqlite_master WHERE type='table' AND name='player_positions'"
    ).fetchone()
    if row is None:
        conn.execute(
            """
            CREATE TABLE player_positions (
                user_id INTEGER NOT NULL,
                scene_name TEXT NOT NULL,
                pos_x REAL NOT NULL,
                pos_y REAL NOT NULL,
                pos_z REAL NOT NULL,
                rot_y REAL NOT NULL DEFAULT 0,
                view_mode TEXT NOT NULL DEFAULT 'third',
                updated_at REAL NOT NULL,
                PRIMARY KEY (user_id, scene_name),
                FOREIGN KEY (user_id) REFERENCES users(id)
            );
            """
        )
        return

    ddl = row[0] or ""
    if not _is_legacy_single_user_pk_player_positions(ddl):
        return

    conn.execute("ALTER TABLE player_positions RENAME TO player_positions_legacy")
    conn.execute(
        """
        CREATE TABLE player_positions (
            user_id INTEGER NOT NULL,
            scene_name TEXT NOT NULL,
            pos_x REAL NOT NULL,
            pos_y REAL NOT NULL,
            pos_z REAL NOT NULL,
            rot_y REAL NOT NULL DEFAULT 0,
            view_mode TEXT NOT NULL DEFAULT 'third',
            updated_at REAL NOT NULL,
            PRIMARY KEY (user_id, scene_name),
            FOREIGN KEY (user_id) REFERENCES users(id)
        );
        """
    )
    legacy_cols = {r[1] for r in conn.execute("PRAGMA table_info(player_positions_legacy)")}
    vm_sel = (
        "view_mode"
        if "view_mode" in legacy_cols
        else "'third'"
    )
    conn.execute(
        f"""
        INSERT INTO player_positions (user_id, scene_name, pos_x, pos_y, pos_z, rot_y, view_mode, updated_at)
        SELECT user_id, scene_name, pos_x, pos_y, pos_z, rot_y, {vm_sel}, updated_at
        FROM player_positions_legacy;
        """
    )
    conn.execute("DROP TABLE player_positions_legacy")


def init_db() -> None:
    DB_PATH.parent.mkdir(parents=True, exist_ok=True)
    with sqlite3.connect(DB_PATH) as conn:
        conn.execute(
            """
            CREATE TABLE IF NOT EXISTS users (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                username TEXT NOT NULL UNIQUE,
                nickname TEXT NOT NULL DEFAULT '',
                password_hash TEXT NOT NULL,
                created_at REAL NOT NULL
            );
            """
        )
        cols = {row[1] for row in conn.execute("PRAGMA table_info(users)")}
        if "nickname" not in cols:
            conn.execute("ALTER TABLE users ADD COLUMN nickname TEXT NOT NULL DEFAULT ''")
        if "character_id" not in cols:
            conn.execute("ALTER TABLE users ADD COLUMN character_id TEXT NOT NULL DEFAULT ''")
        try:
            # 昵称唯一（忽略空昵称）；若历史库里已有重复昵称会创建失败，仍保留接口层校验。
            conn.execute(
                """
                CREATE UNIQUE INDEX IF NOT EXISTS idx_users_nickname_unique
                ON users(nickname COLLATE NOCASE)
                WHERE nickname <> ''
                """
            )
        except sqlite3.IntegrityError:
            print("[GameServer] WARN: 现有数据库存在重复昵称，未能创建唯一索引，将仅依赖接口校验。")
        conn.execute(
            """
            CREATE TABLE IF NOT EXISTS sessions (
                token TEXT PRIMARY KEY,
                user_id INTEGER NOT NULL,
                created_at REAL NOT NULL,
                FOREIGN KEY (user_id) REFERENCES users(id)
            );
            """
        )
        conn.execute(
            """
            CREATE TABLE IF NOT EXISTS user_profiles (
                user_id INTEGER PRIMARY KEY,
                dorm_gold INTEGER NOT NULL DEFAULT 200,
                quest_state_json TEXT NOT NULL DEFAULT '',
                updated_at REAL NOT NULL,
                FOREIGN KEY (user_id) REFERENCES users(id)
            );
            """
        )
        _ensure_player_positions_table(conn)
        _ensure_player_positions_view_mode_column(conn)
        _ensure_social_tables(conn)
        _ensure_scene_shard_tables(conn)
        _ensure_flag_auction_tables(conn)
        _ensure_dorm_tables(conn)
        ensure_sso_tables(conn)
        conn.commit()


def _ensure_dorm_tables(conn: sqlite3.Connection) -> None:
    conn.execute(
        """
        CREATE TABLE IF NOT EXISTS dorm_layouts (
            user_id INTEGER NOT NULL,
            scene_name TEXT NOT NULL,
            layout_json TEXT NOT NULL DEFAULT '{}',
            updated_at REAL NOT NULL DEFAULT 0,
            PRIMARY KEY (user_id, scene_name),
            FOREIGN KEY (user_id) REFERENCES users(id)
        );
        """
    )
    cols = {row[1] for row in conn.execute("PRAGMA table_info(user_profiles)")}
    if "dorm_room_index" not in cols:
        conn.execute(
            "ALTER TABLE user_profiles ADD COLUMN dorm_room_index INTEGER NOT NULL DEFAULT -1"
        )


def ensure_dorm_room_index(conn: sqlite3.Connection, user_id: int) -> int:
    row = conn.execute(
        "SELECT dorm_room_index FROM user_profiles WHERE user_id = ?", (user_id,)
    ).fetchone()
    idx = int(row["dorm_room_index"]) if row is not None else -1
    if idx >= 0:
        return idx % MAX_DORM_ROOMS
    idx = int(user_id) % MAX_DORM_ROOMS
    conn.execute(
        "UPDATE user_profiles SET dorm_room_index = ? WHERE user_id = ?",
        (idx, user_id),
    )
    return idx


FLAG_AUCTION_SEC = 60.0
FLAG_CUSTOMIZE_SEC = 60.0


def _ensure_flag_auction_tables(conn: sqlite3.Connection) -> None:
    conn.execute(
        """
        CREATE TABLE IF NOT EXISTS flag_auction_slots (
            slot_id TEXT PRIMARY KEY,
            phase TEXT NOT NULL DEFAULT 'auction',
            phase_ends_at REAL NOT NULL,
            bid_user_id INTEGER,
            bid_amount INTEGER NOT NULL DEFAULT 0,
            customize_user_id INTEGER,
            display_text TEXT NOT NULL DEFAULT '',
            color_r REAL NOT NULL DEFAULT 1.0,
            color_g REAL NOT NULL DEFAULT 1.0,
            color_b REAL NOT NULL DEFAULT 1.0,
            color_a REAL NOT NULL DEFAULT 1.0
        );
        """
    )


def _tick_flag_auction_slot(conn: sqlite3.Connection, slot_id: str, now: float) -> None:
    """推进竞拍阶段直到当前时刻（同一 slot 可连续多次阶段切换）。"""
    row = conn.execute(
        "SELECT * FROM flag_auction_slots WHERE slot_id = ?", (slot_id,)
    ).fetchone()
    if row is None:
        conn.execute(
            """
            INSERT INTO flag_auction_slots (
                slot_id, phase, phase_ends_at, bid_amount
            ) VALUES (?, 'auction', ?, 0)
            """,
            (slot_id, now + FLAG_AUCTION_SEC),
        )
        return

    while now >= float(row["phase_ends_at"]):
        ph = (row["phase"] or "auction").strip().lower()
        if ph == "auction":
            uid = row["bid_user_id"]
            amt = int(row["bid_amount"] or 0)
            customize_uid: Optional[int] = None
            if uid is not None and amt > 0:
                ensure_user_profile(conn, int(uid))
                gold_row = conn.execute(
                    "SELECT dorm_gold FROM user_profiles WHERE user_id = ?",
                    (int(uid),),
                ).fetchone()
                gold = int(gold_row["dorm_gold"]) if gold_row else 0
                if gold >= amt:
                    conn.execute(
                        """
                        UPDATE user_profiles SET dorm_gold = dorm_gold - ?, updated_at = ?
                        WHERE user_id = ?
                        """,
                        (amt, now, int(uid)),
                    )
                    customize_uid = int(uid)
            conn.execute(
                """
                UPDATE flag_auction_slots SET
                    phase = 'customize',
                    phase_ends_at = ?,
                    bid_user_id = NULL,
                    bid_amount = 0,
                    customize_user_id = ?
                WHERE slot_id = ?
                """,
                (now + FLAG_CUSTOMIZE_SEC, customize_uid, slot_id),
            )
        else:
            conn.execute(
                """
                UPDATE flag_auction_slots SET
                    phase = 'auction',
                    phase_ends_at = ?,
                    bid_user_id = NULL,
                    bid_amount = 0,
                    customize_user_id = NULL
                WHERE slot_id = ?
                """,
                (now + FLAG_AUCTION_SEC, slot_id),
            )
        row = conn.execute(
            "SELECT * FROM flag_auction_slots WHERE slot_id = ?", (slot_id,)
        ).fetchone()
        if row is None:
            break


def _nickname_for_user_id(conn: sqlite3.Connection, user_id: int) -> str:
    row = conn.execute(
        "SELECT COALESCE(NULLIF(TRIM(nickname), ''), username) AS n FROM users WHERE id = ?",
        (user_id,),
    ).fetchone()
    return str(row["n"]) if row else ""


def _pair_key(a: int, b: int) -> tuple[int, int]:
    return (a, b) if a < b else (b, a)


def _ensure_social_tables(conn: sqlite3.Connection) -> None:
    conn.execute(
        """
        CREATE TABLE IF NOT EXISTS friend_requests (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            from_user_id INTEGER NOT NULL,
            to_user_id INTEGER NOT NULL,
            status TEXT NOT NULL DEFAULT 'pending',
            created_at REAL NOT NULL,
            FOREIGN KEY (from_user_id) REFERENCES users(id),
            FOREIGN KEY (to_user_id) REFERENCES users(id)
        );
        """
    )
    conn.execute(
        """
        CREATE UNIQUE INDEX IF NOT EXISTS idx_friend_requests_pending_pair
        ON friend_requests(from_user_id, to_user_id)
        WHERE status = 'pending';
        """
    )
    conn.execute(
        """
        CREATE TABLE IF NOT EXISTS friendships (
            user_low INTEGER NOT NULL,
            user_high INTEGER NOT NULL,
            created_at REAL NOT NULL,
            PRIMARY KEY (user_low, user_high),
            CHECK(user_low < user_high),
            FOREIGN KEY (user_low) REFERENCES users(id),
            FOREIGN KEY (user_high) REFERENCES users(id)
        );
        """
    )
    conn.execute(
        """
        CREATE TABLE IF NOT EXISTS chat_messages (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            from_user_id INTEGER NOT NULL,
            to_user_id INTEGER NOT NULL,
            body TEXT NOT NULL,
            created_at REAL NOT NULL,
            FOREIGN KEY (from_user_id) REFERENCES users(id),
            FOREIGN KEY (to_user_id) REFERENCES users(id)
        );
        """
    )
    conn.execute(
        "CREATE INDEX IF NOT EXISTS idx_chat_to_id ON chat_messages(to_user_id, id);"
    )
    conn.execute(
        "CREATE INDEX IF NOT EXISTS idx_chat_pair ON chat_messages(from_user_id, to_user_id, id);"
    )
    conn.execute(
        """
        CREATE TABLE IF NOT EXISTS proximity_chat_messages (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            scene_name TEXT NOT NULL,
            from_user_id INTEGER NOT NULL,
            to_user_id INTEGER NOT NULL,
            body TEXT NOT NULL,
            created_at REAL NOT NULL,
            FOREIGN KEY (from_user_id) REFERENCES users(id),
            FOREIGN KEY (to_user_id) REFERENCES users(id)
        );
        """
    )
    conn.execute(
        "CREATE INDEX IF NOT EXISTS idx_proximity_chat_scene_id ON proximity_chat_messages(scene_name, id);"
    )
    conn.execute(
        """
        CREATE TABLE IF NOT EXISTS coop_invites (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            from_user_id INTEGER NOT NULL,
            to_user_id INTEGER NOT NULL,
            status TEXT NOT NULL DEFAULT 'pending',
            host_connect_json TEXT,
            created_at REAL NOT NULL,
            expires_at REAL NOT NULL,
            FOREIGN KEY (from_user_id) REFERENCES users(id),
            FOREIGN KEY (to_user_id) REFERENCES users(id)
        );
        """
    )
    conn.execute(
        "CREATE INDEX IF NOT EXISTS idx_coop_from ON coop_invites(from_user_id, status, id);"
    )
    conn.execute(
        "CREATE INDEX IF NOT EXISTS idx_coop_to ON coop_invites(to_user_id, status, id);"
    )
    conn.execute(
        """
        CREATE TABLE IF NOT EXISTS coop_host_registry (
            user_id INTEGER PRIMARY KEY,
            advertised_ip TEXT NOT NULL,
            advertised_port INTEGER NOT NULL,
            transport TEXT NOT NULL DEFAULT 'Telepathy',
            updated_at REAL NOT NULL,
            expires_at REAL NOT NULL,
            FOREIGN KEY (user_id) REFERENCES users(id)
        );
        """
    )
    conn.execute(
        """
        CREATE TABLE IF NOT EXISTS public_coop_rooms (
            room_code TEXT PRIMARY KEY,
            owner_user_id INTEGER NOT NULL,
            advertised_ip TEXT NOT NULL,
            advertised_port INTEGER NOT NULL,
            transport TEXT NOT NULL DEFAULT 'Telepathy',
            updated_at REAL NOT NULL,
            expires_at REAL NOT NULL,
            FOREIGN KEY (owner_user_id) REFERENCES users(id)
        );
        """
    )


@contextmanager
def get_conn():
    conn = sqlite3.connect(DB_PATH)
    conn.row_factory = sqlite3.Row
    try:
        yield conn
    finally:
        conn.close()


class RegisterBody(BaseModel):
    username: str = Field(..., min_length=1, max_length=64)
    password: str = Field(..., min_length=1, max_length=128)


class LoginBody(BaseModel):
    username: str
    password: str


class TokenResponse(BaseModel):
    token: str
    username: str


class MeResponse(BaseModel):
    user_id: int
    username: str
    nickname: str = ""
    character_id: str = ""


class NicknameBody(BaseModel):
    token: str
    nickname: str = Field(..., min_length=1, max_length=24)


class ProfileSetupBody(BaseModel):
    token: str
    nickname: str = Field(..., min_length=1, max_length=24)
    character_id: str = Field(..., min_length=1, max_length=32)


class SpawnResponse(BaseModel):
    scene_name: str
    x: float
    y: float
    z: float
    rot_y: float
    has_saved: bool
    view_mode: str = "third"


class PlayerStateResponse(BaseModel):
    ok: bool = True
    dorm_gold: int = 200
    quest_state_json: str = ""
    has_state: bool = False


class PlayerStateBody(BaseModel):
    token: str
    dorm_gold: int = Field(default=200, ge=0)
    quest_state_json: str = ""


def hash_password(password: str) -> str:
    p = password.encode("utf-8")
    if len(p) > 72:
        p = p[:72]
    return bcrypt.hashpw(p, bcrypt.gensalt()).decode("ascii")


def verify_password(password: str, password_hash: str) -> bool:
    try:
        return bcrypt.checkpw(
            password.encode("utf-8"), password_hash.encode("ascii")
        )
    except (ValueError, TypeError):
        return False


def get_user_id_from_token(token: str) -> int:
    if not token:
        raise HTTPException(status.HTTP_401_UNAUTHORIZED, "未提供 token")
    with get_conn() as conn:
        row = conn.execute(
            "SELECT user_id FROM sessions WHERE token = ?", (token,)
        ).fetchone()
    if row is None:
        raise HTTPException(status.HTTP_401_UNAUTHORIZED, "无效或已过期的 token")
    return int(row["user_id"])


def ensure_user_profile(conn: sqlite3.Connection, user_id: int) -> None:
    now = time.time()
    conn.execute(
        """
        INSERT INTO user_profiles (user_id, dorm_gold, quest_state_json, updated_at)
        VALUES (?, 200, '', ?)
        ON CONFLICT(user_id) DO NOTHING
        """,
        (user_id, now),
    )


def ensure_default_spawn(conn: sqlite3.Connection, user_id: int) -> None:
    now = time.time()
    _ensure_player_positions_table(conn)
    _ensure_player_positions_view_mode_column(conn)
    conn.execute(
        """
        INSERT INTO player_positions (user_id, scene_name, pos_x, pos_y, pos_z, rot_y, view_mode, updated_at)
        VALUES (?, ?, ?, ?, ?, ?, 'third', ?)
        ON CONFLICT(user_id, scene_name) DO NOTHING
        """,
        (
            user_id,
            DEFAULT_SPAWN_SCENE,
            DEFAULT_SPAWN_X,
            DEFAULT_SPAWN_Y,
            DEFAULT_SPAWN_Z,
            DEFAULT_SPAWN_ROT_Y,
            now,
        ),
    )


@app.on_event("startup")
def on_startup():
    init_db()
    # 通过 uvicorn server:app 启动时不会进入 __main__，因此在 startup 里也打印一次 LAN 地址。
    port = int(os.environ.get("GAME_SERVER_PORT", os.environ.get("PORT", "8765")))
    ips = _local_ipv4_candidates()
    if ips:
        print(f"[GameServer] Local IPv4: {', '.join(ips)}")
        for ip in ips:
            print(f"[GameServer] LAN URL: http://{ip}:{port}")
            print(f"[GameServer] Download: http://{ip}:{port}/")
            print(f"[GameServer] Control: http://{ip}:{port}/control/")
            print(f"[GameServer] Admin: http://{ip}:{port}/admin/")
    else:
        print(f"[GameServer] Local IPv4: (not detected), fallback http://127.0.0.1:{port}")
        print(f"[GameServer] Download: http://127.0.0.1:{port}/")
        print(f"[GameServer] Control: http://127.0.0.1:{port}/control/")
        print(f"[GameServer] Admin: http://127.0.0.1:{port}/admin/")


@app.get("/health")
def health():
    return {"ok": True, "time": time.time()}


@app.get("/api/connection_check")
def connection_check(request: Request):
    """无 token。根据该 IP 距上次任意 HTTP 请求是否超过 COLD_SESSION_IDLE_SEC 返回 cold_session。"""
    ip = _peer_ip(request)
    now = time.time()
    last = _ip_last_seen.get(ip)
    cold = last is None or (now - last) > COLD_SESSION_IDLE_SEC
    return {"cold_session": cold}


def _load_dorm_mall_catalog_raw() -> dict[str, Any]:
    """寝室商城目录：由 Unity 导出 JSON（见 Tools/DormMall/导出…），图片可继续用客户端 Resources/DormMall。"""
    if not DORM_MALL_CATALOG_PATH.is_file():
        return {"products": []}
    try:
        data = json.loads(DORM_MALL_CATALOG_PATH.read_text(encoding="utf-8"))
    except (json.JSONDecodeError, OSError):
        return {"products": []}
    if not isinstance(data, dict):
        return {"products": []}
    products = data.get("products")
    if not isinstance(products, list):
        data["products"] = []
    return data


@app.get("/api/dorm_mall/catalog")
def dorm_mall_catalog():
    """供客户端拉取商品列表；字段名与 Unity JsonUtility（productName, iconPath 等）一致。"""
    return _load_dorm_mall_catalog_raw()


def _load_content_manifest_raw() -> dict[str, Any]:
    """剧情等内容热更新 manifest（Unity: Tools → Quest System → 导出剧情内容到 GameServer）。"""
    if not CONTENT_MANIFEST_PATH.is_file():
        return {
            "contentVersion": "0",
            "updatedAtUnix": 0,
            "storyDatabase": {
                "version": "0",
                "sha256": "",
                "url": "/api/content/story_database",
            },
        }
    try:
        data = json.loads(CONTENT_MANIFEST_PATH.read_text(encoding="utf-8"))
    except (json.JSONDecodeError, OSError):
        return {
            "contentVersion": "0",
            "updatedAtUnix": 0,
            "storyDatabase": {
                "version": "0",
                "sha256": "",
                "url": "/api/content/story_database",
            },
        }
    if not isinstance(data, dict):
        return {"contentVersion": "0", "updatedAtUnix": 0, "storyDatabase": {}}
    story = data.get("storyDatabase")
    if not isinstance(story, dict):
        data["storyDatabase"] = {
            "version": data.get("contentVersion", "0"),
            "sha256": "",
            "url": "/api/content/story_database",
        }
    elif not story.get("url"):
        story["url"] = "/api/content/story_database"
    return data


def _load_story_database_raw() -> dict[str, Any]:
    if not STORY_DATABASE_PATH.is_file():
        return {
            "contentVersion": "0",
            "generatedAt": "",
            "sourceFolder": "",
            "locations": [],
            "npcs": [],
            "documents": [],
        }
    try:
        data = json.loads(STORY_DATABASE_PATH.read_text(encoding="utf-8"))
    except (json.JSONDecodeError, OSError):
        return {
            "contentVersion": "0",
            "generatedAt": "",
            "sourceFolder": "",
            "locations": [],
            "npcs": [],
            "documents": [],
        }
    if not isinstance(data, dict):
        return {
            "contentVersion": "0",
            "generatedAt": "",
            "sourceFolder": "",
            "locations": [],
            "npcs": [],
            "documents": [],
        }
    for key in ("locations", "npcs", "documents"):
        if not isinstance(data.get(key), list):
            data[key] = []
    return data


@app.get("/api/content/manifest")
def content_manifest():
    """供客户端检查剧情等内容是否有新版本。"""
    return _load_content_manifest_raw()


@app.get("/api/content/story_database")
def content_story_database():
    """供客户端拉取剧情数据库 JSON（热更新）。"""
    return _load_story_database_raw()


class ContentPatchBody(BaseModel):
    missing: list[str] = Field(default_factory=list)


def _build_patch_file_entry(name: str, manifest: dict[str, Any]) -> dict[str, Any] | None:
    if name == "story_database":
        story = manifest.get("storyDatabase") if isinstance(manifest.get("storyDatabase"), dict) else {}
        if not STORY_DATABASE_PATH.is_file():
            return None
        return {
            "name": "story_database",
            "fileName": "story_database.json",
            "kind": "json",
            "sha256": story.get("sha256", ""),
            "size": STORY_DATABASE_PATH.stat().st_size,
            "url": story.get("url") or "/api/content/story_database",
        }

    bundles = manifest.get("bundles")
    if not isinstance(bundles, list):
        return None

    for raw in bundles:
        if not isinstance(raw, dict) or raw.get("name") != name:
            continue
        kind = str(raw.get("kind") or "bundle").lower()
        file_name = str(raw.get("fileName") or name)
        if kind in ("json", "catalog"):
            file_path = CONTENT_DIR / file_name
        else:
            file_path = CONTENT_BUNDLE_DIR / file_name
        if not file_path.is_file():
            return None
        return {
            "name": name,
            "fileName": file_name,
            "kind": kind,
            "sha256": raw.get("sha256", ""),
            "size": file_path.stat().st_size,
            "url": raw.get("url") or (
                f"/api/content/catalog/dorm_mall"
                if name == "dorm_mall_catalog"
                else f"/api/content/bundle/{name}"
            ),
        }
    return None


@app.post("/api/content/patch")
def content_patch(body: ContentPatchBody):
    """客户端上报缺失资源名，服务端返回补全下载列表。"""
    manifest = _load_content_manifest_raw()
    files: list[dict[str, Any]] = []
    for raw_name in body.missing or []:
        name = str(raw_name or "").strip()
        if not name:
            continue
        entry = _build_patch_file_entry(name, manifest)
        if entry is not None:
            files.append(entry)
    return {"files": files}


@app.get("/api/content/bundle/{bundle_name}")
def content_bundle(bundle_name: str):
    safe = bundle_name.replace("..", "").replace("\\", "").replace("/", "").strip()
    path = CONTENT_BUNDLE_DIR / safe
    if not path.is_file():
        raise HTTPException(status.HTTP_404_NOT_FOUND, "bundle 不存在")
    return FileResponse(str(path), media_type="application/octet-stream", filename=safe)


@app.get("/api/content/catalog/dorm_mall")
def content_dorm_mall_catalog_hotupdate():
    path = CONTENT_DIR / "dorm_mall_catalog.json"
    if path.is_file():
        try:
            return json.loads(path.read_text(encoding="utf-8"))
        except (json.JSONDecodeError, OSError):
            pass
    return _load_dorm_mall_catalog_raw()


@app.post("/api/register", response_model=TokenResponse)
def register(body: RegisterBody):
    uname = body.username.strip()
    ok, err = provision_lguhub_user(uname, body.password)
    if not ok:
        raise HTTPException(
            status.HTTP_503_SERVICE_UNAVAILABLE,
            f"LGUHUB 主站不可用，无法注册游戏账号：{err}",
        )
    now = time.time()
    password_hash = hash_password(body.password)
    token = secrets.token_urlsafe(32)
    with get_conn() as conn:
        try:
            cur = conn.execute(
                "INSERT INTO users (username, password_hash, created_at) VALUES (?,?,?)",
                (uname, password_hash, now),
            )
            user_id = cur.lastrowid
        except sqlite3.IntegrityError:
            raise HTTPException(
                status.HTTP_409_CONFLICT, "用户名已存在"
            ) from None
        conn.execute(
            "INSERT INTO sessions (token, user_id, created_at) VALUES (?,?,?)",
            (token, user_id, now),
        )
        ensure_user_profile(conn, int(user_id))
        ensure_default_spawn(conn, int(user_id))
        conn.execute(
            "UPDATE user_profiles SET dorm_gold = 200, quest_state_json = '', updated_at = ? WHERE user_id = ?",
            (now, int(user_id)),
        )
        conn.commit()
    return TokenResponse(token=token, username=uname)


def _ensure_local_game_user(
    conn: sqlite3.Connection, username: str, password: str
) -> int:
    """LGUHUB 校验通过后，在本地游戏库创建/更新用户并同步密码哈希。"""
    now = time.time()
    pw_hash = hash_password(password)
    row = conn.execute(
        "SELECT id FROM users WHERE username = ?", (username.strip(),)
    ).fetchone()
    if row:
        user_id = int(row["id"])
        conn.execute(
            "UPDATE users SET password_hash = ? WHERE id = ?",
            (pw_hash, user_id),
        )
    else:
        cur = conn.execute(
            "INSERT INTO users (username, password_hash, created_at) VALUES (?,?,?)",
            (username.strip(), pw_hash, now),
        )
        user_id = int(cur.lastrowid)
        ensure_user_profile(conn, user_id)
        ensure_default_spawn(conn, user_id)
    return user_id


@app.post("/api/login", response_model=TokenResponse)
def login(body: LoginBody):
    now = time.time()
    uname = body.username.strip()
    if lguhub_auth_configured():
        ok, _info, err = verify_lguhub_login(uname, body.password)
        if not ok:
            if err and ("不可用" in err or "无法连接" in err):
                raise HTTPException(
                    status.HTTP_503_SERVICE_UNAVAILABLE,
                    "LGUHUB 主站不可用，无法登录游戏",
                )
            raise HTTPException(
                status.HTTP_401_UNAUTHORIZED, err or "用户名或密码错误"
            )
        token = secrets.token_urlsafe(32)
        with get_conn() as conn:
            user_id = _ensure_local_game_user(conn, uname, body.password)
            conn.execute("DELETE FROM sessions WHERE user_id = ?", (user_id,))
            conn.execute(
                "INSERT INTO sessions (token, user_id, created_at) VALUES (?,?,?)",
                (token, user_id, now),
            )
            ensure_user_profile(conn, user_id)
            ensure_default_spawn(conn, user_id)
            conn.commit()
        return TokenResponse(token=token, username=uname)

    with get_conn() as conn:
        row = conn.execute(
            "SELECT id, password_hash FROM users WHERE username = ?",
            (uname,),
        ).fetchone()
    if row is None or not verify_password(body.password, row["password_hash"]):
        raise HTTPException(status.HTTP_401_UNAUTHORIZED, "用户名或密码错误")
    user_id = int(row["id"])
    token = secrets.token_urlsafe(32)
    with get_conn() as conn:
        conn.execute("DELETE FROM sessions WHERE user_id = ?", (user_id,))
        conn.execute(
            "INSERT INTO sessions (token, user_id, created_at) VALUES (?,?,?)",
            (token, user_id, now),
        )
        ensure_user_profile(conn, user_id)
        ensure_default_spawn(conn, user_id)
        conn.commit()
    return TokenResponse(token=token, username=uname)


class LguHubSessionResponse(BaseModel):
    ok: bool = True
    username: str
    lguhub_base_url: str
    unity_token: str
    expires_in: int = 0


class SsoBaseBody(BaseModel):
    secret: str = ""


class SsoLoginVerifyBody(SsoBaseBody):
    username: str = Field(min_length=1, max_length=64)
    password: str = Field(min_length=1)


class SsoRegisterBody(SsoBaseBody):
    username: str = Field(min_length=1, max_length=64)
    password: str = Field(min_length=6)
    nickname: str = ""
    email: str = ""


class SsoTicketBody(SsoBaseBody):
    ticket: str = Field(min_length=8)


class SsoChangePasswordBody(SsoBaseBody):
    username: str = Field(min_length=1, max_length=64)
    new_password: str = Field(min_length=6)
    old_password: str = ""
    force: bool = False


class SsoResetPasswordBody(SsoBaseBody):
    username: str = Field(min_length=1, max_length=64)
    temporary_password: str = ""


class SsoPushProfileBody(SsoBaseBody):
    username: str = Field(min_length=1, max_length=64)
    nickname: str = ""
    email: str = ""


class SsoLoginVerifyResponse(BaseModel):
    ok: bool = True
    user_id: int
    username: str
    nickname: str = ""
    must_change_password: bool = False
    email: str = ""
    sso_ticket: str = ""


@app.post("/api/sso/verify-login", response_model=SsoLoginVerifyResponse)
def sso_verify_login(body: SsoLoginVerifyBody, request: Request):
    """已迁移：密码权威在 LGUHUB，转发至主站 /api/sso/verify-login。"""
    verify_sso_auth(request, body.secret)
    if lguhub_auth_configured():
        ok, data, err = verify_lguhub_login(body.username.strip(), body.password)
        if not ok:
            code = status.HTTP_503_SERVICE_UNAVAILABLE if err and "不可用" in err else status.HTTP_401_UNAUTHORIZED
            raise HTTPException(code, err or "认证失败")
        return SsoLoginVerifyResponse(
            user_id=int(data.get("user_id") or 0),
            username=str(data.get("username") or body.username.strip()),
            nickname=str(data.get("nickname") or ""),
            must_change_password=bool(data.get("must_change_password")),
            email=str(data.get("email") or ""),
            sso_ticket=str(data.get("sso_ticket") or ""),
        )
    with get_conn() as conn:
        info = sso_verify_credentials(conn, body.username.strip(), body.password)
        ticket = issue_sso_ticket(conn, info["user_id"], info["username"])
        conn.commit()
    return SsoLoginVerifyResponse(
        user_id=info["user_id"],
        username=info["username"],
        nickname=info.get("nickname") or "",
        must_change_password=info.get("must_change_password", False),
        email=info.get("email") or "",
        sso_ticket=ticket,
    )


@app.post("/api/sso/register-or-sync")
def sso_register_or_sync(body: SsoRegisterBody, request: Request):
    """LGUHUB 注册入口：在 GameServer 创建账号（已存在则 409）。"""
    verify_sso_auth(request, body.secret)
    with get_conn() as conn:
        try:
            info = sso_register_user(
                conn,
                body.username.strip(),
                body.password,
                body.nickname,
                body.email,
            )
            ensure_user_profile(conn, info["user_id"])
            ensure_default_spawn(conn, info["user_id"])
            conn.commit()
        except HTTPException:
            raise
        except sqlite3.IntegrityError:
            raise HTTPException(status.HTTP_409_CONFLICT, "用户名已存在") from None
    try:
        provision_lguhub_user(info["username"], body.password)
    except Exception as ex:
        print(f"[GameServer] LGUHUB provision after web-register: {ex}")
    return {"ok": True, **info}


@app.post("/api/sso/consume-ticket")
def sso_consume_ticket_route(body: SsoTicketBody, request: Request):
    verify_sso_auth(request, body.secret)
    with get_conn() as conn:
        info = consume_sso_ticket(conn, body.ticket)
        conn.commit()
    return {"ok": True, **info}


@app.post("/api/sso/change-password")
def sso_change_password_route(body: SsoChangePasswordBody, request: Request):
    verify_sso_auth(request, body.secret)
    with get_conn() as conn:
        sso_change_password(
            conn,
            body.username.strip(),
            body.new_password,
            body.old_password or None,
            force=body.force,
        )
        conn.commit()
    return {"ok": True}


@app.post("/api/sso/reset-password")
def sso_reset_password_route(body: SsoResetPasswordBody, request: Request):
    """忘记密码：由 LGUHUB 发信前在 GameServer 设置临时密码。"""
    verify_sso_auth(request, body.secret)
    temp = body.temporary_password.strip() or None
    with get_conn() as conn:
        info = sso_reset_password(conn, body.username.strip(), temp)
        conn.commit()
    return {"ok": True, **info}


@app.post("/api/sso/push-profile")
def sso_push_profile(body: SsoPushProfileBody, request: Request):
    verify_sso_auth(request, body.secret)
    uname = body.username.strip()
    with get_conn() as conn:
        row = conn.execute("SELECT id FROM users WHERE username = ?", (uname,)).fetchone()
        if row is None:
            raise HTTPException(status.HTTP_404_NOT_FOUND, "用户不存在")
        updates: list[str] = []
        params: list[Any] = []
        if body.nickname:
            updates.append("nickname = ?")
            params.append(body.nickname[:24])
        if body.email:
            updates.append("email = ?")
            params.append(body.email[:120])
        if updates:
            params.append(int(row["id"]))
            conn.execute(
                f"UPDATE users SET {', '.join(updates)} WHERE id = ?",
                params,
            )
            conn.commit()
    return {"ok": True, "username": uname}


@app.get("/api/sso/health")
def sso_health():
    from sso_support import sso_secret_configured

    return {"ok": True, "sso_configured": sso_secret_configured()}


@app.get("/api/lguhub/session", response_model=LguHubSessionResponse)
def lguhub_session(token: str = Query(..., min_length=1)):
    """验证游戏 token 后换取 LGUHUB Unity API Bearer token。"""
    uid = get_user_id_from_token(token)
    with get_conn() as conn:
        row = conn.execute("SELECT username FROM users WHERE id = ?", (uid,)).fetchone()
    if row is None:
        raise HTTPException(status.HTTP_404_NOT_FOUND, "用户不存在")
    username = str(row["username"])
    ok, data, err = issue_lguhub_unity_token(username)
    if not ok:
        raise HTTPException(status.HTTP_502_BAD_GATEWAY, err or "无法连接 LGUHUB")
    base = (data.get("base_url") or lguhub_base_url()).strip().rstrip("/")
    return LguHubSessionResponse(
        username=username,
        lguhub_base_url=base,
        unity_token=str(data["token"]),
        expires_in=int(data.get("expires_in") or 0),
    )


@app.get("/api/me", response_model=MeResponse)
def me(token: str):
    uid = get_user_id_from_token(token)
    with get_conn() as conn:
        row = conn.execute(
            "SELECT username, nickname, character_id FROM users WHERE id = ?", (uid,)
        ).fetchone()
    if row is None:
        raise HTTPException(status.HTTP_404_NOT_FOUND, "用户不存在")
    return MeResponse(
        user_id=uid,
        username=str(row["username"]),
        nickname=str(row["nickname"] or "").strip(),
        character_id=str(row["character_id"] or "").strip(),
    )


@app.post("/api/me/nickname")
def set_my_nickname(body: NicknameBody):
    uid = get_user_id_from_token(body.token)
    nickname = body.nickname.strip()
    if not nickname:
        raise HTTPException(status.HTTP_400_BAD_REQUEST, "昵称不能为空")
    with get_conn() as conn:
        existed = conn.execute(
            "SELECT id FROM users WHERE nickname = ? COLLATE NOCASE AND id <> ?",
            (nickname, uid),
        ).fetchone()
        if existed is not None:
            raise HTTPException(status.HTTP_409_CONFLICT, "昵称已被占用")
        conn.execute("UPDATE users SET nickname = ? WHERE id = ?", (nickname, uid))
        conn.commit()
    return {"ok": True, "nickname": nickname}


@app.post("/api/me/profile_setup")
def set_my_profile(body: ProfileSetupBody):
    uid = get_user_id_from_token(body.token)
    nickname = body.nickname.strip()
    character_id = body.character_id.strip()
    if not nickname:
        raise HTTPException(status.HTTP_400_BAD_REQUEST, "昵称不能为空")
    if not character_id:
        raise HTTPException(status.HTTP_400_BAD_REQUEST, "请选择角色")
    with get_conn() as conn:
        existed = conn.execute(
            "SELECT id FROM users WHERE nickname = ? COLLATE NOCASE AND id <> ?",
            (nickname, uid),
        ).fetchone()
        if existed is not None:
            raise HTTPException(status.HTTP_409_CONFLICT, "昵称已被占用")
        conn.execute(
            "UPDATE users SET nickname = ?, character_id = ? WHERE id = ?",
            (nickname, character_id, uid),
        )
        conn.commit()
    return {"ok": True, "nickname": nickname, "character_id": character_id}


@app.get("/api/player_state", response_model=PlayerStateResponse)
def get_player_state(token: str):
    user_id = get_user_id_from_token(token)
    with get_conn() as conn:
        ensure_user_profile(conn, user_id)
        conn.commit()
        row = conn.execute(
            "SELECT dorm_gold, quest_state_json FROM user_profiles WHERE user_id = ?",
            (user_id,),
        ).fetchone()
    if row is None:
        return PlayerStateResponse(ok=True, dorm_gold=200, quest_state_json="", has_state=False)
    return PlayerStateResponse(
        ok=True,
        dorm_gold=max(0, int(row["dorm_gold"])),
        quest_state_json=(row["quest_state_json"] or ""),
        has_state=True,
    )


@app.post("/api/player_state", response_model=PlayerStateResponse)
def save_player_state(body: PlayerStateBody):
    user_id = get_user_id_from_token(body.token)
    now = time.time()
    with get_conn() as conn:
        ensure_user_profile(conn, user_id)
        conn.execute(
            """
            INSERT INTO user_profiles (user_id, dorm_gold, quest_state_json, updated_at)
            VALUES (?, ?, ?, ?)
            ON CONFLICT(user_id) DO UPDATE SET
                dorm_gold = excluded.dorm_gold,
                quest_state_json = excluded.quest_state_json,
                updated_at = excluded.updated_at
            """,
            (user_id, max(0, int(body.dorm_gold)), body.quest_state_json or "", now),
        )
        conn.commit()
    return PlayerStateResponse(
        ok=True,
        dorm_gold=max(0, int(body.dorm_gold)),
        quest_state_json=body.quest_state_json or "",
        has_state=True,
    )


class PositionAuth(BaseModel):
    token: str
    scene_name: str = Field(..., min_length=1, max_length=256)
    x: float
    y: float
    z: float
    rot_y: float = 0.0
    view_mode: str = Field(default="third", max_length=16)


def _normalize_view_mode(raw: str) -> str:
    v = (raw or "third").strip().lower()
    return "first" if v == "first" else "third"


@app.post("/api/position")
def save_position(body: PositionAuth):
    """客户端约每 1 秒上报一次当前场景、坐标与视角（first / third）。"""
    user_id = get_user_id_from_token(body.token)
    now = time.time()
    vm = _normalize_view_mode(body.view_mode)
    with get_conn() as conn:
        _ensure_player_positions_table(conn)
        _ensure_player_positions_view_mode_column(conn)
        conn.execute(
            """
            INSERT INTO player_positions (user_id, scene_name, pos_x, pos_y, pos_z, rot_y, view_mode, updated_at)
            VALUES (?,?,?,?,?,?,?,?)
            ON CONFLICT(user_id, scene_name) DO UPDATE SET
                pos_x = excluded.pos_x,
                pos_y = excluded.pos_y,
                pos_z = excluded.pos_z,
                rot_y = excluded.rot_y,
                view_mode = excluded.view_mode,
                updated_at = excluded.updated_at
            """,
            (
                user_id,
                body.scene_name.strip(),
                body.x,
                body.y,
                body.z,
                body.rot_y,
                vm,
                now,
            ),
        )
        conn.commit()
    return {"ok": True, "updated_at": now}


@app.get("/api/spawn", response_model=SpawnResponse)
def get_spawn(
    token: str,
    scene_name: Optional[str] = Query(
        default=None,
        description="若指定则只返回该场景下的存档；不传则返回最近一次更新的任意场景记录",
    ),
):
    """不传 scene_name：最近一条记录（用于开局接续上次玩的场景）。传 scene_name：仅该场景的存档（用于传送进某场景后恢复该图坐标）。"""
    user_id = get_user_id_from_token(token)
    with get_conn() as conn:
        _ensure_player_positions_table(conn)
        _ensure_player_positions_view_mode_column(conn)
        ensure_default_spawn(conn, user_id)
        conn.commit()
        if scene_name and scene_name.strip():
            sn = scene_name.strip()
            row = conn.execute(
                """
                SELECT scene_name, pos_x, pos_y, pos_z, rot_y, view_mode
                FROM player_positions WHERE user_id = ? AND scene_name = ?
                """,
                (user_id, sn),
            ).fetchone()
        else:
            row = conn.execute(
                """
                SELECT scene_name, pos_x, pos_y, pos_z, rot_y, view_mode
                FROM player_positions WHERE user_id = ?
                ORDER BY updated_at DESC
                LIMIT 1
                """,
                (user_id,),
            ).fetchone()
    if row is None:
        return SpawnResponse(
            scene_name="", x=0, y=0, z=0, rot_y=0, has_saved=False, view_mode="third"
        )
    vm = row["view_mode"] if row["view_mode"] is not None else "third"
    return SpawnResponse(
        scene_name=row["scene_name"],
        x=float(row["pos_x"]),
        y=float(row["pos_y"]),
        z=float(row["pos_z"]),
        rot_y=float(row["rot_y"]),
        has_saved=True,
        view_mode=_normalize_view_mode(str(vm)),
    )


# --- 联机：好友 / 聊天 / 邀请（局域网信令）---

CHAT_MAX_LEN = 2000
COOP_INVITE_TTL_SEC = 3600
_RATE: dict[str, float] = {}


def _rate_ok(key: str, min_interval: float) -> bool:
    now = time.time()
    last = _RATE.get(key, 0.0)
    if now - last < min_interval:
        return False
    _RATE[key] = now
    return True


def _username_for_id(conn: sqlite3.Connection, user_id: int) -> str:
    row = conn.execute("SELECT username FROM users WHERE id = ?", (user_id,)).fetchone()
    return str(row["username"]) if row else ""


def _are_friends(conn: sqlite3.Connection, a: int, b: int) -> bool:
    if a == b:
        return False
    lo, hi = _pair_key(a, b)
    row = conn.execute(
        "SELECT 1 FROM friendships WHERE user_low = ? AND user_high = ?",
        (lo, hi),
    ).fetchone()
    return row is not None


class FriendRequestBody(BaseModel):
    target_username: str = Field(..., min_length=1, max_length=64)


class FriendRespondBody(BaseModel):
    request_id: int = Field(..., ge=1)
    accept: bool


class ChatSendBody(BaseModel):
    token: str
    to_user_id: int = Field(..., ge=1)
    text: str = Field(..., min_length=1, max_length=CHAT_MAX_LEN)


class ProximityChatSendBody(BaseModel):
    token: str
    scene_name: str = Field(..., min_length=1, max_length=128)
    to_user_id: int = Field(..., ge=1)
    text: str = Field(..., min_length=1, max_length=CHAT_MAX_LEN)


class CoopInviteCreateBody(BaseModel):
    token: str
    to_user_id: int = Field(..., ge=1)


class CoopInviteAcceptBody(BaseModel):
    token: str
    invite_id: int = Field(..., ge=1)
    host_connect_json: str = Field(..., min_length=1, max_length=2048)


class CoopInviteDeclineBody(BaseModel):
    token: str
    invite_id: int = Field(..., ge=1)


class CoopInviteAckBody(BaseModel):
    token: str
    invite_id: int = Field(..., ge=1)


@app.post("/api/friends/request")
def friends_request(token: str, body: FriendRequestBody):
    uid = get_user_id_from_token(token)
    if not _rate_ok(f"frq:{uid}", 0.5):
        raise HTTPException(status.HTTP_429_TOO_MANY_REQUESTS, "请求过快")
    name = body.target_username.strip()
    with get_conn() as conn:
        # 纯数字时优先按「玩家数字 ID」（users.id）查找，找不到再按用户名查找，
        # 方便游戏里只显示数字 ID 的玩家互相加好友。
        row = None
        if name.isdigit():
            uid_key = int(name)
            row = conn.execute("SELECT id FROM users WHERE id = ?", (uid_key,)).fetchone()
        if row is None:
            row = conn.execute("SELECT id FROM users WHERE username = ?", (name,)).fetchone()
        if row is None:
            raise HTTPException(status.HTTP_404_NOT_FOUND, "用户不存在")
        tid = int(row["id"])
        if tid == uid:
            raise HTTPException(status.HTTP_400_BAD_REQUEST, "不能添加自己")
        if _are_friends(conn, uid, tid):
            raise HTTPException(status.HTTP_409_CONFLICT, "已是好友")
        ex = conn.execute(
            """
            SELECT id, status FROM friend_requests
            WHERE from_user_id = ? AND to_user_id = ? AND status = 'pending'
            """,
            (uid, tid),
        ).fetchone()
        if ex is not None:
            raise HTTPException(status.HTTP_409_CONFLICT, "已发送过申请")
        ex2 = conn.execute(
            """
            SELECT id FROM friend_requests
            WHERE from_user_id = ? AND to_user_id = ? AND status = 'pending'
            """,
            (tid, uid),
        ).fetchone()
        if ex2 is not None:
            raise HTTPException(
                status.HTTP_409_CONFLICT, "对方已向你发送申请，请先到待处理里回应"
            )
        now = time.time()
        try:
            cur = conn.execute(
                """
                INSERT INTO friend_requests (from_user_id, to_user_id, status, created_at)
                VALUES (?,?, 'pending', ?)
                """,
                (uid, tid, now),
            )
            conn.commit()
            rid = cur.lastrowid
        except sqlite3.IntegrityError:
            conn.rollback()
            raise HTTPException(
                status.HTTP_409_CONFLICT, "已存在待处理的申请"
            ) from None
    return {"ok": True, "request_id": int(rid)}


@app.get("/api/friends/pending")
def friends_pending(token: str):
    uid = get_user_id_from_token(token)
    with get_conn() as conn:
        inbound = conn.execute(
            """
            SELECT fr.id, fr.from_user_id, u.username AS from_username, fr.created_at
            FROM friend_requests fr
            JOIN users u ON u.id = fr.from_user_id
            WHERE fr.to_user_id = ? AND fr.status = 'pending'
            ORDER BY fr.id DESC
            """,
            (uid,),
        ).fetchall()
        outbound = conn.execute(
            """
            SELECT fr.id, fr.to_user_id, u.username AS to_username, fr.created_at
            FROM friend_requests fr
            JOIN users u ON u.id = fr.to_user_id
            WHERE fr.from_user_id = ? AND fr.status = 'pending'
            ORDER BY fr.id DESC
            """,
            (uid,),
        ).fetchall()
    return {
        "inbound": [
            {
                "id": int(r["id"]),
                "from_user_id": int(r["from_user_id"]),
                "from_username": r["from_username"],
                "created_at": float(r["created_at"]),
            }
            for r in inbound
        ],
        "outbound": [
            {
                "id": int(r["id"]),
                "to_user_id": int(r["to_user_id"]),
                "to_username": r["to_username"],
                "created_at": float(r["created_at"]),
            }
            for r in outbound
        ],
    }


@app.post("/api/friends/respond")
def friends_respond(token: str, body: FriendRespondBody):
    uid = get_user_id_from_token(token)
    with get_conn() as conn:
        row = conn.execute(
            """
            SELECT id, from_user_id, to_user_id, status
            FROM friend_requests WHERE id = ?
            """,
            (body.request_id,),
        ).fetchone()
        if row is None:
            raise HTTPException(status.HTTP_404_NOT_FOUND, "申请不存在")
        if int(row["to_user_id"]) != uid:
            raise HTTPException(status.HTTP_403_FORBIDDEN, "无权处理此申请")
        if row["status"] != "pending":
            raise HTTPException(status.HTTP_409_CONFLICT, "申请已处理")
        now = time.time()
        if body.accept:
            a, b = int(row["from_user_id"]), int(row["to_user_id"])
            lo, hi = _pair_key(a, b)
            conn.execute(
                """
                INSERT INTO friendships (user_low, user_high, created_at)
                VALUES (?,?,?)
                ON CONFLICT(user_low, user_high) DO NOTHING
                """,
                (lo, hi, now),
            )
            conn.execute(
                "UPDATE friend_requests SET status = 'accepted' WHERE id = ?",
                (body.request_id,),
            )
        else:
            conn.execute(
                "UPDATE friend_requests SET status = 'rejected' WHERE id = ?",
                (body.request_id,),
            )
        conn.commit()
    return {"ok": True}


@app.get("/api/friends/list")
def friends_list(token: str):
    uid = get_user_id_from_token(token)
    with get_conn() as conn:
        rows = conn.execute(
            """
            SELECT CASE WHEN f.user_low = ? THEN f.user_high ELSE f.user_low END AS fid
            FROM friendships f
            WHERE f.user_low = ? OR f.user_high = ?
            """,
            (uid, uid, uid),
        ).fetchall()
        out = []
        for r in rows:
            fid = int(r["fid"])
            un = _username_for_id(conn, fid)
            out.append({"user_id": fid, "username": un})
    return {"friends": out}


@app.post("/api/chat/send")
def chat_send(body: ChatSendBody):
    uid = get_user_id_from_token(body.token)
    if not _rate_ok(f"chat:{uid}", 0.15):
        raise HTTPException(status.HTTP_429_TOO_MANY_REQUESTS, "发送过快")
    tid = int(body.to_user_id)
    if tid == uid:
        raise HTTPException(status.HTTP_400_BAD_REQUEST, "不能给自己发消息")
    text = body.text.strip()
    if not text:
        raise HTTPException(status.HTTP_400_BAD_REQUEST, "内容为空")
    with get_conn() as conn:
        if not _are_friends(conn, uid, tid):
            raise HTTPException(status.HTTP_403_FORBIDDEN, "仅好友可聊天")
        now = time.time()
        cur = conn.execute(
            """
            INSERT INTO chat_messages (from_user_id, to_user_id, body, created_at)
            VALUES (?,?,?,?)
            """,
            (uid, tid, text, now),
        )
        conn.commit()
        mid = cur.lastrowid
    return {"ok": True, "id": int(mid), "created_at": time.time()}


@app.get("/api/chat/inbox")
def chat_inbox(token: str, since_id: int = 0, limit: int = 100):
    uid = get_user_id_from_token(token)
    limit = max(1, min(limit, 500))
    with get_conn() as conn:
        rows = conn.execute(
            """
            SELECT cm.id, cm.from_user_id, cm.to_user_id, cm.body, cm.created_at,
                   uf.username AS from_username, ut.username AS to_username
            FROM chat_messages cm
            JOIN users uf ON uf.id = cm.from_user_id
            JOIN users ut ON ut.id = cm.to_user_id
            WHERE cm.id > ? AND (cm.from_user_id = ? OR cm.to_user_id = ?)
            ORDER BY cm.id ASC
            LIMIT ?
            """,
            (since_id, uid, uid, limit),
        ).fetchall()
    return {
        "messages": [
            {
                "id": int(r["id"]),
                "from_user_id": int(r["from_user_id"]),
                "to_user_id": int(r["to_user_id"]),
                "from_username": r["from_username"],
                "to_username": r["to_username"],
                "body": r["body"],
                "created_at": float(r["created_at"]),
            }
            for r in rows
        ]
    }


@app.post("/api/proximity-chat/send")
def proximity_chat_send(body: ProximityChatSendBody):
    uid = get_user_id_from_token(body.token)
    tid = int(body.to_user_id)
    if tid == uid:
        raise HTTPException(status.HTTP_400_BAD_REQUEST, "不能给自己发消息")
    scene_name = _normalize_scene_name(body.scene_name)
    if not scene_name:
        raise HTTPException(status.HTTP_400_BAD_REQUEST, "scene_name 不能为空")
    text = body.text.strip()
    if not text:
        raise HTTPException(status.HTTP_400_BAD_REQUEST, "内容为空")
    now = time.time()
    with get_conn() as conn:
        _ensure_scene_shard_tables(conn)
        _cleanup_expired_scene_presence(conn, now)
        me = conn.execute(
            """
            SELECT shard_id FROM scene_shard_presence
            WHERE scene_name = ? AND user_id = ? AND expires_at >= ?
            """,
            (scene_name, uid, now),
        ).fetchone()
        peer = conn.execute(
            """
            SELECT shard_id FROM scene_shard_presence
            WHERE scene_name = ? AND user_id = ? AND expires_at >= ?
            """,
            (scene_name, tid, now),
        ).fetchone()
        if me is None or peer is None:
            raise HTTPException(status.HTTP_403_FORBIDDEN, "仅同场景在线玩家可近聊")
        if str(me["shard_id"]) != str(peer["shard_id"]):
            raise HTTPException(status.HTTP_403_FORBIDDEN, "仅同服务器在线玩家可近聊")

        cur = conn.execute(
            """
            INSERT INTO proximity_chat_messages (scene_name, from_user_id, to_user_id, body, created_at)
            VALUES (?, ?, ?, ?, ?)
            """,
            (scene_name, uid, tid, text, now),
        )
        conn.commit()
        mid = int(cur.lastrowid)
    return {"ok": True, "id": mid, "created_at": now}


@app.get("/api/proximity-chat/inbox")
def proximity_chat_inbox(token: str, scene_name: str, since_id: int = 0, limit: int = 100):
    uid = get_user_id_from_token(token)
    sn = _normalize_scene_name(scene_name)
    if not sn:
        raise HTTPException(status.HTTP_400_BAD_REQUEST, "scene_name 不能为空")
    limit = max(1, min(limit, 300))
    with get_conn() as conn:
        rows = conn.execute(
            """
            SELECT pcm.id, pcm.scene_name, pcm.from_user_id, pcm.to_user_id, pcm.body, pcm.created_at,
                   uf.username AS from_username, ut.username AS to_username
            FROM proximity_chat_messages pcm
            JOIN users uf ON uf.id = pcm.from_user_id
            JOIN users ut ON ut.id = pcm.to_user_id
            WHERE pcm.scene_name = ?
              AND pcm.id > ?
              AND (pcm.from_user_id = ? OR pcm.to_user_id = ?)
            ORDER BY pcm.id ASC
            LIMIT ?
            """,
            (sn, since_id, uid, uid, limit),
        ).fetchall()
    return {
        "messages": [
            {
                "id": int(r["id"]),
                "scene_name": str(r["scene_name"]),
                "from_user_id": int(r["from_user_id"]),
                "to_user_id": int(r["to_user_id"]),
                "from_username": r["from_username"],
                "to_username": r["to_username"],
                "body": r["body"],
                "created_at": float(r["created_at"]),
            }
            for r in rows
        ]
    }


@app.post("/api/coop/invite")
def coop_invite(body: CoopInviteCreateBody):
    uid = get_user_id_from_token(body.token)
    if not _rate_ok(f"coop:{uid}", 0.5):
        raise HTTPException(status.HTTP_429_TOO_MANY_REQUESTS, "请求过快")
    tid = int(body.to_user_id)
    if tid == uid:
        raise HTTPException(status.HTTP_400_BAD_REQUEST, "无效目标")
    now = time.time()
    exp = now + COOP_INVITE_TTL_SEC
    with get_conn() as conn:
        if not _are_friends(conn, uid, tid):
            raise HTTPException(status.HTTP_403_FORBIDDEN, "仅好友可邀请联机")
        conn.execute(
            "UPDATE coop_invites SET status = 'expired' WHERE status = 'pending' AND expires_at < ?",
            (now,),
        )
        ex = conn.execute(
            """
            SELECT id FROM coop_invites
            WHERE from_user_id = ? AND to_user_id = ? AND status = 'pending'
            """,
            (uid, tid),
        ).fetchone()
        if ex is not None:
            raise HTTPException(status.HTTP_409_CONFLICT, "已有待处理邀请")
        cur = conn.execute(
            """
            INSERT INTO coop_invites (from_user_id, to_user_id, status, host_connect_json, created_at, expires_at)
            VALUES (?,?, 'pending', NULL, ?, ?)
            """,
            (uid, tid, now, exp),
        )
        conn.commit()
        iid = cur.lastrowid
    return {"ok": True, "invite_id": int(iid), "expires_at": exp}


@app.post("/api/coop/invite/accept")
def coop_invite_accept(body: CoopInviteAcceptBody):
    """同意方 = 世界主机，写入 host_connect_json（局域网 IP:端口 等）。"""
    uid = get_user_id_from_token(body.token)
    with get_conn() as conn:
        row = conn.execute(
            """
            SELECT id, from_user_id, to_user_id, status, expires_at
            FROM coop_invites WHERE id = ?
            """,
            (body.invite_id,),
        ).fetchone()
        if row is None:
            raise HTTPException(status.HTTP_404_NOT_FOUND, "邀请不存在")
        if int(row["to_user_id"]) != uid:
            raise HTTPException(status.HTTP_403_FORBIDDEN, "只有被邀请者可同意并担任主机")
        if row["status"] != "pending":
            raise HTTPException(status.HTTP_409_CONFLICT, "邀请已失效")
        if float(row["expires_at"]) < time.time():
            conn.execute(
                "UPDATE coop_invites SET status = 'expired' WHERE id = ?",
                (body.invite_id,),
            )
            conn.commit()
            raise HTTPException(status.HTTP_410_GONE, "邀请已过期")
        try:
            json.loads(body.host_connect_json)
        except json.JSONDecodeError as e:
            raise HTTPException(
                status.HTTP_400_BAD_REQUEST, "host_connect_json 须为合法 JSON"
            ) from e
        conn.execute(
            """
            UPDATE coop_invites
            SET status = 'accepted', host_connect_json = ?
            WHERE id = ?
            """,
            (body.host_connect_json.strip(), body.invite_id),
        )
        conn.commit()
    return {"ok": True}


@app.post("/api/coop/invite/decline")
def coop_invite_decline(body: CoopInviteDeclineBody):
    uid = get_user_id_from_token(body.token)
    with get_conn() as conn:
        row = conn.execute(
            "SELECT from_user_id, to_user_id, status FROM coop_invites WHERE id = ?",
            (body.invite_id,),
        ).fetchone()
        if row is None:
            raise HTTPException(status.HTTP_404_NOT_FOUND, "邀请不存在")
        if uid not in (int(row["from_user_id"]), int(row["to_user_id"])):
            raise HTTPException(status.HTTP_403_FORBIDDEN, "无权操作")
        if row["status"] != "pending":
            raise HTTPException(status.HTTP_409_CONFLICT, "邀请已处理")
        conn.execute(
            "UPDATE coop_invites SET status = 'rejected' WHERE id = ?",
            (body.invite_id,),
        )
        conn.commit()
    return {"ok": True}


@app.get("/api/coop/invite/outstanding")
def coop_invite_outstanding(token: str):
    """邀请方轮询：是否有已同意且待读取的连接信息。"""
    uid = get_user_id_from_token(token)
    now = time.time()
    with get_conn() as conn:
        conn.execute(
            "UPDATE coop_invites SET status = 'expired' WHERE status = 'pending' AND expires_at < ?",
            (now,),
        )
        row = conn.execute(
            """
            SELECT id, from_user_id, to_user_id, status, host_connect_json, created_at, expires_at
            FROM coop_invites
            WHERE from_user_id = ? AND status = 'accepted' AND host_connect_json IS NOT NULL
            ORDER BY id DESC LIMIT 1
            """,
            (uid,),
        ).fetchone()
        conn.commit()
    if row is None:
        return {"invite": None}
    inv = {
        "id": int(row["id"]),
        "from_user_id": int(row["from_user_id"]),
        "to_user_id": int(row["to_user_id"]),
        "status": row["status"],
        "host_connect_json": row["host_connect_json"],
        "created_at": float(row["created_at"]),
        "expires_at": float(row["expires_at"]),
    }
    return {"invite": inv}


@app.post("/api/coop/invite/ack")
def coop_invite_ack(body: CoopInviteAckBody):
    """邀请方已读取连接信息并成功发起连接后调用，删除记录以免 outstanding 重复返回。"""
    uid = get_user_id_from_token(body.token)
    with get_conn() as conn:
        row = conn.execute(
            "SELECT from_user_id FROM coop_invites WHERE id = ?",
            (body.invite_id,),
        ).fetchone()
        if row is None:
            raise HTTPException(status.HTTP_404_NOT_FOUND, "邀请不存在")
        if int(row["from_user_id"]) != uid:
            raise HTTPException(status.HTTP_403_FORBIDDEN, "仅邀请方可确认")
        conn.execute("DELETE FROM coop_invites WHERE id = ?", (body.invite_id,))
        conn.commit()
    return {"ok": True}


@app.get("/api/coop/pending")
def coop_pending(token: str):
    uid = get_user_id_from_token(token)
    now = time.time()
    with get_conn() as conn:
        conn.execute(
            "UPDATE coop_invites SET status = 'expired' WHERE status = 'pending' AND expires_at < ?",
            (now,),
        )
        to_me = conn.execute(
            """
            SELECT ci.id, ci.from_user_id, u.username AS from_username, ci.created_at, ci.expires_at
            FROM coop_invites ci
            JOIN users u ON u.id = ci.from_user_id
            WHERE ci.to_user_id = ? AND ci.status = 'pending'
            ORDER BY ci.id DESC
            """,
            (uid,),
        ).fetchall()
        conn.commit()
    return {
        "invites": [
            {
                "id": int(r["id"]),
                "from_user_id": int(r["from_user_id"]),
                "from_username": r["from_username"],
                "created_at": float(r["created_at"]),
                "expires_at": float(r["expires_at"]),
            }
            for r in to_me
        ]
    }


# --- 联机主机地址云端登记（好友通过 resolve 拉取 IP:端口 连接 Mirror）---

HOST_REGISTRY_TTL_SEC = 120
SCENE_SHARD_HEARTBEAT_TTL_SEC = 15
SCENE_SHARD_DEFAULT_CAPACITY = 20
SCENE_SHARD_DEFAULT_ORDER = ("A", "B", "C")


class CoopHostPublishBody(BaseModel):
    token: str
    port: int = Field(..., ge=1, le=65535)
    advertised_address: str = Field(
        ...,
        max_length=128,
        description="主机显式上报的可连接地址（IPv4/IPv6/域名）；Plan B 不再依赖请求源 IP",
    )
    transport: str = Field(default="Telepathy", max_length=32)


class CoopHostUnpublishBody(BaseModel):
    token: str


class SceneShardEnterBody(BaseModel):
    token: str
    scene_name: str = Field(..., min_length=1, max_length=128)


class SceneShardHeartbeatBody(BaseModel):
    token: str
    scene_name: str = Field(..., min_length=1, max_length=128)
    shard_id: str = Field(..., min_length=1, max_length=8)


class SceneShardLeaveBody(BaseModel):
    token: str
    scene_name: str = Field(..., min_length=1, max_length=128)


def _normalize_scene_name(raw: str) -> str:
    return (raw or "").strip()


def _normalize_shard_id(raw: str) -> str:
    sid = (raw or "").strip().upper()
    return sid[:8]


def _normalize_advertised_address(raw: str) -> str:
    """
    Plan B: 地址由主机显式上报，不再由服务器从请求源 IP 推断。
    这里做基础清洗，避免空值或明显非法输入入库。
    """
    addr = (raw or "").strip()
    if not addr:
        return ""
    if len(addr) > 128:
        return ""
    if any(ch.isspace() for ch in addr):
        return ""
    # 端口必须走独立字段 body.port，避免 "1.2.3.4:7777" 这类混填
    if ":" in addr and addr.count(":") == 1 and "." in addr:
        host_part, maybe_port = addr.rsplit(":", 1)
        if host_part and maybe_port.isdigit():
            return ""
    if addr.startswith("http://") or addr.startswith("https://"):
        return ""
    return addr


def _ensure_scene_shard_tables(conn: sqlite3.Connection) -> None:
    conn.execute(
        """
        CREATE TABLE IF NOT EXISTS scene_shard_capacity (
            scene_name TEXT NOT NULL,
            shard_id TEXT NOT NULL,
            capacity INTEGER NOT NULL DEFAULT 20,
            order_index INTEGER NOT NULL DEFAULT 0,
            PRIMARY KEY (scene_name, shard_id)
        );
        """
    )
    conn.execute(
        """
        CREATE TABLE IF NOT EXISTS scene_shard_presence (
            scene_name TEXT NOT NULL,
            shard_id TEXT NOT NULL,
            user_id INTEGER NOT NULL,
            updated_at REAL NOT NULL,
            expires_at REAL NOT NULL,
            PRIMARY KEY (scene_name, user_id),
            FOREIGN KEY (user_id) REFERENCES users(id)
        );
        """
    )
    conn.execute(
        "CREATE INDEX IF NOT EXISTS idx_scene_shard_presence_lookup ON scene_shard_presence(scene_name, shard_id, expires_at);"
    )


def _ensure_default_scene_shards(conn: sqlite3.Connection, scene_name: str) -> None:
    for i, sid in enumerate(SCENE_SHARD_DEFAULT_ORDER):
        conn.execute(
            """
            INSERT INTO scene_shard_capacity (scene_name, shard_id, capacity, order_index)
            VALUES (?, ?, ?, ?)
            ON CONFLICT(scene_name, shard_id) DO NOTHING
            """,
            (scene_name, sid, SCENE_SHARD_DEFAULT_CAPACITY, i),
        )


def _cleanup_expired_scene_presence(conn: sqlite3.Connection, now: float) -> None:
    conn.execute("DELETE FROM scene_shard_presence WHERE expires_at < ?", (now,))


def _query_scene_shard_rows(conn: sqlite3.Connection, scene_name: str) -> list[sqlite3.Row]:
    return list(
        conn.execute(
            """
            SELECT c.shard_id, c.capacity, c.order_index, COUNT(p.user_id) AS online_count
            FROM scene_shard_capacity c
            LEFT JOIN scene_shard_presence p
              ON p.scene_name = c.scene_name
             AND p.shard_id = c.shard_id
             AND p.expires_at >= ?
            WHERE c.scene_name = ?
            GROUP BY c.scene_name, c.shard_id, c.capacity, c.order_index
            ORDER BY c.order_index ASC, c.shard_id ASC
            """,
            (time.time(), scene_name),
        ).fetchall()
    )


def _pick_shard_by_fill_rule(rows: list[sqlite3.Row]) -> tuple[str, int, int]:
    if not rows:
        return ("A", SCENE_SHARD_DEFAULT_CAPACITY, 0)
    normalized: list[dict[str, Any]] = []
    for r in rows:
        cap = max(1, int(r["capacity"]))
        cnt = max(0, int(r["online_count"]))
        normalized.append(
            {
                "shard_id": str(r["shard_id"]),
                "capacity": cap,
                "count": cnt,
                "ratio": float(cnt) / float(cap),
            }
        )

    # 先按顺序把 A/B/C 填到 70%
    for x in normalized:
        if x["count"] < x["capacity"] and x["ratio"] < 0.7:
            return (x["shard_id"], x["capacity"], x["count"])

    # 若都 >=70%，按顺序继续填满
    for x in normalized:
        if x["count"] < x["capacity"]:
            return (x["shard_id"], x["capacity"], x["count"])

    # 全满时兜底返回首服
    first = normalized[0]
    return (str(first["shard_id"]), int(first["capacity"]), int(first["count"]))


def _shard_load_label(count: int, capacity: int) -> str:
    if capacity <= 0:
        return "流畅"
    if count >= capacity:
        return "满载"
    if float(count) / float(capacity) >= 0.7:
        return "人多"
    return "流畅"


def _scene_shard_status_payload(
    conn: sqlite3.Connection, scene_name: str, shard_id: str
) -> dict[str, Any]:
    rows = _query_scene_shard_rows(conn, scene_name)
    for r in rows:
        if str(r["shard_id"]) == shard_id:
            count = max(0, int(r["online_count"]))
            cap = max(1, int(r["capacity"]))
            return {
                "scene_name": scene_name,
                "shard_id": shard_id,
                "online_count": count,
                "capacity": cap,
                "status_label": _shard_load_label(count, cap),
            }
    return {
        "scene_name": scene_name,
        "shard_id": shard_id,
        "online_count": 0,
        "capacity": SCENE_SHARD_DEFAULT_CAPACITY,
        "status_label": "流畅",
    }


@app.post("/api/coop/host/publish")
def coop_host_publish(body: CoopHostPublishBody):
    """主机显式登记可连接地址与 Mirror 端口；需周期性调用续期。"""
    uid = get_user_id_from_token(body.token)
    if not _rate_ok(f"hostpub:{uid}", 0.25):
        raise HTTPException(status.HTTP_429_TOO_MANY_REQUESTS, "发布过快")
    ip = _normalize_advertised_address(body.advertised_address)
    if not ip:
        raise HTTPException(
            status.HTTP_400_BAD_REQUEST,
            "advertised_address 必填，且不能包含协议或端口（如: 10.24.20.18 / host.example.edu）",
        )
    now = time.time()
    exp = now + HOST_REGISTRY_TTL_SEC
    tr = (body.transport or "Telepathy").strip()[:32] or "Telepathy"
    with get_conn() as conn:
        conn.execute(
            """
            INSERT INTO coop_host_registry (user_id, advertised_ip, advertised_port, transport, updated_at, expires_at)
            VALUES (?,?,?,?,?,?)
            ON CONFLICT(user_id) DO UPDATE SET
                advertised_ip = excluded.advertised_ip,
                advertised_port = excluded.advertised_port,
                transport = excluded.transport,
                updated_at = excluded.updated_at,
                expires_at = excluded.expires_at
            """,
            (uid, ip, int(body.port), tr, now, exp),
        )
        conn.commit()
    return {
        "ok": True,
        "effective_ip": ip,
        "advertised_port": int(body.port),
        "transport": tr,
        "expires_at": exp,
    }


@app.post("/api/coop/host/unpublish")
def coop_host_unpublish(body: CoopHostUnpublishBody):
    uid = get_user_id_from_token(body.token)
    with get_conn() as conn:
        conn.execute("DELETE FROM coop_host_registry WHERE user_id = ?", (uid,))
        conn.commit()
    return {"ok": True}


@app.post("/api/shard/enter")
def scene_shard_enter(body: SceneShardEnterBody):
    uid = get_user_id_from_token(body.token)
    scene_name = _normalize_scene_name(body.scene_name)
    if not scene_name:
        raise HTTPException(status.HTTP_400_BAD_REQUEST, "scene_name 不能为空")
    now = time.time()
    exp = now + SCENE_SHARD_HEARTBEAT_TTL_SEC
    with get_conn() as conn:
        _ensure_scene_shard_tables(conn)
        _ensure_default_scene_shards(conn, scene_name)
        _cleanup_expired_scene_presence(conn, now)

        current = conn.execute(
            """
            SELECT shard_id FROM scene_shard_presence
            WHERE scene_name = ? AND user_id = ? AND expires_at >= ?
            """,
            (scene_name, uid, now),
        ).fetchone()

        if current is not None:
            sid = str(current["shard_id"])
            conn.execute(
                """
                UPDATE scene_shard_presence
                SET updated_at = ?, expires_at = ?
                WHERE scene_name = ? AND user_id = ?
                """,
                (now, exp, scene_name, uid),
            )
            conn.commit()
            out = _scene_shard_status_payload(conn, scene_name, sid)
            out.update({"ok": True, "assigned": False, "expires_at": exp})
            return out

        rows = _query_scene_shard_rows(conn, scene_name)
        sid, cap, cnt = _pick_shard_by_fill_rule(rows)
        if cnt >= cap:
            raise HTTPException(status.HTTP_503_SERVICE_UNAVAILABLE, "当前场景服务器已满")
        conn.execute(
            """
            INSERT INTO scene_shard_presence (scene_name, shard_id, user_id, updated_at, expires_at)
            VALUES (?, ?, ?, ?, ?)
            ON CONFLICT(scene_name, user_id) DO UPDATE SET
                shard_id = excluded.shard_id,
                updated_at = excluded.updated_at,
                expires_at = excluded.expires_at
            """,
            (scene_name, sid, uid, now, exp),
        )
        conn.commit()
        out = _scene_shard_status_payload(conn, scene_name, sid)
        out.update({"ok": True, "assigned": True, "expires_at": exp})
        return out


@app.post("/api/shard/heartbeat")
def scene_shard_heartbeat(body: SceneShardHeartbeatBody):
    uid = get_user_id_from_token(body.token)
    scene_name = _normalize_scene_name(body.scene_name)
    sid = _normalize_shard_id(body.shard_id)
    if not scene_name or not sid:
        raise HTTPException(status.HTTP_400_BAD_REQUEST, "scene_name/shard_id 不能为空")
    now = time.time()
    exp = now + SCENE_SHARD_HEARTBEAT_TTL_SEC
    with get_conn() as conn:
        _ensure_scene_shard_tables(conn)
        _ensure_default_scene_shards(conn, scene_name)
        _cleanup_expired_scene_presence(conn, now)
        conn.execute(
            """
            INSERT INTO scene_shard_presence (scene_name, shard_id, user_id, updated_at, expires_at)
            VALUES (?, ?, ?, ?, ?)
            ON CONFLICT(scene_name, user_id) DO UPDATE SET
                shard_id = excluded.shard_id,
                updated_at = excluded.updated_at,
                expires_at = excluded.expires_at
            """,
            (scene_name, sid, uid, now, exp),
        )
        conn.commit()
        out = _scene_shard_status_payload(conn, scene_name, sid)
        out.update({"ok": True, "expires_at": exp})
        return out


@app.post("/api/shard/leave")
def scene_shard_leave(body: SceneShardLeaveBody):
    uid = get_user_id_from_token(body.token)
    scene_name = _normalize_scene_name(body.scene_name)
    if not scene_name:
        raise HTTPException(status.HTTP_400_BAD_REQUEST, "scene_name 不能为空")
    with get_conn() as conn:
        _ensure_scene_shard_tables(conn)
        conn.execute(
            "DELETE FROM scene_shard_presence WHERE scene_name = ? AND user_id = ?",
            (scene_name, uid),
        )
        conn.commit()
    return {"ok": True}


@app.get("/api/shard/status")
def scene_shard_status(token: str, scene_name: str):
    uid = get_user_id_from_token(token)
    sn = _normalize_scene_name(scene_name)
    if not sn:
        raise HTTPException(status.HTTP_400_BAD_REQUEST, "scene_name 不能为空")
    now = time.time()
    with get_conn() as conn:
        _ensure_scene_shard_tables(conn)
        _ensure_default_scene_shards(conn, sn)
        _cleanup_expired_scene_presence(conn, now)
        row = conn.execute(
            """
            SELECT shard_id FROM scene_shard_presence
            WHERE scene_name = ? AND user_id = ? AND expires_at >= ?
            """,
            (sn, uid, now),
        ).fetchone()
        if row is None:
            return {"ok": True, "has_assignment": False, "scene_name": sn}
        sid = str(row["shard_id"])
        out = _scene_shard_status_payload(conn, sn, sid)
        out.update({"ok": True, "has_assignment": True})
        return out


@app.get("/api/coop/host/resolve")
def coop_host_resolve(token: str, host_user_id: int = Query(..., ge=1)):
    """好友查询某用户当前登记的主机地址（需在好友关系中且登记未过期）。"""
    requester = get_user_id_from_token(token)
    if requester == host_user_id:
        raise HTTPException(status.HTTP_400_BAD_REQUEST, "不能解析自己")
    now = time.time()
    with get_conn() as conn:
        if not _are_friends(conn, requester, host_user_id):
            raise HTTPException(status.HTTP_403_FORBIDDEN, "仅好友可查询对方主机地址")
        row = conn.execute(
            """
            SELECT advertised_ip, advertised_port, transport, expires_at
            FROM coop_host_registry WHERE user_id = ?
            """,
            (host_user_id,),
        ).fetchone()
    if row is None or float(row["expires_at"]) < now:
        return {
            "ok": True,
            "has_host": False,
            "effective_ip": "",
            "advertised_port": 0,
            "transport": "",
            "expires_at": 0.0,
        }
    return {
        "ok": True,
        "has_host": True,
        "effective_ip": row["advertised_ip"],
        "advertised_port": int(row["advertised_port"]),
        "transport": row["transport"] or "Telepathy",
        "expires_at": float(row["expires_at"]),
    }


# --- 公开房间号（无需好友，凭房间号加入 Mirror）---

PUBLIC_ROOM_TTL_SEC = 1800
_PUBLIC_ROOM_CODE_ALPHABET = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"


def _generate_public_room_code(conn: sqlite3.Connection) -> str:
    for _ in range(80):
        c = "".join(secrets.choice(_PUBLIC_ROOM_CODE_ALPHABET) for _ in range(6))
        if conn.execute("SELECT 1 FROM public_coop_rooms WHERE room_code=?", (c,)).fetchone() is None:
            return c
    raise HTTPException(status.HTTP_500_INTERNAL_SERVER_ERROR, "无法生成房间号")


class PublicRoomCreateBody(BaseModel):
    token: str


class PublicRoomBindBody(BaseModel):
    token: str
    room_code: str = Field(..., min_length=4, max_length=8)
    port: int = Field(..., ge=1, le=65535)
    advertised_address: str = Field(
        ...,
        max_length=128,
        description="房主显式上报的可连接地址（IPv4/IPv6/域名）；Plan B 不再依赖请求源 IP",
    )
    transport: str = Field(default="Telepathy", max_length=32)


class PublicRoomCloseBody(BaseModel):
    token: str
    room_code: str = Field(..., min_length=4, max_length=8)


@app.post("/api/coop/public-room/create")
def public_room_create(body: PublicRoomCreateBody):
    """创建 6 位房间号；同一用户再次创建会覆盖其旧房间。绑定主机前 resolve 无地址。"""
    uid = get_user_id_from_token(body.token)
    if not _rate_ok(f"prcreate:{uid}", 0.4):
        raise HTTPException(status.HTTP_429_TOO_MANY_REQUESTS, "创建过快")
    now = time.time()
    exp = now + PUBLIC_ROOM_TTL_SEC
    with get_conn() as conn:
        conn.execute("DELETE FROM public_coop_rooms WHERE owner_user_id=?", (uid,))
        code = _generate_public_room_code(conn)
        conn.execute(
            """
            INSERT INTO public_coop_rooms (room_code, owner_user_id, advertised_ip, advertised_port, transport, updated_at, expires_at)
            VALUES (?,?,?,?,?,?,?)
            """,
            (code, uid, "", 0, "Telepathy", now, exp),
        )
        conn.commit()
    return {"ok": True, "room_code": code, "expires_at": exp}


@app.post("/api/coop/public-room/bind")
def public_room_bind(body: PublicRoomBindBody):
    """房主显式绑定地址/端口；可重复调用续期 TTL。"""
    uid = get_user_id_from_token(body.token)
    if not _rate_ok(f"prbind:{uid}", 0.2):
        raise HTTPException(status.HTTP_429_TOO_MANY_REQUESTS, "绑定过快")
    code = body.room_code.strip().upper()
    ip = _normalize_advertised_address(body.advertised_address)
    if not ip:
        raise HTTPException(
            status.HTTP_400_BAD_REQUEST,
            "advertised_address 必填，且不能包含协议或端口（如: 10.24.20.18 / host.example.edu）",
        )
    now = time.time()
    exp = now + PUBLIC_ROOM_TTL_SEC
    tr = (body.transport or "Telepathy").strip()[:32] or "Telepathy"
    with get_conn() as conn:
        row = conn.execute(
            "SELECT owner_user_id FROM public_coop_rooms WHERE room_code=?",
            (code,),
        ).fetchone()
        if row is None:
            raise HTTPException(status.HTTP_404_NOT_FOUND, "房间号不存在")
        if int(row["owner_user_id"]) != uid:
            raise HTTPException(status.HTTP_403_FORBIDDEN, "仅房主可绑定")
        conn.execute(
            """
            UPDATE public_coop_rooms SET
                advertised_ip=?, advertised_port=?, transport=?, updated_at=?, expires_at=?
            WHERE room_code=?
            """,
            (ip, int(body.port), tr, now, exp, code),
        )
        conn.commit()
    return {
        "ok": True,
        "effective_ip": ip,
        "advertised_port": int(body.port),
        "transport": tr,
        "expires_at": exp,
    }


@app.post("/api/coop/public-room/close")
def public_room_close(body: PublicRoomCloseBody):
    """房主关闭公开房间记录。"""
    uid = get_user_id_from_token(body.token)
    code = body.room_code.strip().upper()
    with get_conn() as conn:
        row = conn.execute(
            "SELECT owner_user_id FROM public_coop_rooms WHERE room_code=?",
            (code,),
        ).fetchone()
        if row is None:
            return {"ok": True}
        if int(row["owner_user_id"]) != uid:
            raise HTTPException(status.HTTP_403_FORBIDDEN, "仅房主可关闭")
        conn.execute("DELETE FROM public_coop_rooms WHERE room_code=?", (code,))
        conn.commit()
    return {"ok": True}


@app.get("/api/coop/public-room/resolve")
def public_room_resolve(token: str, room_code: str = Query(..., min_length=4, max_length=8)):
    """任意已登录用户凭房间号查询主机地址（无需好友）。"""
    get_user_id_from_token(token)
    if not _rate_ok(f"prresolve:{token[:24]}", 0.15):
        raise HTTPException(status.HTTP_429_TOO_MANY_REQUESTS, "查询过快")
    code = room_code.strip().upper()
    now = time.time()
    with get_conn() as conn:
        row = conn.execute(
            """
            SELECT advertised_ip, advertised_port, transport, expires_at
            FROM public_coop_rooms WHERE room_code=?
            """,
            (code,),
        ).fetchone()
    if row is None or float(row["expires_at"]) < now:
        return {
            "ok": True,
            "has_room": False,
            "has_host": False,
            "effective_ip": "",
            "advertised_port": 0,
            "transport": "",
            "expires_at": 0.0,
        }
    ip = row["advertised_ip"] or ""
    port = int(row["advertised_port"])
    if not ip or port <= 0:
        return {
            "ok": True,
            "has_room": True,
            "has_host": False,
            "effective_ip": "",
            "advertised_port": 0,
            "transport": "",
            "expires_at": float(row["expires_at"]),
        }
    return {
        "ok": True,
        "has_room": True,
        "has_host": True,
        "effective_ip": ip,
        "advertised_port": port,
        "transport": row["transport"] or "Telepathy",
        "expires_at": float(row["expires_at"]),
    }


class FlagAuctionBidBody(BaseModel):
    token: str
    slot_id: str = Field(..., min_length=1, max_length=128)
    amount: int = Field(..., ge=1, le=1_000_000_000)


class FlagAuctionDisplayBody(BaseModel):
    token: str
    slot_id: str = Field(..., min_length=1, max_length=128)
    text: str = Field(default="", max_length=32)
    color_r: float = Field(default=1.0, ge=0.0, le=1.0)
    color_g: float = Field(default=1.0, ge=0.0, le=1.0)
    color_b: float = Field(default=1.0, ge=0.0, le=1.0)
    color_a: float = Field(default=1.0, ge=0.0, le=1.0)


def _flag_text_len_ok(s: str) -> bool:
    t = (s or "").strip()
    return len(t) <= 20


@app.get("/api/flag_auction/state")
def flag_auction_state(
    request: Request,
    slots: str = Query(
        ...,
        description="逗号分隔的 slot_id 列表（与场景中旗帜组件一致）",
        min_length=1,
        max_length=4096,
    ),
):
    """全服可见：无需登录。客户端轮询以同步旗帜文字与竞拍状态。"""
    ip = _peer_ip(request)
    if not _rate_ok(f"fa_state:{ip}", 0.12):
        raise HTTPException(status.HTTP_429_TOO_MANY_REQUESTS, "请求过快")
    now = time.time()
    parts = [p.strip() for p in (slots or "").split(",") if p.strip()]
    if not parts or len(parts) > 64:
        raise HTTPException(status.HTTP_400_BAD_REQUEST, "slots 数量无效")
    out_slots: list[dict[str, Any]] = []
    with get_conn() as conn:
        for sid in parts:
            _tick_flag_auction_slot(conn, sid, now)
            conn.commit()
            row = conn.execute(
                "SELECT * FROM flag_auction_slots WHERE slot_id = ?", (sid,)
            ).fetchone()
            if row is None:
                continue
            ph = (row["phase"] or "auction").strip().lower()
            bid_uid = row["bid_user_id"]
            bid_amt = int(row["bid_amount"] or 0)
            high_name = ""
            if bid_uid is not None and bid_amt > 0:
                high_name = _nickname_for_user_id(conn, int(bid_uid))
            cust = row["customize_user_id"]
            out_slots.append(
                {
                    "slot_id": sid,
                    "phase": ph,
                    "phase_ends_at": float(row["phase_ends_at"]),
                    "bid_amount": bid_amt,
                    "high_bidder_nickname": high_name,
                    "customize_user_id": int(cust) if cust is not None else 0,
                    "display_text": row["display_text"] or "",
                    "color_r": float(row["color_r"]),
                    "color_g": float(row["color_g"]),
                    "color_b": float(row["color_b"]),
                    "color_a": float(row["color_a"]),
                }
            )
    return {
        "ok": True,
        "server_time": now,
        "auction_sec": int(FLAG_AUCTION_SEC),
        "customize_sec": int(FLAG_CUSTOMIZE_SEC),
        "slots": out_slots,
    }


@app.post("/api/flag_auction/bid")
def flag_auction_bid(body: FlagAuctionBidBody):
    """出价：使用寝室金币（dorm_gold）；结束时从最高出价者扣除对应金额。"""
    uid = get_user_id_from_token(body.token)
    slot_id = body.slot_id.strip()
    now = time.time()
    if not _rate_ok(f"fa_bid:{uid}", 0.2):
        raise HTTPException(status.HTTP_429_TOO_MANY_REQUESTS, "操作过快")
    with get_conn() as conn:
        _tick_flag_auction_slot(conn, slot_id, now)
        row = conn.execute(
            "SELECT * FROM flag_auction_slots WHERE slot_id = ?", (slot_id,)
        ).fetchone()
        if row is None:
            raise HTTPException(status.HTTP_404_NOT_FOUND, "旗帜槽位不存在")
        ph = (row["phase"] or "auction").strip().lower()
        if ph != "auction":
            raise HTTPException(status.HTTP_400_BAD_REQUEST, "当前不在竞拍阶段")
        cur = int(row["bid_amount"] or 0)
        if body.amount <= cur:
            raise HTTPException(
                status.HTTP_400_BAD_REQUEST, f"出价须高于当前最高 {cur}"
            )
        ensure_user_profile(conn, uid)
        gold_row = conn.execute(
            "SELECT dorm_gold FROM user_profiles WHERE user_id = ?", (uid,)
        ).fetchone()
        gold = int(gold_row["dorm_gold"]) if gold_row else 0
        if gold < body.amount:
            raise HTTPException(status.HTTP_400_BAD_REQUEST, "寝室金币不足")
        conn.execute(
            """
            UPDATE flag_auction_slots SET bid_user_id = ?, bid_amount = ?
            WHERE slot_id = ?
            """,
            (uid, int(body.amount), slot_id),
        )
        conn.commit()
    return {"ok": True}


@app.post("/api/flag_auction/display")
def flag_auction_display(body: FlagAuctionDisplayBody):
    """本周期竞拍胜者：在「自定义」阶段更新旗帜文字与颜色（最多 20 字）。"""
    uid = get_user_id_from_token(body.token)
    slot_id = body.slot_id.strip()
    now = time.time()
    if not _rate_ok(f"fa_disp:{uid}", 0.2):
        raise HTTPException(status.HTTP_429_TOO_MANY_REQUESTS, "操作过快")
    raw = (body.text or "").strip()
    if not _flag_text_len_ok(raw):
        raise HTTPException(status.HTTP_400_BAD_REQUEST, "文字最多 20 个字")
    with get_conn() as conn:
        _tick_flag_auction_slot(conn, slot_id, now)
        row = conn.execute(
            "SELECT phase, customize_user_id FROM flag_auction_slots WHERE slot_id = ?",
            (slot_id,),
        ).fetchone()
        if row is None:
            raise HTTPException(status.HTTP_404_NOT_FOUND, "旗帜槽位不存在")
        ph = (row["phase"] or "").strip().lower()
        if ph != "customize":
            raise HTTPException(status.HTTP_400_BAD_REQUEST, "当前不在自定义阶段")
        cust = row["customize_user_id"]
        if cust is None or int(cust) != uid:
            raise HTTPException(status.HTTP_403_FORBIDDEN, "仅本周期胜者可在此时修改")
        conn.execute(
            """
            UPDATE flag_auction_slots SET
                display_text = ?,
                color_r = ?, color_g = ?, color_b = ?, color_a = ?
            WHERE slot_id = ?
            """,
            (
                raw,
                float(body.color_r),
                float(body.color_g),
                float(body.color_b),
                float(body.color_a),
                slot_id,
            ),
        )
        conn.commit()
    return {"ok": True}


class DormRoomResponse(BaseModel):
    ok: bool = True
    dorm_room_index: int
    max_rooms: int = MAX_DORM_ROOMS


class DormLayoutGetResponse(BaseModel):
    ok: bool = True
    has_saved: bool = False
    scene_name: str = ""
    layout_json: str = "{}"
    updated_at: float = 0.0


class DormLayoutPostBody(BaseModel):
    token: str
    scene_name: str
    layout_json: str = "{}"
    client_updated_at: float = 0.0


class DormLayoutPostResponse(BaseModel):
    ok: bool = True
    skipped: bool = False
    scene_name: str = ""
    updated_at: float = 0.0


class DormLayoutDiffOp(BaseModel):
    op: str
    id: str = ""
    itemId: int = 0
    productName: str = ""
    prefabPath: str = ""
    position: Optional[dict[str, float]] = None
    rotation: Optional[dict[str, float]] = None
    visualScale: Optional[dict[str, float]] = None


class DormLayoutDiffPostBody(BaseModel):
    token: str
    scene_name: str
    client_updated_at: float = 0.0
    base_updated_at: float = 0.0
    operations: list[DormLayoutDiffOp] = Field(default_factory=list)


@app.get("/api/dorm_room", response_model=DormRoomResponse)
def dorm_room(token: str):
    uid = get_user_id_from_token(token)
    with get_conn() as conn:
        ensure_user_profile(conn, uid)
        idx = ensure_dorm_room_index(conn, uid)
        conn.commit()
    return DormRoomResponse(ok=True, dorm_room_index=idx, max_rooms=MAX_DORM_ROOMS)


@app.get("/api/dorm_layout", response_model=DormLayoutGetResponse)
def dorm_layout_get(token: str, scene_name: str = Query(..., min_length=1)):
    uid = get_user_id_from_token(token)
    sn = scene_name.strip()
    with get_conn() as conn:
        row = conn.execute(
            "SELECT layout_json, updated_at FROM dorm_layouts WHERE user_id = ? AND scene_name = ?",
            (uid, sn),
        ).fetchone()
    if row is None:
        return DormLayoutGetResponse(
            ok=True, has_saved=False, scene_name=sn, layout_json="{}", updated_at=0.0
        )
    return DormLayoutGetResponse(
        ok=True,
        has_saved=True,
        scene_name=sn,
        layout_json=str(row["layout_json"] or "{}"),
        updated_at=float(row["updated_at"] or 0.0),
    )


@app.post("/api/dorm_layout", response_model=DormLayoutPostResponse)
def dorm_layout_post(body: DormLayoutPostBody):
    uid = get_user_id_from_token(body.token)
    sn = (body.scene_name or "").strip()
    if not sn:
        raise HTTPException(status.HTTP_400_BAD_REQUEST, "scene_name 不能为空")
    now = time.time()
    client_ts = float(body.client_updated_at or 0.0)
    with get_conn() as conn:
        row = conn.execute(
            "SELECT updated_at FROM dorm_layouts WHERE user_id = ? AND scene_name = ?",
            (uid, sn),
        ).fetchone()
        if row is not None and float(row["updated_at"] or 0.0) > client_ts + 0.001:
            return DormLayoutPostResponse(
                ok=True, skipped=True, scene_name=sn, updated_at=float(row["updated_at"])
            )
        updated_at = max(now, client_ts)
        conn.execute(
            """
            INSERT INTO dorm_layouts (user_id, scene_name, layout_json, updated_at)
            VALUES (?, ?, ?, ?)
            ON CONFLICT(user_id, scene_name) DO UPDATE SET
                layout_json = excluded.layout_json,
                updated_at = excluded.updated_at
            """,
            (uid, sn, body.layout_json or "{}", updated_at),
        )
        conn.commit()
    return DormLayoutPostResponse(ok=True, skipped=False, scene_name=sn, updated_at=updated_at)


def _apply_dorm_layout_diff(
    existing_json: str, operations: list[DormLayoutDiffOp]
) -> str:
    try:
        data = json.loads(existing_json or "{}")
    except json.JSONDecodeError:
        data = {}
    if not isinstance(data, dict):
        data = {}
    items = data.get("items")
    if not isinstance(items, list):
        items = []
    by_id: dict[str, dict[str, Any]] = {}
    for it in items:
        if isinstance(it, dict) and it.get("id"):
            by_id[str(it["id"])] = it
    for op in operations:
        op_name = (op.op or "").strip().lower()
        oid = (op.id or "").strip()
        if not oid:
            continue
        if op_name == "delete":
            by_id.pop(oid, None)
            continue
        if op_name == "upsert":
            row: dict[str, Any] = {
                "id": oid,
                "itemId": int(op.itemId),
                "productName": op.productName or "",
                "prefabPath": op.prefabPath or "",
            }
            if op.position is not None:
                row["position"] = op.position
            if op.rotation is not None:
                row["rotation"] = op.rotation
            if op.visualScale is not None:
                row["visualScale"] = op.visualScale
            by_id[oid] = row
    data["items"] = list(by_id.values())
    return json.dumps(data, ensure_ascii=False)


@app.post("/api/dorm_layout_diff", response_model=DormLayoutPostResponse)
def dorm_layout_diff_post(body: DormLayoutDiffPostBody):
    uid = get_user_id_from_token(body.token)
    sn = (body.scene_name or "").strip()
    if not sn:
        raise HTTPException(status.HTTP_400_BAD_REQUEST, "scene_name 不能为空")
    now = time.time()
    client_ts = float(body.client_updated_at or 0.0)
    with get_conn() as conn:
        row = conn.execute(
            "SELECT layout_json, updated_at FROM dorm_layouts WHERE user_id = ? AND scene_name = ?",
            (uid, sn),
        ).fetchone()
        server_ts = float(row["updated_at"] or 0.0) if row is not None else 0.0
        if server_ts > float(body.base_updated_at or 0.0) + 0.001 and server_ts > client_ts + 0.001:
            return DormLayoutPostResponse(
                ok=True, skipped=True, scene_name=sn, updated_at=server_ts
            )
        base_json = str(row["layout_json"]) if row is not None else "{}"
        merged = _apply_dorm_layout_diff(base_json, body.operations)
        updated_at = max(now, client_ts)
        conn.execute(
            """
            INSERT INTO dorm_layouts (user_id, scene_name, layout_json, updated_at)
            VALUES (?, ?, ?, ?)
            ON CONFLICT(user_id, scene_name) DO UPDATE SET
                layout_json = excluded.layout_json,
                updated_at = excluded.updated_at
            """,
            (uid, sn, merged, updated_at),
        )
        conn.commit()
    return DormLayoutPostResponse(ok=True, skipped=False, scene_name=sn, updated_at=updated_at)


register_server_hub(app, get_conn_fn=get_conn, peer_ip_fn=_peer_ip)

PORTAL_STATIC_DIR = ROOT / "static" / "portal"
PORTAL_STATIC_DIR.mkdir(parents=True, exist_ok=True)
app.mount(
    "/",
    StaticFiles(directory=str(PORTAL_STATIC_DIR), html=True),
    name="portal_static",
)


if __name__ == "__main__":
    import uvicorn

    port = int(os.environ.get("PORT", "8765"))
    ips = _local_ipv4_candidates()
    if ips:
        print(f"[GameServer] Local IPv4: {', '.join(ips)}")
        for ip in ips:
            print(f"[GameServer] LAN URL: http://{ip}:{port}")
            print(f"[GameServer] Download: http://{ip}:{port}/")
            print(f"[GameServer] Control: http://{ip}:{port}/control/")
            print(f"[GameServer] Admin: http://{ip}:{port}/admin/")
    else:
        print(f"[GameServer] Local IPv4: (not detected), fallback http://127.0.0.1:{port}")
        print(f"[GameServer] Download: http://127.0.0.1:{port}/")
        print(f"[GameServer] Control: http://127.0.0.1:{port}/control/")
        print(f"[GameServer] Admin: http://127.0.0.1:{port}/admin/")
    uvicorn.run(app, host="0.0.0.0", port=port)
