# -*- coding: utf-8 -*-
"""Generate LGU EXPLORER audio asset checklist xlsx."""

from pathlib import Path

from openpyxl import Workbook
from openpyxl.styles import Font, PatternFill, Alignment, Border, Side
from openpyxl.utils import get_column_letter

# Columns: 序号, 资产类型, 一级分类, 二级分类, 中文名称, 建议文件名, 存放路径,
#          触发时机/使用场景, 是否循环, 空间类型, 建议时长, 优先级, 是否已有, 制作人, 递交状态, 备注

def row(*args):
    return list(args)

assets = []

# ========== 命名规范说明（作为首条说明行之后的数据） ==========
# BGM: BGM_{场景英文名}_{时段}.mp3  例: BGM_undergarden_day.mp3
# SFX_UI: SFX_UI_{动作}.mp3
# SFX_Player: SFX_Player_{动作}_{材质?}.mp3
# SFX_System: SFX_System_{系统}_{动作}.mp3
# AMB: AMB_{环境}.mp3

def add(asset_type, cat1, cat2, cn_name, filename, path, trigger, loop, spatial, duration, priority, exists, note=""):
    assets.append(row(
        len(assets) + 1, asset_type, cat1, cat2, cn_name, filename, path,
        trigger, loop, spatial, duration, priority, exists, "", "", note
    ))

# ==================== A. 背景音乐 BGM ====================
bgm_scenes = [
    ("Loadingscene", "登录/启动场景", "钢琴或轻电子，校园期待感，镜头巡轨时播放"),
    ("undergarden_day", "主校区下园（日间）", "户外广场、道路、迎新点，开阔校园氛围"),
    ("undergarden_night", "主校区下园（夜间）", "昼夜系统切换后，安静夜色校园"),
    ("undergarden_patriotic", "国旗旗杆区域", "进入国旗区触发，爱国氛围（可原创或授权改编）"),
    ("uppergarden", "上园宿舍区", "宿舍外围、书院入口，温馨日常"),
    ("middlegarden", "中园音乐学院区", "音乐楼、跑道周边，略带艺术感"),
    ("lingcollege", "三大书院（A/B/C通用）", "书院庭院、公共休息区"),
    ("dorm", "个人寝室室内", "安静 Lo-fi / 居家氛围，家具布置时"),
    ("library", "图书馆（各楼层通用）", "安静阅读、学术氛围，音量偏低"),
    ("library_pool", "图书馆露台/水池边", "水声衬托的轻柔 ambient"),
    ("computerroom", "机房/档案室", "轻微电子低频，冷静专注"),
    ("classroom", "社团教室", "空教室氛围，课间感"),
    ("teachingroom", "教学楼教室", "课堂空场，轻微回声"),
    ("lecture_hall", "大礼堂/报告厅", "宏大空间感，庄重"),
    ("learning_room", "学习室", "安静自习氛围"),
    ("discussion_room", "讨论室（TD一楼）", "小组讨论区，轻松"),
    ("corridor", "走廊连通场景", "室内走廊脚步回声底噪"),
    ("cec", "CEC建筑", "特色建筑氛围"),
    ("eight_building", "八号楼区域", "宿舍楼氛围"),
    ("shaw", "Shaw楼", "特色建筑氛围"),
    ("muse", "博物馆C馆", "展览/文化参观氛围"),
    ("diligentia", "Diligentia书院", "书院特色氛围"),
    ("lingdrom", "体育场/道扬馆", "活力运动氛围"),
    ("minival", "迷你谷地", "户外自然小景"),
    ("bus_station", "公交站场景", "城市交通轻氛围"),
    ("main_story_tension", "主线剧情紧张段", "叙事高潮配乐，按章节使用"),
    ("main_story_warm", "主线剧情温情段", "NPC对话、回忆段落"),
    ("main_story_credits", "主线完结片尾", "抒情致谢，配合滚动字幕"),
    ("multiplayer_overlay", "联机游戏中（可选）", "轻量不抢语音，可静音设计"),
]

for scene, cn, trigger in bgm_scenes:
    fn = f"BGM_{scene}.mp3"
    exists = "是" if scene in ("uppergarden",) else "否"
    if scene == "uppergarden":
        fn = "BGM_uppergarden.mp3（现有: 1(3).mp3，建议重命名）"
        exists = "部分"
    if scene == "undergarden_patriotic":
        exists = "部分"
        note = "现有《我和我的祖国》引用，Resources可能缺失"
    elif scene == "main_story_credits":
        note = "主线全部完成后触发"
    else:
        note = ""
    add("BGM", "背景音乐", "场景BGM", f"{cn}背景音乐", fn,
        "Assets/Music/BGM/", trigger, "是", "2D", "2-4分钟", "P0" if scene in ("Loadingscene","undergarden_day","uppergarden","dorm","library") else "P1",
        exists, note)

# ==================== B. 环境音 Ambience ====================
amb_items = [
    ("wind", "风声", "户外主校区开阔区域", "否", "30s-1min"),
    ("rain_light", "小雨", "雨天/水池边", "是", "1-2min"),
    ("birds_day", "白天鸟鸣", "校园绿化区域", "是", "1-2min"),
    ("birds_night", "夜晚虫鸣", "夜间户外", "是", "1-2min"),
    ("campus_distant", "校园远景人声", "广场、食堂附近远景", "是", "1-2min"),
    ("clock_bell", "远处钟声", "整点可选触发", "否", "5-10s"),
    ("library_hvac", "图书馆空调/安静底噪", "图书馆室内", "是", "1-2min"),
    ("library_page", "翻书页环境", "图书馆阅览区点缀", "否", "3-5s"),
    ("dorm_night", "寝室夜晚安静底噪", "dorm场景", "是", "1-2min"),
    ("pool_fountain", "喷泉/水池", "图书馆水池、喷泉", "是", "1-2min"),
    ("stadium_crowd", "体育场远景观众", "Lingdrom远景", "是", "1-2min"),
    ("indoor_corridor", "室内走廊混响底噪", "走廊、教学楼", "是", "1-2min"),
    ("traffic_distant", "远处道路交通", "校门口、公交站", "是", "1-2min"),
]

for key, cn, trigger, loop, dur in amb_items:
    exists_map = {"wind": "部分", "rain_light": "部分", "birds_day": "部分"}
    add("环境音", "环境音", "场景氛围", cn, f"AMB_{key}.mp3", "Assets/Music/AMB/",
        trigger, loop, "3D", dur, "P1", exists_map.get(key, "否"), "现有meta可能仅残留GUID")

# ==================== C. UI 通用点击/反馈（全覆盖） ====================
ui_items = [
    # 通用按钮
    ("click_default", "通用按钮点击", "所有可点击按钮的默认音效", "0.1-0.3s", "P0"),
    ("click_primary", "主要确认按钮", "确认、提交、进入等主操作", "0.2-0.4s", "P0"),
    ("click_secondary", "次要按钮", "取消、返回、关闭等", "0.1-0.3s", "P0"),
    ("click_tab", "标签页/选项卡切换", "设置档位、手机App切换、分页", "0.1-0.2s", "P0"),
    ("click_toggle_on", "开关打开", "画质选项、功能开关", "0.1-0.2s", "P1"),
    ("click_toggle_off", "开关关闭", "画质选项、功能开关", "0.1-0.2s", "P1"),
    ("click_slider", "滑条拖动", "时间/光照/音量滑条", "0.05-0.1s", "P1"),
    ("hover", "按钮悬停（PC）", "鼠标悬停可交互元素", "0.05-0.1s", "P2"),
    ("success", "操作成功", "通用成功反馈", "0.3-0.8s", "P0"),
    ("error", "操作失败/错误", "金币不足、无效操作、冲突提示", "0.3-0.6s", "P0"),
    ("notification", "通知弹出", "Toast、任务更新横幅", "0.2-0.5s", "P0"),
    # 手机UI
    ("phone_open", "打开手机", "F1或HUD打开手机界面", "0.2-0.4s", "P0"),
    ("phone_close", "关闭手机", "ESC或返回关闭", "0.2-0.4s", "P0"),
    ("phone_app_open", "打开App", "点击地图/任务/商城/背包/联机等", "0.1-0.3s", "P0"),
    ("phone_app_close", "关闭App", "从子App返回主屏", "0.1-0.2s", "P0"),
    # 面板
    ("panel_open", "面板打开", "地图、任务、背包、商城等大面板", "0.2-0.4s", "P0"),
    ("panel_close", "面板关闭", "关闭各类面板", "0.2-0.4s", "P0"),
    ("esc_back", "ESC层级返回", "逐层关闭UI", "0.1-0.2s", "P1"),
    # 登录
    ("login_click_enter", "点击进入游戏", "Loadingscene主按钮", "0.3-0.6s", "P0"),
    ("login_success", "登录成功", "账号验证通过", "0.5-1s", "P0"),
    ("login_fail", "登录失败", "账号密码错误", "0.3-0.6s", "P0"),
    ("register_confirm", "注册确认", "昵称/角色选择确认", "0.2-0.4s", "P0"),
    ("register_random_name", "随机昵称", "点击随机生成昵称", "0.1-0.3s", "P1"),
    ("character_select", "角色选择切换", "切换本科/研一/博一角色", "0.1-0.3s", "P0"),
    ("legal_doc_open", "法律文档弹窗打开", "LG U Hub协议", "0.2-0.3s", "P2"),
    ("legal_doc_close", "法律文档弹窗关闭", "同意/关闭协议", "0.1-0.2s", "P2"),
    # 设置
    ("settings_open", "设置面板打开", "画质/时间设置", "0.2-0.3s", "P0"),
    ("settings_close", "设置面板关闭", "", "0.2-0.3s", "P0"),
    ("quality_change", "画质档位切换", "Low/Medium/High/Ultra/Cinematic", "0.1-0.2s", "P1"),
    ("time_pause", "时间暂停", "昼夜系统暂停", "0.1-0.2s", "P2"),
    ("time_resume", "时间继续", "昼夜系统恢复", "0.1-0.2s", "P2"),
    ("time_reset", "时间重置", "重置到默认时刻", "0.2-0.3s", "P2"),
    ("logout", "登出账号", "手机设置内登出", "0.3-0.5s", "P1"),
    # 地图
    ("map_open", "打开大地图", "M键或手机地图App", "0.2-0.4s", "P0"),
    ("map_close", "关闭大地图", "", "0.2-0.3s", "P0"),
    ("map_select_point", "地图选点", "点击传送点", "0.1-0.2s", "P0"),
    ("map_confirm_teleport", "确认传送", "选点后确认传送", "0.3-0.5s", "P0"),
    # 任务
    ("quest_open", "打开任务面板", "J键", "0.2-0.3s", "P0"),
    ("quest_close", "关闭任务面板", "", "0.2-0.3s", "P0"),
    ("quest_page_turn", "任务日志翻页", "查看子任务列表", "0.1-0.2s", "P1"),
    # 画质曲线
    ("curve_panel_open", "画质曲线面板打开", "CurvePanelController", "0.2-0.3s", "P2"),
    ("curve_panel_close", "画质曲线面板关闭", "", "0.2-0.3s", "P2"),
    # 键位绑定
    ("keybind_open", "键位绑定面板打开", "手机Custom1", "0.2-0.3s", "P2"),
    ("keybind_rebind", "重绑定按键", "点击重绑定", "0.1-0.2s", "P2"),
    ("keybind_conflict", "键位冲突提示", "绑定冲突时", "0.3-0.5s", "P2"),
    # 死亡复活
    ("death_panel_open", "死亡界面出现", "HP归零", "0.5-1s", "P1"),
    ("revive_click", "复活按钮点击", "点击复活", "0.3-0.5s", "P1"),
    # 制作人员名单
    ("credits_trigger", "触发制作人员名单", "花坛等交互点", "0.3-0.5s", "P2"),
]

for key, cn, trigger, dur, pri in ui_items:
    add("音效", "UI音效", "界面交互", cn, f"SFX_UI_{key}.mp3", "Assets/Music/SFX/UI/",
        trigger, "否", "2D", dur, pri, "否")

# ==================== D. 玩家行为音效 ====================
player_items = [
    ("walk_concrete_01", "水泥地走路1", "主校区道路行走", "是", "0.5-1s", "P0", "部分"),
    ("walk_concrete_02", "水泥地走路2", "交替防重复", "是", "0.5-1s", "P0", "部分"),
    ("walk_grass_01", "草地走路1", "绿化区域", "是", "0.5-1s", "P0", "部分"),
    ("walk_grass_02", "草地走路2", "交替", "是", "0.5-1s", "P0", "部分"),
    ("walk_stair_01", "楼梯脚步1", "上下楼梯", "是", "0.5-1s", "P0", "部分"),
    ("walk_stair_02", "楼梯脚步2", "交替", "是", "0.5-1s", "P0", "部分"),
    ("walk_indoor_01", "室内地板走路1", "寝室、教室、图书馆", "是", "0.5-1s", "P1", "否"),
    ("walk_indoor_02", "室内地板走路2", "交替", "是", "0.5-1s", "P1", "否"),
    ("walk_crouch_01", "蹲走脚步", "Ctrl蹲伏移动", "是", "0.5-1s", "P1", "否"),
    ("run_01", "跑步1", "Shift疾跑", "是", "0.5-1s", "P0", "部分"),
    ("run_02", "跑步2", "交替", "是", "0.5-1s", "P0", "部分"),
    ("pant_low_stamina", "低体力喘气", "体力≤30持续", "是", "1-2s", "P0", "部分"),
    ("jump_takeoff", "跳跃起跳", "Space起跳", "否", "0.2-0.4s", "P0", "否"),
    ("jump_land_light", "轻落地", "普通高度落地", "否", "0.2-0.4s", "P0", "否"),
    ("jump_land_heavy", "重落地", "高处落地", "否", "0.3-0.5s", "P1", "否"),
    ("crouch_down", "蹲下", "Ctrl蹲下动作", "否", "0.2-0.3s", "P1", "否"),
    ("crouch_up", "站起", "取消蹲伏", "否", "0.2-0.3s", "P1", "否"),
    ("sit_down", "坐下", "椅子F交互坐下", "否", "0.3-0.5s", "P0", "否"),
    ("stand_up", "从椅子站起", "离开座位", "否", "0.3-0.5s", "P0", "否"),
    ("sit_idle_fidget", "坐姿小动作", "坐10秒随机动画（可选）", "否", "0.3-0.6s", "P2", "否"),
    ("eat_food", "吃东西", "背包使用食物", "否", "0.5-1s", "P0", "否"),
    ("drink", "喝水", "若食物分饮料类", "否", "0.5-1s", "P2", "否"),
    ("heal_restore", "恢复HP/体力", "食物生效时", "否", "0.3-0.6s", "P0", "否"),
    ("perspective_switch", "视角切换", "V键一/三人称", "否", "0.2-0.3s", "P1", "否"),
    ("stamina_depleted", "体力耗尽提示", "无法疾跑时", "否", "0.3-0.5s", "P1", "否"),
    ("fly_takeoff", "飞行升空", "调试飞行模式（若保留）", "否", "0.3-0.5s", "P2", "否"),
    ("fly_land", "飞行降落", "", "否", "0.3-0.5s", "P2", "否"),
    ("interact_prompt", "可交互提示", "靠近可F交互物体", "否", "0.1-0.3s", "P0", "否"),
    ("interact_confirm", "交互确认", "按F执行交互", "否", "0.2-0.4s", "P0", "否"),
]

for key, cn, trigger, loop, dur, pri, exists in player_items:
    add("音效", "玩家音效", "角色行为", cn, f"SFX_Player_{key}.mp3", "Assets/Music/SFX/Player/",
        trigger, loop, "3D", dur, pri, exists)

# ==================== E. 任务/经济系统 ====================
system_quest = [
    ("quest_update", "任务更新提示", "新任务/子任务出现", "0.3-0.6s"),
    ("quest_sub_complete", "子任务完成", "到达地点/事件达成", "0.5-1s"),
    ("quest_chain_complete", "任务链完成", "整条支线完成", "1-2s"),
    ("quest_main_complete", "主线任务完成", "主线节点完成", "1-2s"),
    ("coin_gain", "获得金币", "任务奖励到账", "0.3-0.6s"),
    ("coin_spend", "消费金币", "商城购买扣款", "0.3-0.5s"),
    ("tutorial_step", "新手教学步骤完成", "14步教程每步", "0.3-0.5s"),
]
for key, cn, trigger, dur in system_quest:
    add("音效", "系统音效", "任务/经济", cn, f"SFX_System_{key}.mp3", "Assets/Music/SFX/System/",
        trigger, "否", "2D", dur, "P0", "否")

# ==================== F. 对话系统 ====================
dialogue_items = [
    ("dialogue_page", "剧情对话翻页", "StoryDialogueUI翻页", "0.1-0.2s"),
    ("dialogue_option", "剧情选项选择", "分支选项点击", "0.1-0.3s"),
    ("dialogue_open", "打开剧情对话", "F触发剧情NPC", "0.2-0.3s"),
    ("dialogue_close", "关闭剧情对话", "对话结束", "0.2-0.3s"),
    ("llm_send", "LLM消息发送", "玩家发送消息", "0.1-0.3s"),
    ("llm_receive", "LLM消息接收", "NPC回复到达", "0.2-0.4s"),
    ("llm_open", "打开LLM对话", "F触发游走NPC", "0.2-0.3s"),
    ("llm_close", "关闭LLM对话", "", "0.2-0.3s"),
    ("npc_emotion_happy", "NPC开心情绪", "情绪动画happy", "0.3-0.6s"),
    ("npc_emotion_angry", "NPC生气情绪", "情绪动画angry", "0.3-0.6s"),
    ("npc_emotion_wave", "NPC挥手", "Wave动画", "0.2-0.4s"),
]
for key, cn, trigger, dur in dialogue_items:
    add("音效", "系统音效", "对话", cn, f"SFX_System_{key}.mp3", "Assets/Music/SFX/System/",
        trigger, "否", "2D", dur, "P0" if "dialogue" in key or "llm" in key else "P1", "否")

# ==================== G. 寝室商城/背包/放置 ====================
mall_items = [
    ("mall_open", "打开寝室商城", "T键", "0.2-0.4s"),
    ("mall_close", "关闭寝室商城", "", "0.2-0.3s"),
    ("mall_browse", "浏览商品", "切换商品列表/分类", "0.1-0.2s"),
    ("mall_buy_click", "点击购买", "购买按钮", "0.1-0.3s"),
    ("mall_buy_success", "购买成功", "扣款成功+Toast", "0.5-1s"),
    ("mall_buy_fail", "购买失败", "金币不足", "0.3-0.5s"),
    ("inventory_open", "打开背包", "B键", "0.2-0.4s"),
    ("inventory_close", "关闭背包", "", "0.2-0.3s"),
    ("inventory_select_item", "选择背包物品", "点击物品格子", "0.1-0.2s"),
    ("inventory_use_item", "使用物品", "消耗食物等", "0.2-0.4s"),
    ("place_mode_enter", "进入放置模式", "从背包选家具", "0.3-0.5s"),
    ("place_mode_exit", "退出放置模式", "取消布置", "0.2-0.3s"),
    ("place_preview_valid", "放置预览有效", "绿色可放置", "0.1-0.2s"),
    ("place_preview_invalid", "放置预览无效", "红色不可放置", "0.2-0.3s"),
    ("place_confirm", "确认放置家具", "确认落地", "0.3-0.5s"),
    ("place_rotate", "旋转家具", "放置模式微调", "0.1-0.2s"),
    ("place_cancel", "取消放置", "", "0.2-0.3s"),
    ("furniture_land", "家具落地", "放置到位", "0.3-0.5s"),
]
for key, cn, trigger, dur in mall_items:
    add("音效", "系统音效", "商城/背包/放置", cn, f"SFX_System_{key}.mp3", "Assets/Music/SFX/System/",
        trigger, "否", "2D", dur, "P0", "否")

# ==================== H. 传送/电梯 ====================
teleport_items = [
    ("teleport_normal", "普通传送", "大地图/传送点", "1-2s", "部分"),
    ("teleport_scifi", "科幻传送", "特殊传送点备选", "1-2s", "部分"),
    ("teleport_loading_start", "传送Loading开始", "加载遮罩出现", "0.5-1s", "否"),
    ("teleport_loading_end", "传送Loading结束", "进入新场景", "0.5-1s", "否"),
    ("teleport_video_audio", "传送视频音轨", "TeleportButton视频", "按视频", "否"),
    ("inscene_teleport", "场景内传送", "InSceneTeleportLoading", "0.5-1s", "否"),
    ("elevator_call", "呼叫电梯", "电梯口F", "0.3-0.5s", "否"),
    ("elevator_door_open", "电梯门开", "", "0.5-1s", "否"),
    ("elevator_door_close", "电梯门关", "", "0.5-1s", "否"),
    ("elevator_running", "电梯运行", "升降过程", "3-5s", "否"),
    ("elevator_arrive", "电梯到达", "到站叮声", "0.3-0.5s", "否"),
    ("elevator_ui_open", "电梯楼层面板打开", "选层UI", "0.2-0.3s", "否"),
    ("elevator_floor_select", "电梯选层确认", "点击楼层", "0.1-0.3s", "否"),
]
for item in teleport_items:
    key, cn, trigger, dur, exists = item
    add("音效", "系统音效", "传送/电梯", cn, f"SFX_System_{key}.mp3", "Assets/Music/SFX/System/",
        trigger, "否" if key != "elevator_running" else "是", "2D" if "ui" in key else "3D", dur, "P0" if "teleport" in key else "P1", exists)

# ==================== I. 载具/公交 ====================
vehicle_items = [
    ("vehicle_engine_idle", "引擎怠速", "上车后待机", "是"),
    ("vehicle_engine_accel", "引擎加速", "W加速", "是"),
    ("vehicle_brake", "刹车", "减速/停止", "否"),
    ("vehicle_collision", "碰撞", "撞墙/急停", "否"),
    ("vehicle_enter", "上车", "F进入载具", "否"),
    ("vehicle_exit", "下车", "离开载具", "否"),
    ("vehicle_horn", "鸣笛", "可选功能", "否"),
    ("vehicle_summon", "召唤载具", "长按F2召唤", "否"),
    ("vehicle_recall", "收回载具", "长按F2收回", "否"),
    ("vehicle_shop_open", "载具商店打开", "靠近商人F", "否"),
    ("vehicle_shop_close", "载具商店关闭", "", "否"),
    ("vehicle_select", "选车确认", "选车UI确认", "否"),
    ("bus_arrive", "巴士到站", "公交站生成巴士", "否"),
    ("bus_door", "巴士车门", "上下车", "否"),
    ("bus_depart", "巴士离站", "场景切换前", "否"),
    ("bus_engine_fade", "巴士引擎远去", "切场景过渡", "否"),
]
for key, cn, trigger, loop in vehicle_items:
    add("音效", "系统音效", "载具/交通", cn, f"SFX_System_{key}.mp3", "Assets/Music/SFX/System/",
        trigger, loop, "3D", "0.5-3s", "P1", "否")

# ==================== J. 联机/社交 ====================
multi_items = [
    ("coop_panel_open", "联机面板打开", "K键/手机联机App", "0.2-0.4s"),
    ("coop_panel_close", "联机面板关闭", "", "0.2-0.3s"),
    ("coop_connect_success", "连接成功", "加入房间", "0.5-1s"),
    ("coop_connect_fail", "连接失败", "房间错误", "0.3-0.6s"),
    ("coop_player_join", "玩家加入", "他人进入房间", "0.3-0.5s"),
    ("coop_player_leave", "玩家离开", "他人离开", "0.3-0.5s"),
    ("coop_disconnect", "断开连接", "网络断开", "0.3-0.5s"),
    ("chat_friend_open", "好友聊天打开", "C键", "0.2-0.3s"),
    ("chat_friend_close", "好友聊天关闭", "", "0.2-0.3s"),
    ("chat_friend_send", "好友消息发送", "发送按钮", "0.1-0.2s"),
    ("chat_friend_receive", "好友消息接收", "收到消息", "0.2-0.4s"),
    ("chat_proximity_open", "近距聊天打开", "靠近玩家F", "0.2-0.3s"),
    ("chat_proximity_close", "近距聊天关闭", "", "0.2-0.3s"),
    ("chat_proximity_send", "近距消息发送", "", "0.1-0.2s"),
    ("chat_proximity_receive", "近距消息接收", "", "0.2-0.4s"),
]
for key, cn, trigger, dur in multi_items:
    add("音效", "系统音效", "联机/聊天", cn, f"SFX_System_{key}.mp3", "Assets/Music/SFX/System/",
        trigger, "否", "2D", dur, "P1", "否")

# ==================== K. 旗帜竞拍 ====================
auction_items = [
    ("auction_open", "竞拍面板打开", "O键", "0.2-0.4s"),
    ("auction_close", "竞拍面板关闭", "", "0.2-0.3s"),
    ("auction_bid_click", "出价按钮", "点击出价", "0.1-0.3s"),
    ("auction_bid_success", "出价成功", "出价被接受", "0.3-0.5s"),
    ("auction_outbid", "被超越", "他人更高出价", "0.3-0.5s"),
    ("auction_leading", "当前领先", "成为最高出价者", "0.3-0.5s"),
    ("auction_win", "竞拍获胜", "周期结束获胜", "1-2s"),
    ("auction_lose", "竞拍失败", "未中标", "0.5-1s"),
    ("auction_color_pick", "旗帜颜色选择", "自定义颜色", "0.1-0.2s"),
]
for key, cn, trigger, dur in auction_items:
    add("音效", "系统音效", "旗帜竞拍", cn, f"SFX_System_{key}.mp3", "Assets/Music/SFX/System/",
        trigger, "否", "2D", dur, "P2", "否")

# ==================== L. 水体/环境交互 ====================
water_items = [
    ("water_footstep", "踩水", "走过浅水", "0.3-0.5s"),
    ("water_splash_small", "小溅起", "轻踩水面", "0.3-0.5s"),
    ("water_splash_large", "大溅起", "跳入/重踩", "0.5-1s"),
    ("water_ripple", "水面涟漪", "LiquidWaterInteractiveRipples", "0.2-0.4s"),
]
for key, cn, trigger, dur in water_items:
    add("音效", "环境音效", "水体", cn, f"SFX_World_{key}.mp3", "Assets/Music/SFX/World/",
        trigger, "否", "3D", dur, "P1", "否", "KriptoFX RippleCreator接口已预留")

# ==================== M. 特殊/预留 ====================
special_items = [
    ("death_stinger", "死亡", "HP归零", "1-2s", "P1"),
    ("revive", "复活", "复活成功", "0.5-1s", "P1"),
    ("main_story_credits_start", "片尾开始", "主线完结", "2-3s", "P1"),
    ("save_sync", "存档同步", "服务器同步成功", "0.3-0.5s", "P2"),
    ("save_sync_fail", "存档同步失败", "", "0.3-0.5s", "P2"),
]
for key, cn, trigger, dur, pri in special_items:
    add("音效", "系统音效", "特殊事件", cn, f"SFX_System_{key}.mp3", "Assets/Music/SFX/System/",
        trigger, "否", "2D", dur, pri, "否")

# ==================== Build Workbook ====================
wb = Workbook()

# Sheet 1: 命名规范
ws0 = wb.active
ws0.title = "命名规范"
rules = [
    ["《LGU EXPLORER / 未名之海》音频资产命名与递交规范"],
    [""],
    ["一、文件格式", "统一 MP3，采样率 44100Hz，比特率 192kbps 及以上；UI音效建议单声道，BGM立体声"],
    ["二、命名规则"],
    ["资产类型", "命名格式", "示例"],
    ["背景音乐", "BGM_{场景英文名}.mp3", "BGM_undergarden_day.mp3"],
    ["环境音", "AMB_{环境名}.mp3", "AMB_wind.mp3"],
    ["UI音效", "SFX_UI_{动作}.mp3", "SFX_UI_click_default.mp3"],
    ["玩家音效", "SFX_Player_{动作}_{材质}.mp3", "SFX_Player_walk_concrete_01.mp3"],
    ["系统音效", "SFX_System_{系统}_{动作}.mp3", "SFX_System_mall_buy_success.mp3"],
    ["世界音效", "SFX_World_{动作}.mp3", "SFX_World_water_splash_small.mp3"],
    [""],
    ["三、目录结构"],
    ["Assets/Music/BGM/", "所有背景音乐"],
    ["Assets/Music/AMB/", "所有环境音"],
    ["Assets/Music/SFX/UI/", "所有UI点击/面板音效"],
    ["Assets/Music/SFX/Player/", "玩家脚步/动作音效"],
    ["Assets/Music/SFX/System/", "任务/商城/传送/联机等系统音效"],
    ["Assets/Music/SFX/World/", "水体等世界交互音效"],
    [""],
    ["四、递交要求"],
    ["1. 文件名必须与「建议文件名」列完全一致（英文小写+下划线）"],
    ["2. 每完成一条在清单中填写「制作人」和「递交状态」"],
    ["3. UI类音效必须全覆盖：每个可点击按钮都要有对应或通用点击音"],
    ["4. 循环音效（脚步/环境/BGM）需提供无缝循环点"],
    ["5. 同一动作提供至少2个变体（如走路01/02）避免机械重复"],
    [""],
    ["五、优先级说明"],
    ["P0", "核心体验，首批必须完成"],
    ["P1", "重要功能，第二批完成"],
    ["P2", "锦上添花，时间允许再做"],
]
for r in rules:
    ws0.append(r)
ws0.column_dimensions['A'].width = 20
ws0.column_dimensions['B'].width = 45
ws0.column_dimensions['C'].width = 40
ws0['A1'].font = Font(bold=True, size=14)

# Sheet 2: 完整清单
ws = wb.create_sheet("音频资产清单")
headers = ["序号", "资产类型", "一级分类", "二级分类", "中文名称", "建议文件名", "存放路径",
           "触发时机/使用场景", "是否循环", "空间类型", "建议时长", "优先级", "是否已有", "制作人", "递交状态", "备注"]
ws.append(headers)

for a in assets:
    ws.append(a)

# Style header
header_fill = PatternFill(start_color="4472C4", end_color="4472C4", fill_type="solid")
header_font = Font(bold=True, color="FFFFFF", size=11)
thin = Side(style='thin', color='D9D9D9')
for col in range(1, len(headers) + 1):
    cell = ws.cell(row=1, column=col)
    cell.fill = header_fill
    cell.font = header_font
    cell.alignment = Alignment(horizontal='center', vertical='center', wrap_text=True)

# Column widths
widths = [6, 10, 12, 14, 22, 32, 28, 36, 8, 10, 12, 8, 8, 10, 10, 24]
for i, w in enumerate(widths, 1):
    ws.column_dimensions[get_column_letter(i)].width = w

# Freeze and filter
ws.freeze_panes = "A2"
ws.auto_filter.ref = f"A1:{get_column_letter(len(headers))}{len(assets)+1}"

# Priority color coding
priority_fills = {
    "P0": PatternFill(start_color="FCE4D6", end_color="FCE4D6", fill_type="solid"),
    "P1": PatternFill(start_color="E2EFDA", end_color="E2EFDA", fill_type="solid"),
    "P2": PatternFill(start_color="F2F2F2", end_color="F2F2F2", fill_type="solid"),
}
for row_idx in range(2, len(assets) + 2):
    pri = ws.cell(row=row_idx, column=12).value
    if pri in priority_fills:
        for col in range(1, len(headers) + 1):
            ws.cell(row=row_idx, column=col).fill = priority_fills[pri]
    for col in range(1, len(headers) + 1):
        ws.cell(row=row_idx, column=col).alignment = Alignment(vertical='center', wrap_text=True)

# Sheet 3: 统计汇总
ws2 = wb.create_sheet("统计汇总")
ws2.append(["分类", "数量"])
from collections import Counter
type_counts = Counter(a[1] for a in assets)
cat_counts = Counter(f"{a[2]}-{a[3]}" for a in assets)
pri_counts = Counter(a[11] for a in assets)
ws2.append(["", ""])
ws2.append(["按资产类型", ""])
for k, v in sorted(type_counts.items()):
    ws2.append([k, v])
ws2.append(["", ""])
ws2.append(["按一级分类", ""])
for k, v in sorted(cat_counts.items()):
    ws2.append([k, v])
ws2.append(["", ""])
ws2.append(["按优先级", ""])
for k in ["P0", "P1", "P2"]:
    ws2.append([k, pri_counts.get(k, 0)])
ws2.append(["", ""])
ws2.append(["总计", len(assets)])
ws2.column_dimensions['A'].width = 30
ws2.column_dimensions['B'].width = 10

out_path = str(Path(__file__).resolve().parent.parent / "egg" / "workbooks" / "LGU_EXPLORER_音频资产清单.xlsx")
wb.save(out_path)
print(f"Generated: {out_path}")
print(f"Total assets: {len(assets)}")
