# temp scan
import re
from pathlib import Path
from collections import defaultdict

ROOT = Path(r"Z:\CEC2000\刘厚安 Unity实机\My project (2)")
ASSETS = ROOT / "Assets"
PACKAGES = ROOT / "Packages"
PACKAGE_CACHE = ROOT / "Library" / "PackageCache"

REF_EXT = {".unity", ".prefab", ".asset", ".mat", ".controller", ".overrideController", ".mask", ".anim", ".playable", ".scene", ".fontsettings", ".spriteatlas", ".guiskin", ".shadervariants"}
GUID_IN_REF = re.compile(r"guid: ([0-9a-f]{32})")
META_GUID = re.compile(r"^guid: ([0-9a-f]{32})", re.M)
BUILTIN = {"0000000000000000e000000000000000", "0000000000000000f000000000000000"}

def load_meta(root):
    idx = {}
    for base in [root]:
        if not base.is_dir():
            continue
        for p in base.rglob("*.meta"):
            try:
                t = p.read_text(encoding="utf-8", errors="replace")
            except OSError:
                continue
            m = META_GUID.search(t)
            if m:
                idx[m.group(1)] = str(p.relative_to(ROOT)).replace("\\", "/")
    return idx

meta = {}
meta.update(load_meta(ASSETS))
meta.update(load_meta(PACKAGES))
if PACKAGE_CACHE.is_dir():
    meta.update(load_meta(PACKAGE_CACHE))

missing = defaultdict(set)
total_refs = 0
for p in ASSETS.rglob("*"):
    if not p.is_file() or p.suffix.lower() not in REF_EXT:
        continue
    if ".broken_" in p.name or p.name.endswith(".bak") or "DO_NOT_USE" in p.name:
        continue
    try:
        t = p.read_text(encoding="utf-8", errors="replace")
    except OSError:
        continue
    rel = str(p.relative_to(ROOT)).replace("\\", "/")
    for m in GUID_IN_REF.finditer(t):
        g = m.group(1)
        total_refs += 1
        if g in BUILTIN or g in meta:
            continue
        missing[g].add(rel)

print("meta entries", len(meta))
print("total guid refs scanned", total_refs)
print("missing unique guids", len(missing))
for g, files in sorted(missing.items(), key=lambda x: -len(x[1]))[:40]:
    print(len(files), g, list(files)[:3])
