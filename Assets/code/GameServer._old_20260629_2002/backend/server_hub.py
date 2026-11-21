"""统一控制台：门户访问统计、在线人数、服务器日志、管理员鉴权。"""
from __future__ import annotations

import json
import os
import secrets
import time
from collections import deque
from datetime import datetime
from pathlib import Path
from typing import Any, Callable

from fastapi import APIRouter, Header, HTTPException, Request, status
from pydantic import BaseModel, Field

from runtime_paths import HUB_STATS_PATH, LOG_FILE_PATH, ensure_runtime_dirs, migrate_legacy_data_dir

migrate_legacy_data_dir()
ensure_runtime_dirs()

STATS_PATH = HUB_STATS_PATH

DEFAULT_CONSOLE_COMMAND = "kz-lguexplorer"
DEFAULT_ADMIN_TOKEN = "sc-lguexplorer"
CONSOLE_COMMAND = os.environ.get("GAME_CONSOLE_COMMAND", DEFAULT_CONSOLE_COMMAND).strip()
ADMIN_TOKEN = os.environ.get("GAME_ADMIN_TOKEN", DEFAULT_ADMIN_TOKEN).strip()
CONSOLE_TOKEN_TTL_SEC = float(os.environ.get("GAME_CONSOLE_TOKEN_TTL_SEC", "43200"))  # 12h
ONLINE_IDLE_SEC = float(os.environ.get("GAME_ONLINE_IDLE_SEC", "120"))

router = APIRouter()

_logs: deque[dict[str, Any]] = deque(maxlen=800)
_console_tokens: dict[str, float] = {}
_stats: dict[str, Any] = {
    "portalVisitsTotal": 0,
    "portalVisitsToday": 0,
    "portalUniqueIps": [],
    "portalLastVisitUnix": 0,
    "downloadClicks": 0,
}
_get_conn: Callable | None = None
_peer_ip: Callable[[Request], str] | None = None


class ConsoleAuthBody(BaseModel):
    command: str = Field(default="", max_length=128)


def _today_key() -> str:
    return datetime.now().strftime("%Y-%m-%d")


def _load_stats() -> None:
    global _stats
    if not STATS_PATH.is_file():
        return
    try:
        raw = json.loads(STATS_PATH.read_text(encoding="utf-8"))
        if isinstance(raw, dict):
            _stats.update(raw)
            if not isinstance(_stats.get("portalUniqueIps"), list):
                _stats["portalUniqueIps"] = []
    except (json.JSONDecodeError, OSError):
        pass


def _save_stats() -> None:
    ensure_runtime_dirs()
    STATS_PATH.write_text(json.dumps(_stats, ensure_ascii=False, indent=2), encoding="utf-8")


def hub_log(level: str, message: str, source: str = "server") -> None:
    entry = {
        "timeUnix": time.time(),
        "timeText": datetime.now().strftime("%Y-%m-%d %H:%M:%S"),
        "level": level,
        "source": source,
        "message": message,
    }
    _logs.appendleft(entry)
    line = f"[{entry['timeText']}] [{level}] {message}\n"
    try:
        ensure_runtime_dirs()
        with LOG_FILE_PATH.open("a", encoding="utf-8") as f:
            f.write(line)
    except OSError:
        pass
    try:
        print(line, end="", flush=True)
    except OSError:
        pass


class _HubLogWriter:
    def write(self, text: str) -> int:
        return len(text or "")

    def flush(self) -> None:
        pass


def _record_portal_visit(request: Request) -> None:
    global _stats
    ip = _peer_ip(request) if _peer_ip else "127.0.0.1"
    today = _today_key()
    if _stats.get("_dayKey") != today:
        _stats["_dayKey"] = today
        _stats["portalVisitsToday"] = 0

    _stats["portalVisitsTotal"] = int(_stats.get("portalVisitsTotal") or 0) + 1
    _stats["portalVisitsToday"] = int(_stats.get("portalVisitsToday") or 0) + 1
    _stats["portalLastVisitUnix"] = time.time()

    ips = set(_stats.get("portalUniqueIps") or [])
    ips.add(ip)
    _stats["portalUniqueIps"] = sorted(ips)[-500:]
    _save_stats()


def _record_download_click() -> None:
    _stats["downloadClicks"] = int(_stats.get("downloadClicks") or 0) + 1
    _save_stats()


def _validate_console_token(token: str | None) -> None:
    if not token:
        raise HTTPException(status.HTTP_401_UNAUTHORIZED, "需要控制台令牌")
    exp = _console_tokens.get(token.strip())
    if exp is None or exp < time.time():
        if token.strip() in _console_tokens:
            del _console_tokens[token.strip()]
        raise HTTPException(status.HTTP_401_UNAUTHORIZED, "控制台令牌无效或已过期")


def _issue_console_token() -> str:
    token = secrets.token_urlsafe(24)
    _console_tokens[token] = time.time() + CONSOLE_TOKEN_TTL_SEC
    return token


def _count_online_players() -> int:
    if _get_conn is None:
        return 0
    cutoff = time.time() - ONLINE_IDLE_SEC
    try:
        with _get_conn() as conn:
            row = conn.execute(
                "SELECT COUNT(DISTINCT user_id) AS c FROM player_positions WHERE updated_at >= ?",
                (cutoff,),
            ).fetchone()
            if row is None:
                return 0
            return int(row["c"] if isinstance(row, dict) or hasattr(row, "keys") else row[0])
    except Exception as e:
        hub_log("WARN", f"统计在线人数失败: {e}")
        return 0


def _count_registered_sessions() -> int:
    if _get_conn is None:
        return 0
    try:
        with _get_conn() as conn:
            row = conn.execute("SELECT COUNT(*) AS c FROM sessions").fetchone()
            if row is None:
                return 0
            return int(row["c"] if isinstance(row, dict) or hasattr(row, "keys") else row[0])
    except Exception:
        return 0


@router.post("/api/hub/console/auth")
def console_auth(body: ConsoleAuthBody):
    """门户页脚输入命令后鉴权，进入统一控制台。"""
    cmd = (body.command or "").strip()
    ok = False
    if cmd and CONSOLE_COMMAND and cmd == CONSOLE_COMMAND:
        ok = True
    elif cmd and ADMIN_TOKEN and cmd == ADMIN_TOKEN:
        ok = True
    elif not CONSOLE_COMMAND and not ADMIN_TOKEN and cmd == DEFAULT_CONSOLE_COMMAND:
        ok = True

    if not ok:
        hub_log("WARN", f"控制台鉴权失败: command={cmd[:32]!r}")
        raise HTTPException(status.HTTP_403_FORBIDDEN, "控制台命令无效")

    token = _issue_console_token()
    hub_log("INFO", "管理员已进入统一控制台")
    return {
        "ok": True,
        "token": token,
        "expiresInSec": int(CONSOLE_TOKEN_TTL_SEC),
        "consoleUrl": "/control/",
    }


@router.get("/api/hub/dashboard")
def hub_dashboard(x_console_token: str | None = Header(default=None, alias="X-Console-Token")):
    _validate_console_token(x_console_token)
    return {
        "ok": True,
        "onlinePlayers": _count_online_players(),
        "onlineIdleSec": ONLINE_IDLE_SEC,
        "activeSessions": _count_registered_sessions(),
        "portal": {
            "visitsTotal": int(_stats.get("portalVisitsTotal") or 0),
            "visitsToday": int(_stats.get("portalVisitsToday") or 0),
            "uniqueIps": len(_stats.get("portalUniqueIps") or []),
            "downloadClicks": int(_stats.get("downloadClicks") or 0),
            "lastVisitUnix": float(_stats.get("portalLastVisitUnix") or 0),
        },
        "serverTimeUnix": time.time(),
    }


@router.get("/api/hub/logs")
def hub_logs(
    limit: int = 200,
    x_console_token: str | None = Header(default=None, alias="X-Console-Token"),
):
    _validate_console_token(x_console_token)
    limit = max(1, min(limit, 500))
    items = list(_logs)[:limit]
    file_tail: list[str] = []
    if LOG_FILE_PATH.is_file():
        try:
            lines = LOG_FILE_PATH.read_text(encoding="utf-8", errors="replace").splitlines()
            file_tail = lines[-limit:]
        except OSError:
            file_tail = []
    return {"ok": True, "logs": items, "fileTail": file_tail}


@router.post("/api/hub/portal/download-click")
def portal_download_click():
    _record_download_click()
    return {"ok": True}


def register_server_hub(app, get_conn_fn: Callable, peer_ip_fn: Callable[[Request], str]) -> None:
    global _get_conn, _peer_ip
    _get_conn = get_conn_fn
    _peer_ip = peer_ip_fn
    _load_stats()
    app.include_router(router)
    hub_log("INFO", "统一控制台模块已加载（游戏后端 + 下载门户 + 热更新）")

    @app.middleware("http")
    async def hub_tracking_middleware(request: Request, call_next):
        path = request.url.path
        method = request.method
        start = time.time()
        try:
            response = await call_next(request)
            elapsed_ms = int((time.time() - start) * 1000)
            if path.startswith("/api/") or path in ("/", "/health"):
                hub_log(
                    "HTTP",
                    f"{method} {path} -> {response.status_code} ({elapsed_ms}ms)",
                    source="access",
                )
            if method == "GET" and path in ("/", "/index.html"):
                _record_portal_visit(request)
            return response
        except Exception as e:
            hub_log("ERROR", f"{method} {path} 异常: {e}", source="access")
            raise
