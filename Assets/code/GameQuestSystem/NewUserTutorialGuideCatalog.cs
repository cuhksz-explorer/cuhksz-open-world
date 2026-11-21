using System.Collections.Generic;

namespace GameQuestSystem
{
    public enum NewUserGuideHighlight
    {
        None,
        BottomMovement,
        CenterLook,
        TopLeftSettings,
        TopRightHud,
        BottomKeys,
        LeftPanel
    }

    public struct NewUserGuideStepUi
    {
        public string[] Keys;
        public string Hint;
        public NewUserGuideHighlight Highlight;
        public bool ShowGroundLine;
    }

    /// <summary>新手步骤 → 引导 UI 文案与高亮区域。</summary>
    public static class NewUserTutorialGuideCatalog
    {
        static Dictionary<string, NewUserGuideStepUi> Map = Build();

        static NewUserTutorialGuideCatalog()
        {
            Loc.OnLanguageChanged += Rebuild;
        }

        static void Rebuild() => Map = Build();

        public static bool TryGet(string subTaskId, out NewUserGuideStepUi ui)
        {
            if (string.IsNullOrEmpty(subTaskId))
            {
                ui = default;
                return false;
            }
            return Map.TryGetValue(subTaskId, out ui);
        }

        static Dictionary<string, NewUserGuideStepUi> Build()
        {
            var d = new Dictionary<string, NewUserGuideStepUi>();
            d["tutorial_01_wasd"] = Ui(new[] { "W", "A", "S", "D" }, Loc.Choose("底部方向键区会高亮。请按下任意方向键。", "The movement keys at the bottom will highlight. Press any direction key."), NewUserGuideHighlight.BottomMovement, false);
            d["tutorial_02_reach"] = Ui(new[] { "WASD" }, Loc.Choose("跟随地面引导线与屏幕标记前往光柱。", "Follow the ground guide line and on-screen marker to the beacon."), NewUserGuideHighlight.None, true);
            d["tutorial_03_sprint"] = Ui(new[] { "Shift" }, Loc.Choose("按住 Shift + WASD 疾跑。", "Hold Shift + WASD to sprint."), NewUserGuideHighlight.BottomMovement, false);
            d["tutorial_04_jump"] = Ui(new[] { "Space" }, Loc.Choose("按 Space 跳跃。", "Press Space to jump."), NewUserGuideHighlight.BottomKeys, false);
            d["tutorial_05_crouch"] = Ui(new[] { "Ctrl" }, Loc.Choose("按住 Ctrl 蹲伏。", "Hold Ctrl to crouch."), NewUserGuideHighlight.BottomKeys, false);
            d["tutorial_06_look"] = Ui(new[] { Loc.Choose("鼠标", "Mouse") }, Loc.Choose("移动鼠标环视，屏幕中央会出现注视框。", "Move the mouse to look around; a focus frame appears at the center."), NewUserGuideHighlight.CenterLook, false);
            d["tutorial_07_camera"] = Ui(new[] { "V" }, Loc.Choose("按 V 切换第一/第三人称。", "Press V to toggle first/third person."), NewUserGuideHighlight.BottomKeys, false);
            d["tutorial_08_settings"] = Ui(new[] { Loc.Choose("设置按钮", "Settings"), "Esc" }, Loc.Choose("点击左上角设置打开，Esc 或关闭按钮退出即可。", "Open settings from the top-left; Esc or Close exits."), NewUserGuideHighlight.TopLeftSettings, false);
            d["tutorial_09_interact"] = Ui(new[] { "F" }, Loc.Choose("靠近提示对象后按 F 交互。", "Approach a prompted object and press F."), NewUserGuideHighlight.BottomKeys, false);
            d["tutorial_10_inventory"] = Ui(new[] { "B" }, Loc.Choose("B 打开背包 → 关闭面板完成。", "B opens inventory — close the panel to complete."), NewUserGuideHighlight.BottomKeys, false);
            d["tutorial_11_quest"] = Ui(new[] { "J", "Esc" }, Loc.Choose("J 打开任务日志，Esc 或 J 关闭。", "J opens the quest journal; Esc or J closes it."), NewUserGuideHighlight.LeftPanel, false);
            d["tutorial_12_map"] = Ui(new[] { "M" }, Loc.Choose("M 打开大地图，再按 M 关闭。", "M opens the world map; press M again to close."), NewUserGuideHighlight.BottomKeys, false);
            d["tutorial_14_social"] = Ui(new[] { "K", "C" }, Loc.Choose("K 联机房间；C 好友聊天。打开后关闭即可。", "K for multiplayer room; C for friend chat. Open then close to complete."), NewUserGuideHighlight.BottomKeys, false);
            d["tutorial_15_chair_sit"] = Ui(new[] { "F" }, Loc.Choose("靠近椅子或长椅，按 F 坐下；再按 F 或 Esc 站起。", "Approach a chair or bench, press F to sit; F or Esc to stand."), NewUserGuideHighlight.BottomKeys, false);
            return d;
        }

        static NewUserGuideStepUi Ui(string[] keys, string hint, NewUserGuideHighlight region, bool line)
        {
            return new NewUserGuideStepUi
            {
                Keys = keys,
                Hint = hint,
                Highlight = region,
                ShowGroundLine = line
            };
        }
    }
}
