using System;
using System.Collections.Generic;
using UnityEngine;

namespace DormMall
{
    /// <summary>单个商品数据</summary>
    [Serializable]
    public class ProductEntry
    {
        public int id;
        public string productName;
        public string category;
        [TextArea(2, 4)]
        public string description;
        /// <summary>Resources 路径（不含扩展名），如 DormMall/多肉。若为空则使用 icon 字段</summary>
        public string iconPath;
        /// <summary>在 Inspector 中直接指定的图标，优先于 iconPath</summary>
        public Sprite icon;
        public float price = 49.9f;
        /// <summary>可放置 3D 预制体在 Resources 下的路径（如 DormMall/Placeables/Desk）。为空则背包中该物品不可放置。</summary>
        public string placeablePrefabPath;
        [Header("食物消耗（可选）")]
        [Tooltip("勾选后该物品在背包中点击会被消耗（不走放置模式）。")]
        public bool isConsumableFood;
        [Tooltip("消耗后恢复血量")]
        public float healAmount;
        [Tooltip("消耗后恢复体力")]
        public float staminaRecover;
        [Tooltip("移速倍率增益（1 表示无增益）")]
        public float speedBoostMultiplier = 1f;
        [Tooltip("移速增益持续秒数")]
        public float speedBoostDuration;
    }

    /// <summary>寝室商城商品数据库</summary>
    [CreateAssetMenu(fileName = "ProductDatabase", menuName = "DormMall/Product Database")]
    public class ProductDatabase : ScriptableObject
    {
        /// <summary>商品未填分类时，界面使用的统一分类名（与 GetByCategory 对应）</summary>
        public const string UncategorizedCategoryLabel = "全部";

        public List<ProductEntry> products = new List<ProductEntry>();
        static readonly Dictionary<string, Sprite> RuntimeSpriteCache = new Dictionary<string, Sprite>();

        public static string NormalizeIconPath(string iconPath)
        {
            string p = (iconPath ?? "").Trim();
            if (string.IsNullOrEmpty(p))
                return p;
            if (p == "DormMall/Food/apple") return "DormMall/Food/apple_real";
            if (p == "DormMall/Food/banana") return "DormMall/Food/banana_real";
            if (p == "DormMall/Food/milk") return "DormMall/Food/milk_real";
            if (p == "DormMall/Food/bread") return "DormMall/Food/bread_real";
            return p;
        }

        /// <summary>获取某分类下的商品</summary>
        public List<ProductEntry> GetByCategory(string category)
        {
            if (category == UncategorizedCategoryLabel)
                return products.FindAll(p => string.IsNullOrEmpty(p.category));
            return products.FindAll(p => p.category == category);
        }

        /// <summary>获取所有不重复的分类</summary>
        public List<string> GetAllCategories()
        {
            var set = new HashSet<string>();
            var hasUncategorized = false;
            foreach (var p in products)
            {
                if (string.IsNullOrEmpty(p.category))
                    hasUncategorized = true;
                else
                    set.Add(p.category);
            }
            if (hasUncategorized)
                set.Add(UncategorizedCategoryLabel);
            var list = new List<string>(set);
            list.Sort();
            return list;
        }

        /// <summary>根据 id 查找商品</summary>
        public ProductEntry GetById(int id)
        {
            return products.Find(p => p.id == id);
        }

        /// <summary>获取或加载商品的图标 Sprite</summary>
        public Sprite GetIcon(ProductEntry entry)
        {
            if (entry.icon != null) return entry.icon;
            string path = NormalizeIconPath(entry.iconPath);
            if (!string.IsNullOrEmpty(path))
            {
                var s = Resources.Load<Sprite>(path);
                if (s != null) return s;
                // 某些运行时生成/外部写入的 PNG 可能被导入为 Texture(Default) 而非 Sprite，
                // 这里兜底把 Texture2D 动态包装成 Sprite，避免商城出现纯色块。
                var tex = Resources.Load<Texture2D>(path);
                if (tex != null)
                {
                    string key = path;
                    if (!RuntimeSpriteCache.TryGetValue(key, out var dyn) || dyn == null)
                    {
                        dyn = Sprite.Create(
                            tex,
                            new Rect(0, 0, tex.width, tex.height),
                            new Vector2(0.5f, 0.5f),
                            100f);
                        RuntimeSpriteCache[key] = dyn;
                    }
                    return dyn;
                }
            }
            return null;
        }

        public static bool IsFoodCategory(string category)
        {
            var c = (category ?? "").Trim();
            return c == "食物" || c.Contains("食");
        }
    }
}
