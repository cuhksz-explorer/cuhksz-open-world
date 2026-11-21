"""Wait for LGUHUB SSH tunnel, then start local GameServer (uvicorn)."""
from __future__ import annotations

import os
import subprocess
import sys
import time
import urllib.error
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parent
ENV_FILE = ROOT / ".env.lguhub.local"
TUNNEL_PORT = 5001
GAME_PORT = 8765


def load_env() -> None:
    if ENV_FILE.is_file():
        for line in ENV_FILE.read_text(encoding="utf-8").splitlines():
            line = line.strip()
            if not line or line.startswith("#") or "=" not in line:
                continue
            key, value = line.split("=", 1)
            key, value = key.strip(), value.strip()
            if key:
                os.environ.setdefault(key, value)
    os.environ["LGUHUB_BASE_URL"] = f"http://127.0.0.1:{TUNNEL_PORT}"


def wait_tunnel(timeout_sec: int = 30) -> bool:
    url = f"http://127.0.0.1:{TUNNEL_PORT}/api/sso/health"
    for _ in range(timeout_sec):
        try:
            with urllib.request.urlopen(url, timeout=3) as resp:
                if resp.status == 200:
                    return True
        except (urllib.error.URLError, TimeoutError, OSError):
            pass
        time.sleep(1)
    return False


def main() -> int:
    load_env()
    secret = (os.environ.get("GAME_SSO_SECRET") or "").strip()
    if not secret:
        print("[GameServer] ERROR: GAME_SSO_SECRET missing in .env.lguhub.local")
        return 1

    print("[GameServer] ========================================")
    print("[GameServer]  Tunnel mode (SSH -> LGUHUB)")
    print("[GameServer]  LGUHUB_BASE_URL=", os.environ["LGUHUB_BASE_URL"])
    print("[GameServer]  Game API: http://127.0.0.1:%s" % GAME_PORT)
    print("[GameServer] ========================================")

    if not wait_tunnel():
        print("[GameServer] ERROR: tunnel not ready in 30s.")
        print("[GameServer]         Check the 'LGUHUB SSH Tunnel' window.")
        return 1

    print("[GameServer] Tunnel OK (SSO health passed).")

    backend = ROOT / "backend"
    if not (backend / "server.py").is_file():
        print("[GameServer] ERROR: backend/server.py not found")
        return 1

    py = ROOT / ".venv" / "Scripts" / "python.exe"
    if not py.is_file():
        print("[GameServer] ERROR: .venv missing. Run start_server.bat first.")
        return 1

    cmd = [
        str(py),
        "-m",
        "uvicorn",
        "server:app",
        "--app-dir",
        str(backend),
        "--host",
        "0.0.0.0",
        "--port",
        str(GAME_PORT),
    ]
    print("[GameServer] Starting uvicorn...")
    return subprocess.call(cmd, env=os.environ.copy())


if __name__ == "__main__":
    raise SystemExit(main())
