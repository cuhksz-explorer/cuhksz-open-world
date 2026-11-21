# temp helper - list root story docs
import sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parent))
from import_narration_voice import parse_asset, ASSET_PATH

docs = parse_asset(ASSET_PATH)
roots = [d for d in docs if str(d.get("isRootTask", "0")) in ("1", "True", "true")]
roots.sort(key=lambda d: d.get("documentId", ""))
for d in roots:
    pages = d.get("pages") or []
    snippet = ""
    for p in pages[:3]:
        t = (p.get("text") or "").replace("\n", " ")[:60]
        if t:
            snippet = t
            break
    print("|".join([
        d.get("documentId", ""),
        d.get("chapterId", ""),
        d.get("npcId", ""),
        d.get("targetSceneName", ""),
        d.get("finishLocationName", ""),
        snippet,
    ]))
print("TOTAL", len(roots))
