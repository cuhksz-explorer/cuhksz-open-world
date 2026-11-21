using System;
using UnityEngine;

namespace CombatSystem
{
    [Serializable]
    public class WeaponEntry
    {
        public int id;
        public string brand;
        public string modelName;
        public string displayName;
        [TextArea(2, 4)]
        public string description;
        public WeaponRarity rarity = WeaponRarity.Blue;
        public PenStyle penStyle = PenStyle.Gel;
        public int realWorldTier;
        public float damage = 10f;
        public int sellPrice = 15;
        public float fireRate = 8f;
        public float maxSpread = 6f;
        public float maxRange = 80f;
        [Header("弹药 (CSGO 风格)")]
        public int magazineSize;
        public int maxReserveAmmo;
        public float reloadTime;
        public string iconPath;
        public Sprite icon;

        public string FullDisplayName =>
            string.IsNullOrWhiteSpace(displayName)
                ? $"{brand} {modelName}".Trim()
                : displayName.Trim();

        public Color RarityColor => WeaponDatabase.GetRarityColor(rarity);
    }
}
