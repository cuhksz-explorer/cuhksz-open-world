"""客户端整包/增量版本发布：网页后台上传 zip，按文件 hash 生成增量补丁。"""
from __future__ import annotations

import hashlib
import json
import os
import re
import shutil
import time
import zipfile
from pathlib import Path
from typing import Any
from urllib.parse import quote

from fastapi import APIRouter, File, Form, Header, HTTPException, UploadFile, status
from fastapi.responses import FileResponse

from runtime_paths import CLIENT_RELEASE_DIR, ensure_runtime_dirs, migrate_legacy_data_dir

migrate_legacy_data_dir()
ensure_runtime_dirs()

RELEASE_DIR = CLIENT_RELEASE_DIR
FILES_DIR = RELEASE_DIR / "files"
MANIFEST_PATH = RELEASE_DIR / "manifest.json"
DEFAULT_ADMIN_TOKEN = "sc-lguexplorer"
ADMIN_TOKEN = os.environ.get("GAME_ADMIN_TOKEN", DEFAULT_ADMIN_TOKEN).strip()
VERSION_PATTERN = re.compile(r"^[0-9A-Za-z][0-9A-Za-z._\-]{0,63}$")

router = APIRouter()


def _ensure_dirs() -> None:
    RELEASE_DIR.mkdir(parents=True, exist_ok=True)
    FILES_DIR.mkdir(parents=True, exist_ok=True)


def _load_manifest() -> dict[str, Any]:
    _ensure_dirs()
    if not MANIFEST_PATH.is_file():
        return {"activeVersion": "", "releases": []}
    try:
        data = json.loads(MANIFEST_PATH.read_text(encoding="utf-8"))
    except (json.JSONDecodeError, OSError):
        return {"activeVersion": "", "releases": []}
    if not isinstance(data, dict):
        return {"activeVersion": "", "releases": []}
    if not isinstance(data.get("releases"), list):
        data["releases"] = []
    if not isinstance(data.get("activeVersion"), str):
        data["activeVersion"] = ""
    return data


def _save_manifest(data: dict[str, Any]) -> None:
    _ensure_dirs()
    MANIFEST_PATH.write_text(
        json.dumps(data, ensure_ascii=False, indent=2),
        encoding="utf-8",
    )


def _sha256_file(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as f:
        for chunk in iter(lambda: f.read(1024 * 1024), b""):
            h.update(chunk)
    return h.hexdigest()


def _find_release(manifest: dict[str, Any], version: str) -> dict[str, Any] | None:
    for item in manifest.get("releases", []):
        if isinstance(item, dict) and item.get("version") == version:
            return item
    return None


def _active_release(manifest: dict[str, Any]) -> dict[str, Any] | None:
    version = str(manifest.get("activeVersion") or "").strip()
    if not version:
        return None
    return _find_release(manifest, version)


def _require_admin(x_admin_token: str | None) -> None:
    if not ADMIN_TOKEN:
        return
    if (x_admin_token or "").strip() != ADMIN_TOKEN:
        raise HTTPException(status.HTTP_401_UNAUTHORIZED, "需要管理员令牌 (X-Admin-Token)")


def _parse_version_parts(version: str) -> list[int]:
    v = (version or "").strip()
    if v and v[0] in "vV":
        v = v[1:].strip()
    if not v:
        return [0, 0, 1]
    parts: list[int] = []
    for seg in v.split("."):
        seg = seg.strip()
        if not seg:
            parts.append(0)
            continue
        if not seg.isdigit():
            raise HTTPException(status.HTTP_400_BAD_REQUEST, f"版本号格式无效: {version}")
        parts.append(min(int(seg), 9))
    while len(parts) < 3:
        parts.append(0)
    return parts[:3]


def _versions_equal(left: str, right: str) -> bool:
    try:
        return _parse_version_parts(left) == _parse_version_parts(right)
    except HTTPException:
        return (left or "").strip() == (right or "").strip()


def _normalize_version(version: str) -> str:
    parts = _parse_version_parts(version)
    v = f"{parts[0]}.{parts[1]}.{parts[2]}"
    if not VERSION_PATTERN.match(v):
        raise HTTPException(status.HTTP_400_BAD_REQUEST, "版本号格式无效")
    return v


def _try_safe_xyz_version(candidate: str) -> str | None:
    """只接受每段都在 0-9 的 X.Y.Z，避免把其他版本号（例如 Unity/日期）误判。"""
    v = (candidate or "").strip()
    if not v:
        return None
    if v[0] in ("v", "V"):
        v = v[1:].strip()
    m = re.fullmatch(r"(\d{1,2})\.(\d{1,2})\.(\d{1,2})", v)
    if not m:
        return None
    a = int(m.group(1))
    b = int(m.group(2))
    c = int(m.group(3))
    if a < 0 or b < 0 or c < 0:
        return None
    if a > 9 or b > 9 or c > 9:
        return None
    return f"{a}.{b}.{c}"


def _detect_version_from_zip(zip_path: Path) -> str | None:
    """从上传 zip 内自动识别版本号（优先读取版本文件，其次从 exe/zip 文件名推断）。"""
    try:
        with zipfile.ZipFile(zip_path, "r") as zf:
            # 1) 优先从 StreamingAssets 里的版本文件读取（最可靠）
            candidates = []
            for name in zf.namelist():
                low = name.lower()
                if low.endswith("client_version.txt") or low.endswith("version.txt"):
                    candidates.append(name)

            for member in candidates:
                try:
                    raw = zf.read(member)
                    text = raw.decode("utf-8", errors="ignore").strip()
                    if not text:
                        continue
                    # 支持文本多行，仅取第一行/第一个形如 X.Y.Z 的片段
                    first_line = text.splitlines()[0].strip()
                    safe = _try_safe_xyz_version(first_line)
                    if safe:
                        return safe
                    m = re.search(r"(\d{1,2}\.\d{1,2}\.\d{1,2})", text)
                    if m:
                        safe = _try_safe_xyz_version(m.group(1))
                        if safe:
                            return safe
                except Exception:
                    continue

            # 2) 尝试解析 client_version.json
            for name in zf.namelist():
                low = name.lower()
                if low.endswith("client_version.json"):
                    try:
                        raw = zf.read(name)
                        data = json.loads(raw.decode("utf-8", errors="ignore"))
                        v = data.get("version") or data.get("clientVersion") or data.get("client_version")
                        safe = _try_safe_xyz_version(str(v))
                        if safe:
                            return safe
                    except Exception:
                        pass

            # 3) 从 exe 的二进制内容中搜索 X.Y.Z（次可靠）
            exe_members = [n for n in zf.namelist() if n.lower().endswith(".exe")]
            if exe_members:
                exe_bytes = zf.read(exe_members[0])
                # 为避免误判，只扫描前若干 MB 内容
                scan_bytes = exe_bytes[: min(len(exe_bytes), 6 * 1024 * 1024)]
                text = scan_bytes.decode("utf-8", errors="ignore")
                for m in re.finditer(r"(\d{1,2}\.\d{1,2}\.\d{1,2})", text):
                    safe = _try_safe_xyz_version(m.group(1))
                    if safe:
                        return safe

    except Exception:
        pass

    # 4) 最后退回：zip 文件名推断
    m = re.search(r"(\d{1,2}\.\d{1,2}\.\d{1,2})", zip_path.name)
    if m:
        return _try_safe_xyz_version(m.group(1))
    return None


def _version_files_dir(version: str) -> Path:
    return FILES_DIR / _normalize_version(version)


def _flatten_single_root_folder(root: Path) -> None:
    entries = [p for p in root.iterdir() if p.name not in {".DS_Store"}]
    if len(entries) != 1 or not entries[0].is_dir():
        return
    inner = entries[0]
    for child in inner.iterdir():
        dest = root / child.name
        if dest.exists():
            if dest.is_dir():
                shutil.rmtree(dest)
            else:
                dest.unlink()
        shutil.move(str(child), str(dest))
    inner.rmdir()


def _build_file_manifest(version: str) -> dict[str, Any]:
    root = _version_files_dir(version)
    if not root.is_dir():
        return {"files": [], "fileCount": 0, "totalSize": 0}
    files: list[dict[str, Any]] = []
    total_size = 0
    for fp in sorted(root.rglob("*")):
        if not fp.is_file():
            continue
        rel = fp.relative_to(root).as_posix()
        size = fp.stat().st_size
        files.append({"path": rel, "sha256": _sha256_file(fp), "size": size})
        total_size += size
    return {"files": files, "fileCount": len(files), "totalSize": total_size}


def _extract_zip_to_version(version: str, zip_path: Path) -> dict[str, Any]:
    root = _version_files_dir(version)
    if root.exists():
        shutil.rmtree(root)
    root.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(zip_path, "r") as zf:
        zf.extractall(root)
    _flatten_single_root_folder(root)
    return _build_file_manifest(version)


def _files_map(manifest: dict[str, Any]) -> dict[str, dict[str, Any]]:
    result: dict[str, dict[str, Any]] = {}
    for item in manifest.get("files", []):
        if isinstance(item, dict) and item.get("path"):
            result[str(item["path"])] = item
    return result


def _compute_patch(from_manifest: dict[str, Any], to_manifest: dict[str, Any]) -> dict[str, Any]:
    from_map = _files_map(from_manifest)
    to_map = _files_map(to_manifest)
    changed: list[dict[str, Any]] = []
    patch_size = 0
    for path, info in to_map.items():
        old = from_map.get(path)
        if old is None or old.get("sha256") != info.get("sha256"):
            changed.append(info)
            patch_size += int(info.get("size") or 0)
    removed = [path for path in from_map if path not in to_map]
    return {
        "changedFiles": changed,
        "removedFiles": removed,
        "changedCount": len(changed),
        "removedCount": len(removed),
        "patchSize": patch_size,
    }


def _file_download_url(version: str, rel_path: str) -> str:
    return f"/api/client/file/{quote(version, safe='')}/{quote(rel_path, safe='')}"


def _patch_payload(from_version: str, to_version: str, release: dict[str, Any]) -> dict[str, Any]:
    to_manifest = release.get("fileManifest") or {}
    full_size = int(release.get("size") or 0)
    if not to_manifest.get("files"):
        return {
            "ok": True,
            "mode": "full",
            "fromVersion": from_version,
            "toVersion": to_version,
            "fullDownloadUrl": f"/api/client/download/{to_version}",
            "sha256": release.get("sha256", ""),
            "size": full_size,
            "files": [],
        }

    from_release = _find_release(_load_manifest(), from_version)
    if from_release is None or not from_release.get("fileManifest"):
        files = []
        for item in to_manifest.get("files", []):
            if not isinstance(item, dict) or not item.get("path"):
                continue
            path = str(item["path"])
            files.append(
                {
                    "path": path,
                    "sha256": item.get("sha256", ""),
                    "size": int(item.get("size") or 0),
                    "url": _file_download_url(to_version, path),
                }
            )
        patch_size = sum(int(f.get("size") or 0) for f in files)
        return {
            "ok": True,
            "mode": "incremental",
            "fromVersion": from_version,
            "toVersion": to_version,
            "changedCount": len(files),
            "removedCount": 0,
            "patchSize": patch_size,
            "fullSize": full_size,
            "files": files,
            "removed": [],
            "fullDownloadUrl": f"/api/client/download/{to_version}",
            "sha256": release.get("sha256", ""),
            "size": full_size,
            "syncMissingOnly": True,
        }

    from_manifest = from_release.get("fileManifest") or {}
    patch = _compute_patch(from_manifest, to_manifest)
    files = []
    for item in patch["changedFiles"]:
        path = str(item.get("path") or "")
        files.append(
            {
                "path": path,
                "sha256": item.get("sha256", ""),
                "size": int(item.get("size") or 0),
                "url": _file_download_url(to_version, path),
            }
        )
    full_size = int(release.get("size") or 0)
    patch_size = int(patch.get("patchSize") or 0)
    use_incremental = len(files) > 0 and (full_size <= 0 or patch_size < full_size)
    return {
        "ok": True,
        "mode": "incremental" if use_incremental else "full",
        "fromVersion": from_version,
        "toVersion": to_version,
        "changedCount": patch["changedCount"],
        "removedCount": patch["removedCount"],
        "patchSize": patch_size,
        "fullSize": full_size,
        "files": files,
        "removed": patch["removedFiles"],
        "fullDownloadUrl": f"/api/client/download/{to_version}",
        "sha256": release.get("sha256", ""),
        "size": full_size,
    }


def _release_to_client_info(release: dict[str, Any], from_version: str = "") -> dict[str, Any]:
    version = str(release.get("version") or "")
    info = {
        "version": version,
        "sha256": release.get("sha256", ""),
        "size": int(release.get("size") or 0),
        "downloadUrl": f"/api/client/download/{version}",
        "releaseNotes": release.get("releaseNotes", ""),
        "forceUpdate": bool(release.get("forceUpdate", True)),
        "uploadedAtUnix": float(release.get("uploadedAtUnix") or 0),
        "fileCount": int((release.get("fileManifest") or {}).get("fileCount") or 0),
    }
    if from_version and from_version != version:
        patch = _patch_payload(from_version, version, release)
        info["updateMode"] = patch.get("mode", "full")
        info["patchUrl"] = f"/api/client/patch?from={quote(from_version, safe='')}&to={quote(version, safe='')}"
        info["patchSize"] = int(patch.get("patchSize") or 0)
        info["changedCount"] = int(patch.get("changedCount") or 0)
    return info


def _resolve_file_path(version: str, rel_path: str) -> Path:
    version = _normalize_version(version)
    rel_path = (rel_path or "").replace("\\", "/").lstrip("/")
    if not rel_path or ".." in rel_path.split("/"):
        raise HTTPException(status.HTTP_400_BAD_REQUEST, "非法文件路径")
    root = _version_files_dir(version).resolve()
    target = (root / rel_path).resolve()
    if root not in target.parents and target != root:
        raise HTTPException(status.HTTP_400_BAD_REQUEST, "非法文件路径")
    if not target.is_file():
        raise HTTPException(status.HTTP_404_NOT_FOUND, "文件不存在")
    return target


@router.get("/api/client/public/latest")
def client_public_latest():
    """下载门户页：最新客户端公开信息（无需登录）。"""
    manifest = _load_manifest()
    active = _active_release(manifest)
    if active is None:
        return {
            "ok": True,
            "hasRelease": False,
            "message": "管理员尚未发布客户端，请稍后再试",
        }
    version = str(active.get("version") or "")
    return {
        "ok": True,
        "hasRelease": True,
        "version": version,
        "releaseNotes": active.get("releaseNotes", ""),
        "size": int(active.get("size") or 0),
        "fileCount": int((active.get("fileManifest") or {}).get("fileCount") or 0),
        "uploadedAtUnix": float(active.get("uploadedAtUnix") or 0),
        "downloadUrl": f"/api/client/download/{version}",
        "downloadName": active.get("fileName") or f"client_{version}.zip",
    }


@router.get("/api/portal/backgrounds")
def portal_backgrounds():
    """返回下载页可用的背景图列表。"""
    bg_dir = ROOT / "static" / "portal" / "backgrounds"
    items: list[str] = []
    if bg_dir.is_dir():
        for fp in sorted(bg_dir.glob("bg*.png")):
            items.append(f"/backgrounds/{fp.name}")
        if not items:
            for fp in sorted(bg_dir.glob("*.png")):
                items.append(f"/backgrounds/{fp.name}")
    return {"backgrounds": items}


# 根路径由 static/portal 站点提供；统一控制台见 /control/；客户端发布见 /admin/


@router.get("/api/client/version")
def client_version(current: str = ""):
    """Unity 客户端启动时校验版本；current 为 Application.version。"""
    manifest = _load_manifest()
    active = _active_release(manifest)
    local = (current or "").strip()
    if active is None:
        return {
            "ok": True,
            "updateRequired": False,
            "localVersion": local,
            "latestVersion": local,
            "message": "服务端尚未发布客户端版本",
        }
    latest = _normalize_version(str(active.get("version") or ""))
    local_norm = ""
    if local:
        try:
            parts = _parse_version_parts(local)
            local_norm = f"{parts[0]}.{parts[1]}.{parts[2]}"
        except HTTPException:
            local_norm = local
    update_required = bool(latest) and not _versions_equal(local_norm or local, latest)
    payload: dict[str, Any] = {
        "ok": True,
        "updateRequired": update_required,
        "localVersion": local_norm or local,
        "latestVersion": latest,
    }
    if update_required:
        from_v = local_norm or local
        payload.update(_release_to_client_info(active, from_version=from_v))
    return payload


@router.get("/api/client/patch")
def client_patch(from_version: str = "", to: str = ""):
    """返回 from_version → to 的增量补丁清单；无法增量时 mode=full。"""
    manifest = _load_manifest()
    to_version = _normalize_version(to or manifest.get("activeVersion", ""))
    from_v = (from_version or "").strip()
    if not from_v:
        raise HTTPException(status.HTTP_400_BAD_REQUEST, "缺少 from_version")
    release = _find_release(manifest, to_version)
    if release is None:
        raise HTTPException(status.HTTP_404_NOT_FOUND, "目标版本不存在")
    return _patch_payload(from_v, to_version, release)


@router.get("/api/client/file/{version}/{file_path:path}")
def client_download_file(version: str, file_path: str):
    target = _resolve_file_path(version, file_path)
    return FileResponse(str(target), media_type="application/octet-stream", filename=target.name)


@router.get("/api/client/download/latest")
def client_download_latest():
    manifest = _load_manifest()
    active = _active_release(manifest)
    if active is None:
        raise HTTPException(status.HTTP_404_NOT_FOUND, "暂无激活版本")
    return _download_release(active)


@router.get("/api/client/download/{version}")
def client_download_version(version: str):
    manifest = _load_manifest()
    release = _find_release(manifest, _normalize_version(version))
    if release is None:
        raise HTTPException(status.HTTP_404_NOT_FOUND, "版本不存在")
    return _download_release(release)


def _download_release(release: dict[str, Any]) -> FileResponse:
    file_name = str(release.get("fileName") or "")
    path = RELEASE_DIR / file_name
    if not path.is_file():
        raise HTTPException(status.HTTP_404_NOT_FOUND, "安装包文件缺失")
    return FileResponse(str(path), media_type="application/zip", filename=file_name)


@router.get("/api/admin/client/releases")
def admin_list_releases():
    manifest = _load_manifest()
    releases_raw = [r for r in manifest.get("releases", []) if isinstance(r, dict)]
    active = str(manifest.get("activeVersion") or "")
    releases = []
    for item in releases_raw:
        version = str(item.get("version") or "")
        patch_hint = None
        older = sorted(
            [r for r in releases_raw if float(r.get("uploadedAtUnix") or 0) < float(item.get("uploadedAtUnix") or 0)],
            key=lambda x: float(x.get("uploadedAtUnix") or 0),
            reverse=True,
        )
        if older:
            prev = older[0]
            prev_version = str(prev.get("version") or "")
            patch_hint = _compute_patch(
                prev.get("fileManifest") or {"files": []},
                item.get("fileManifest") or {"files": []},
            )
            patch_hint = {**patch_hint, "fromVersion": prev_version}
        releases.append(
            {
                "version": version,
                "fileName": item.get("fileName", ""),
                "sha256": item.get("sha256", ""),
                "size": int(item.get("size") or 0),
                "fileCount": int((item.get("fileManifest") or {}).get("fileCount") or 0),
                "releaseNotes": item.get("releaseNotes", ""),
                "forceUpdate": bool(item.get("forceUpdate", True)),
                "uploadedAtUnix": float(item.get("uploadedAtUnix") or 0),
                "isActive": version == active,
                "downloadUrl": f"/api/client/download/{version}" if version else "",
                "incrementalFromPrev": patch_hint,
            }
        )
    releases.sort(key=lambda x: float(x.get("uploadedAtUnix") or 0), reverse=True)
    return {
        "ok": True,
        "activeVersion": active,
        "adminTokenRequired": bool(ADMIN_TOKEN),
        "releases": releases,
    }


@router.post("/api/admin/client/upload")
async def admin_upload_release(
    version: str = Form(""),
    release_notes: str = Form(""),
    force_update: bool = Form(True),
    set_active: bool = Form(True),
    file: UploadFile = File(...),
    x_admin_token: str | None = Header(default=None, alias="X-Admin-Token"),
):
    _require_admin(x_admin_token)
    if not file.filename or not file.filename.lower().endswith(".zip"):
        raise HTTPException(status.HTTP_400_BAD_REQUEST, "请上传 .zip 客户端包")

    _ensure_dirs()
    # 先保存为临时文件，确保在未提供 version 时也能先提取 zip 内版本号
    tmp = RELEASE_DIR / f".upload_auto_{int(time.time())}.tmp"
    dest = None
    version_final = None

    try:
        with tmp.open("wb") as out:
            while True:
                chunk = await file.read(1024 * 1024)
                if not chunk:
                    break
                out.write(chunk)
        # 识别版本号
        version_final = (version or "").strip()
        if not version_final:
            version_final = _detect_version_from_zip(tmp)
        if not version_final:
            raise HTTPException(
                status.HTTP_400_BAD_REQUEST,
                "无法从 zip 自动识别版本号。请把版本号写在上传表单里，或在客户端 Build 时加入 StreamingAssets/client_version.txt。",
            )
        version_final = _normalize_version(version_final)

        safe_name = f"client_{version_final}.zip"
        dest = RELEASE_DIR / safe_name
        tmp.replace(dest)
    except OSError as e:
        if tmp.is_file():
            tmp.unlink(missing_ok=True)
        raise HTTPException(status.HTTP_500_INTERNAL_SERVER_ERROR, f"保存文件失败: {e}") from e

    # 后续逻辑全部使用 version_final
    sha256 = _sha256_file(dest)
    size = dest.stat().st_size
    file_manifest = _extract_zip_to_version(version_final, dest)
    now = time.time()
    manifest = _load_manifest()
    releases: list[dict[str, Any]] = [
        r for r in manifest.get("releases", []) if isinstance(r, dict) and r.get("version") != version_final
    ]
    releases.append(
        {
            "version": version_final,
            "fileName": safe_name,
            "sha256": sha256,
            "size": size,
            "fileManifest": file_manifest,
            "releaseNotes": (release_notes or "").strip(),
            "forceUpdate": bool(force_update),
            "uploadedAtUnix": now,
        }
    )
    releases.sort(key=lambda x: float(x.get("uploadedAtUnix") or 0), reverse=True)
    manifest["releases"] = releases
    if set_active:
        manifest["activeVersion"] = version_final
    _save_manifest(manifest)

    prev = ""
    patch_stats = None
    if len(releases) > 1:
        prev_release = releases[1]
        prev = str(prev_release.get("version") or "")
        if prev:
            patch_stats = _compute_patch(
                prev_release.get("fileManifest") or {"files": []},
                file_manifest,
            )

    return {
        "ok": True,
        "version": version_final,
        "sha256": sha256,
        "size": size,
        "fileCount": file_manifest.get("fileCount", 0),
        "activeVersion": manifest.get("activeVersion", ""),
        "incrementalFrom": prev,
        "incrementalChangedCount": int((patch_stats or {}).get("changedCount") or 0),
        "incrementalPatchSize": int((patch_stats or {}).get("patchSize") or 0),
    }


@router.post("/api/admin/client/reindex/{version}")
def admin_reindex_release(version: str, x_admin_token: str | None = Header(default=None, alias="X-Admin-Token")):
    """从已保存 zip 重新解压并建立文件索引（用于升级旧版本记录）。"""
    _require_admin(x_admin_token)
    version = _normalize_version(version)
    manifest = _load_manifest()
    release = _find_release(manifest, version)
    if release is None:
        raise HTTPException(status.HTTP_404_NOT_FOUND, "版本不存在")
    zip_path = RELEASE_DIR / str(release.get("fileName") or "")
    if not zip_path.is_file():
        raise HTTPException(status.HTTP_404_NOT_FOUND, "zip 文件缺失")
    file_manifest = _extract_zip_to_version(version, zip_path)
    release["fileManifest"] = file_manifest
    _save_manifest(manifest)
    return {"ok": True, "version": version, "fileCount": file_manifest.get("fileCount", 0)}


@router.post("/api/admin/client/set-active/{version}")
def admin_set_active(version: str, x_admin_token: str | None = Header(default=None, alias="X-Admin-Token")):
    _require_admin(x_admin_token)
    version = _normalize_version(version)
    manifest = _load_manifest()
    if _find_release(manifest, version) is None:
        raise HTTPException(status.HTTP_404_NOT_FOUND, "版本不存在")
    manifest["activeVersion"] = version
    _save_manifest(manifest)
    return {"ok": True, "activeVersion": version}


@router.delete("/api/admin/client/release/{version}")
def admin_delete_release(version: str, x_admin_token: str | None = Header(default=None, alias="X-Admin-Token")):
    _require_admin(x_admin_token)
    version = _normalize_version(version)
    manifest = _load_manifest()
    release = _find_release(manifest, version)
    if release is None:
        raise HTTPException(status.HTTP_404_NOT_FOUND, "版本不存在")

    file_name = str(release.get("fileName") or "")
    if file_name:
        path = RELEASE_DIR / file_name
        if path.is_file():
            path.unlink()

    files_root = _version_files_dir(version)
    if files_root.exists():
        shutil.rmtree(files_root, ignore_errors=True)

    manifest["releases"] = [
        r for r in manifest.get("releases", []) if not (isinstance(r, dict) and r.get("version") == version)
    ]
    if manifest.get("activeVersion") == version:
        next_active = ""
        if manifest["releases"]:
            next_active = str(manifest["releases"][0].get("version") or "")
        manifest["activeVersion"] = next_active
    _save_manifest(manifest)
    return {"ok": True, "activeVersion": manifest.get("activeVersion", "")}


def register_client_release_routes(app) -> None:
    app.include_router(router)
