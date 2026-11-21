#if UNITY_EDITOR
using System.IO;
using DormMall;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 将 Unity 里的 ProductDatabase 导出为 GameServer 可读的 dorm_mall_catalog.json。
/// 图片仍放在客户端 Resources/DormMall；JSON 里只存 iconPath（与 ProductEntry 一致）。
/// </summary>
public static class DormMallCatalogExportToGameServer
{
    [MenuItem("Tools/DormMall/导出商品目录到 GameServer (dorm_mall_catalog.json)")]
    public static void Export()
    {
        var db = Resources.Load<ProductDatabase>("DormMall/ProductDatabase");
        if (db == null)
        {
            EditorUtility.DisplayDialog("DormMall", "未找到 Resources/DormMall/ProductDatabase\n请先放置 ProductDatabase.asset。", "确定");
            return;
        }

        string dataDir = Path.Combine(Application.dataPath, "code", "GameServer", "backend", "data");
        Directory.CreateDirectory(dataDir);
        string outPath = Path.Combine(dataDir, "dorm_mall_catalog.json");

        var file = new DormMallCatalogFileJson
        {
            products = new DormMallCatalogProductJson[db.products.Count]
        };
        for (int i = 0; i < db.products.Count; i++)
        {
            var p = db.products[i];
            file.products[i] = new DormMallCatalogProductJson
            {
                id = p.id,
                productName = p.productName,
                category = p.category,
                description = p.description,
                iconPath = p.iconPath,
                price = p.price,
                placeablePrefabPath = p.placeablePrefabPath
            };
        }

        string json = JsonUtility.ToJson(file, true);
        File.WriteAllText(outPath, json, System.Text.Encoding.UTF8);
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("DormMall", $"已导出:\n{outPath}\n\n请重启或保持 GameServer 运行，客户端将通过 /api/dorm_mall/catalog 读取。", "确定");
        Debug.Log($"[DormMall] 导出 catalog -> {outPath}");
    }
}
#endif
