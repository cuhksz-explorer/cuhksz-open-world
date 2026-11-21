"""GameServer → LGUHUB 主站认证（游戏登录依赖 LGUHUB）。"""
from __future__ import annotations

import json
import os
import urllib.error
import urllib.request


def lguhub_sso_secret() -> str:
    return (os.environ.get("GAME_SSO_SECRET") or os.environ.get("LGUHUB_SSO_SECRET") or "").strip()


def lguhub_base_url() -> str:
    return (os.environ.get("LGUHUB_BASE_URL") or "https://lguhub.cn").strip().rstrip("/")


def lguhub_auth_configured() -> bool:
    return bool(lguhub_base_url() and lguhub_sso_secret())


def _post_json(path: str, payload: dict, timeout: float = 12.0) -> tuple[int, dict]:
    secret = lguhub_sso_secret()
    if not secret:
        return 0, {"error": "GAME_SSO_SECRET 未配置"}
    url = f"{lguhub_base_url()}{path}"
    body = json.dumps({**payload, "secret": secret}).encode("utf-8")
    req = urllib.request.Request(
        url,
        data=body,
        headers={
            "Content-Type": "application/json",
            "X-Game-Sso-Secret": secret,
        },
        method="POST",
    )
    try:
        with urllib.request.urlopen(req, timeout=timeout) as resp:
            raw = resp.read().decode("utf-8", errors="replace")
            return resp.status, json.loads(raw) if raw else {}
    except urllib.error.HTTPError as e:
        try:
            raw = e.read().decode("utf-8", errors="replace")
            data = json.loads(raw) if raw else {"error": str(e)}
        except Exception:
            data = {"error": str(e)}
        if isinstance(data, dict) and "detail" in data and "error" not in data:
            data["error"] = str(data["detail"])
        return e.code, data
    except urllib.error.URLError as e:
        return 0, {"error": f"无法连接 LGUHUB: {e}"}
    except Exception as e:
        return 0, {"error": str(e)}


def verify_lguhub_login(username: str, password: str) -> tuple[bool, dict, str]:
    status, data = _post_json(
        "/api/sso/verify-login",
        {"username": username.strip(), "password": password},
    )
    if status == 200 and data.get("ok") is not False:
        return True, data, ""
    err = data.get("error") or f"认证失败 ({status})"
    if status == 0:
        err = data.get("error") or "LGUHUB 主站不可用"
    return False, data, str(err)
