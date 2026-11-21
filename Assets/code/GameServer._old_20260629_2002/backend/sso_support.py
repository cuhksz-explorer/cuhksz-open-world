"""LGUHUB 等服务端调用的账号池 SSO（密码权威在 GameServer）。"""
from __future__ import annotations

import os
import secrets
import time
from typing import Any, Optional

from fastapi import HTTPException, Request, status

SSO_TICKET_TTL_SEC = int(os.environ.get("SSO_TICKET_TTL_SEC", "300"))
GAME_SSO_SECRET = (
    os.environ.get("GAME_SSO_SECRET") or os.environ.get("LGUHUB_SSO_SECRET") or ""
).strip()


def sso_secret_configured() -> bool:
    return bool(GAME_SSO_SECRET)


def verify_sso_client_ip(request: Request) -> None:
    allowed = (os.environ.get("GAME_SSO_ALLOWED_IPS") or "").strip()
    if not allowed:
        return
    peer = _peer_ip(request)
    ok = any(peer == ip.strip() for ip in allowed.split(",") if ip.strip())
    if not ok:
        raise HTTPException(status.HTTP_403_FORBIDDEN, "来源 IP 未授权")


def verify_sso_auth(request: Request, body_secret: str = "") -> None:
    if not GAME_SSO_SECRET:
        raise HTTPException(status.HTTP_503_SERVICE_UNAVAILABLE, "SSO 未配置")
    verify_sso_client_ip(request)
    hdr = (request.headers.get("X-Game-Sso-Secret") or "").strip()
    if hdr == GAME_SSO_SECRET:
        return
    verify_sso_secret_value(body_secret)


def verify_sso_secret_value(secret: str) -> None:
    if not GAME_SSO_SECRET:
        raise HTTPException(status.HTTP_503_SERVICE_UNAVAILABLE, "SSO 未配置")
    if (secret or "").strip() != GAME_SSO_SECRET:
        raise HTTPException(status.HTTP_403_FORBIDDEN, "SSO 密钥无效")


def _peer_ip(request: Request) -> str:
    xff = request.headers.get("x-forwarded-for") or request.headers.get("X-Forwarded-For")
    if xff:
        return xff.split(",")[0].strip()
    if request.client and request.client.host:
        return request.client.host
    return "127.0.0.1"


def ensure_sso_tables(conn) -> None:
    conn.execute(
        """
        CREATE TABLE IF NOT EXISTS sso_tickets (
            ticket TEXT PRIMARY KEY,
            user_id INTEGER NOT NULL,
            username TEXT NOT NULL,
            created_at REAL NOT NULL,
            expires_at REAL NOT NULL,
            consumed INTEGER NOT NULL DEFAULT 0,
            FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE
        );
        """
    )
    cols = {row[1] for row in conn.execute("PRAGMA table_info(users)")}
    if "must_change_password" not in cols:
        conn.execute(
            "ALTER TABLE users ADD COLUMN must_change_password INTEGER NOT NULL DEFAULT 0"
        )
    if "email" not in cols:
        conn.execute("ALTER TABLE users ADD COLUMN email TEXT NOT NULL DEFAULT ''")


def cleanup_sso_tickets(conn) -> None:
    now = time.time()
    conn.execute("DELETE FROM sso_tickets WHERE expires_at < ? OR consumed = 1", (now,))


def get_user_by_username(conn, username: str):
    return conn.execute(
        "SELECT id, username, nickname, password_hash, must_change_password, email FROM users WHERE username = ?",
        (username.strip(),),
    ).fetchone()


def _verify_password(password: str, password_hash: str) -> bool:
    import bcrypt

    try:
        return bcrypt.checkpw(
            password.encode("utf-8"), password_hash.encode("ascii")
        )
    except (ValueError, TypeError):
        return False


def _hash_password(password: str) -> str:
    import bcrypt

    p = password.encode("utf-8")
    if len(p) > 72:
        p = p[:72]
    return bcrypt.hashpw(p, bcrypt.gensalt()).decode("ascii")


def verify_credentials(conn, username: str, password: str) -> dict[str, Any]:
    row = get_user_by_username(conn, username)
    if row is None:
        raise HTTPException(status.HTTP_401_UNAUTHORIZED, "用户名或密码错误")
    if not _verify_password(password, row["password_hash"]):
        raise HTTPException(status.HTTP_401_UNAUTHORIZED, "用户名或密码错误")
    return {
        "user_id": int(row["id"]),
        "username": str(row["username"]),
        "nickname": str(row["nickname"] or "").strip(),
        "must_change_password": bool(int(row["must_change_password"] or 0)),
        "email": str(row["email"] or "").strip(),
    }


def issue_sso_ticket(conn, user_id: int, username: str) -> str:
    cleanup_sso_tickets(conn)
    ticket = secrets.token_urlsafe(32)
    now = time.time()
    conn.execute(
        "INSERT INTO sso_tickets (ticket, user_id, username, created_at, expires_at, consumed) VALUES (?,?,?,?,?,0)",
        (ticket, user_id, username, now, now + SSO_TICKET_TTL_SEC),
    )
    return ticket


def consume_sso_ticket(conn, ticket: str) -> dict[str, Any]:
    cleanup_sso_tickets(conn)
    row = conn.execute(
        "SELECT ticket, user_id, username, expires_at, consumed FROM sso_tickets WHERE ticket = ?",
        (ticket.strip(),),
    ).fetchone()
    if not row:
        raise HTTPException(status.HTTP_401_UNAUTHORIZED, "票据无效或已过期")
    if int(row["consumed"] or 0) == 1:
        raise HTTPException(status.HTTP_401_UNAUTHORIZED, "票据已使用")
    if time.time() > float(row["expires_at"]):
        raise HTTPException(status.HTTP_401_UNAUTHORIZED, "票据已过期")
    conn.execute("UPDATE sso_tickets SET consumed = 1 WHERE ticket = ?", (ticket,))
    return {
        "user_id": int(row["user_id"]),
        "username": str(row["username"]),
    }


def register_user(
    conn,
    username: str,
    password: str,
    nickname: str = "",
    email: str = "",
) -> dict[str, Any]:
    uname = username.strip()
    if len(uname) < 2 or len(uname) > 64:
        raise HTTPException(status.HTTP_400_BAD_REQUEST, "用户名长度无效")
    if len(password) < 6:
        raise HTTPException(status.HTTP_400_BAD_REQUEST, "密码至少6位")
    existing = get_user_by_username(conn, uname)
    if existing:
        raise HTTPException(status.HTTP_409_CONFLICT, "用户名已存在")
    now = time.time()
    cur = conn.execute(
        "INSERT INTO users (username, password_hash, nickname, email, must_change_password, created_at) VALUES (?,?,?,?,0,?)",
        (uname, _hash_password(password), (nickname or uname)[:24], (email or "")[:120], now),
    )
    user_id = int(cur.lastrowid)
    return {
        "user_id": user_id,
        "username": uname,
        "nickname": (nickname or uname)[:24],
        "created": True,
    }


def change_password(
    conn,
    username: str,
    new_password: str,
    old_password: Optional[str] = None,
    force: bool = False,
    clear_must_change: bool = True,
) -> None:
    if len(new_password) < 6:
        raise HTTPException(status.HTTP_400_BAD_REQUEST, "密码至少6位")
    row = get_user_by_username(conn, username)
    if row is None:
        raise HTTPException(status.HTTP_404_NOT_FOUND, "用户不存在")
    if not force:
        if not old_password:
            raise HTTPException(status.HTTP_400_BAD_REQUEST, "需要当前密码")
        if not _verify_password(old_password, row["password_hash"]):
            raise HTTPException(status.HTTP_403_FORBIDDEN, "当前密码不正确")
    must_flag = 0 if clear_must_change else int(row["must_change_password"] or 0)
    conn.execute(
        "UPDATE users SET password_hash = ?, must_change_password = ? WHERE id = ?",
        (_hash_password(new_password), must_flag, int(row["id"])),
    )


def reset_password(conn, username: str, temp_password: Optional[str] = None) -> dict[str, Any]:
    """生成临时密码并标记必须改密（供 LGUHUB 忘记密码发信）。"""
    row = get_user_by_username(conn, username.strip())
    if row is None:
        raise HTTPException(status.HTTP_404_NOT_FOUND, "用户不存在")
    plain = temp_password or secrets.token_urlsafe(10)

    conn.execute(
        "UPDATE users SET password_hash = ?, must_change_password = 1 WHERE id = ?",
        (_hash_password(plain), int(row["id"])),
    )
    return {
        "username": str(row["username"]),
        "temporary_password": plain,
        "must_change_password": True,
    }
