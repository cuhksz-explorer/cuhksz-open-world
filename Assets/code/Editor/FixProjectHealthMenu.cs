#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 修复常见工程健康检查项（损坏 meta、误拷 Library、工程迁移后包缓存路径错误等）。
/// </summary>
public static class FixProjectHealthMenu
{
    const string CorruptTextureMeta =
        "Assets/pictures/Scenes/textures2/Mat3d66_9667639_38_74036_4.jpg.meta";

    [MenuItem("Tools/场景配置/修复工程健康检查项", false, 5)]
    [MenuItem("Tools/Quest System/修复工程健康检查项", false, 5)]
    public static void FixCommonIssues()
    {
        int fixes = 0;
        var messages = new System.Text.StringBuilder();

        if (File.Exists(CorruptTextureMeta))
        {
            File.Delete(CorruptTextureMeta);
            fixes++;
            messages.AppendLine("· 已删除损坏的纹理 .meta");
        }

        string carsLibrary = Path.Combine(Application.dataPath, "cars", "Library");
        if (Directory.Exists(carsLibrary))
        {
            Directory.Delete(carsLibrary, true);
            fixes++;
            messages.AppendLine("· 已删除误拷的 Assets/cars/Library");
        }

        if (TryClearStalePackageManagerCache(out string cacheMessage))
        {
            fixes++;
            messages.AppendLine("· " + cacheMessage);
        }

        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog(
            "工程修复",
            fixes > 0
                ? $"已处理 {fixes} 项：\n{messages}\n\n请等待 Unity 重新解析包并编译（首次可能较慢）。\n若 Console 仍有 Package 报错，请关闭 Unity 后手动删除整个 Library 文件夹再打开工程。"
                : "未发现可自动修复项。\n\n若 Recorder / Visual Studio 包仍报错：关闭 Unity，删除项目根目录 Library 文件夹，再重新打开工程。",
            "确定");
    }

    /// <summary>
    /// 工程迁移盘符或 Unity 版本切换后，Library/PackageManager 可能仍指向旧路径（如 E:），导致 Recorder 等包 CS0246。
    /// </summary>
    static bool TryClearStalePackageManagerCache(out string message)
    {
        message = string.Empty;
        string projectRoot = Path.GetDirectoryName(Application.dataPath);
        if (string.IsNullOrEmpty(projectRoot))
            return false;

        string resolutionPath = Path.Combine(projectRoot, "Library", "PackageManager", "projectResolution.json");
        if (!File.Exists(resolutionPath))
            return false;

        string json = File.ReadAllText(resolutionPath);
        string normalizedRoot = projectRoot.Replace('\\', '/');
        bool pointsToOtherDrive =
            json.Contains("E:\\", System.StringComparison.OrdinalIgnoreCase) ||
            json.Contains("E:/", System.StringComparison.OrdinalIgnoreCase);
        bool missingCurrentPath =
            !json.Replace('\\', '/').Contains(normalizedRoot, System.StringComparison.OrdinalIgnoreCase);
        bool wrongUnityVersion =
            json.Contains("\"unityVersion\": \"6000.", System.StringComparison.Ordinal) &&
            !Application.unityVersion.StartsWith("6000.", System.StringComparison.Ordinal);

        if (!pointsToOtherDrive && !missingCurrentPath && !wrongUnityVersion)
            return false;

        string packageManagerDir = Path.Combine(projectRoot, "Library", "PackageManager");
        string scriptAssembliesDir = Path.Combine(projectRoot, "Library", "ScriptAssemblies");
        string beeDir = Path.Combine(projectRoot, "Library", "Bee");

        if (Directory.Exists(packageManagerDir))
            Directory.Delete(packageManagerDir, true);
        if (Directory.Exists(scriptAssembliesDir))
            Directory.Delete(scriptAssembliesDir, true);
        if (Directory.Exists(beeDir))
            Directory.Delete(beeDir, true);

        message = "已清理迁移后失效的包缓存（PackageManager / ScriptAssemblies / Bee）";
        return true;
    }
}
#endif
