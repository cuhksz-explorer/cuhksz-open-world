using System.Collections.Generic;
using UnityEngine;

namespace GameQuestSystem
{
    /// <summary>
    /// 全局新手教学主线：覆盖移动、战斗基础、视角、交互与全部常用热键。
    /// 文案显示在原有简要任务栏（QuestSystemUI），不另做任务面板。
    /// </summary>
    public static class NewUserTutorialQuestPreset
    {
        public const string QuestId = "main_new_user_tutorial";
        public const string UndergardenScene = "undergarden";
        public const int SubTaskCount = 14;

        public static QuestChainDefinition CreateChain()
        {
            return new QuestChainDefinition
            {
                questId = QuestId,
                questName = Loc.Choose("新手教学", "Tutorial"),
                category = QuestCategory.Main,
                shortDescription = Loc.Choose(
                    "熟悉这座校园的行走、视角与每一次按键。",
                    "Learn how this campus moves—your feet, your eyes, and every key."),
                longDescription = Loc.Choose(
                    "十四步教学，从 WASD 到坐下休息。跟随地面引导线与左侧任务栏，把操作刻进肌肉记忆。",
                    "Fourteen steps from WASD to sitting down. Follow the ground guide and the brief quest panel until controls feel natural."),
                finalRewardGold = 120,
                mainChapterIndex = 0,
                subTasks = new List<QuestSubTask>
                {
                    Sub("tutorial_01_wasd", Loc.Choose("基础移动", "Basic Movement"),
                        Loc.Choose("用 WASD 迈出在这座校园的第一步。", "Take your first steps on campus with WASD."),
                        Loc.Choose("前后左右，四个方向键即可。任意按下一个方向完成本步。", "Forward, back, left, right—press any direction key to complete."), "learn_wasd", 8),
                    Reach("tutorial_02_reach", Loc.Choose("前往引导点", "Reach the Guide Point"),
                        Loc.Choose("沿白色引导线，走向光柱标记。", "Follow the white guide line to the light pillar."),
                        Loc.Choose("贴地引导线与屏幕标记会为你指路。抵达光柱下即可。", "Ground guides and on-screen markers lead the way. Stand beneath the pillar."), new Vector3(-981.44f, 3.82f, 1285.55f), 12),
                    Sub("tutorial_03_sprint", Loc.Choose("疾跑", "Sprint"),
                        Loc.Choose("按住 Shift，让风从耳边掠过。", "Hold Shift and let the wind catch your ears."),
                        Loc.Choose("Shift 配合 WASD 疾跑，会消耗体力。试一次即可。", "Shift + WASD sprints and drains stamina. Try it once."), "learn_sprint", 8),
                    Sub("tutorial_04_jump", Loc.Choose("跳跃", "Jump"),
                        Loc.Choose("按 Space，短暂地离开地面。", "Press Space and leave the ground for a moment."),
                        Loc.Choose("跳跃同样消耗体力。腾空一次完成教学。", "Jumping costs stamina too. Leave the ground once."), "learn_jump", 8),
                    Sub("tutorial_05_crouch", Loc.Choose("蹲伏", "Crouch"),
                        Loc.Choose("按住 Ctrl，把身形压低。", "Hold Ctrl and lower your profile."),
                        Loc.Choose("蹲伏通过低矮空间时很有用。保持蹲下片刻即可。", "Useful under low obstacles. Crouch briefly to finish."), "learn_crouch", 8),
                    Sub("tutorial_06_look", Loc.Choose("环视", "Look Around"),
                        Loc.Choose("转动鼠标，环顾四周。", "Move the mouse and look around."),
                        Loc.Choose("视角决定你能看见什么。轻轻移动鼠标完成本步。", "The camera decides what you see. Move the mouse slightly."), "learn_mouse_look", 8),
                    Sub("tutorial_07_camera", Loc.Choose("切换视角", "Switch View"),
                        Loc.Choose("按 V，在第一与第三人称间切换。", "Press V to switch first- and third-person view."),
                        Loc.Choose("两种视角各有用处。切换一次即可。", "Each view has its uses. Toggle once."), "learn_camera_v", 8),
                    Sub("tutorial_08_settings", Loc.Choose("画质与时间", "Graphics & Time"),
                        Loc.Choose("点开左上角设置，再安然关上。", "Open settings from the top-left, then close them."),
                        Loc.Choose("Esc 或界面上的关闭按钮均可。打开并关闭即完成。", "Esc or the close button works. Open and close to finish."), "settings_open_close", 10),
                    Sub("tutorial_09_interact", Loc.Choose("交互", "Interact"),
                        Loc.Choose("靠近提示对象，按 F 与之交谈或互动。", "Approach a prompted object and press F."),
                        Loc.Choose("F 键是对话、拾取、载具的通用钥匙。完成一次交互。", "F talks, picks up, and enters vehicles. Interact once."), "press_f", 10),
                    Sub("tutorial_10_inventory", Loc.Choose("背包", "Inventory"),
                        Loc.Choose("按 B 打开背包，确认物品栏存在。", "Press B to open your inventory."),
                        Loc.Choose("背包承载你在校园收集的一切。打开后关闭即可。", "Your inventory holds what you gather. Open, then close."), "inventory_open_close", 10),
                    Sub("tutorial_11_quest", Loc.Choose("任务日志", "Quest Journal"),
                        Loc.Choose("按 J 展开完整任务栏，再合上。", "Press J to open the full quest journal, then close it."),
                        Loc.Choose("完整任务栏记录更长的目标与奖励。切换一次即完成。", "The full panel holds longer goals and rewards. Toggle once."), "quest_journal_toggle", 10),
                    Sub("tutorial_12_map", Loc.Choose("大地图", "World Map"),
                        Loc.Choose("按 M 唤出大地图，再按 M 收起。", "Press M for the world map, then M again to dismiss."),
                        Loc.Choose("部分场景支持大地图。打开并关闭即可。", "Available in some scenes. Open and close the map."), "map_toggle", 10),
                    Sub("tutorial_14_social", Loc.Choose("联机与好友", "Multiplayer & Friends"),
                        Loc.Choose("按 K 或 C 打开社交面板，随后关闭。", "Press K or C for social panels, then close."),
                        Loc.Choose("K 为联机房间码，C 为好友聊天。任一打开后关闭即可。", "K for multiplayer rooms; C for friend chat. Open either, then close."), "social_panel_toggle", 15),
                    Sub("tutorial_15_chair_sit", Loc.Choose("坐下休息", "Sit Down"),
                        Loc.Choose("找一把椅子或长椅，按 F 坐下。", "Find a chair or bench and press F to sit."),
                        Loc.Choose("F 坐下，F 或 Esc 站起。久坐还会随机播放待机动画。", "F to sit; F or Esc to stand. Idle long enough for a random animation."), "sit_on_chair", 12)
                }
            };
        }

        static QuestSubTask Sub(string id, string name, string brief, string detail, string evt, int gold)
        {
            return new QuestSubTask
            {
                subTaskId = id,
                subTaskName = name,
                shortDescription = brief,
                longDescription = detail,
                triggerType = QuestTriggerType.ManualEvent,
                triggerEventId = evt,
                rewardGold = gold,
                finishLocationName = name
            };
        }

        static QuestSubTask Reach(string id, string name, string brief, string detail, Vector3 pos, int gold)
        {
            return new QuestSubTask
            {
                subTaskId = id,
                subTaskName = name,
                shortDescription = brief,
                longDescription = detail,
                triggerType = QuestTriggerType.ReachLocation,
                targetSceneName = UndergardenScene,
                fallbackTargetPosition = pos,
                autoCompleteDistance = 6f,
                rewardGold = gold,
                finishLocationName = Loc.Choose("引导点", "Guide Point")
            };
        }
    }
}
