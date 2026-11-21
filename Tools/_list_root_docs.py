# one-off index for quest review — not used at runtime
import re
from pathlib import Path

p = Path(__file__).resolve().parent.parent / "Assets/code/GameQuestSystem/Content1StoryDatabase.asset"
text = p.read_text(encoding="utf-8")

def decode(s):
    s = s.strip()
    if (s.startswith('"') and s.endswith('"')) or (s.startswith("'") and s.endswith("'")):
        s = s[1:-1]
    try:
        return s.encode("utf-8").decode("unicode_escape")
    except UnicodeDecodeError:
        return s

docs = []
doc = None
section = None
page = None
for line in text.splitlines():
    if line.strip() == "documents:":
        continue
    if line.startswith("  - documentId:"):
        if doc:
            docs.append(doc)
        doc = {"pages": [], "choices": []}
        doc["documentId"] = decode(line.split(":", 1)[1])
        section = None
        continue
    if doc is None:
        continue
    if line.strip() == "pages:":
        section = "pages"
        continue
    if line.strip() == "choices:":
        section = "choices"
        continue
    m = re.match(r"^    (\w+): (.*)$", line)
    if m and section is None:
        k, v = m.group(1), decode(m.group(2))
        if k != "documentId":
            doc[k] = v
        continue
    if section == "pages":
        m = re.match(r"^    - pageNumber: (\d+)$", line)
        if m:
            page = {"pageNumber": int(m.group(1)), "speakerName": "", "text": ""}
            doc["pages"].append(page)
            continue
        m = re.match(r"^      (\w+): (.*)$", line)
        if m and page is not None:
            page[m.group(1)] = decode(m.group(2))

if doc:
    docs.append(doc)

roots = [d for d in docs if str(d.get("isRootTask", "0")) in ("1", "True", "true")]
out = Path(__file__).resolve().parent / "_quest_doc_index.txt"
lines = [f"root docs: {len(roots)}"]
for ch in ["M0", "M1", "M2", "M3", "M4", "M5"]:
    n = sum(1 for d in roots if d.get("chapterId", "").upper() == ch)
    if n:
        lines.append(f"{ch}: {n}")
for d in roots:
    snippet = ""
    for pg in d.get("pages", [])[:2]:
        t = (pg.get("text") or "").replace("\n", " ")[:100]
        if t:
            snippet = t
            break
    lines.append("---")
    lines.append(
        f"{d.get('documentId')} | title={d.get('title','')} | npc={d.get('npcId','')} "
        f"| scene={d.get('targetSceneName','')} | loc={d.get('finishLocationName','')}"
    )
    if snippet:
        lines.append("  " + snippet)
out.write_text("\n".join(lines), encoding="utf-8")
print(f"written {len(roots)} docs -> {out}")
