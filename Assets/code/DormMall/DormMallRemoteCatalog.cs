using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace DormMall
{
    /// <summary>从 GameServer 拉取寝室商城目录，生成运行时 ProductDatabase（精灵仍用 iconPath + Resources）。</summary>
    public static class DormMallRemoteCatalog
    {
        public static ProductDatabase CachedRuntimeDatabase { get; private set; }
        public static bool LastFetchOk { get; private set; }
        public static string LastError { get; private set; }

        public static void ClearCache()
        {
            if (CachedRuntimeDatabase != null)
            {
                UnityEngine.Object.Destroy(CachedRuntimeDatabase);
                CachedRuntimeDatabase = null;
            }

            LastFetchOk = false;
            LastError = null;
        }

        public static IEnumerator FetchCoroutine(string baseUrlTrimmed, Action<ProductDatabase> onSuccess, Action<string> onFail)
        {
            if (CachedRuntimeDatabase != null && LastFetchOk)
            {
                NormalizeDatabaseInPlace(CachedRuntimeDatabase);
                onSuccess?.Invoke(CachedRuntimeDatabase);
                yield break;
            }

            baseUrlTrimmed = (baseUrlTrimmed ?? "").TrimEnd('/');
            if (string.IsNullOrEmpty(baseUrlTrimmed))
            {
                onFail?.Invoke("baseUrl 为空");
                yield break;
            }

            string url = $"{baseUrlTrimmed}/api/dorm_mall/catalog";
            using (var req = UnityWebRequest.Get(url))
            {
                yield return req.SendWebRequest();
#if UNITY_2020_2_OR_NEWER
                if (req.result != UnityWebRequest.Result.Success)
#else
                if (req.isNetworkError || req.isHttpError)
#endif
                {
                    LastFetchOk = false;
                    LastError = req.error;
                    onFail?.Invoke(req.error);
                    yield break;
                }

                try
                {
                    string text = req.downloadHandler.text;
                    var file = JsonUtility.FromJson<DormMallCatalogFileJson>(text);
                    if (file == null || file.products == null)
                    {
                        LastFetchOk = false;
                        LastError = "解析 catalog JSON 失败";
                        onFail?.Invoke(LastError);
                        yield break;
                    }

                    var db = ScriptableObject.CreateInstance<ProductDatabase>();
                    var list = new List<ProductEntry>(file.products.Length);
                    for (int i = 0; i < file.products.Length; i++)
                    {
                        var j = file.products[i];
                        if (j == null) continue;
                        list.Add(new ProductEntry
                        {
                            id = j.id,
                            productName = j.productName ?? "",
                            category = j.category ?? "",
                            description = j.description ?? "",
                            iconPath = ProductDatabase.NormalizeIconPath(j.iconPath ?? ""),
                            icon = null,
                            price = j.price,
                            placeablePrefabPath = j.placeablePrefabPath ?? "",
                            isConsumableFood = j.isConsumableFood,
                            healAmount = j.healAmount,
                            staminaRecover = j.staminaRecover,
                            speedBoostMultiplier = j.speedBoostMultiplier <= 0f ? 1f : j.speedBoostMultiplier,
                            speedBoostDuration = Mathf.Max(0f, j.speedBoostDuration)
                        });
                    }

                    db.products = list;
                    NormalizeDatabaseInPlace(db);
                    if (CachedRuntimeDatabase != null)
                        UnityEngine.Object.Destroy(CachedRuntimeDatabase);
                    CachedRuntimeDatabase = db;
                    LastFetchOk = true;
                    LastError = null;
                    onSuccess?.Invoke(db);
                }
                catch (Exception e)
                {
                    LastFetchOk = false;
                    LastError = e.Message;
                    onFail?.Invoke(e.Message);
                }
            }
        }

        static void NormalizeDatabaseInPlace(ProductDatabase db)
        {
            if (db == null || db.products == null)
                return;
            for (int i = 0; i < db.products.Count; i++)
            {
                var p = db.products[i];
                if (p == null) continue;
                p.iconPath = ProductDatabase.NormalizeIconPath(p.iconPath);
            }
        }
    }
}
