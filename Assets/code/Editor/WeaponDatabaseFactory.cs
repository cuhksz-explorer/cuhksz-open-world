#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CombatSystem.EditorTools
{
    public static class WeaponDatabaseFactory
    {
        const string AssetPath = "Assets/Resources/CombatSystem/WeaponDatabase.asset";

        [MenuItem("Tools/Combat/Create Weapon Database (30 Pens)")]
        public static void CreateOrUpdateDatabase()
        {
            EnsureFolder("Assets/Resources/CombatSystem");

            var db = AssetDatabase.LoadAssetAtPath<WeaponDatabase>(AssetPath);
            if (db == null)
            {
                db = ScriptableObject.CreateInstance<WeaponDatabase>();
                AssetDatabase.CreateAsset(db, AssetPath);
            }

            db.weapons = BuildAllWeapons();
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Combat] WeaponDatabase 已生成/更新: " + AssetPath);
        }

        static List<WeaponEntry> BuildAllWeapons()
        {
            var list = new List<WeaponEntry>();
            void Add(int id, string brand, string model, WeaponRarity rarity, PenStyle style, float dmg, int sell, int tier, float rate = 8f, float spread = 6f)
            {
                list.Add(new WeaponEntry
                {
                    id = id,
                    brand = brand,
                    modelName = model,
                    displayName = $"{brand} {model}",
                    description = $"{brand} 出品的 {WeaponDatabase.GetPenStyleLabel(style)}，校园常见笔款之一。",
                    rarity = rarity,
                    penStyle = style,
                    realWorldTier = tier,
                    damage = dmg,
                    sellPrice = sell,
                    fireRate = rate,
                    maxSpread = spread,
                    maxRange = 80f,
                    iconPath = $"CombatSystem/WeaponIcons/pen_{id:D2}"
                });
            }

            Add(1, "晨光", "K35 按动中性笔", WeaponRarity.Blue, PenStyle.Gel, 8, 10, 1);
            Add(2, "真彩", "009 中性笔", WeaponRarity.Blue, PenStyle.Gel, 9, 12, 1);
            Add(3, "白雪", "PVR144 直液式", WeaponRarity.Blue, PenStyle.Rollerball, 9, 12, 1);
            Add(4, "得力", "S08 按动中性笔", WeaponRarity.Blue, PenStyle.Gel, 10, 15, 2);
            Add(5, "爱好", "GP1628 中性笔", WeaponRarity.Blue, PenStyle.Gel, 10, 15, 2);
            Add(6, "齐心", "K35 办公中性笔", WeaponRarity.Blue, PenStyle.Gel, 11, 18, 2);
            Add(7, "广博", "G-112 按动笔", WeaponRarity.Blue, PenStyle.Gel, 11, 18, 2);
            Add(8, "三菱 Uni", "UM-151 按动中性笔", WeaponRarity.Blue, PenStyle.Gel, 12, 20, 3);
            Add(9, "百乐 PILOT", "BP-S 圆珠笔", WeaponRarity.Blue, PenStyle.Ballpoint, 12, 22, 3, 9f, 6.5f);
            Add(10, "斑马 Zebra", "JJ15 按动中性笔", WeaponRarity.Blue, PenStyle.Gel, 13, 25, 3);
            Add(11, "文正", "0.5 针管中性笔", WeaponRarity.Blue, PenStyle.Fineliner, 13, 25, 3, 8.5f, 5f);
            Add(12, "点石", "DS-904 按动中性笔", WeaponRarity.Blue, PenStyle.Gel, 15, 30, 4);

            Add(13, "百乐 PILOT", "V5 直液式签字笔", WeaponRarity.Purple, PenStyle.Rollerball, 18, 45, 5);
            Add(14, "百乐 PILOT", "P500 针管笔", WeaponRarity.Purple, PenStyle.Fineliner, 19, 48, 5, 8f, 4.5f);
            Add(15, "三菱 Uni", "Jetstream 按动圆珠笔", WeaponRarity.Purple, PenStyle.Ballpoint, 20, 50, 5, 9.5f, 6f);
            Add(16, "斑马 Zebra", "Sarasa Grand 复古中性笔", WeaponRarity.Purple, PenStyle.Gel, 21, 55, 6);
            Add(17, "派克 Parker", "Jotter 经典圆珠笔", WeaponRarity.Purple, PenStyle.Ballpoint, 22, 60, 6, 9f, 5.5f);
            Add(18, "凌美 Lamy", "Safari 狩猎钢笔", WeaponRarity.Purple, PenStyle.Fountain, 23, 65, 7, 6.5f, 5f);
            Add(19, "派通 Pentel", "EnerGel 速干中性笔", WeaponRarity.Purple, PenStyle.Gel, 24, 70, 7);
            Add(20, "威迪文 Waterman", "Hemisphere 半球钢笔", WeaponRarity.Purple, PenStyle.Fountain, 25, 75, 8, 6f, 4.8f);
            Add(21, "高仕 Cross", "Century II 圆珠笔", WeaponRarity.Purple, PenStyle.Ballpoint, 26, 78, 8);
            Add(22, "红环 Rotring", "600 制图针管笔", WeaponRarity.Purple, PenStyle.Fineliner, 28, 80, 8, 7f, 4f);

            Add(23, "凌美 Lamy", "2000 经典钢笔", WeaponRarity.Orange, PenStyle.Fountain, 32, 120, 9, 5.5f, 4.5f);
            Add(24, "派克 Parker", "Duofold 世纪钢笔", WeaponRarity.Orange, PenStyle.Fountain, 35, 140, 9, 5f, 4.2f);
            Add(25, "威迪文 Waterman", "Expert 专家钢笔", WeaponRarity.Orange, PenStyle.Fountain, 38, 160, 10);
            Add(26, "百利金 Pelikan", "Souverän M400 绿条钢笔", WeaponRarity.Orange, PenStyle.Fountain, 40, 180, 10);
            Add(27, "万宝龙 Montblanc", "StarWalker 星际行者", WeaponRarity.Orange, PenStyle.Rollerball, 42, 200, 11);
            Add(28, "百乐 PILOT", "Custom 74 14K 金尖钢笔", WeaponRarity.Orange, PenStyle.Fountain, 45, 220, 11, 5f, 4f);

            Add(29, "万宝龙 Montblanc", "Meisterstück 149 大班钢笔", WeaponRarity.Red, PenStyle.Fountain, 58, 450, 12, 4.5f, 3.5f);
            Add(30, "辉柏嘉 Faber-Castell", "Pen of the Year 年度笔", WeaponRarity.Red, PenStyle.Fountain, 65, 500, 12, 4f, 3.2f);

            for (int i = 0; i < list.Count; i++)
            {
                var w = list[i];
                w.description = $"【{WeaponDatabase.GetRarityLabel(w.rarity)}】{w.brand} {w.modelName}。现实价位档 {w.realWorldTier}，适合作为 {WeaponDatabase.GetPenStyleLabel(w.penStyle)} 使用。";
            }

            return list;
        }

        [MenuItem("Tools/Combat/Generate Placeholder Weapon Icons")]
        public static void GeneratePlaceholderIcons()
        {
            EnsureFolder("Assets/Resources/CombatSystem/WeaponIcons");
            var db = AssetDatabase.LoadAssetAtPath<WeaponDatabase>(AssetPath);
            if (db == null)
            {
                CreateOrUpdateDatabase();
                db = AssetDatabase.LoadAssetAtPath<WeaponDatabase>(AssetPath);
            }

            for (int i = 0; i < db.weapons.Count; i++)
            {
                var w = db.weapons[i];
                string path = $"Assets/Resources/CombatSystem/WeaponIcons/pen_{w.id:D2}.png";
                if (File.Exists(path))
                    continue;

                var tex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
                var col = WeaponDatabase.GetRarityColor(w.rarity);
                for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                    tex.SetPixel(x, y, col);
                tex.Apply();
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
            }

            AssetDatabase.Refresh();
            Debug.Log("[Combat] 占位武器图标已生成。");
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            var parts = path.Split('/');
            string cur = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }
    }
}
#endif
