# -*- coding: utf-8 -*-
import re
import openpyxl
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
extract = ROOT / "_import_narration_temp" / "generated"

mp3s = sorted(extract.rglob("*.mp3"))
pat = re.compile(r"NAR_(?P<ch>M\d+)_(?P<doc>.+)_p(?P<page>\d+)_(?P<seq>\d+)\.mp3$")
parsed = []
for p in mp3s:
    m = pat.match(p.name)
    if not m:
        print("bad name", p.name)
        continue
    parsed.append({**m.groupdict(), "path": p})

print("mp3 count", len(parsed))

wb = openpyxl.load_workbook(
    next(
        p
        for p in (
            ROOT / "egg" / "workbooks" / "剧情配音文案.xlsx",
            ROOT / "剧情配音文案.xlsx",
        )
        if p.is_file()
    ),
    read_only=True,
    data_only=True,
)
ws = wb["旁白"]
headers = [c.value for c in next(ws.iter_rows(min_row=1, max_row=1))]
idx = {h: i for i, h in enumerate(headers)}
rows = []
for r in ws.iter_rows(min_row=2, values_only=True):
    if not r or not r[idx["章节ID"]]:
        continue
    rows.append(
        {
            "seq": r[idx["序号"]],
            "chapter": r[idx["章节ID"]],
            "doc": r[idx["任务ID"]],
            "page": int(r[idx["页码"]]),
        }
    )
print("xlsx narration rows", len(rows))

by_key = {(r["chapter"], r["doc"], r["page"]): r for r in rows}
mismatch = []
for p in parsed:
    key = (p["ch"], p["doc"], int(p["page"]))
    row = by_key.get(key)
    if row is None:
        mismatch.append(("missing xlsx", p["path"].name))
        continue
    if str(row["seq"]).zfill(4) != p["seq"]:
        mismatch.append(("seq mismatch", p["path"].name, p["seq"], row["seq"], key))

print("mismatches", len(mismatch))
for m in mismatch[:15]:
    print(m)

mp3_seq = set(p["seq"] for p in parsed)
xlsx_seq = set(str(r["seq"]).zfill(4) for r in rows)
print("mp3 not in xlsx seq", len(mp3_seq - xlsx_seq))
print("xlsx seq not in mp3", len(xlsx_seq - mp3_seq))
