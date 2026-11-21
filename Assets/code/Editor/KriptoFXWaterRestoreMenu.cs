#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 从 _import_temp/unitypackages/water 恢复因错误导入而缺失的 KriptoFX 材质/纹理（紫色水面）。
/// </summary>
public static class KriptoFXWaterRestoreMenu
{
    const string CacheRoot = "_import_temp/unitypackages/water";
    const string PoolMaterialPath = "Assets/KriptoFX/Water2.0/Materials/PC/Pool/Unity5/Pool.mat";

    static readonly string[] RestorePrefixes =
    {
        "Assets/KriptoFX/Water2.0/Materials/",
        "Assets/KriptoFX/Water2.0/Textures/",
    };

    [MenuItem("Tools/场景配置/修复 KriptoFX 水面资源(紫色材质)", false, 50)]
    [MenuItem("Tools/环境/修复 KriptoFX 水面资源(紫色材质)", false, 50)]
    public static void RestoreWaterAssetsMenu()
    {
        int restored = TryRestoreMissingAssets(out string error);
        if (error != null)
        {
            EditorUtility.DisplayDialog("KriptoFX 水面", error, "确定");
            return;
        }

        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog(
            "KriptoFX 水面",
            restored > 0
                ? $"已恢复 {restored} 个资源文件（材质、纹理等）。\nUnity 将重新导入，请稍等后查看 Game 视图。"
                : "水面材质与纹理已齐全，无需恢复。",
            "确定");
    }

    /// <summary>若 Pool.mat 等缺失则从解包缓存恢复。返回恢复的文件数量；error 非空表示失败。</summary>
    public static int TryRestoreMissingAssets(out string error)
    {
        error = null;
        if (File.Exists(Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? "", PoolMaterialPath)))
            return 0;

        string projectRoot = Path.GetDirectoryName(Application.dataPath);
        string src = Path.Combine(projectRoot, CacheRoot);
        if (!Directory.Exists(src))
        {
            error = "未找到资源缓存：\n" + src +
                    "\n\n请确认 _import_temp/unitypackages/water 存在（由 water.unitypackage 解包）。";
            return 0;
        }

        int restored = 0;
        foreach (var guidDir in Directory.GetDirectories(src))
        {
            string pathnameFile = Path.Combine(guidDir, "pathname");
            string assetFile = Path.Combine(guidDir, "asset");
            string metaFile = Path.Combine(guidDir, "asset.meta");
            if (!File.Exists(pathnameFile) || !File.Exists(assetFile))
                continue;

            string rel = File.ReadAllText(pathnameFile).Trim().Replace('\\', '/');
            if (!StartsWithAny(rel, RestorePrefixes))
                continue;

            string dest = Path.Combine(projectRoot, rel.Replace('/', Path.DirectorySeparatorChar));
            if (!Path.GetFileName(rel).Contains("."))
            {
                EnsureFolderPlaceholder(dest, metaFile);
                continue;
            }

            EnsureParentDirectory(dest);
            File.Copy(assetFile, dest, true);
            if (File.Exists(metaFile))
                File.Copy(metaFile, dest + ".meta", true);
            restored++;
        }

        return restored;
    }

    static bool StartsWithAny(string path, string[] prefixes)
    {
        foreach (var p in prefixes)
        {
            if (path.StartsWith(p, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    static void EnsureParentDirectory(string filePath)
    {
        string parent = Path.GetDirectoryName(filePath);
        if (string.IsNullOrEmpty(parent))
            return;
        if (File.Exists(parent))
            File.Delete(parent);
        Directory.CreateDirectory(parent);
    }

    static void EnsureFolderPlaceholder(string folderPath, string metaFile)
    {
        if (Directory.Exists(folderPath))
            return;
        if (File.Exists(folderPath))
            File.Delete(folderPath);
        Directory.CreateDirectory(folderPath);
        if (File.Exists(metaFile))
            File.Copy(metaFile, folderPath + ".meta", true);
    }
}
#endif
