# -*- coding: utf-8 -*-
"""Generate game SFX checklist xlsx for Unity project."""
from pathlib import Path

from openpyxl import Workbook
from openpyxl.styles import Alignment, Font, PatternFill, Border, Side
from openpyxl.utils import get_column_letter

OUT = Path(__file__).resolve().parent.parent / "egg" / "workbooks" / "游戏音效补充清单.xlsx"

HEADERS = [
    "优先级",
    "分类",
    "音效名称",
    "触发时机 / 说明",
    "现有资源文件",
    "接入位置 / 脚本",
    "当前状态",
    "备注",
]

ROWS = [
    # --- 总览：铺开配置 ---
    ("P0", "铺开配置", "传送 whoosh", "地图 M 传送、同场景加载传送完成时", "普通传送.mp3 / 科幻传送.mp3", "MapSystem.teleportSound；InSceneTeleportLoading", "仅 uppergarden 已绑；其余 20+ 场景为空", "优先用普通传送；科幻点用科幻传送"),
    ("P0", "铺开配置", "走路脚步（水泥地）", "WASD 移动、Walk 状态", "水泥地走路1.mp3 / 水泥地走路2.mp3", "PlayerStepSound.walkClip", "仅 uppergarden 已挂组件", "建议绑到玩家预制体或运行时添加"),
    ("P0", "铺开配置", "跑步脚步", "Shift 疾跑、Run 状态", "跑步1.mp3 / 跑步2.mp3", "PlayerStepSound.runClip", "仅 uppergarden", ""),
    ("P0", "铺开配置", "喘气循环", "体力 ≤ 30", "喘气.mp3", "PlayerStepSound.pantClip", "仅 uppergarden", "循环播放"),
    ("P0", "铺开配置", "户外 BGM", "场景循环背景音乐", "1(3).mp3", "BackgroundMusic.bgmClip", "仅 uppergarden", "DontDestroy 可选"),
    ("P0", "铺开配置", "场景环境音", "进入场景淡入循环", "鸟叫.mp3 / 风.mp3 / 小雨.mp3", "SceneAmbientAudio.clip", "undergarden、lingcollegeB、Loadingscene 部分有；部分 GUID 可能 Missing", "花园：风+鸟叫；雨天：小雨"),
    # --- P0 新素材 ---
    ("P0", "玩家", "跳跃起跳", "Space 离地瞬间", "—", "PlayerMovement 跳跃逻辑", "未实现", ""),
    ("P0", "玩家", "落地", "跳跃/下落着地", "—", "PlayerMovement / CharacterController", "未实现", ""),
    ("P0", "玩家", "受伤 / 受击", "TakeDamage 扣血", "—", "PlayerMovement.TakeDamage", "未实现", ""),
    ("P0", "玩家", "死亡", "HP ≤ 0", "—", "PlayerMovement.Die / OnDeath", "未实现", "DeathReviveUI 有 UI 无音"),
    ("P0", "玩家", "复活", "死亡面板点击复活", "—", "DeathReviveUI.CoRunDeathPanel", "未实现", ""),
    ("P0", "UI", "通用按钮点击", "各类 Button 确认/取消", "—", "全局 UI", "未实现", ""),
    ("P0", "UI", "面板打开", "手机/背包/任务/地图/联机等打开", "—", "PhoneUIManager、InventoryUI、QuestSystemUI、MapSystem 等", "未实现", ""),
    ("P0", "UI", "面板关闭", "Esc / Exit / 再按快捷键关闭", "—", "同上", "未实现", ""),
    ("P0", "交互", "交互提示出现", "靠近显示 [F] 提示", "—", "InteractPrompt、InteractableChair、ElevatorInteractor", "未实现", ""),
    ("P0", "交互", "交互成功", "按 F 开始对话/坐下/上车/电梯", "—", "interaction system、NpcDialogue、椅子、电梯", "未实现", ""),
    ("P0", "对话", "对话翻页 / 节点进入", "进入对话节点", "—", "NpcDialogueInteractor.onNodeEntered", "预留 UnityEvent，需配置", ""),
    ("P0", "对话", "对话结束", "关闭对话面板", "—", "NpcDialogueInteractor.onDialogueClosed", "未实现", ""),
    ("P0", "任务", "任务完成", "整条任务链完成", "—", "QuestSystemManager.AsyncCompletion", "仅 Debug.Log", ""),
    ("P0", "任务", "子任务完成", "单步教学/支线完成", "—", "QuestSystemManager.AsyncCompletion", "仅 Debug.Log", ""),
    ("P0", "经济", "金币获得", "任务奖励、购买反馈", "—", "QuestSystemManager、DormMallUI", "未实现", ""),
    ("P0", "流程", "加载开始", "服务器拉档 / 地图 loading", "—", "ServerSpawnLoadingOverlay、MapSystem loading", "视频原声为主", "建议短 SFX 叠加"),
    ("P0", "流程", "加载结束", "加载条完成、进入场景", "—", "同上", "未实现", ""),
    ("P0", "流程", "场景切换确认", "公交站等按钮切场景", "—", "SpawnMoveRotateDisappearSceneSwitch", "视频原声为主", ""),
    ("P0", "流程", "登录成功", "LGU 登录进入世界", "—", "LguHubLoginSuccessBanner、MenuLoginPortalFlow", "未实现", ""),
    # --- 地面脚步扩展（资源已有）---
    ("P0", "脚步扩展", "草地走路", "户外草地区域", "草地走路1.mp3 / 草地走路2.mp3", "PlayerStepSound（需地面检测）", "资源有，代码未切换", "按 Tag/材质切换 walkClip"),
    ("P0", "脚步扩展", "楼梯脚步", "楼梯区域", "楼梯1.mp3 / 楼梯2.mp3", "PlayerStepSound（需地面检测）", "资源有，代码未切换", ""),
    ("P0", "脚步扩展", "蹲下脚步", "Ctrl 蹲伏移动", "可复用 walk 降 pitch 或单独素材", "PlayerStepSound Crouch 分支", "仅 pitch 变化", "可选单独短循环"),
    # --- P1 ---
    ("P1", "载具", "引擎循环", "驾驶中随速度", "—", "VehicleController", "未实现", "pitch/音量随速度"),
    ("P1", "载具", "刹车 / 撞墙", "正面碰撞急停", "—", "VehicleController frontCollision", "未实现", ""),
    ("P1", "载具", "上下车", "进入/离开载具", "—", "VehicleController", "未实现", ""),
    ("P1", "载具", "救援复位", "长按 R 传送回出生点", "—", "VehicleController.rescueKey", "未实现", ""),
    ("P1", "环境", "踩水 / 溅水", "液态水面行走", "KriptoFX water_splash（可选）", "PlayerWaterRippleEmitter、RippleCreator.SplashAudioSource", "预制体多为空", ""),
    ("P1", "电梯", "电梯门开闭", "呼叫/到达", "—", "ElevatorSystem、ElevatorInteractor", "未实现", ""),
    ("P1", "电梯", "运行嗡鸣", "楼层传送中", "—", "InSceneTeleportLoading", "未实现", "可与传送音区分"),
    ("P1", "电梯", "到达叮声", "到达目标层", "—", "ElevatorSystem", "未实现", ""),
    ("P1", "椅子", "坐下", "F 坐下动画开始", "—", "PlayerSitSequence、InteractableChair", "未实现", ""),
    ("P1", "椅子", "站起", "F / Esc 站起", "—", "PlayerSitSequence", "未实现", ""),
    ("P1", "商城", "购买成功", "确认购买", "—", "DormMallUI toast", "未实现", ""),
    ("P1", "商城", "购买失败", "金币不足等", "—", "DormMallUI", "未实现", ""),
    ("P1", "寝室", "家具放置确认", "放置模式确认", "—", "PlacementModeController", "未实现", ""),
    ("P1", "寝室", "家具放置取消", "Esc 取消", "—", "PlacementModeController", "未实现", ""),
    ("P1", "寝室", "家具旋转", "放置模式旋转", "—", "PlacementModeController", "未实现", ""),
    ("P1", "背包", "打开 / 关闭", "B 键", "—", "InventoryUI", "未实现", ""),
    ("P1", "联机", "好友消息提示", "新消息", "—", "MultiplayerFriendChatSidebarController", "未实现", ""),
    ("P1", "联机", "房间加入成功", "联机面板", "—", "GameServerSync / 联机 UI", "未实现", ""),
    ("P1", "竞拍", "出价", "O 面板出价", "—", "FlagAuctionPanelController", "未实现", ""),
    ("P1", "竞拍", "被超越 / 成交", "竞拍状态变化", "—", "FlagAuctionPanelController", "未实现", ""),
    # --- P2 场景氛围 ---
    ("P2", "场景 BGM", "uppergarden / middlegarden", "户外花园主场景", "1(3).mp3", "BackgroundMusic", "uppergarden 已绑", ""),
    ("P2", "场景 BGM", "undergarden", "新手教学花园", "1(3).mp3 或欢快的小曲", "BackgroundMusic + SceneAmbientAudio", "环境部分已挂", "新手关重点"),
    ("P2", "场景 BGM", "运动场", "运动场区域", "运动场音效.mp3", "SceneAmbientAudio / BGM", "未引用", ""),
    ("P2", "场景环境", "教学楼 / 教室", "classroom1、teachingroomA、computerroom 等", "低音量室内环境（需新录或库素材）", "SceneAmbientAudio", "未配置", "空调/走廊远景"),
    ("P2", "场景环境", "寝室 dorm", "dorm.scene", "安静室内", "SceneAmbientAudio", "未配置", ""),
    ("P2", "场景环境", "图书馆", "libraryfloor1/4、library", "翻页、脚步回声", "SceneAmbientAudio", "未配置", ""),
    ("P2", "场景环境", "礼堂 / 博物馆", "lecture hall、museC、shawA", "大厅混响", "SceneAmbientAudio", "未配置", ""),
    ("P2", "场景环境", "Loadingscene", "加载/菜单", "欢快的小曲.mp3", "SceneAmbientAudio", "已挂组件", "检查 clip 是否 Missing"),
    # --- 战斗 ---
    ("P1", "战斗", "战斗环境 / BGM", "战斗区域", "battle_sfx.ogg", "待建战斗 Audio 管理器", "文件存在，未引用", "需确认战斗玩法范围"),
    ("P1", "战斗", "普攻 / 技能", "释放技能", "—", "aiming / 战斗脚本", "未实现", ""),
    ("P1", "战斗", "命中 / 受击", "造成伤害", "—", "伤害事件", "未实现", ""),
    ("P1", "战斗", "敌人死亡", "敌人 HP 归零", "—", "—", "未实现", ""),
]

STATUS_ROWS = [
    ("项目", "说明"),
    ("工程路径", "Assets/Music/、Assets/code/battle_sfx.ogg"),
    ("已完整配置场景", "uppergarden（BGM + 脚步 + 喘气 + 传送）"),
    ("PlayerStepSound", "仅 uppergarden 场景玩家挂了组件"),
    ("MapSystem.teleportSound", "约 20+ 场景为 null，仅 uppergarden 已绑普通传送"),
    ("SceneAmbientAudio", "undergarden、lingcollegeB、Loadingscene；注意 Missing 引用"),
    ("Credits 音效制作", "屈子胜、黄晨朗（GameCreditsCatalog）"),
    ("建议实施顺序", "1.铺开现有素材 → 2.P0 新素材 → 3.地面脚步切换 → 4.P1 → 5.P2 分场景 BGM"),
]

SPEC_ROWS = [
    ("项目", "建议"),
    ("格式", ".wav 或 .ogg（短 SFX）；循环 BGM/环境勾选 Loop"),
    ("目录", "SFX/Player、SFX/UI、SFX/Teleport、SFX/Vehicle、Ambient、BGM"),
    ("2D 音效", "UI、任务、传送、加载"),
    ("3D 音效", "脚步、喘气、载具、溅水"),
    ("音量层级", "环境 0.25–0.4 → BGM 0.4–0.6 → 脚步 0.6–0.8 → UI/传送 0.8–1.0"),
]


def style_header(ws, row=1):
    fill = PatternFill("solid", fgColor="4472C4")
    font = Font(bold=True, color="FFFFFF", size=11)
    thin = Side(style="thin", color="CCCCCC")
    border = Border(left=thin, right=thin, top=thin, bottom=thin)
    for col in range(1, len(HEADERS) + 1):
        c = ws.cell(row=row, column=col, value=HEADERS[col - 1])
        c.fill = fill
        c.font = font
        c.alignment = Alignment(horizontal="center", vertical="center", wrap_text=True)
        c.border = border


def write_table(ws, start_row, headers, data_rows, col_widths=None):
    for i, h in enumerate(headers):
        ws.cell(row=start_row, column=i + 1, value=h)
    style_header(ws, start_row) if headers == HEADERS else None
    r = start_row + 1
    thin = Side(style="thin", color="DDDDDD")
    border = Border(left=thin, right=thin, top=thin, bottom=thin)
    priority_fills = {
        "P0": PatternFill("solid", fgColor="FFF2CC"),
        "P1": PatternFill("solid", fgColor="E2EFDA"),
        "P2": PatternFill("solid", fgColor="DEEBF7"),
    }
    for row_data in data_rows:
        for col, val in enumerate(row_data, 1):
            cell = ws.cell(row=r, column=col, value=val)
            cell.alignment = Alignment(vertical="top", wrap_text=True)
            cell.border = border
            if headers == HEADERS and col == 1 and val in priority_fills:
                for c in range(1, len(HEADERS) + 1):
                    ws.cell(row=r, column=c).fill = priority_fills[val]
        r += 1
    if col_widths:
        for i, w in enumerate(col_widths, 1):
            ws.column_dimensions[get_column_letter(i)].width = w
    ws.freeze_panes = ws.cell(row=start_row + 1, column=1).coordinate
    return r


def main():
    wb = Workbook()
    # Sheet 1: 清单
    ws1 = wb.active
    ws1.title = "音效补充清单"
    widths = [8, 12, 18, 32, 28, 36, 22, 24]
    write_table(ws1, 1, HEADERS, ROWS, widths)
    ws1.row_dimensions[1].height = 28

    # Sheet 2: 现状
    ws2 = wb.create_sheet("项目现状")
    for r, (a, b) in enumerate(STATUS_ROWS, 1):
        ws2.cell(row=r, column=1, value=a)
        ws2.cell(row=r, column=2, value=b)
        ws2.cell(row=r, column=1).font = Font(bold=True) if r == 1 else Font()
    ws2.column_dimensions["A"].width = 22
    ws2.column_dimensions["B"].width = 70
    ws2.cell(row=1, column=1).fill = PatternFill("solid", fgColor="4472C4")
    ws2.cell(row=1, column=2).fill = PatternFill("solid", fgColor="4472C4")
    ws2.cell(row=1, column=1).font = Font(bold=True, color="FFFFFF")
    ws2.cell(row=1, column=2).font = Font(bold=True, color="FFFFFF")

    # Sheet 3: 已有资源
    asset_data = [
        ("1(3).mp3", "户外/校园 BGM", "仅 uppergarden", "88102760..."),
        ("普通传送.mp3", "地图/同场景传送", "仅 uppergarden", "d624aded..."),
        ("科幻传送.mp3", "科幻传送点", "未使用", "a97f9a59..."),
        ("水泥地走路1.mp3", "走路脚步", "uppergarden walkClip", "a000a8ee..."),
        ("水泥地走路2.mp3", "走路变体", "未使用", ""),
        ("草地走路1.mp3", "草地脚步", "未使用", "fa6d3f57..."),
        ("草地走路2.mp3", "草地脚步变体", "未使用", ""),
        ("楼梯1.mp3", "楼梯脚步", "未使用", "b685f9af..."),
        ("楼梯2.mp3", "楼梯脚步变体", "未使用", ""),
        ("跑步1.mp3", "跑步", "未使用", "76a2a470..."),
        ("跑步2.mp3", "跑步", "uppergarden runClip", "5fc0d7b3..."),
        ("喘气.mp3", "低体力循环", "uppergarden", "563320a2..."),
        ("鸟叫.mp3", "室外环境", "未挂场景", "296f47e4..."),
        ("风.mp3", "室外环境", "未挂场景", "e82a3728..."),
        ("小雨.mp3", "雨天环境", "未挂场景", "171b473d..."),
        ("欢快的小曲.mp3", "菜单/轻快 BGM", "未使用", "Resources"),
        ("运动场音效.mp3", "运动场", "未使用", "Resources"),
        ("battle_sfx.ogg", "战斗相关", "完全未引用", "Assets/code/"),
    ]
    ah = ["文件名", "建议用途", "当前状态", "备注"]
    ws3 = wb.create_sheet("已有音频资源")
    for i, h in enumerate(ah, 1):
        cell = ws3.cell(row=1, column=i, value=h)
        cell.fill = PatternFill("solid", fgColor="4472C4")
        cell.font = Font(bold=True, color="FFFFFF")
    for r, row in enumerate(asset_data, 2):
        for c, val in enumerate(row, 1):
            ws3.cell(row=r, column=c, value=val)
    ws3.column_dimensions["A"].width = 20
    ws3.column_dimensions["B"].width = 24
    ws3.column_dimensions["C"].width = 28
    ws3.column_dimensions["D"].width = 18

    # Sheet 4: 制作规格
    ws4 = wb.create_sheet("制作规格")
    for r, (a, b) in enumerate(SPEC_ROWS, 1):
        ws4.cell(row=r, column=1, value=a)
        ws4.cell(row=r, column=2, value=b)
    ws4.column_dimensions["A"].width = 14
    ws4.column_dimensions["B"].width = 60
    ws4.cell(row=1, column=1).fill = PatternFill("solid", fgColor="4472C4")
    ws4.cell(row=1, column=2).fill = PatternFill("solid", fgColor="4472C4")
    ws4.cell(row=1, column=1).font = Font(bold=True, color="FFFFFF")
    ws4.cell(row=1, column=2).font = Font(bold=True, color="FFFFFF")

    wb.save(OUT)
    print(f"Written: {OUT}")


if __name__ == "__main__":
    main()
