using System.Collections.Generic;
using GameQuestSystem;
using UnityEngine;

namespace CombatSystem
{
    public static class GachaService
    {
        public const int SinglePullCost = 100;
        public const int TenPullCost = 900;

        static readonly float[] RarityWeights =
        {
            60f, // Blue
            25f, // Purple
            12f, // Orange
            3f   // Red
        };

        public static bool CanPull(int count)
        {
            if (count <= 0)
                return false;
            if (WeaponInventoryData.OwnedWeaponIds.Count + count > WeaponInventoryData.MaxWeapons)
                return false;
            var q = QuestSystemManager.Instance;
            if (q == null)
                return false;
            int cost = count == 10 ? TenPullCost : SinglePullCost * count;
            return q.CurrentGold >= cost;
        }

        public static int GetPullCost(int count)
        {
            if (count == 10)
                return TenPullCost;
            return SinglePullCost * Mathf.Max(1, count);
        }

        public static List<WeaponEntry> Roll(int count)
        {
            var results = new List<WeaponEntry>();
            var db = WeaponDatabase.Load();
            if (db == null || db.weapons == null || db.weapons.Count == 0)
                return results;

            var q = QuestSystemManager.Instance;
            if (q == null)
                return results;

            int cost = GetPullCost(count);
            if (!q.TryConsumeGold(cost, count == 10 ? "gacha_ten" : "gacha_single"))
                return results;

            for (int i = 0; i < count; i++)
            {
                if (WeaponInventoryData.IsFull)
                    break;
                var rolled = RollOne(db);
                if (rolled == null)
                    continue;
                if (WeaponInventoryData.TryAddWeapon(rolled.id))
                    results.Add(rolled);
            }

            return results;
        }

        static WeaponEntry RollOne(WeaponDatabase db)
        {
            var rarity = RollRarity();
            var pool = db.GetByRarity(rarity);
            if (pool == null || pool.Count == 0)
            {
                pool = db.weapons;
            }
            if (pool == null || pool.Count == 0)
                return null;
            return pool[Random.Range(0, pool.Count)];
        }

        static WeaponRarity RollRarity()
        {
            float total = 0f;
            for (int i = 0; i < RarityWeights.Length; i++)
                total += RarityWeights[i];

            float roll = Random.Range(0f, total);
            float acc = 0f;
            for (int i = 0; i < RarityWeights.Length; i++)
            {
                acc += RarityWeights[i];
                if (roll <= acc)
                    return (WeaponRarity)i;
            }
            return WeaponRarity.Blue;
        }
    }
}
