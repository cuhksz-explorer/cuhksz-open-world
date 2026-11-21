#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Scan Unity/Tuanjie project and generate quotation workbook (xlsx)."""

from __future__ import annotations

import os
import re
import json
from datetime import datetime
from pathlib import Path
from collections import defaultdict

from openpyxl import Workbook
from openpyxl.styles import Font, Alignment, PatternFill, Border, Side
from openpyxl.utils import get_column_letter

ROOT = Path(r"z:\CEC2000\刘厚安 Unity实机 小部分guid丢失\My project (2)")
ASSETS = ROOT / "Assets"
OUTPUT = ROOT / "egg" / "workbooks" / "项目资产与报价清单.xlsx"

SKIP_DIRS = {"Library", "Temp", "Logs", "obj", ".vs", ".git"}
THIRD_PARTY_MARKERS = [
    "TextMesh Pro",
    "Supercyan",
    "KriptoFX",
    "Fantasy Skybox",
    "Wild Scenery Kit",
    os.path.join("Packages", "com.mirrornetworking.mirror"),
]

HEADER_FILL = PatternFill("solid", fgColor="1F4E79")
HEADER_FONT = Font(bold=True, color="FFFFFF", size=11)
SUBHEADER_FILL = PatternFill("solid", fgColor="D6E4F0")
THIN = Side(style="thin", color="B4B4B4")
BORDER = Border(left=THIN, right=THIN, top=THIN, bottom=THIN)
MONEY_FMT = '#,##0'
PCT_FMT = '0.0%'


def is_third_party(path: Path) -> bool:
    s = str(path)
    return any(m in s for m in THIRD_PARTY_MARKERS)


def count_lines(path: Path) -> int:
    try:
        with open(path, "r", encoding="utf-8", errors="ignore") as f:
            return sum(1 for _ in f)
    except OSError:
        return 0


def file_size_mb(path: Path) -> float:
    try:
        return path.stat().st_size / (1024 * 1024)
    except OSError:
        return 0.0


def scan_files(base: Path, skip: set[str] | None = None) -> list[Path]:
    out: list[Path] = []
    if not base.exists():
        return out
    for dirpath, dirnames, filenames in os.walk(base):
        dirnames[:] = [d for d in dirnames if d not in (skip or SKIP_DIRS)]
        for fn in filenames:
            out.append(Path(dirpath) / fn)
    return out


def top_folder(path: Path, under: Path) -> str:
    try:
        rel = path.relative_to(under)
        return rel.parts[0] if rel.parts else "(root)"
    except ValueError:
        return "?"


def read_project_version() -> dict:
    info = {"engine": "", "tuanjie": ""}
    pv = ROOT / "ProjectSettings" / "ProjectVersion.txt"
    if pv.exists():
        for line in pv.read_text(encoding="utf-8", errors="ignore").splitlines():
            if "m_EditorVersion:" in line:
                info["engine"] = line.split(":", 1)[1].strip()
            if "m_TuanjieEditorVersion:" in line:
                info["tuanjie"] = line.split(":", 1)[1].strip()
    ps = ROOT / "ProjectSettings" / "ProjectSettings.asset"
    if ps.exists():
        m = re.search(r"productName:\s*(.+)", ps.read_text(encoding="utf-8", errors="ignore"))
        if m:
            info["product"] = m.group(1).strip()
    return info


def read_build_scenes() -> list[dict]:
    p = ROOT / "ProjectSettings" / "EditorBuildSettings.asset"
    scenes = []
    if not p.exists():
        return scenes
    text = p.read_text(encoding="utf-8", errors="ignore")
    for m in re.finditer(
        r"- enabled: (\d)\s+path: (.+?)\s+guid:",
        text,
        re.DOTALL,
    ):
        enabled = m.group(1) == "1"
        path = m.group(2).strip().strip('"')
        scenes.append({"path": path, "enabled": enabled, "name": Path(path).name})
    return scenes


def read_manifest() -> list[dict]:
    p = ROOT / "Packages" / "manifest.json"
    if not p.exists():
        return []
    data = json.loads(p.read_text(encoding="utf-8"))
    return [{"id": k, "version": v} for k, v in data.get("dependencies", {}).items()]


def scan_cs_stats() -> dict:
    all_cs = list(ASSETS.rglob("*.cs")) if ASSETS.exists() else []
    rows = []
    by_module: dict[str, dict] = defaultdict(lambda: {"files": 0, "lines": 0, "size_kb": 0.0})
    editor_files = 0
    runtime_files = 0
    custom_files = 0
    custom_lines = 0

    for f in sorted(all_cs):
        lines = count_lines(f)
        kb = f.stat().st_size / 1024
        third = is_third_party(f)
        is_editor = "\\Editor\\" in str(f) or "/Editor/" in str(f) or ".Editor." in f.name
        if is_editor:
            editor_files += 1
        else:
            runtime_files += 1
        if not third:
            custom_files += 1
            custom_lines += lines

        rel = str(f.relative_to(ASSETS))
        parts = f.relative_to(ASSETS).parts
        if parts[0] == "code" and len(parts) > 2:
            module = f"code/{parts[1]}"
        elif parts[0] == "code":
            module = "code/(根目录)"
        else:
            module = parts[0]

        by_module[module]["files"] += 1
        by_module[module]["lines"] += lines
        by_module[module]["size_kb"] += kb

        rows.append({
            "file": rel,
            "module": module,
            "lines": lines,
            "kb": round(kb, 1),
            "editor": "是" if is_editor else "否",
            "third_party": "是" if third else "否",
        })

    return {
        "rows": rows,
        "by_module": dict(by_module),
        "total_files": len(all_cs),
        "total_lines": sum(r["lines"] for r in rows),
        "editor_files": editor_files,
        "runtime_files": runtime_files,
        "custom_files": custom_files,
        "custom_lines": custom_lines,
    }


def scan_assets() -> dict:
    files = scan_files(ASSETS)
    by_ext: dict[str, int] = defaultdict(int)
    by_ext_mb: dict[str, float] = defaultdict(float)
    by_top: dict[str, int] = defaultdict(int)
    by_top_mb: dict[str, float] = defaultdict(float)

    for f in files:
        ext = f.suffix.lower() or "(无扩展名)"
        mb = file_size_mb(f)
        by_ext[ext] += 1
        by_ext_mb[ext] += mb
        top = top_folder(f, ASSETS)
        by_top[top] += 1
        by_top_mb[top] += mb

    total_mb = sum(by_ext_mb.values())
    return {
        "file_count": len(files),
        "total_mb": total_mb,
        "by_ext": by_ext,
        "by_ext_mb": by_ext_mb,
        "by_top": by_top,
        "by_top_mb": by_top_mb,
    }


def scan_models() -> list[dict]:
    rows = []
    for ext in ("*.fbx", "*.glb", "*.obj"):
        for f in ASSETS.rglob(ext):
            rows.append({
                "name": f.name,
                "format": f.suffix.lower().lstrip("."),
                "folder": str(f.parent.relative_to(ASSETS)),
                "mb": round(file_size_mb(f), 2),
            })
    return sorted(rows, key=lambda r: (-r["mb"], r["name"]))


def scan_content1() -> list[dict]:
    base = ASSETS / "content1"
    rows = []
    if not base.exists():
        return rows
    for ch in sorted(base.iterdir()):
        if ch.is_dir():
            docx = list(ch.glob("*.docx"))
            rows.append({
                "chapter": ch.name,
                "docx_count": len(docx),
                "files": ", ".join(sorted(x.name for x in docx)[:8]) + ("..." if len(docx) > 8 else ""),
            })
    return rows


def scan_story_npcs() -> list[dict]:
    mapping_file = ASSETS / "code" / "Editor" / "StoryNpcUserModelMapping.cs"
    rows = []
    if mapping_file.exists():
        text = mapping_file.read_text(encoding="utf-8", errors="ignore")
        for m in re.finditer(r'\{\s*"([^"]+)",\s*CharacterFolder \+ "/([^"]+)"\s*\}', text):
            rows.append({"display_name": m.group(1), "glb": m.group(2), "source": "Sketchfab/Imports"})
    char_dir = ASSETS / "Imports" / "Sketchfab" / "Characters"
    if char_dir.exists():
        existing = {f.name for f in char_dir.glob("*.glb")}
        for r in rows:
            r["exists"] = "是" if r["glb"] in existing else "否"
    return rows


def scan_audio() -> dict:
    exts = {".mp3", ".wav", ".ogg"}
    by_folder: dict[str, int] = defaultdict(int)
    by_folder_mb: dict[str, float] = defaultdict(float)
    total = 0
    for f in ASSETS.rglob("*"):
        if f.suffix.lower() in exts:
            total += 1
            top = top_folder(f, ASSETS)
            by_folder[top] += 1
            by_folder_mb[top] += file_size_mb(f)
    return {"total": total, "by_folder": dict(by_folder), "by_folder_mb": dict(by_folder_mb)}


def feature_modules() -> list[dict]:
    """Functional modules with effort estimates for quotation."""
    return [
        {"module": "核心玩法框架", "desc": "角色移动/相机/交互/坐下/体力HP/昼夜光影", "cs_area": "code根目录+Controls", "est_days": 35, "complexity": "高", "deliverable": "可玩角色与基础交互"},
        {"module": "开放世界与场景", "desc": "28+主场景、传送、电梯、空气墙、地图系统", "cs_area": "MAPSYSTEM/Elevator/scene", "est_days": 55, "complexity": "高", "deliverable": "校园多场景联通"},
        {"module": "主线任务系统", "desc": "M0-M5章节、支线、引导路径、任务UI、Journal", "cs_area": "GameQuestSystem", "est_days": 90, "complexity": "很高", "deliverable": "完整剧情任务链"},
        {"module": "剧情对话与配音", "desc": "对话UI、旁白、语音裁剪、120+docx导入管线", "cs_area": "StoryDialogue*/Content1*", "est_days": 40, "complexity": "高", "deliverable": "剧情演出+配音对接"},
        {"module": "剧情NPC与模型", "desc": "Sketchfab GLB映射、NPC游走、情绪、幽灵效果", "cs_area": "StoryNpc*/LLMcode", "est_days": 30, "complexity": "中高", "deliverable": "具名剧情角色"},
        {"module": "本地LLM智能NPC", "desc": "Qwen兼容API、流式对话、人格JSON、情绪联动", "cs_area": "LLMcode", "est_days": 20, "complexity": "中高", "deliverable": "AI对话NPC"},
        {"module": "账号与LguHub", "desc": "登录门户、协议、布局、注册选角", "cs_area": "LguHub/MenuLogin*", "est_days": 35, "complexity": "高", "deliverable": "学校账号入口"},
        {"module": "GameServer后端对接", "desc": "存档同步、商城目录、信令、HTTP API", "cs_area": "GameServer*", "est_days": 35, "complexity": "高", "deliverable": "联网账号体系"},
        {"module": "多人联机", "desc": "Mirror+Telepathy、房间码、协作任务、Proximity聊天", "cs_area": "Multiplayer", "est_days": 45, "complexity": "很高", "deliverable": "局域网/房间联机"},
        {"module": "宿舍商城与摆放", "desc": "购买、背包、家具摆放、持久化、编辑模式", "cs_area": "DormMall", "est_days": 45, "complexity": "很高", "deliverable": "可定制宿舍"},
        {"module": "载具系统", "desc": "驾驶物理、召唤、翻车救援、车行NPC/3D预览", "cs_area": "Carsystem+VehicleController", "est_days": 35, "complexity": "高", "deliverable": "校园驾车"},
        {"module": "手机UI系统", "desc": "应用路由、设置、键位、账号、HUD隐藏", "cs_area": "PhoneUI", "est_days": 35, "complexity": "高", "deliverable": "游戏内手机"},
        {"module": "中英本地化", "desc": "语言切换、剧情/公交站/UI词条", "cs_area": "Localization", "est_days": 18, "complexity": "中", "deliverable": "双语支持"},
        {"module": "片尾与迎新", "desc": "滚动字幕、新手引导、开场电影", "cs_area": "GameCredits/NewUser*", "est_days": 22, "complexity": "中", "deliverable": "迎新与片尾"},
        {"module": "抢旗/选课玩法", "desc": "FlagAuction计时竞拍面板", "cs_area": "FlagAuction", "est_days": 12, "complexity": "中", "deliverable": "特色小游戏"},
        {"module": "场景美术与资源", "desc": "建筑/地形/贴图/材质/天空/水体/植被优化", "cs_area": "scene/Meshes/textures等", "est_days": 180, "complexity": "很高", "deliverable": "28GB+场景资产"},
        {"module": "图书馆高斯泼溅", "desc": "3D Gaussian Splatting场景", "cs_area": "GaussianAssets+Package", "est_days": 20, "complexity": "高", "deliverable": "图书馆沉浸展示"},
        {"module": "音频与BGM", "desc": "1300+mp3配音BGM、场景批量音频配置", "cs_area": "Music/Resources", "est_days": 25, "complexity": "中高", "deliverable": "全场景音频"},
        {"module": "Editor交付工具", "desc": "一键配置、GUID修复、场景导入导出", "cs_area": "code/Editor", "est_days": 50, "complexity": "高", "deliverable": "运维工具链"},
        {"module": "测试调优与交付", "desc": "性能优化、实机适配、Bug修复、文档培训", "cs_area": "全项目", "est_days": 50, "complexity": "高", "deliverable": "可部署版本"},
    ]


def pricing_tiers(total_days: int) -> list[dict]:
    """Generate quotation tiers (CNY). Blended man-day rates for school projects."""
    rates = {
        "保守单价(元/人日)": 1200,
        "标准单价(元/人日)": 1600,
        "精品单价(元/人日)": 2200,
    }
    rows = []
    for label, rate in rates.items():
        replacement = total_days * rate
        rows.append({
            "测算口径": label,
            "参考人日": total_days,
            "单价": rate,
            "重置成本(元)": replacement,
            "成果移交价建议(元)": int(replacement * 0.45),
            "含部署培训价建议(元)": int(replacement * 0.55),
            "含1年维护价建议(元)": int(replacement * 0.65),
        })
    return rows


def style_header_row(ws, row: int, ncol: int):
    for c in range(1, ncol + 1):
        cell = ws.cell(row=row, column=c)
        cell.fill = HEADER_FILL
        cell.font = HEADER_FONT
        cell.alignment = Alignment(horizontal="center", vertical="center", wrap_text=True)
        cell.border = BORDER


def write_table(ws, headers: list[str], rows: list[list], col_widths: list[int] | None = None):
    ws.append(headers)
    style_header_row(ws, 1, len(headers))
    for r in rows:
        ws.append(r)
        for c in range(1, len(headers) + 1):
            cell = ws.cell(row=ws.max_row, column=c)
            cell.border = BORDER
            cell.alignment = Alignment(vertical="top", wrap_text=True)
    if col_widths:
        for i, w in enumerate(col_widths, 1):
            ws.column_dimensions[get_column_letter(i)].width = w
    ws.freeze_panes = "A2"


def scan_voice_workbook() -> list[dict]:
    """Stats from 剧情配音文案.xlsx if present."""
    for p in (
        ROOT / "egg" / "workbooks" / "剧情配音文案.xlsx",
        ROOT / "剧情配音文案.xlsx",
    ):
        if p.exists():
            break
    else:
        return []
    try:
        from openpyxl import load_workbook
        wb = load_workbook(p, read_only=True, data_only=True)
        rows = []
        for sn in wb.sheetnames:
            ws = wb[sn]
            data_rows = max(0, (ws.max_row or 1) - 1)
            rows.append({"sheet": sn, "approx_rows": data_rows})
        wb.close()
        return rows
    except Exception as e:
        return [{"sheet": "(读取失败)", "approx_rows": 0, "error": str(e)}]


def main():
    ver = read_project_version()
    cs = scan_cs_stats()
    assets = scan_assets()
    scenes = read_build_scenes()
    packages = read_manifest()
    models = scan_models()
    content1 = scan_content1()
    npcs = scan_story_npcs()
    audio = scan_audio()
    modules = feature_modules()
    total_est_days = sum(m["est_days"] for m in modules)
    pricing = pricing_tiers(total_est_days)
    voice_stats = scan_voice_workbook()

    wb = Workbook()

    # --- Sheet 1: 项目概览 ---
    ws = wb.active
    ws.title = "01-项目概览"
    overview = [
        ["统计项", "数值", "说明"],
        ["统计日期", datetime.now().strftime("%Y-%m-%d %H:%M"), ""],
        ["项目名称", ver.get("product", "My project (2)"), ""],
        ["引擎版本", ver.get("engine", ""), "团结引擎/Unity"],
        ["团结引擎版本", ver.get("tuanjie", ""), ""],
        ["项目路径", str(ROOT), ""],
        ["Assets文件数", assets["file_count"], ""],
        ["Assets体积(GB)", round(assets["total_mb"] / 1024, 2), "不含Library"],
        ["C#文件总数", cs["total_files"], "Assets目录"],
        ["C#代码总行数", cs["total_lines"], ""],
        ["自研C#文件数", cs["custom_files"], "排除TMP/Supercyan/KriptoFX等"],
        ["自研代码行数", cs["custom_lines"], ""],
        ["Editor脚本数", cs["editor_files"], ""],
        ["Runtime脚本数", cs["runtime_files"], ""],
        ["Build场景总数", len(scenes), "含禁用"],
        ["Build启用场景", sum(1 for s in scenes if s["enabled"]), "可打包场景"],
        ["主游戏场景(Assets/scene)", len(list((ASSETS / "scene").glob("*.unity"))) + len(list((ASSETS / "scene").glob("*.scene"))), "不含Backups"],
        ["3D模型FBX", sum(1 for m in models if m["format"] == "fbx"), ""],
        ["3D模型GLB", sum(1 for m in models if m["format"] == "glb"), ""],
        ["Prefab数量", assets["by_ext"].get(".prefab", 0), ""],
        ["材质mat数量", assets["by_ext"].get(".mat", 0), ""],
        ["MP3音频数量", assets["by_ext"].get(".mp3", 0), "含配音BGM"],
        ["配音文案表(总行)", sum(v.get("approx_rows", 0) for v in voice_stats), "剧情配音文案.xlsx"],
        ["剧情DOCX(M0-M5)", sum(r["docx_count"] for r in content1), "content1目录"],
        ["UPM包数量", len(packages), "Packages/manifest.json"],
        ["参考总人日(重置成本)", total_est_days, "见08-报价测算"],
        ["保守报价区间(万元)", f"{pricing[0]['成果移交价建议(元)']/10000:.1f} - {pricing[0]['含1年维护价建议(元)']/10000:.1f}", "见09-报价建议"],
        ["标准报价区间(万元)", f"{pricing[1]['成果移交价建议(元)']/10000:.1f} - {pricing[1]['含1年维护价建议(元)']/10000:.1f}", ""],
    ]
    write_table(ws, overview[0], overview[1:], [28, 22, 50])

    # --- Sheet 2: 功能模块 ---
    ws2 = wb.create_sheet("02-功能模块清单")
    mod_rows = [[m["module"], m["desc"], m["cs_area"], m["est_days"], m["complexity"], m["deliverable"]] for m in modules]
    mod_rows.append(["合计", "", "", total_est_days, "", ""])
    write_table(ws2, ["模块", "功能描述", "代码/资产区域", "参考人日", "复杂度", "交付物"], mod_rows, [18, 42, 22, 10, 8, 28])

    # --- Sheet 3: 代码按模块 ---
    ws3 = wb.create_sheet("03-代码统计-模块")
    mod_stats = sorted(cs["by_module"].items(), key=lambda x: -x[1]["lines"])
    mod_code_rows = [[k, v["files"], v["lines"], round(v["size_kb"], 1)] for k, v in mod_stats]
    write_table(ws3, ["模块/目录", "文件数", "代码行数", "体积(KB)"], mod_code_rows, [28, 10, 12, 12])

    # --- Sheet 4: 代码明细(Top200) ---
    ws4 = wb.create_sheet("04-代码明细Top200")
    top_cs = sorted(cs["rows"], key=lambda r: -r["lines"])[:200]
    cs_rows = [[r["file"], r["module"], r["lines"], r["kb"], r["editor"], r["third_party"]] for r in top_cs]
    write_table(ws4, ["相对路径", "模块", "行数", "KB", "Editor", "第三方"], cs_rows, [48, 20, 8, 8, 8, 8])

    # --- Sheet 5: 场景清单 ---
    ws5 = wb.create_sheet("05-场景清单")
    scene_rows = [[s["name"], s["path"], "是" if s["enabled"] else "否", "主场景" if s["path"].startswith("Assets/scene") else "插件/示例"] for s in scenes]
    write_table(ws5, ["场景名", "路径", "Build启用", "分类"], scene_rows, [32, 58, 10, 14])

    # --- Sheet 6: 资产按类型 ---
    ws6 = wb.create_sheet("06-资产按扩展名")
    ext_rows = []
    for ext, cnt in sorted(assets["by_ext"].items(), key=lambda x: -x[1]):
        mb = assets["by_ext_mb"][ext]
        ext_rows.append([ext, cnt, round(mb, 2), round(mb / 1024, 3)])
    write_table(ws6, ["扩展名", "数量", "体积(MB)", "体积(GB)"], ext_rows, [14, 10, 12, 10])

    # --- Sheet 7: 资产按目录 ---
    ws7 = wb.create_sheet("07-资产按目录")
    top_rows = []
    for top, cnt in sorted(assets["by_top"].items(), key=lambda x: -assets["by_top_mb"][x[0]]):
        mb = assets["by_top_mb"][top]
        top_rows.append([top, cnt, round(mb, 2), round(mb / 1024, 3)])
    write_table(ws7, ["一级目录", "文件数", "体积(MB)", "体积(GB)"], top_rows, [28, 10, 12, 10])

    # --- Sheet 8: 报价测算 ---
    ws8 = wb.create_sheet("08-报价测算")
    price_rows = [[p["测算口径"], p["参考人日"], p["单价"], p["重置成本(元)"], p["成果移交价建议(元)"], p["含部署培训价建议(元)"], p["含1年维护价建议(元)"]] for p in pricing]
    write_table(ws8, ["测算口径", "参考人日", "单价(元/人日)", "重置成本(元)", "成果移交价(元)", "含部署培训(元)", "含1年维护(元)"], price_rows, [18, 10, 14, 16, 16, 16, 16])
    note_row = ws8.max_row + 2
    ws8.cell(note_row, 1, "说明：重置成本=若从零重做所需人力；学校采购成交价通常为重置成本的45%-65%，视是否含服务器、运维、定制而定。")
    ws8.merge_cells(start_row=note_row, start_column=1, end_row=note_row, end_column=7)

    # --- Sheet 9: 报价建议 ---
    ws9 = wb.create_sheet("09-报价建议")
    std = pricing[1]
    rec = [
        ["报价档位", "建议金额(万元)", "包含内容", "适用场景"],
        ["基础移交版", round(std["成果移交价建议(元)"] / 10000, 1), "完整工程源码+资产+Build说明+1次现场培训(半天)", "学校已有技术团队接手维护"],
        ["标准交付版", round(std["含部署培训价建议(元)"] / 10000, 1), "基础版+服务器部署协助+账号后台对接文档+3个月Bug修复", "学校信息化部门采购教学/展示用"],
        ["精品服务版", round(std["含1年维护价建议(元)"] / 10000, 1), "标准版+1年技术支持+小功能迭代(≤15人日/年)+实机活动保障1次", "校庆/招生宣传重点项目的长期运营"],
        ["授权使用版(年费)", round(std["成果移交价建议(元)"] / 10000 * 0.15, 1), "校内使用授权不含源码转让，按年续费", "预算有限、仅需校内体验"],
    ]
    write_table(ws9, rec[0], rec[1:], [14, 14, 48, 32])
    ws9.cell(ws9.max_row + 2, 1, "综合建议：对本项目体量（约8.4万行自研代码、28+场景、完整剧情与联机/宿舍/载具系统），面向学校客户的合理成交区间建议为 "
                              f"{pricing[0]['成果移交价建议(元)']/10000:.0f}-{pricing[1]['含1年维护价建议(元)']/10000:.0f} 万元；推荐对外报价锚点：标准交付版约 {std['含部署培训价建议(元)']/10000:.1f} 万元。")
    ws9.merge_cells(start_row=ws9.max_row, start_column=1, end_row=ws9.max_row, end_column=4)

    # --- Sheet 10: 第三方依赖 ---
    ws10 = wb.create_sheet("10-第三方依赖")
    pkg_rows = [[p["id"], p["version"]] for p in sorted(packages, key=lambda x: x["id"])]
    write_table(ws10, ["包ID", "版本"], pkg_rows, [42, 14])

    # --- Sheet 11: 剧情章节 ---
    ws11 = wb.create_sheet("11-剧情章节content1")
    c1_rows = [[r["chapter"], r["docx_count"], r["files"]] for r in content1]
    write_table(ws11, ["章节", "DOCX数量", "文件示例"], c1_rows, [10, 10, 80])

    # --- Sheet 12: NPC模型 ---
    ws12 = wb.create_sheet("12-剧情NPC模型")
    npc_rows = [[n.get("display_name", ""), n.get("glb", ""), n.get("source", ""), n.get("exists", "")] for n in npcs]
    write_table(ws12, ["剧情角色名", "GLB文件", "来源", "文件存在"], npc_rows, [16, 28, 16, 10])

    # --- Sheet 13: 3D模型清单 ---
    ws13 = wb.create_sheet("13-3D模型清单")
    model_rows = [[m["name"], m["format"], m["folder"], m["mb"]] for m in models]
    write_table(ws13, ["文件名", "格式", "目录", "MB"], model_rows, [36, 8, 42, 8])

    # --- Sheet 14: 配音文案统计 ---
    ws14a = wb.create_sheet("14-配音文案统计")
    if voice_stats:
        vrows = [[v.get("sheet", ""), v.get("approx_rows", 0), v.get("error", "")] for v in voice_stats]
    else:
        vrows = [["(未找到剧情配音文案.xlsx)", 0, ""]]
    write_table(ws14a, ["工作表", "约数据行数", "备注"], vrows, [24, 14, 40])

    # --- Sheet 15: 音频统计 ---
    ws14 = wb.create_sheet("15-音频统计")
    aud_rows = [[k, audio["by_folder"][k], round(audio["by_folder_mb"][k], 2)] for k in sorted(audio["by_folder"], key=lambda x: -audio["by_folder_mb"][x])]
    aud_rows.insert(0, ["合计", audio["total"], round(sum(audio["by_folder_mb"].values()), 2)])
    write_table(ws14, ["目录", "音频文件数", "体积(MB)"], aud_rows, [28, 12, 12])

    # --- Sheet 16: 报价分项明细 ---
    ws15 = wb.create_sheet("16-报价分项明细")
    rate = 1600
    detail_rows = []
    for m in modules:
        cost = m["est_days"] * rate
        detail_rows.append([m["module"], m["est_days"], rate, cost, round(cost / (total_est_days * rate), 4)])
    write_table(ws15, ["模块", "人日", "单价(元)", "小计(元)", "占重置成本比"], detail_rows, [22, 8, 10, 14, 12])
    for row in range(2, ws15.max_row + 1):
        ws15.cell(row, 4).number_format = MONEY_FMT
        ws15.cell(row, 5).number_format = PCT_FMT

    # --- Sheet 17: 风险与调价因素 ---
    ws17 = wb.create_sheet("17-风险与调价因素")
    risk_rows = [
        ["GUID部分丢失", "中", "工程内存在meta引用断裂风险", "交付前需GUID修复验收；可扣减2-5万人力或单列修复费"],
        ["需GameServer后端", "高", "默认依赖内网HTTP服务", "若学校无后端需另报价8-15万开发+部署"],
        ["LLM需本地GPU服务", "中", "智能NPC依赖Qwen等API", "不含硬件；可提供部署方案单列3-8万"],
        ["团结引擎绑定", "中", "Tuanjie 1.8.5非标准Unity", "学校环境需安装团结引擎；跨引擎迁移成本高"],
        ["资产版权", "高", "含Sketchfab/第三方插件", "报价应声明：校内使用授权；商用需核对素材许可"],
        ["实机性能", "中", "28GB场景对显卡有要求", "建议报价含1次实机优化与推荐配置清单"],
        ["多人联机", "低", "Mirror局域网方案", "公网联机需额外信令/中继服务"],
        ["配音完成度", "中", f"文案约{sum(v.get('approx_rows',0) for v in voice_stats)}行/MP3约{assets['by_ext'].get('.mp3',0)}个", "若配音未全完成按成品度折价或分期"],
    ]
    write_table(ws17, ["因素", "影响", "说明", "报价建议"], risk_rows, [18, 8, 36, 36])

    # Format money columns in sheet 8
    for row in range(2, ws8.max_row + 1):
        for col in (4, 5, 6, 7):
            ws8.cell(row, col).number_format = MONEY_FMT

    wb.save(OUTPUT)
    print(f"Saved: {OUTPUT}")
    print(f"Total est days: {total_est_days}")
    print(f"Standard delivery quote: {std['含部署培训价建议(元)']/10000:.1f} wan CNY")


if __name__ == "__main__":
    main()
