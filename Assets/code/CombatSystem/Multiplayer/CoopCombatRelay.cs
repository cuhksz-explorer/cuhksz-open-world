using Mirror;
using UnityEngine;

namespace CombatSystem
{
    [DisallowMultipleComponent]
    public class CoopCombatRelay : NetworkBehaviour
    {
        const float MaxHitDistance = 100f;
        const float MinShotInterval = 0.05f;

        float _lastShotTime;

        [Command(requiresAuthority = false)]
        public void CmdReportHit(uint targetNetId, int weaponId, float claimedDamage, Vector3 shooterPos)
        {
            if (!NetworkServer.active)
                return;

            if (Time.time - _lastShotTime < MinShotInterval)
                return;
            _lastShotTime = Time.time;

            if (!NetworkServer.spawned.TryGetValue(targetNetId, out var identity) || identity == null)
                return;

            var targetCoop = identity.GetComponent<CoopNetworkPlayer>();
            if (targetCoop == null || targetCoop.connectionToClient == connectionToClient)
                return;

            float dist = Vector3.Distance(shooterPos, identity.transform.position);
            if (dist > MaxHitDistance)
                return;

            var db = WeaponDatabase.Load();
            var weapon = db != null ? db.GetById(weaponId) : null;
            float maxDamage = weapon != null ? weapon.damage * 1.15f : 80f;
            float damage = Mathf.Clamp(claimedDamage, 0f, maxDamage);

            var targetRelay = identity.GetComponent<CoopCombatRelay>();
            if (targetRelay != null)
                targetRelay.TargetApplyCombatDamage(targetCoop.connectionToClient, damage);
        }

        [TargetRpc]
        public void TargetApplyCombatDamage(NetworkConnection target, float damage)
        {
            var pm = FindObjectOfType<PlayerMovement>();
            if (pm != null)
                CombatHostBridge.ApplyCombatDamage(pm, damage);
        }
    }
}
