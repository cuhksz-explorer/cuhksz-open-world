using GameQuestSystem;
using UnityEngine;

namespace CombatSystem
{
    [DisallowMultipleComponent]
    public class CombatMonster : MonoBehaviour, ICombatDamageable
    {
        [Header("属性")]
        public string displayName = "文具守卫";
        public float maxHealth = 120f;
        public float attackDamage = 8f;
        public float attackInterval = 2f;
        public float attackRange = 12f;
        public int goldReward = 25;

        float _health;
        float _nextAttackTime;
        Renderer _renderer;
        Color _baseColor;

        public CombatTargetKind TargetKind => CombatTargetKind.Monster;
        public bool IsAlive => _health > 0f;

        void Awake()
        {
            _health = maxHealth;
            _renderer = GetComponentInChildren<Renderer>();
            if (_renderer != null && _renderer.material != null)
                _baseColor = _renderer.material.color;
        }

        void Update()
        {
            if (!IsAlive)
                return;
            if (Time.time < _nextAttackTime)
                return;

            var pm = FindNearestPlayer();
            if (pm == null)
                return;

            float dist = Vector3.Distance(transform.position, pm.transform.position);
            if (dist > attackRange)
                return;

            _nextAttackTime = Time.time + attackInterval;
            CombatHostBridge.ApplyCombatDamage(pm, attackDamage);
        }

        PlayerMovement FindNearestPlayer()
        {
            var all = FindObjectsOfType<PlayerMovement>();
            PlayerMovement best = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < all.Length; i++)
            {
                var pm = all[i];
                if (pm == null || !pm.enabled)
                    continue;
                float d = Vector3.Distance(transform.position, pm.transform.position);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = pm;
                }
            }
            return best;
        }

        public void TakeCombatDamage(float damage, GameObject source)
        {
            if (!IsAlive)
                return;

            _health = Mathf.Max(0f, _health - damage);
            FlashHit();
            if (_health <= 0f)
                Die();
        }

        void FlashHit()
        {
            if (_renderer == null || _renderer.material == null)
                return;
            _renderer.material.color = Color.red;
            CancelInvoke(nameof(RestoreColor));
            Invoke(nameof(RestoreColor), 0.08f);
        }

        void RestoreColor()
        {
            if (_renderer != null && _renderer.material != null)
                _renderer.material.color = _baseColor;
        }

        void Die()
        {
            var q = QuestSystemManager.Instance;
            if (q != null)
                q.AddGold(goldReward, "monster_kill");
            Destroy(gameObject);
        }
    }
}
