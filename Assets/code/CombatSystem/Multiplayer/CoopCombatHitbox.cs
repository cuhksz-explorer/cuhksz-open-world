using Mirror;
using UnityEngine;

namespace CombatSystem
{
    [DisallowMultipleComponent]
    public class CoopCombatHitbox : NetworkBehaviour
    {
        public CoopNetworkPlayer Owner { get; private set; }

        void Awake()
        {
            Owner = GetComponentInParent<CoopNetworkPlayer>();
        }

        public bool IsLocalOwnedPlayer()
        {
            return Owner != null && Owner.isLocalPlayer;
        }
    }
}
