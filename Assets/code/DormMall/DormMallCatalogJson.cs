using System;
using UnityEngine;

namespace DormMall
{
    /// <summary>与 GameServer GET /api/dorm_mall/catalog 返回 JSON 对应的结构（供 JsonUtility 反序列化）。</summary>
    [Serializable]
    public class DormMallCatalogFileJson
    {
        public DormMallCatalogProductJson[] products;
    }

    [Serializable]
    public class DormMallCatalogProductJson
    {
        public int id;
        public string productName;
        public string category;
        public string description;
        public string iconPath;
        public float price;
        public string placeablePrefabPath;
        public bool isConsumableFood;
        public float healAmount;
        public float staminaRecover;
        public float speedBoostMultiplier;
        public float speedBoostDuration;
    }
}
