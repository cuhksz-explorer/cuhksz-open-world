using System.Collections.Generic;
using UnityEngine;

namespace CombatSystem
{
    static class WeaponDatabaseRuntimeDefaults
    {
        public static void EnsurePopulated(WeaponDatabase db)
        {
            if (db == null || (db.weapons != null && db.weapons.Count >= 30))
                return;
            if (db.weapons == null)
                db.weapons = new List<WeaponEntry>();

            if (db.weapons.Count > 0)
                return;

            void Add(int id, string brand, string model, WeaponRarity rarity, PenStyle style, float dmg, int sell)
            {
                db.weapons.Add(new WeaponEntry
                {
                    id = id,
                    brand = brand,
                    modelName = model,
                    displayName = brand + " " + model,
                    description = brand + " " + model,
                    rarity = rarity,
                    penStyle = style,
                    damage = dmg,
                    sellPrice = sell,
                    fireRate = 8f,
                    maxSpread = 6f,
                    maxRange = 80f
                });
            }

            Add(1, "晨光", "K35 按动中性笔", WeaponRarity.Blue, PenStyle.Gel, 8, 10);
            Add(2, "真彩", "009 中性笔", WeaponRarity.Blue, PenStyle.Gel, 9, 12);
            Add(3, "白雪", "PVR144 直液式", WeaponRarity.Blue, PenStyle.Rollerball, 9, 12);
            Add(4, "得力", "S08 按动中性笔", WeaponRarity.Blue, PenStyle.Gel, 10, 15);
            Add(5, "爱好", "GP1628 中性笔", WeaponRarity.Blue, PenStyle.Gel, 10, 15);
            Add(6, "齐心", "K35 办公中性笔", WeaponRarity.Blue, PenStyle.Gel, 11, 18);
            Add(7, "广博", "G-112 按动笔", WeaponRarity.Blue, PenStyle.Gel, 11, 18);
            Add(8, "三菱 Uni", "UM-151 按动中性笔", WeaponRarity.Blue, PenStyle.Gel, 12, 20);
            Add(9, "百乐 PILOT", "BP-S 圆珠笔", WeaponRarity.Blue, PenStyle.Ballpoint, 12, 22);
            Add(10, "斑马 Zebra", "JJ15 按动中性笔", WeaponRarity.Blue, PenStyle.Gel, 13, 25);
            Add(11, "文正", "0.5 针管中性笔", WeaponRarity.Blue, PenStyle.Fineliner, 13, 25);
            Add(12, "点石", "DS-904 按动中性笔", WeaponRarity.Blue, PenStyle.Gel, 15, 30);
            Add(13, "百乐 PILOT", "V5 直液式签字笔", WeaponRarity.Purple, PenStyle.Rollerball, 18, 45);
            Add(14, "百乐 PILOT", "P500 针管笔", WeaponRarity.Purple, PenStyle.Fineliner, 19, 48);
            Add(15, "三菱 Uni", "Jetstream 按动圆珠笔", WeaponRarity.Purple, PenStyle.Ballpoint, 20, 50);
            Add(16, "斑马 Zebra", "Sarasa Grand 复古中性笔", WeaponRarity.Purple, PenStyle.Gel, 21, 55);
            Add(17, "派克 Parker", "Jotter 经典圆珠笔", WeaponRarity.Purple, PenStyle.Ballpoint, 22, 60);
            Add(18, "凌美 Lamy", "Safari 狩猎钢笔", WeaponRarity.Purple, PenStyle.Fountain, 23, 65);
            Add(19, "派通 Pentel", "EnerGel 速干中性笔", WeaponRarity.Purple, PenStyle.Gel, 24, 70);
            Add(20, "威迪文 Waterman", "Hemisphere 半球钢笔", WeaponRarity.Purple, PenStyle.Fountain, 25, 75);
            Add(21, "高仕 Cross", "Century II 圆珠笔", WeaponRarity.Purple, PenStyle.Ballpoint, 26, 78);
            Add(22, "红环 Rotring", "600 制图针管笔", WeaponRarity.Purple, PenStyle.Fineliner, 28, 80);
            Add(23, "凌美 Lamy", "2000 经典钢笔", WeaponRarity.Orange, PenStyle.Fountain, 32, 120);
            Add(24, "派克 Parker", "Duofold 世纪钢笔", WeaponRarity.Orange, PenStyle.Fountain, 35, 140);
            Add(25, "威迪文 Waterman", "Expert 专家钢笔", WeaponRarity.Orange, PenStyle.Fountain, 38, 160);
            Add(26, "百利金 Pelikan", "Souverän M400 绿条钢笔", WeaponRarity.Orange, PenStyle.Fountain, 40, 180);
            Add(27, "万宝龙 Montblanc", "StarWalker 星际行者", WeaponRarity.Orange, PenStyle.Rollerball, 42, 200);
            Add(28, "百乐 PILOT", "Custom 74 14K 金尖钢笔", WeaponRarity.Orange, PenStyle.Fountain, 45, 220);
            Add(29, "万宝龙 Montblanc", "Meisterstück 149 大班钢笔", WeaponRarity.Red, PenStyle.Fountain, 58, 450);
            Add(30, "辉柏嘉 Faber-Castell", "Pen of the Year 年度笔", WeaponRarity.Red, PenStyle.Fountain, 65, 500);
        }
    }
}
