using UnityEngine;

namespace CombatSystem
{
    /// <summary>
    /// F2 切换战斗模式：默认关闭；开启后显示战斗 UI 并允许开火等战斗输入。
    /// </summary>
    public static class CombatMode
    {
        public static bool IsArmed { get; private set; }

        public static void SetArmed(bool armed)
        {
            if (IsArmed == armed)
                return;

            IsArmed = armed;
            if (!IsArmed && GachaPanelController.IsOpen)
                GachaPanelController.Toggle();
        }

        public static void Toggle()
        {
            SetArmed(!IsArmed);
        }

        /// <summary>
        /// 在 CombatHud 每帧调用：处理 F2，且在输入框聚焦 / Loading 时忽略。
        /// </summary>
        public static void TickHotkey()
        {
            if (!CombatHostBridge.WasKeyDown(CombatHostBridge.ToggleCombatModeKey))
                return;
            if (SceneManagerHelper.IsLoadingScene())
                return;
            if (GameplayUiHotkeyGuard.IsAnyInputFieldFocused())
                return;

            Toggle();
        }
    }
}
