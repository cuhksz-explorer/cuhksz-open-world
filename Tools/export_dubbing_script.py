#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Export story dialogue from Content1StoryDatabase.asset to xlsx for voice acting."""

import re
import sys
from pathlib import Path

try:
    from openpyxl import Workbook
    from openpyxl.styles import Alignment, Font, PatternFill
    from openpyxl.utils import get_column_letter
except ImportError:
    print("Installing openpyxl...")
    import subprocess
    subprocess.check_call([sys.executable, "-m", "pip", "install", "openpyxl", "-q"])
    from openpyxl import Workbook
    from openpyxl.styles import Alignment, Font, PatternFill
    from openpyxl.utils import get_column_letter


PROJECT_ROOT = Path(__file__).resolve().parent.parent
ASSET_PATH = PROJECT_ROOT / "Assets" / "code" / "GameQuestSystem" / "Content1StoryDatabase.asset"
OUTPUT_PATH = PROJECT_ROOT / "egg" / "workbooks" / "剧情配音文案.xlsx"

CHAPTER_NAMES = {
    "M0": "序章 M0",
    "M1": "第一章 M1",
    "M2": "第二章 M2",
    "M3": "第三章 M3",
    "M4": "第四章 M4",
    "M5": "终章 M5",
}


def decode_yaml_string(value: str) -> str:
    value = value.strip()
    if not value:
        return ""
    if (value.startswith('"') and value.endswith('"')) or (
        value.startswith("'") and value.endswith("'")
    ):
        value = value[1:-1]
    try:
        return value.encode("utf-8").decode("unicode_escape")
    except UnicodeDecodeError:
        return value


def parse_asset(path: Path):
    text = path.read_text(encoding="utf-8")
    lines = text.splitlines()

    in_documents = False
    doc = None
    section = None
    page = None
    choice = None
    documents = []

    doc_field_re = re.compile(r"^    (\w+): (.*)$")
    page_field_re = re.compile(r"^      (\w+): (.*)$")
    list_item_re = re.compile(r"^  - (\w+): (.*)$")
    page_start_re = re.compile(r"^    - pageNumber: (\d+)$")
    choice_start_re = re.compile(r"^    - label: (.*)$")

    for line in lines:
        if line.strip() == "documents:":
            in_documents = True
            continue
        if not in_documents:
            continue
        if line and not line.startswith("  "):
            break

        if line.startswith("  - documentId:"):
            if doc:
                documents.append(doc)
            doc = {"pages": [], "choices": []}
            section = None
            doc["documentId"] = decode_yaml_string(line.split(":", 1)[1])
            continue

        if doc is None:
            continue

        m = list_item_re.match(line)
        if m and m.group(1) not in ("pageNumber", "label"):
            key, val = m.group(1), m.group(2)
            if key == "documentId":
                continue
            doc[key] = decode_yaml_string(val)
            continue

        if line.strip() == "pages:":
            section = "pages"
            continue
        if line.strip() == "choices:":
            section = "choices"
            continue

        m = doc_field_re.match(line)
        if m and section is None:
            key, val = m.group(1), m.group(2)
            doc[key] = decode_yaml_string(val)
            continue

        if section == "pages":
            m = page_start_re.match(line)
            if m:
                page = {"pageNumber": int(m.group(1)), "speakerName": "", "text": ""}
                doc["pages"].append(page)
                continue
            m = page_field_re.match(line)
            if m and page is not None:
                key, val = m.group(1), m.group(2)
                page[key] = decode_yaml_string(val)
                continue

        if section == "choices":
            m = choice_start_re.match(line)
            if m:
                choice = {"label": decode_yaml_string(m.group(1)), "targetDocumentId": ""}
                doc["choices"].append(choice)
                continue
            m = page_field_re.match(line)
            if m and choice is not None:
                key, val = m.group(1), m.group(2)
                choice[key] = decode_yaml_string(val)
                continue

    if doc:
        documents.append(doc)

    return documents


def speaker_category(speaker: str) -> str:
    if not speaker or not speaker.strip():
        return "旁白"
    if speaker in ("林渡", "【系统提示】"):
        return speaker
    return "角色台词"


def build_rows(documents):
    rows = []
    seq = 0
    for doc in documents:
        chapter = doc.get("chapterId", "")
        chapter_name = CHAPTER_NAMES.get(chapter, chapter)
        doc_id = doc.get("documentId", "")
        title = doc.get("title", doc_id)
        npc_id = doc.get("npcId", "")
        scene = doc.get("targetSceneName", "")
        location = doc.get("finishLocationName", "")
        category = "主线" if str(doc.get("category", "0")) == "0" else "支线"

        for page in doc.get("pages", []):
            seq += 1
            speaker = page.get("speakerName", "") or ""
            text = page.get("text", "") or ""
            if not text.strip():
                continue
            rows.append(
                {
                    "序号": seq,
                    "章节": chapter_name,
                    "章节ID": chapter,
                    "任务ID": doc_id,
                    "任务标题": title,
                    "任务类型": category,
                    "内容类型": "台词/旁白",
                    "页码": page.get("pageNumber", ""),
                    "角色": speaker if speaker else "（旁白）",
                    "配音分类": speaker_category(speaker),
                    "文案": text,
                    "NPC ID": npc_id,
                    "场景": scene,
                    "地点": location,
                    "跳转目标": "",
                    "配音状态": "",
                    "备注": "主角名「林渡」游戏中会替换为玩家昵称" if speaker == "林渡" or "林渡" in text else "",
                }
            )

        for choice in doc.get("choices", []):
            label = choice.get("label", "") or ""
            if not label.strip():
                continue
            seq += 1
            rows.append(
                {
                    "序号": seq,
                    "章节": chapter_name,
                    "章节ID": chapter,
                    "任务ID": doc_id,
                    "任务标题": title,
                    "任务类型": category,
                    "内容类型": "分支选项",
                    "页码": "",
                    "角色": "（玩家选项）",
                    "配音分类": "玩家选项",
                    "文案": label,
                    "NPC ID": npc_id,
                    "场景": scene,
                    "地点": location,
                    "跳转目标": choice.get("targetDocumentId", ""),
                    "配音状态": "",
                    "备注": "可选配音：玩家分支选择按钮文字",
                }
            )

    return rows


def build_speaker_summary(rows):
    summary = {}
    for row in rows:
        if row["内容类型"] == "分支选项":
            key = "（玩家选项）"
        elif row["角色"] == "（旁白）":
            key = "（旁白）"
        else:
            key = row["角色"]
        if key not in summary:
            summary[key] = {"count": 0, "chars": 0}
        summary[key]["count"] += 1
        summary[key]["chars"] += len(row["文案"])
    return [
        {
            "角色": k,
            "台词条数": v["count"],
            "总字数": v["chars"],
        }
        for k, v in sorted(summary.items(), key=lambda x: -x[1]["count"])
    ]


def style_sheet(ws, headers):
    header_fill = PatternFill("solid", fgColor="4472C4")
    header_font = Font(bold=True, color="FFFFFF")
    for col, header in enumerate(headers, 1):
        cell = ws.cell(row=1, column=col, value=header)
        cell.fill = header_fill
        cell.font = header_font
        cell.alignment = Alignment(horizontal="center", vertical="center", wrap_text=True)
    ws.freeze_panes = "A2"
    ws.auto_filter.ref = f"A1:{get_column_letter(len(headers))}1"


def auto_width(ws, max_width=60):
    for col_cells in ws.columns:
        length = 0
        col_letter = get_column_letter(col_cells[0].column)
        for cell in col_cells:
            if cell.value:
                length = max(length, min(len(str(cell.value)), max_width))
        ws.column_dimensions[col_letter].width = min(max(length + 2, 10), max_width)


def write_xlsx(rows, summary, output_path: Path):
    wb = Workbook()

    # Sheet 1: All lines
    ws = wb.active
    ws.title = "全部配音文案"
    headers = [
        "序号", "章节", "章节ID", "任务ID", "任务标题", "任务类型",
        "内容类型", "页码", "角色", "配音分类", "文案",
        "NPC ID", "场景", "地点", "跳转目标", "配音状态", "备注",
    ]
    style_sheet(ws, headers)
    for r, row in enumerate(rows, 2):
        for c, h in enumerate(headers, 1):
            cell = ws.cell(row=r, column=c, value=row.get(h, ""))
            if h == "文案":
                cell.alignment = Alignment(wrap_text=True, vertical="top")
    auto_width(ws)
    ws.column_dimensions["K"].width = 60

    # Sheet 2: By speaker
    ws2 = wb.create_sheet("按角色统计")
    sum_headers = ["角色", "台词条数", "总字数"]
    style_sheet(ws2, sum_headers)
    for r, item in enumerate(summary, 2):
        for c, h in enumerate(sum_headers, 1):
            ws2.cell(row=r, column=c, value=item[h])
    auto_width(ws2)

    # Sheet 3: Narration only
    ws3 = wb.create_sheet("旁白")
    style_sheet(ws3, headers)
    nar_rows = [row for row in rows if row["配音分类"] == "旁白"]
    for r, row in enumerate(nar_rows, 2):
        for c, h in enumerate(headers, 1):
            cell = ws3.cell(row=r, column=c, value=row.get(h, ""))
            if h == "文案":
                cell.alignment = Alignment(wrap_text=True, vertical="top")
    auto_width(ws3)
    ws3.column_dimensions["K"].width = 60

    # Sheet 4: Protagonist
    ws4 = wb.create_sheet("林渡")
    style_sheet(ws4, headers)
    ld_rows = [row for row in rows if row["角色"] == "林渡"]
    for r, row in enumerate(ld_rows, 2):
        for c, h in enumerate(headers, 1):
            cell = ws4.cell(row=r, column=c, value=row.get(h, ""))
            if h == "文案":
                cell.alignment = Alignment(wrap_text=True, vertical="top")
    auto_width(ws4)
    ws4.column_dimensions["K"].width = 60

    # Sheet 5: Credits & epilogue
    ws5 = wb.create_sheet("片尾与完结提示")
    epilogue_headers = ["类型", "标题", "文案"]
    style_sheet(ws5, epilogue_headers)
    credits_body = (
        "【制作团队】\n"
        "总制作人：刘厚安\n"
        "主策划：朱熹\n"
        "主程序：刘厚安\n"
        "主美术：温怡蕊\n"
        "叙事 / 文案：常意\n"
        "音频：屈子胜、黄晨朗\n\n"
        "【游戏设计】\n"
        "世界观设定：刘厚安\n"
        "主线剧情：常意、昌振威、温怡蕊\n"
        "支线剧情：常意、昌振威、温怡蕊\n"
        "任务系统：刘厚安、李泰阳、昌振威\n"
        "NPC 对话：蒋皓泽、张睿涵、昌振威\n\n"
        "【程序开发】\n"
        "角色与相机：刘厚安、李泰阳、胡智皓\n"
        "交互与对话：蒋皓泽、吴悠、吴家铎、刘厚安、张睿涵\n"
        "任务系统：刘厚安、刘畅、吴悠、昌振威\n"
        "手机 UI：李泰阳、胡智皓、刘厚安\n"
        "寝室商城：刘畅、刘厚安\n"
        "载具系统：刘厚安、李泰阳、昌振威\n"
        "联机与服务器：刘厚安、吴悠、刘畅\n"
        "场景与传送：刘厚安、张琢晓、王佳皓\n\n"
        "【美术】\n"
        "场景搭建：王璟诚、崔映庭、屈子胜、张洲语、许浩哲、潘铭洋、刘东昊、李泰阳、欧阳君玮、陈天乐、黄子明、王奕翔、王子琛、李晓萌、沈致文、张琢晓、张睿涵、温怡蕊、赵芯、郝季贝赫\n"
        "角色 / NPC：温怡蕊、刘东昊、刘厚安、常意、蒋皓泽、吴家铎、张睿涵\n"
        "UI 设计：刘厚安、李泰阳、昌振威、胡智皓、蒋皓泽、吴家铎、黄子明、崔映庭、王璟诚、王佳皓、吴悠\n"
        "特效：刘厚安、李泰阳、张睿涵、王璟诚、王佳皓、吴悠\n\n"
        "【音频】\n"
        "音乐：屈子胜、黄晨朗\n"
        "音效：屈子胜、黄晨朗\n\n"
        "【测试与支持】\n"
        "测试：刘厚安、吴悠、屈子胜、蒋皓泽\n"
        "指导教师：朱熹\n"
        "特别感谢：赖镇海、王子奥\n\n"
        "© 2026 LGU EXPLORER\n"
        "感谢游玩"
    )
    extras = [
        ("片尾字幕", "《LGU EXPLORER》致谢", credits_body),
        ("完结提示", "主线已完结", "主线旅程已告一段落。\n\n校园中仍有未完成的支线剧情与人物故事，\n你可以继续自由探索，完成它们。\n\n感谢游玩《未名之海》"),
    ]
    for r, (t, title, body) in enumerate(extras, 2):
        ws5.cell(row=r, column=1, value=t)
        ws5.cell(row=r, column=2, value=title)
        cell = ws5.cell(row=r, column=3, value=body)
        cell.alignment = Alignment(wrap_text=True, vertical="top")
    auto_width(ws5)
    ws5.column_dimensions["C"].width = 60

    wb.save(output_path)


def main():
    if not ASSET_PATH.exists():
        print(f"Error: asset not found: {ASSET_PATH}")
        sys.exit(1)

    documents = parse_asset(ASSET_PATH)
    rows = build_rows(documents)
    summary = build_speaker_summary(rows)
    write_xlsx(rows, summary, OUTPUT_PATH)

    line_count = sum(1 for r in rows if r["内容类型"] == "台词/旁白")
    choice_count = sum(1 for r in rows if r["内容类型"] == "分支选项")
    print(f"已导出: {OUTPUT_PATH}")
    print(f"剧情文档数: {len(documents)}")
    print(f"台词/旁白: {line_count} 条")
    print(f"分支选项: {choice_count} 条")
    print(f"合计: {len(rows)} 条")


if __name__ == "__main__":
    main()
