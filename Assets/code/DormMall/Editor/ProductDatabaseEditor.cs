#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace DormMall.Editor
{
    public static class ProductDatabaseEditor
    {
        private static readonly (int id, string name, string cat, string desc, string path, float price)[] DefaultProducts = {
            (1, "海报", "墙面装饰", "简约北欧风格墙面海报", "DormMall/墙面装饰/海报", 49.9f),
            (2, "LED星星串灯", "照明氛围", "USB充电LED星星串灯", "DormMall/照明氛围/LED星星串灯", 39.9f),
            (5, "小型多肉植物套装", "绿植摆件", "可爱多肉植物组合", "DormMall/绿植摆件/多肉", 29.9f),
            (6, "创意照片墙贴纸", "墙面装饰", "可移除照片墙贴纸", "DormMall/墙面装饰/照片墙贴纸", 39.9f),
            (8, "护眼台灯", "照明氛围", "触控式护眼台灯", "DormMall/照明氛围/护眼台灯", 89.9f),
            (9, "桌面小风扇", "生活用品", "USB充电桌面小风扇", "DormMall/生活用品/桌面小风扇", 29.9f),
            (13, "香薰蜡烛套装", "生活用品", "天然精油香薰蜡烛", "DormMall/生活用品/香薰蜡烛套装", 49.9f),
            (15, "立体墙贴画", "墙面装饰", "3D立体墙贴画", "DormMall/墙面装饰/立体墙贴画", 29.9f),
            (17, "水培绿萝套装", "绿植摆件", "透明玻璃瓶水培绿萝", "DormMall/绿植摆件/绿箩", 39.9f),
            (18, "迷你仙人掌组合", "绿植摆件", "多种迷你仙人掌组合", "DormMall/绿植摆件/仙人掌", 49.9f),
            (21, "文竹盆栽", "绿植摆件", "优雅文竹盆栽", "DormMall/绿植摆件/文竹", 59.9f),
            (24, "虎皮兰盆栽", "绿植摆件", "虎皮兰盆栽", "DormMall/绿植摆件/虎皮兰", 35.9f),
            (25, "龟背竹小苗", "绿植摆件", "龟背竹小苗", "DormMall/绿植摆件/龟背竹", 25.9f),
            (26, "迷你牵牛花盆栽", "绿植摆件", "迷你牵牛花盆栽", "DormMall/绿植摆件/牵牛花", 32.9f),
            (27, "凤仙花种子盆栽", "绿植摆件", "凤仙花种子盆栽", "DormMall/绿植摆件/凤仙花", 45.9f),
            (28, "紫罗兰", "绿植摆件", "紫罗兰", "DormMall/绿植摆件/紫罗兰", 69.9f),
            (30, "3D水晶墙贴", "墙面装饰", "3D水晶质感墙贴", "DormMall/墙面装饰/3D水晶墙贴", 45.9f),
            (31, "磁性墙贴", "墙面装饰", "可擦写磁性黑板墙贴", "DormMall/墙面装饰/磁性墙贴", 59.9f),
            (32, "挂钟", "墙面装饰", "简约北欧风格挂钟", "DormMall/墙面装饰/挂钟", 79.9f),
            (33, "照片墙相框组合", "墙面装饰", "多尺寸相框组合", "DormMall/墙面装饰/照片墙相框组合", 89.9f),
            (34, "手工编织挂毯", "墙面装饰", "手工编织棉麻挂毯", "DormMall/墙面装饰/手工编织挂毯", 129.9f),
            (35, "LED霓虹灯", "墙面装饰", "LED霓虹灯", "DormMall/墙面装饰/LED霓虹灯", 99.9f),
            (36, "投影星空灯", "照明氛围", "投影星空灯", "DormMall/照明氛围/投影星空灯", 129.9f),
            (37, "智能小夜灯", "照明氛围", "感应式智能小夜灯", "DormMall/照明氛围/智能小夜灯", 49.9f),
            (38, "氛围香薰灯", "照明氛围", "超声波香薰灯", "DormMall/照明氛围/氛围香薰灯", 79.9f),
            (40, "床头阅读灯", "照明氛围", "免打孔床头阅读灯", "DormMall/照明氛围/床头阅读灯", 69.9f),
            (41, "LED装饰灯串", "照明氛围", "LED装饰灯串", "DormMall/照明氛围/LED装饰灯串", 45.9f),
            (43, "KTV氛围灯", "照明氛围", "KTV舞台效果氛围灯", "DormMall/照明氛围/KTV氛围灯", 119.9f),
            (44, "蓝牙音箱灯", "照明氛围", "集照明与蓝牙音箱于一体", "DormMall/照明氛围/蓝牙音响灯", 159.9f),
            (45, "便携式充电宝", "生活用品", "20000mAh大容量充电宝", "DormMall/生活用品/便携式充电宝", 129.9f),
            (46, "折叠雨伞", "生活用品", "全自动折叠雨伞", "DormMall/生活用品/折叠雨伞", 39.9f),
            (47, "保温杯", "生活用品", "304不锈钢保温杯", "DormMall/生活用品/保温杯", 59.9f),
            (48, "便携式收纳袋", "生活用品", "防水折叠收纳袋", "DormMall/生活用品/便携式收纳袋", 29.9f),
            (49, "USB数据线", "生活用品", "三合一快充数据线", "DormMall/生活用品/USB数据线", 19.9f),
            (50, "便携式牙刷架", "生活用品", "多功能牙刷架", "DormMall/生活用品/便携式牙刷架", 25.9f),
            (51, "桌面加湿器", "生活用品", "USB充电桌面加湿器", "DormMall/生活用品/桌面加湿器", 69.9f),
        };

        [MenuItem("Assets/DormMall/创建商品数据库", false)]
        static void CreateProductDatabase()
        {
            var db = ScriptableObject.CreateInstance<DormMall.ProductDatabase>();
            db.products = new List<DormMall.ProductEntry>();
            foreach (var p in DefaultProducts)
            {
                db.products.Add(new DormMall.ProductEntry
                {
                    id = p.id,
                    productName = p.name,
                    category = p.cat,
                    description = p.desc,
                    iconPath = p.path,
                    price = p.price
                });
            }
            string path = "Assets/Resources/DormMall";
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder("Assets/Resources/DormMall")) AssetDatabase.CreateFolder("Assets/Resources", "DormMall");
            AssetDatabase.CreateAsset(db, path + "/ProductDatabase.asset");
            AssetDatabase.SaveAssets();
            Selection.activeObject = db;
            Debug.Log("[DormMall] 已创建 ProductDatabase.asset，请将图片放入 Resources/DormMall/ 对应子文件夹。");
        }
    }
}
#endif
