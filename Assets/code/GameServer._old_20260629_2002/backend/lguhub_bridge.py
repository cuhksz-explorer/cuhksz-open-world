"""GameServer ↔ LGUHUB SSO 桥接（urllib，无额外依赖）。"""
from __future__ import annotations

import json
import os
import urllib.error
import urllib.request


def lguhub_base_url() -> str:
    return (os.environ.get("LGUHUB_BASE_URL") or "https://lguhub.cn").strip().rstrip("/")


def lguhub_sso_secret() -> str:
    return (os.environ.get("GAME_SSO_SECRET") or os.environ.get("LGUHUB_SSO_SECRET") or "").strip()


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
        return e.code, data
    except Exception as e:
        return 0, {"error": str(e)}


def provision_lguhub_user(username: str, password: str | None = None) -> tuple[bool, str]:
    payload = {"username": username.strip()}
    if password:
        payload["password"] = password
    status, data = _post_json("/api/unity/game-provision", payload)
    if status == 200 and data.get("success"):
        return True, ""
    return False, data.get("error") or f"provision failed ({status})"


def issue_lguhub_unity_token(username: str) -> tuple[bool, dict, str]:
    status, data = _post_json("/api/unity/issue-token", {"username": username.strip()})
    if status == 200 and data.get("success") and data.get("token"):
        return True, data, ""
    if status == 404:
        ok, err = provision_lguhub_user(username, None)
        if ok:
            status, data = _post_json("/api/unity/issue-token", {"username": username.strip()})
            if status == 200 and data.get("success") and data.get("token"):
                return True, data, ""
        return False, {}, err or data.get("error") or "issue-token failed"
    return False, {}, data.get("error") or f"issue-token failed ({status})"
