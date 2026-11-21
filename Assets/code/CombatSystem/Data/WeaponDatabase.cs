using System.Collections.Generic;
using UnityEngine;

namespace CombatSystem
{
    [CreateAssetMenu(fileName = "WeaponDatabase", menuName = "CombatSystem/Weapon Database")]
    public class WeaponDatabase : ScriptableObject
    {
        public const string DefaultResourcesPath = "CombatSystem/WeaponDatabase";
        public const int DefaultStarterWeaponId = 1;

        public List<WeaponEntry> weapons = new List<WeaponEntry>();

        static WeaponDatabase _cached;

        public static WeaponDatabase Load()
        {
            if (_cached != null)
                return _cached;
            _cached = Resources.Load<WeaponDatabase>(DefaultResourcesPath);
            if (_cached == null)
            {
                _cached = ScriptableObject.CreateInstance<WeaponDatabase>();
                WeaponDatabaseRuntimeDefaults.EnsurePopulated(_cached);
            }
            else
            {
                WeaponDatabaseRuntimeDefaults.EnsurePopulated(_cached);
            }
            return _cached;
        }

        public WeaponEntry GetById(int id)
        {
            if (weapons == null)
                return null;
            for (int i = 0; i < weapons.Count; i++)
            {
                if (weapons[i] != null && weapons[i].id == id)
                    return weapons[i];
            }
            return null;
        }

        public List<WeaponEntry> GetByRarity(WeaponRarity rarity)
        {
            var list = new List<WeaponEntry>();
            if (weapons == null)
                return list;
            for (int i = 0; i < weapons.Count; i++)
            {
                var w = weapons[i];
                if (w != null && w.rarity == rarity)
                    list.Add(w);
            }
            return list;
        }

        public static Color GetRarityColor(WeaponRarity rarity)
        {
            switch (rarity)
            {
                case WeaponRarity.Purple: return new Color(0.55f, 0.35f, 0.95f);
                case WeaponRarity.Orange: return new Color(1f, 0.55f, 0.12f);
                case WeaponRarity.Red: return new Color(0.95f, 0.22f, 0.28f);
                default: return new Color(0.35f, 0.65f, 1f);
            }
        }

        public static string GetRarityLabel(WeaponRarity rarity)
        {
            switch (rarity)
            {
                case WeaponRarity.Purple: return "紫";
                case WeaponRarity.Orange: return "橙";
                case WeaponRarity.Red: return "红";
                default: return "蓝";
            }
        }

        public static string GetPenStyleLabel(PenStyle style)
        {
            switch (style)
            {
                case PenStyle.Ballpoint: return "圆珠笔";
                case PenStyle.Fountain: return "钢笔";
                case PenStyle.Rollerball: return "签字笔";
                case PenStyle.Fineliner: return "针管笔";
                default: return "中性笔";
            }
        }

        public Sprite ResolveIcon(WeaponEntry entry)
        {
            if (entry == null)
                return null;
            if (entry.icon != null)
                return entry.icon;
            if (!string.IsNullOrEmpty(entry.iconPath))
            {
                var sprite = Resources.Load<Sprite>(entry.iconPath);
                if (sprite != null)
                    entry.icon = sprite;
                return sprite;
            }
            return null;
        }
    }
}
