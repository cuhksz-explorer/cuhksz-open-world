# -*- coding: utf-8 -*-
"""Download or install 我和我的祖国 into Assets/Music + Resources/Music.

Primary source: Netease Music (李谷一 version, song id 440101789).

Usage:
  py -3 Tools/download_wodezuoguo.py
  py -3 Tools/download_wodezuoguo.py --local "D:\\path\\to\\song.mp3"
"""
from __future__ import annotations

import argparse
import shutil
import subprocess
import sys
import uuid
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
MUSIC_DIR = ROOT / "Assets" / "Music"
RES_DIR = ROOT / "Assets" / "Resources" / "Music"
INCOMING = Path(__file__).resolve().parent / "_incoming"
OUT_FILE = MUSIC_DIR / "我和我的祖国.mp3"
RES_FILE = RES_DIR / "我和我的祖国.mp3"
NETEASE_URL = "https://music.163.com/song?id=440101789"
MUSIC_GUID = "f3a8c21e9b4d6e2f8a1c0d5e7b9f2a4c"
RES_GUID = "a7e2d19c4b6f8e3a9d0c1b2e5f6a7b8c"


def write_audio_meta(mp3: Path, guid: str) -> None:
    meta = Path(str(mp3) + ".meta")
    meta.write_text(
        f"""fileFormatVersion: 2
guid: {guid}
AudioImporter:
  externalObjects: {{}}
  serializedVersion: 7
  defaultSettings:
    serializedVersion: 2
    loadType: 0
    sampleRateSetting: 0
    sampleRateOverride: 44100
    compressionFormat: 1
    quality: 1
    conversionMode: 0
    preloadAudioData: 0
  platformSettingOverrides: {{}}
  forceToMono: 0
  normalize: 1
  loadInBackground: 0
  ambisonic: 0
  3D: 0
  userData: 
  assetBundleName: 
  assetBundleVariant: 
""",
        encoding="utf-8",
    )


def install_file(src: Path) -> int:
    if not src.is_file() or src.stat().st_size < 100_000:
        print(f"Invalid or missing file: {src}")
        return 1
    MUSIC_DIR.mkdir(parents=True, exist_ok=True)
    RES_DIR.mkdir(parents=True, exist_ok=True)
    shutil.copy2(src, OUT_FILE)
    shutil.copy2(src, RES_FILE)
    write_audio_meta(OUT_FILE, MUSIC_GUID)
    write_audio_meta(RES_FILE, RES_GUID)
    print(f"OK: {OUT_FILE} ({OUT_FILE.stat().st_size} bytes)")
    print(f"OK: {RES_FILE}")
    return 0


def try_incoming() -> int | None:
    for name in ("我和我的祖国.mp3", "wodezuoguo.mp3", "patriotic.mp3"):
        p = INCOMING / name
        if p.is_file():
            return install_file(p)
    if OUT_FILE.is_file() and OUT_FILE.stat().st_size > 100_000:
        return install_file(OUT_FILE)
    return None


def ffmpeg_location() -> str | None:
    try:
        import imageio_ffmpeg
        return imageio_ffmpeg.get_ffmpeg_exe()
    except Exception:
        return None


def try_netease() -> int:
    MUSIC_DIR.mkdir(parents=True, exist_ok=True)
    try:
        import yt_dlp  # noqa: F401
    except ImportError:
        subprocess.check_call([sys.executable, "-m", "pip", "install", "yt-dlp", "-q"])

    opts = {
        "format": "bestaudio/best",
        "outtmpl": str(MUSIC_DIR / "我和我的祖国.%(ext)s"),
        "quiet": False,
        "noplaylist": True,
    }
    ff = ffmpeg_location()
    if ff:
        opts["ffmpeg_location"] = str(Path(ff).parent)

    import yt_dlp

    try:
        with yt_dlp.YoutubeDL(opts) as ydl:
            ydl.download([NETEASE_URL])
    except Exception as ex:
        # Netease may already have saved mp3 before postprocess error
        if not OUT_FILE.is_file():
            print(f"Netease download failed: {ex}")
            return 1

    if OUT_FILE.is_file():
        return install_file(OUT_FILE)
    return 1


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--local", help="Copy existing mp3 instead of downloading")
    args = parser.parse_args()

    if args.local:
        return install_file(Path(args.local))

    incoming = try_incoming()
    if incoming is not None:
        return incoming

    code = try_netease()
    if code == 0:
        return 0

    print(
        "\n请将 mp3 放到以下位置后重试：\n"
        f"  {INCOMING / '我和我的祖国.mp3'}\n"
        f"  或: py -3 Tools/download_wodezuoguo.py --local <路径>"
    )
    return 1


if __name__ == "__main__":
    raise SystemExit(main())
