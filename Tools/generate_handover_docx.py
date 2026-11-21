#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Generate 《未名之海》项目移交操作手册.docx for human handover."""

from __future__ import annotations

import sys
from datetime import date
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "egg" / "design_docs" / "未名之海_项目移交操作手册.docx"
OUT_FALLBACK = ROOT / "egg" / "design_docs" / "未名之海_项目移交操作手册_更新版.docx"


def _ensure_docx():
    try:
        from docx import Document
        from docx.enum.text import WD_ALIGN_PARAGRAPH
        from docx.oxml.ns import qn
        from docx.shared import Cm, Pt, RGBColor
        return Document, WD_ALIGN_PARAGRAPH, qn, Cm, Pt, RGBColor
    except ImportError:
        import subprocess

        subprocess.check_call([sys.executable, "-m", "pip", "install", "python-docx", "-q"])
        from docx import Document
        from docx.enum.text import WD_ALIGN_PARAGRAPH
        from docx.oxml.ns import qn
        from docx.shared import Cm, Pt, RGBColor

        return Document, WD_ALIGN_PARAGRAPH, qn, Cm, Pt, RGBColor


def set_cell_shading(cell, hex_color: str):
    from docx.oxml import OxmlElement
    from docx.oxml.ns import qn

    shading = OxmlElement("w:shd")
    shading.set(qn("w:fill"), hex_color)
    cell._tc.get_or_add_tcPr().append(shading)


def add_table(doc, headers: list[str], rows: list[list[str]], col_widths_cm: list[float] | None = None):
    from docx.shared import Cm

    table = doc.add_table(rows=1, cols=len(headers))
    table.style = "Table Grid"
    hdr = table.rows[0].cells
    for i, h in enumerate(headers):
        hdr[i].text = h
        set_cell_shading(hdr[i], "D9E2F3")
        for p in hdr[i].paragraphs:
            for r in p.runs:
                r.bold = True
    for row in rows:
        cells = table.add_row().cells
        for i, val in enumerate(row):
            cells[i].text = str(val)
    if col_widths_cm:
        for i, w in enumerate(col_widths_cm):
            for row in table.rows:
                row.cells[i].width = Cm(w)
    doc.add_paragraph()
    return table


def build_document():
    Document, WD_ALIGN_PARAGRAPH, qn, Cm, Pt, RGBColor = _ensure_docx()
    doc = Document()

    # Default font for East Asian text
    style = doc.styles["Normal"]
    style.font.name = "Microsoft YaHei"
    style.font.size = Pt(11)
    style._element.rPr.rFonts.set(qn("w:eastAsia"), "Microsoft YaHei")

    # Title page
    t = doc.add_paragraph()
    t.alignment = WD_ALIGN_PARAGRAPH.CENTER
    r = t.add_run("《未名之海》\n项目移交操作手册")
    r.bold = True
    r.font.size = Pt(26)
    r.font.color.rgb = RGBColor(0x1F, 0x4E, 0x79)

    sub = doc.add_paragraph()
    sub.alignment = WD_ALIGN_PARAGRAPH.CENTER
    sub.add_run(f"版本：1.0　　生成日期：{date.today().isoformat()}\n")
    sub.add_run("适用对象：项目接任人（开发/运维）\n")
    sub.add_run("配套 AI 文档：egg/design_docs/AI_PROJECT_TAKEOVER.md")

    doc.add_page_break()

    # TOC placeholder
    doc.add_heading("目录", level=1)
    toc_items = [
        "一、项目概述",
        "二、环境与首次运行",
        "三、目录与模块地图",
        "四、客户端架构",
        "五、服务端架构",
        "六、未完成功能（接任人必读）",
        "七、工具链与 Editor 菜单",
        "八、构建与发布",
        "九、测试与已知问题",
        "十、一个月公测开发计划",
        "十一、附录（密钥交接清单）",
    ]
    for item in toc_items:
        doc.add_paragraph(item, style="List Number")
    doc.add_page_break()

    # Chapter 1
    doc.add_heading("一、项目概述", level=1)
    doc.add_paragraph(
        "《未名之海》是一款校园开放世界叙事 RPG，玩家在大学场景中探索、完成任务、"
        "社交联机、装饰寝室，并体验都市传说式支线。工程使用 Unity 2022.3.62t7（团结引擎 1.8.5），"
        "配套 Python FastAPI 终端服务器与 Mirror 局域网联机。"
    )
    doc.add_heading("1.1 当前完成度", level=2)
    add_table(
        doc,
        ["模块", "状态", "说明"],
        [
            ["客户端主体玩法", "基本完成", "移动、地图、任务、商城、联机、手机 UI、载具等可运行"],
            ["服务端 API", "功能完整", "账号、存档、好友、聊天、联机信令、竞拍、寝室布局等"],
            ["智能 Agent", "未交付", "LLM 代码存在，无统一编排、无场景实例、手机格未接入"],
            ["旗帜竞拍", "半完成", "前后端逻辑齐全，场景中未放置旗帜实体"],
            ["每日任务系统", "未开发", "无按日刷新任务与稳定金币产出链路"],
            ["支线与小游戏", "未开发", "计划公测后通过内容热更新分批上线"],
            ["知识问答 PK", "未开发", "计划公测后热更新，非公测阻塞项"],
            ["场景改造", "进行中", "undergarden / 花园区待验收改造"],
            ["安全与压测", "未做", "CORS 宽松、无渗透/高并发测试记录"],
        ],
        [4, 3, 9],
    )

    doc.add_heading("1.2 已知风险", level=2)
    risks = [
        "客户端可能存在少量未修复 Bug，需接任人建立问题清单。",
        "服务端 allow_origins=[\"*\"]，公测前必须收紧并配置 HTTPS。",
        "SQLite 单文件数据库，高并发下需监控锁与备份策略。",
        "LLM 依赖外部 API（千问/DashScope 或自建 vLLM），需预算与限流。",
        "Build Settings 中含多个 Backups 场景条目，发布前应清理。",
        "竞拍使用寝室金币 dorm_gold，需确认新用户金币来源足够体验。",
        "每日任务系统尚未实现，公测前建议完成以保障金币经济闭环。",
    ]
    for r in risks:
        doc.add_paragraph(r, style="List Bullet")

    # Chapter 2
    doc.add_heading("二、环境与首次运行（约 30 分钟）", level=1)
    doc.add_heading("2.1 开发环境", level=2)
    add_table(
        doc,
        ["组件", "版本/要求"],
        [
            ["Unity", "2022.3.62t7（团结 1.8.5）"],
            ["Python", "3.10+（服务端，推荐 3.12）"],
            ["操作系统", "Windows 10/11（客户端构建）；服务器可用 Linux"],
            ["网络", "局域网或公网；HTTP 8765 + 联机 TCP 7777"],
        ],
        [5, 10],
    )

    doc.add_heading("2.2 启动服务端", level=2)
    steps = [
        "进入 Assets/code/GameServer/，双击 start_server.bat（建议右键「以管理员身份运行」以开放防火墙）。",
        "脚本自动创建 .venv、安装 requirements.txt、启动 Uvicorn 监听 0.0.0.0:8765。",
        "浏览器访问 http://本机IP:8765/health 应返回健康状态。",
        "运行时数据写入项目根目录 GameServerRuntime/（game.db、日志、热更新包等）。",
        "详细联机部署见 egg/design_docs/联机与服务器配置指南.md。",
    ]
    for i, s in enumerate(steps, 1):
        doc.add_paragraph(f"{i}. {s}")

    doc.add_heading("2.3 配置客户端服务器地址", level=2)
    doc.add_paragraph(
        "任选其一：（1）修改 Assets/code/GameServerEndpoint.cs 中 DefaultBaseUrl；"
        "（2）场景中 GameServerSync 组件的 Base Url 字段；"
        "（3）运行时 PlayerPrefs 键 GameServer_BaseUrl_Override。"
        f"当前代码默认：http://192.168.1.113:8765"
    )

    doc.add_heading("2.4 本机联调清单", level=2)
    add_table(
        doc,
        ["按键", "功能", "预期"],
        [
            ["P", "登录面板", "注册/登录成功，token 写入 PlayerPrefs"],
            ["—", "进入世界", "Loadingscene 进入 undergarden，位置同步"],
            ["J", "任务", "任务日志打开，金币/进度可见"],
            ["T", "寝室商城", "靠近商户或手机 Shop 打开商城"],
            ["K", "联机", "房间面板打开，主机可启动 Mirror"],
            ["C", "好友聊天", "侧栏显示好友与消息"],
            ["O", "旗帜竞拍", "面板打开（无旗帜时提示暂无 slot）"],
            ["M", "地图", "大地图打开并可传送"],
        ],
        [2, 4, 8],
    )

    # Chapter 3
    doc.add_heading("三、目录与模块地图", level=1)
    doc.add_heading("3.1 根目录说明", level=2)
    add_table(
        doc,
        ["路径", "说明"],
        [
            ["Assets/", "Unity 资源与全部 C# 脚本"],
            ["Assets/code/", "核心玩法代码"],
            ["Assets/scene/", "关卡场景文件"],
            ["Assets/PhoneUI/", "游戏内手机 UI"],
            ["Assets/Carsystem/", "载具与车行"],
            ["Assets/content1/", "剧情源文本（任务系统引用）"],
            ["GameServerRuntime/", "服务端运行时数据（勿放入 Assets）"],
            ["egg/", "策划表、设计文档、场景备份（不参与构建）"],
            ["Tools/", "离线 Python 工具（配音导入、音频审计等）"],
            ["Library/", "Unity 缓存，勿提交版本库"],
        ],
        [5, 10],
    )

    doc.add_heading("3.2 核心模块", level=2)
    add_table(
        doc,
        ["模块", "路径", "要点"],
        [
            ["账号同步", "Assets/code/GameServerSync.cs", "登录、每秒上报位置、跨场景存档"],
            ["任务系统", "Assets/code/GameQuestSystem/", "主线/支线、HTTP 存档、热更新剧情"],
            ["联机", "Assets/code/Multiplayer/", "Mirror 7777、好友、房间号、协作任务"],
            ["寝室商城", "Assets/code/DormMall/", "购买、背包、家具摆放、金币"],
            ["LLM NPC", "Assets/code/LLMcode/", "千问兼容 API、流式对话、情绪动画"],
            ["旗帜竞拍", "Assets/code/FlagAuction/", "O 键面板、全服同步 slot"],
            ["LguHub", "Assets/code/LguHub/", "图书馆场景 P 键社区面板"],
            ["本地化", "Assets/code/Localization/", "中英文切换"],
            ["载具", "Assets/Carsystem/", "驾驶、车行 NPC、3D 预览"],
            ["手机 UI", "Assets/PhoneUI/", "九宫格应用路由"],
        ],
        [3, 5, 7],
    )

    doc.add_heading("3.3 已启用场景（Build Settings）", level=2)
    scenes = [
        "Loadingscene", "uppergarden", "lingcollegeB/C/A", "cec", "classroom1",
        "corridor", "computerroom", "discussion room (TD一楼）", "dlilgentiac",
        "eightA", "undergarden", "learning room", "eigthB", "lecture hall(TB)",
        "middlegarden", "teachingroomA", "museC", "bus", "minival", "shawA",
        "dorm", "libraryfloor1", "libraryfloor4", "libraryfloor2",
    ]
    doc.add_paragraph("、".join(scenes))
    doc.add_paragraph(
        "默认出生场景：undergarden（服务端 DEFAULT_SPAWN_SCENE）。"
        "公测重点改造：undergarden、uppergarden、middlegarden。"
    )

    # Chapter 4
    doc.add_heading("四、客户端架构", level=1)
    doc.add_paragraph(
        "客户端以 GameServerSync 为网络枢纽（通常 DontDestroyOnLoad）。"
        "登录成功后每秒 POST /api/position 上报场景与坐标；场景加载时按当前场景名拉取独立存档并传送。"
        "任务与金币通过 QuestSystemManager 与 /api/player_state 双向同步。"
    )
    doc.add_heading("4.1 关键流程", level=2)
    flows = [
        "冷启动（可选）：GET /api/connection_check → 若 cold_session 则进 Loadingscene。",
        "注册/登录：POST /api/register 或 /api/login → token 存 PlayerPrefs。",
        "进入世界：拉取 spawn/position → 可能 LoadScene → 锁定输入直至同步完成。",
        "联机：HTTP 信令邀请 → 主机 LanCoopNetworkManager 监听 7777 → 客机连接。",
        "热更新：ClientVersionUpdateService 对比 manifest.json；ContentHotUpdateService 拉剧情 JSON。",
    ]
    for f in flows:
        doc.add_paragraph(f, style="List Bullet")

    doc.add_heading("4.2 手机 UI", level=2)
    doc.add_paragraph(
        "PhoneUI 通过 PhoneAppRouter 桥接地图、任务、商城、背包、联机、账号等。"
        "Custom1=键位设置，Custom2=登出（公测前改为 AI 助手入口）。"
        "详见 egg/design_docs/PhoneUI_README.md。"
    )

    # Chapter 5
    doc.add_heading("五、服务端架构", level=1)
    doc.add_paragraph(
        "FastAPI 应用位于 Assets/code/GameServer/backend/server.py，"
        "默认端口 8765，数据库 SQLite 位于 GameServerRuntime/game.db。"
    )
    doc.add_heading("5.1 管理界面", level=2)
    add_table(
        doc,
        ["URL", "用途"],
        [
            ["/admin", "管理后台（需 GAME_ADMIN_TOKEN）"],
            ["/control", "控制面板"],
            ["/static/portal", "登录门户页"],
            ["/docs", "FastAPI 自动文档（开发用）"],
        ],
        [5, 10],
    )

    doc.add_heading("5.2 API 分组摘要", level=2)
    add_table(
        doc,
        ["分组", "主要端点", "说明"],
        [
            ["健康", "GET /health, /api/connection_check", "存活与冷启动判定"],
            ["账号", "POST /api/register, /api/login, /api/me/*", "本地账号体系"],
            ["SSO", "POST /api/sso/*, GET /api/lguhub/session", "LguHub 学校账号桥接"],
            ["存档", "GET/POST /api/player_state, /api/position", "任务金币与坐标"],
            ["社交", "/api/friends/*, /api/chat/*", "好友与私聊"],
            ["近聊", "/api/proximity-chat/*", "同场景近距离聊天"],
            ["联机", "/api/coop/*, /api/shard/*", "邀请、主机登记、房间号"],
            ["竞拍", "/api/flag_auction/*", "全服旗帜竞拍与展示"],
            ["寝室", "/api/dorm_layout*, /api/dorm_room", "家具布局持久化"],
            ["内容", "/api/content/*", "剧情热更新与商城目录"],
        ],
        [2.5, 5, 7],
    )

    doc.add_heading("5.3 环境变量", level=2)
    add_table(
        doc,
        ["变量", "说明", "填写"],
        [
            ["GAME_SSO_SECRET", "SSO 签名密钥", "________________"],
            ["LGUHUB_BASE_URL", "LguHub 根地址", "https://lguhub.cn"],
            ["GAME_ADMIN_TOKEN", "管理接口令牌", "________________"],
            ["GAME_SERVER_RUNTIME_DIR", "可选，覆盖运行时目录", "默认 GameServerRuntime/"],
        ],
        [4, 5, 5],
    )

    # Chapter 6
    doc.add_heading("六、未完成功能（接任人必读）", level=1)

    doc.add_heading("6.1 智能 Agent 系统", level=2)
    doc.add_paragraph(
        "目标：手机内 AI 助手 + 场景 LLM 对话 NPC，统一人格、短期记忆与任务上下文。"
    )
    doc.add_heading("已有基础", level=3)
    bases = [
        "LLMChatClient_LocalQwen：OpenAI 兼容流式 API、情绪 JSON、对话记忆。",
        "LlmNpcApiSettings：集中配置 API Key、模型、人格 prompt（Assets/Settings/）。",
        "LlmWanderingNpcOneClickSetup：编辑器一键配置游走 NPC + F 对话 + 任务上报。",
        "LlmNpcQuestReporter / LlmWanderingNpcQuestPreset：支线对话进度联动。",
    ]
    for b in bases:
        doc.add_paragraph(b, style="List Bullet")

    doc.add_heading("待开发", level=3)
    todos_agent = [
        "新建 AgentOrchestrator：统一注入 QuestSystemManager 当前追踪任务摘要。",
        "新建 AgentSessionStore：手机与 NPC 共享对话历史（PlayerPrefs 或服务端）。",
        "修改 PhoneAppRouter.Custom2：从登出改为打开 AI 助手 UI。",
        "在 undergarden 放置 2–3 个 LLM 游走 NPC（需 NavMesh 烘焙）。",
        "（推荐）服务端 LLM 代理：隐藏 API Key、限流、审计日志。",
    ]
    for t in todos_agent:
        doc.add_paragraph(t, style="List Bullet")

    doc.add_heading("6.2 场景改造与旗帜竞拍", level=2)
    doc.add_paragraph(
        "目标场景：undergarden、uppergarden、middlegarden。"
    )
    doc.add_heading("竞拍系统现状", level=3)
    auction = [
        "客户端：FlagAuctionPanelController（O 键）、FlagAuctionStatePoller、FlagAuctionApi。",
        "服务端：flag_auction_slots 表，竞拍 60 秒/轮，出价扣 dorm_gold，胜者自定义文字颜色。",
        "缺口：场景中无任何 FlagAuctionSlotView 组件——需在旗帜物体上挂载并设置唯一 slotId。",
        "建议 slotId：undergarden_flag_01、uppergarden_flag_01、middlegarden_flag_01 等。",
    ]
    for a in auction:
        doc.add_paragraph(a, style="List Bullet")

    doc.add_heading("场景改造要点", level=3)
    scene_work = [
        "传送点：SceneTeleportEntryRegistry.asset 与 TeleportButton 对齐。",
        "undergarden：出生点周边、爱国音乐触发 UndergardenPatrioticMusicBootstrap。",
        "花园区：BGM/光照/植被剔除 VegetationDistanceOptimization 性能抽查。",
        "UI 标准化：Editor/GameplayUiStandardizeAllScenesSetup。",
    ]
    for s in scene_work:
        doc.add_paragraph(s, style="List Bullet")

    doc.add_heading("6.3 每日任务系统", level=2)
    doc.add_paragraph(
        "优先级：公测前建议完成（P0）。目标：每个自然日为用户派发若干任务，"
        "完成后发放寝室金币 dorm_gold，支撑商城消费与旗帜竞拍。"
    )
    daily = [
        "现状：QuestSystemManager 支持任务链与金币同步，但无「按日刷新」调度与专用 UI。",
        "服务端：需新增每日任务表与 API（建议 GET 当日任务列表、POST 领取奖励）；以服务器日期为准防刷。",
        "客户端：建议 DailyQuestManager + 任务日志/手机入口展示「今日任务」；完成条件复用现有 QuestSubTask 类型。",
        "配置：每日任务池可放 GameServerRuntime/content/ 或热更新 JSON，便于运营调数值。",
        "验收：新用户注册次日仍能获得任务；完成后金币写入 player_state 并与竞拍扣款一致。",
    ]
    for d in daily:
        doc.add_paragraph(d, style="List Bullet")

    doc.add_heading("6.4 支线与小游戏（热更新，非紧急）", level=2)
    doc.add_paragraph(
        "优先级：P2，公测后通过内容热更新分批上线，不阻塞 7/17 首发。"
    )
    side = [
        "策划来源：egg/design_docs/1.md 支线池（食堂线、巴士传说、社团选举等）。",
        "技术路径：ContentHotUpdateService 拉取 story_database.json patch，无需强制发新客户端包。",
        "小游戏可参考 FlagAuction 的计时循环与全服状态轮询模式；或独立场景 minival 等。",
        "建议每批 1–2 条支线 + 1 个小游戏玩法验证后再扩大内容量。",
    ]
    for s in side:
        doc.add_paragraph(s, style="List Bullet")

    doc.add_heading("6.5 知识问答模块（做题 + PK，热更新，非紧急）", level=2)
    doc.add_paragraph(
        "优先级：P2，公测后迭代。支持单人刷题与好友/匹配 PK，可作为校园知识向玩法。"
    )
    quiz = [
        "现状：工程中无 QuizSystem 模块，需从零新建客户端 UI 与服务端题库。",
        "客户端建议：Assets/code/QuizSystem/（题库展示、答题、PK 对战界面）；可占用 PhoneUI 新 App 格或独立 NPC 触发。",
        "服务端建议：quiz_questions、quiz_sessions、quiz_pk_matches 等表；REST 匹配与结算；可选与 dorm_gold/每日任务联动奖励。",
        "内容：题库 JSON 走 /api/content/bundle 热更新，便于教务/运营更新题目。",
        "联机 PK：可先用 HTTP 回合制（类似好友聊天轮询），高并发阶段再评估 WebSocket。",
    ]
    for q in quiz:
        doc.add_paragraph(q, style="List Bullet")

    # Chapter 7
    doc.add_heading("七、工具链与 Editor 菜单", level=1)
    add_table(
        doc,
        ["菜单/脚本", "用途"],
        [
            ["Tools → 联机 → 一键配置联机服务", "创建 Multiplayer_Services"],
            ["Tools → 联机 → Create Coop Player Prefab", "生成 CoopPlayer.prefab"],
            ["Tools → Quest System → 一键配置 LLM 游走 NPC", "选中模型一键配置"],
            ["Tools → Quest System → 配置千问 API", "生成 LlmNpcApiSettings"],
            ["Tools → 椅子交互 → 完整配置", "坐下动画与长椅交互"],
            ["Tools → 场景配置 → libraryfloor2 传送落点", "修正图书馆传送"],
            ["Tools/import_dialogue_voice.py", "从 Excel 导入配音"],
            ["Tools/export_dubbing_script.py", "导出配音文案"],
            ["Tools/audit_scene_bgm.py", "场景 BGM 审计"],
        ],
        [6, 9],
    )

    # Chapter 8
    doc.add_heading("八、构建与发布", level=1)
    build_items = [
        "Player Settings → Version 与 ClientBuildVersion.cs 保持一致。",
        "Build 前检查 EditorBuildSettings：移除 Backups 场景、禁用 Mirror Benchmark 示例场景。",
        "Windows 构建后，将 zip 上传至 GameServerRuntime/client_releases/ 并更新 manifest.json。",
        "当前活跃版本：0.0.1（forceUpdate: true）。公测目标版本建议：0.1.0。",
        "客户端启动时 ClientVersionUpdateGate 校验版本并按需热更新文件。",
    ]
    for b in build_items:
        doc.add_paragraph(b, style="List Bullet")

    # Chapter 9
    doc.add_heading("九、测试与已知问题", level=1)
    doc.add_heading("9.1 服务端", level=2)
    for item in [
        "未进行渗透测试与 OWASP 审计。",
        "未进行高并发压测（建议 locust/hey 压 /api/position、登录、竞拍轮询）。",
        "Token 无文档化过期轮换策略。",
        "多用户同一公网 IP 时 connection_check 冷启动判定偏粗。",
    ]:
        doc.add_paragraph(item, style="List Bullet")

    doc.add_heading("9.2 客户端", level=2)
    for item in [
        "任务脏存档自愈：QuestSystemManager 会重置异常进度。",
        "联机客机不可在寝室商城购买（仅房主扣币）。",
        "NavMesh 未烘焙时 LLM NPC 无法游走。",
        "Esc 被多系统共用，修改 UI 时注意 InteractionEscGate 互斥。",
    ]:
        doc.add_paragraph(item, style="List Bullet")

    doc.add_heading("9.3 日志位置", level=2)
    doc.add_paragraph("服务端：GameServerRuntime/server.log")
    doc.add_paragraph("客户端：Unity Console（开发构建可开 GameServerSync.verboseLogging）")

    # Chapter 10 - Month plan
    doc.add_heading("十、一个月公测开发计划", level=1)
    doc.add_paragraph(
        "周期：2026-06-17 至 2026-07-17（公测上线）。"
        "假设 1 名全职开发 + 原团队验收。"
    )

    doc.add_heading("第 1 周（6/17–6/23）：接手与 Agent 基础", level=2)
    add_table(
        doc,
        ["日期", "任务", "产出"],
        [
            ["D1–D2", "按本手册跑通服务端+客户端+联机；记录 Bug 清单", "环境问题文档"],
            ["D3", "Agent 技术方案：Orchestrator 接口、记忆存储、prompt 模板", "设计说明"],
            ["D4–D5", "实现 AgentContextProvider（注入 QuestSystemManager 上下文）", "代码"],
            ["D6–D7", "手机 AI 助手 MVP：Custom2 → 对话 UI", "M1 里程碑"],
        ],
        [2, 6, 4],
    )
    doc.add_paragraph("里程碑 M1（6/23）：手机可打开 AI 助手并完成带任务上下文的一轮对话。")

    doc.add_heading("第 2 周（6/24–6/30）：世界 NPC 与花园区", level=2)
    add_table(
        doc,
        ["日期", "任务", "产出"],
        [
            ["D8–D9", "undergarden 放置 2–3 个 LLM 游走 NPC + NavMesh", "场景"],
            ["D10", "AgentSessionStore：手机与 NPC 共享记忆", "代码"],
            ["D11–D12", "uppergarden / middlegarden 场景改造与性能抽查", "场景"],
            ["D13–D14", "花园区引导点复核、专属 Bug 修复", "测试报告"],
        ],
        [2, 6, 4],
    )
    doc.add_paragraph("里程碑 M2（6/30）：统一 Agent 可用；花园区稳定可玩。")

    doc.add_heading("第 3 周（7/1–7/7）：竞拍落地、每日任务与 undergarden 改造", level=2)
    add_table(
        doc,
        ["日期", "任务", "产出"],
        [
            ["D15", "三场景放置 FlagAuctionSlotView + 唯一 slotId", "场景"],
            ["D16", "竞拍全流程测试（金币→出价→自定义→全服同步）", "测试用例"],
            ["D17–D18", "undergarden 主场景改造验收", "场景"],
            ["D19–D20", "每日任务：服务端 API + DailyQuestManager + 今日任务 UI", "代码"],
            ["D21", "每日任务与竞拍/商城金币经济联调", "测试报告"],
        ],
        [2, 6, 4],
    )
    doc.add_paragraph(
        "里程碑 M3（7/7）：竞拍可玩；undergarden 改造完成。"
        "里程碑 M3b（7/10）：每日任务可领取并完成，金币产出稳定。"
    )

    doc.add_heading("第 4 周（7/8–7/14）：安全、压测、发布", level=2)
    add_table(
        doc,
        ["日期", "任务", "产出"],
        [
            ["D22", "CORS 收紧、admin token 轮换、输入校验审计", "安全清单"],
            ["D23", "压测 position/login/flag_auction/daily_quests", "压测报告"],
            ["D24", "LLM 代理（可选）+ 竞拍/每日任务 API 安全复核", "服务端"],
            ["D25", "P0/P1 Bug 清零；Build 清理", "稳定构建"],
            ["D26", "打包 0.1.0 + 上传 client_releases", "发布包"],
            ["D27–D28", "Nginx HTTPS 部署 + 公测公告", "生产环境"],
        ],
        [2, 6, 4],
    )
    doc.add_paragraph("里程碑 M4（7/14）：公测包与生产服务器就绪。")

    doc.add_heading("缓冲与上线（7/15–7/17）", level=2)
    for item in [
        "7/15：小范围灰度内测，监控 server.log 与客户端崩溃。",
        "7/16：热修复与回滚预案演练。",
        "7/17：公测正式上线。",
    ]:
        doc.add_paragraph(item, style="List Bullet")

    doc.add_heading("公测后路线图（热更新，非紧急）", level=2)
    doc.add_paragraph(
        "以下模块不纳入 7/17 公测阻塞项，建议在核心循环稳定后通过内容/服务端热更新迭代："
    )
    add_table(
        doc,
        ["模块", "优先级", "交付方式", "说明"],
        [
            ["支线剧情扩展", "P2", "/api/content/patch", "复用 QuestSystem + story_database"],
            ["小游戏玩法", "P2", "客户端小版本 + 内容包", "可参考竞拍/计时玩法模板"],
            ["知识问答（做题）", "P2", "QuizSystem + 题库 bundle", "单人刷题、错题本"],
            ["知识问答 PK", "P2", "服务端匹配 + 客户端 UI", "好友 PK 或异步对战"],
        ],
        [3, 2, 4, 6],
    )

    # Chapter 11
    doc.add_heading("十一、附录", level=1)
    doc.add_heading("11.1 密钥与账号交接清单（请填写）", level=2)
    add_table(
        doc,
        ["项目", "位置", "交接值（请填写）", "备注"],
        [
            ["游戏服务器公网地址", "GameServerEndpoint / DNS", "http://____________:8765", "所有客户端统一"],
            ["LLM API Key", "LlmNpcApiSettings.asset", "sk-________________", "勿提交 Git"],
            ["LLM API Base", "LlmNpcApiSettings", "________________", "DashScope 或自建"],
            ["GAME_SSO_SECRET", ".env.lguhub.local", "________________", "LguHub SSO"],
            ["GAME_ADMIN_TOKEN", "start_server.bat / 环境变量", "________________", "公测前务必轮换"],
            ["生产数据库备份", "GameServerRuntime/game.db", "路径：________________", "定期备份"],
            ["Unity 账号", "—", "________________", "构建权限"],
            ["域名与 SSL 证书", "—", "________________", "HTTPS"],
            ["原负责人联系方式", "—", "姓名：________ 电话：________", "技术答疑"],
            ["接任人", "—", "姓名：________ 电话：________", ""],
        ],
        [3.5, 4, 4, 4],
    )

    doc.add_heading("11.2 相关文档索引", level=2)
    add_table(
        doc,
        ["文档", "路径"],
        [
            ["AI 接管手册", "egg/design_docs/AI_PROJECT_TAKEOVER.md"],
            ["代码总 README", "egg/design_docs/README.md"],
            ["联机与服务器指南", "egg/design_docs/联机与服务器配置指南.md"],
            ["手机 UI 说明", "egg/design_docs/PhoneUI_README.md"],
            ["任务策划手册", "egg/design_docs/1.md"],
            ["寝室商城搭建", "egg/design_docs/搭建步骤.md"],
            ["载具系统", "egg/design_docs/carreadme.md"],
        ],
        [5, 10],
    )

    doc.add_paragraph()
    p = doc.add_paragraph("— 文档结束 —")
    p.alignment = WD_ALIGN_PARAGRAPH.CENTER

    OUT.parent.mkdir(parents=True, exist_ok=True)
    target = OUT
    try:
        doc.save(str(target))
    except PermissionError:
        target = OUT_FALLBACK
        doc.save(str(target))
    return target


if __name__ == "__main__":
    path = build_document()
    print(f"Generated: {path}")
