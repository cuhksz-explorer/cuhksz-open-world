using System.Reflection;
using GameQuestSystem;
using UnityEngine;

namespace CombatSystem
{
    /// <summary>
    /// 宿主工程桥接：不修改 PlayerMovement / QuestSystemManager 等外部脚本，
    /// 通过 KeyCode 与反射调用可选 API（TakeCombatDamage、MarkCombatStateDirtyFromBridge）。
    /// </summary>
    public static class CombatHostBridge
    {
        public static readonly KeyCode ReloadKey = KeyCode.R;
        public static readonly KeyCode QuickSwitchKey = KeyCode.Q;
        public static readonly KeyCode OpenGachaKey = KeyCode.G;
        public static readonly KeyCode ToggleCombatModeKey = KeyCode.F2;

        static MethodInfo _takeCombatDamage;
        static MethodInfo _markCombatDirty;

        public static bool WasKeyDown(KeyCode key) => Input.GetKeyDown(key);

        public static void ApplyCombatDamage(PlayerMovement player, float damage)
        {
            if (player == null || damage <= 0f)
                return;

            _takeCombatDamage ??= typeof(PlayerMovement).GetMethod(
                "TakeCombatDamage",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(float) },
                null);

            if (_takeCombatDamage != null)
            {
                _takeCombatDamage.Invoke(player, new object[] { damage });
                return;
            }

            player.TakeDamage(damage);
        }

        public static void MarkWeaponSaveDirty()
        {
            var q = QuestSystemManager.Instance;
            if (q == null)
                return;

            _markCombatDirty ??= typeof(QuestSystemManager).GetMethod(
                "MarkCombatStateDirtyFromBridge",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (_markCombatDirty != null)
                _markCombatDirty.Invoke(q, null);
        }
    }
}
