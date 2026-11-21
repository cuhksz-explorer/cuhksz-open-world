import re
from pathlib import Path

def fbx_materials(path):
    raw = Path(path).read_bytes()
    names = set()
    for m in re.finditer(rb"Material::([^\x00\x01\x02]{2,80})", raw):
        n = m.group(1).decode("utf-8", "ignore").strip()
        if n and not n.startswith("{"):
            names.add(n)
    return sorted(names)

scene = Path(r"z:\CEC2000\刘厚安 Unity实机\My project (2)\Assets\scene")
mats_dir = scene / "Materials"
for fname in ["lib2f.fbx", "图书馆1楼.fbx"]:
    p = scene / fname
    names = fbx_materials(p)
    print(fname, len(names))
    miss = 0
    for n in names[:80]:
        ok = (mats_dir / f"{n}.mat").is_file()
        if not ok:
            alt = list(mats_dir.glob(f"{n}.*.mat"))
            ok = bool(alt)
        if not ok:
            miss += 1
            if miss <= 25:
                print("  MISSING:", n)
    print("  total missing:", miss, "/", len(names))
