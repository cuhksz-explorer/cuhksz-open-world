#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PhoneUI.Editor
{
    /// <summary>将 CellPhone/UI 贴图设为 Sprite，并复制到 Resources 供运行时加载。</summary>
    public static class PhoneUiSpriteSetup
    {
        const string UiFolder = "Assets/PhoneUI/CellPhone/UI";
        const string ResourcesRoot = "Assets/PhoneUI/Resources/PhoneUI";
        const string IconsResources = "Assets/PhoneUI/Resources/PhoneUI/Icons";

        [MenuItem("PhoneUI/修复贴图(Sprite)并同步到 Resources", false, 2)]
        public static void FixSpritesAndCopyToResources()
        {
            if (!Directory.Exists(UiFolder))
            {
                EditorUtility.DisplayDialog("PhoneUI", "未找到目录: " + UiFolder, "确定");
                return;
            }

            EnsureFolder(IconsResources);
            int fixedTextures = 0;
            int copied = 0;

            foreach (var png in Directory.GetFiles(UiFolder, "*.png"))
            {
                var path = png.Replace('\\', '/');
                if (FixTextureImporter(path))
                    fixedTextures++;
            }

            CopyAsset($"{UiFolder}/Phoneee.png", $"{ResourcesRoot}/PhoneFrame.png");

            var iconMap = new Dictionary<string, string>
            {
                { "setting.png", "setting.png" },
                { "map.png", "map.png" },
                { "quest.png", "quest.png" },
                { "shop.png", "shop.png" },
                { "inventory.png", "inventory.png" },
                { "online.png", "online.png" },
                { "account.png", "account.png" },
                { "17.png", "17.png" },
                { "18.png", "18.png" },
            };

            foreach (var pair in iconMap)
            {
                var src = $"{UiFolder}/{pair.Key}";
                var dst = $"{IconsResources}/{pair.Value}";
                if (CopyAsset(src, dst))
                    copied++;
            }

            var smartphoneSrc = "Assets/PhoneUI/Resources/PhoneUI/smartphone.png";
            if (!File.Exists(smartphoneSrc) && File.Exists($"{UiFolder}/Phoneee.png"))
                CopyAsset($"{UiFolder}/Phoneee.png", smartphoneSrc);

            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            PhoneUiImportAndSceneConfigurator.ReimportPhoneUiPrefab();

            EditorUtility.DisplayDialog(
                "PhoneUI",
                $"贴图导入修复: {fixedTextures} 个\n复制到 Resources: {copied} 个图标 + PhoneFrame\n\n请重新进入 Play 测试手机 UI 与镜头。",
                "确定");
        }

        static bool FixTextureImporter(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
                return false;

            bool changed = false;
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                changed = true;
            }

            if (importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                changed = true;
            }

            if (!importer.alphaIsTransparency)
            {
                importer.alphaIsTransparency = true;
                changed = true;
            }

            if (!changed)
                return false;

            importer.SaveAndReimport();
            return true;
        }

        static bool CopyAsset(string src, string dst)
        {
            if (!File.Exists(src))
                return false;

            var dstDir = Path.GetDirectoryName(dst);
            if (!string.IsNullOrEmpty(dstDir))
                EnsureFolder(dstDir.Replace('\\', '/'));

            if (File.Exists(dst))
                AssetDatabase.DeleteAsset(dst);

            return AssetDatabase.CopyAsset(src, dst);
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            var parts = path.Split('/');
            var current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
#endif
