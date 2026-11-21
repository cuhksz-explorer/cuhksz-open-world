using UnityEngine;

namespace CombatSystem
{
    /// <summary>武器背包变更时标记 Quest 存档脏（若宿主已接 CombatPersistence partial）。</summary>
    public static class WeaponPersistenceBridge
    {
        static bool _dirty;

        public static void MarkDirty()
        {
            _dirty = true;
            CombatHostBridge.MarkWeaponSaveDirty();
        }

        public static bool ConsumeDirtyFlag()
        {
            if (!_dirty)
                return false;
            _dirty = false;
            return true;
        }
    }
}
