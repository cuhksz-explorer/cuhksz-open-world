using GameQuestSystem;
using UnityEngine;

namespace CombatSystem
{
    public static class CombatHitUtility
    {
        public static bool IsProtectedNpc(GameObject go)
        {
            if (go == null)
                return false;

            if (go.GetComponentInParent<StoryQuestNpc>() != null)
                return true;
            if (go.GetComponentInParent<NpcDialogueInteractor>() != null)
                return true;
            if (go.GetComponentInParent<DormMall.DormMallMerchantNpc>() != null)
                return true;
            if (go.GetComponentInParent<InteractableVehicle>() != null)
                return true;
            if (go.GetComponentInParent<VehicleDealerNPC>() != null)
                return true;
            if (go.GetComponentInParent<CanvasInteractionUIManager>() != null)
                return true;

            return false;
        }

        public static ICombatDamageable FindDamageable(Collider col)
        {
            if (col == null)
                return null;

            var damageable = col.GetComponentInParent<ICombatDamageable>();
            if (damageable == null)
                return null;

            if (IsProtectedNpc(col.gameObject))
                return null;

            return damageable;
        }

        public static CoopCombatHitbox FindRemotePlayerHitbox(Collider col)
        {
            if (col == null)
                return null;
            return col.GetComponentInParent<CoopCombatHitbox>();
        }
    }
}
