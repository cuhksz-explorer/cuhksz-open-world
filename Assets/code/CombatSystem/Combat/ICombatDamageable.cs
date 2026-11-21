using UnityEngine;

namespace CombatSystem
{
    public enum CombatTargetKind
    {
        Player,
        Monster,
        Friendly
    }

    public interface ICombatDamageable
    {
        CombatTargetKind TargetKind { get; }
        bool IsAlive { get; }
        void TakeCombatDamage(float damage, GameObject source);
    }
}
