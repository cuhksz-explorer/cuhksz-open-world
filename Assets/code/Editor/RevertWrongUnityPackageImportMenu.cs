#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 清理错误导入「完整？.unitypackage」后混入的旧版脚本、模板与重复资源。
/// 保留当前工程正在使用的 code/GameQuestSystem、undergarden 植物（Assets/草坪）等。
/// </summary>
public static class RevertWrongUnityPackageImportMenu
{
    const string PackageName = "完整？.unitypackage";

    static readonly string[] DeleteFilePaths =
    {
        "Assets/code/freshman/Questmanager.cs",
        "Assets/code/freshman/QuestmanagerUI.cs",
        "Assets/code/freshman/QuesData.cs",
        "Assets/textures/Node.mat",
    };

    static readonly string[] DeleteFolderPaths =
    {
        "Assets/Scripts",
        "Assets/TutorialInfo",
        "Assets/Assets",
        "Assets/__MACOSX",
    };

    [MenuItem("Tools/场景配置/清理错误 unitypackage 导入（旧版代码/材质）", false, 6)]
    [MenuItem("Tools/Quest System/清理错误 unitypackage 导入", false, 6)]
    public static void RunCleanup()
    {
        if (!EditorUtility.DisplayDialog(
                "清理错误 unitypackage 导入",
                $"将删除来自「{PackageName}」的多余内容：\n\n" +
                "· Assets/Scripts（旧版 Node/Content/Trigger 导览系统）\n" +
                "· Assets/TutorialInfo（Unity 模板）\n" +
                "· Assets/code/freshman 内旧 QuestManager 三件套\n" +
                "· Assets/Assets 重复嵌套目录\n" +
                "· Assets/textures/Node.mat\n" +
                "· Assets/__MACOSX\n\n" +
                "不会删除 Assets/草坪 植物资源与现有 GameQuestSystem。",
                "执行清理",
                "取消"))
        {
            return;
        }

        var report = ExecuteCleanup();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "清理完成",
            report,
            "确定");

        Debug.Log("[RevertWrongUnityPackageImport]\n" + report);
    }

    public static string ExecuteCleanup()
    {
        var sb = new StringBuilder();
        int removed = 0;

        foreach (string assetPath in DeleteFilePaths)
        {
            removed += DeleteAssetIfExists(assetPath, sb);
            removed += DeleteAssetIfExists(assetPath + ".meta", sb);
        }

        foreach (string folderPath in DeleteFolderPaths)
        {
            removed += DeleteFolderIfExists(folderPath, sb);
        }

        sb.AppendLine();
        sb.AppendLine($"共处理 {removed} 项。");
        sb.AppendLine("请等待 Unity 重新编译；若 Console 仍有报错，把完整错误贴出。");
        return sb.ToString();
    }

    static int DeleteAssetIfExists(string assetPath, StringBuilder sb)
    {
        string fullPath = Path.Combine(Application.dataPath, "..", assetPath).Replace('\\', '/');
        fullPath = Path.GetFullPath(fullPath);
        if (!File.Exists(fullPath))
            return 0;

        if (AssetDatabase.DeleteAsset(assetPath))
        {
            sb.AppendLine("· 已删除 " + assetPath);
            return 1;
        }

        File.Delete(fullPath);
        sb.AppendLine("· 已删除（文件系统） " + assetPath);
        return 1;
    }

    static int DeleteFolderIfExists(string folderPath, StringBuilder sb)
    {
        string fullPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", folderPath));
        if (!Directory.Exists(fullPath))
            return 0;

        if (AssetDatabase.DeleteAsset(folderPath))
        {
            sb.AppendLine("· 已删除文件夹 " + folderPath);
            return 1;
        }

        Directory.Delete(fullPath, true);
        string metaPath = fullPath + ".meta";
        if (File.Exists(metaPath))
            File.Delete(metaPath);

        sb.AppendLine("· 已删除文件夹（文件系统） " + folderPath);
        return 1;
    }

    [MenuItem("Tools/场景配置/检查 unitypackage 导入冲突", false, 7)]
    public static void CheckConflicts()
    {
        var issues = new List<string>();

        if (Directory.Exists(Path.Combine(Application.dataPath, "Scripts")))
            issues.Add("仍存在 Assets/Scripts（旧版导览代码）");

        if (File.Exists(Path.Combine(Application.dataPath, "code", "freshman", "Questmanager.cs")))
            issues.Add("仍存在 freshman/Questmanager.cs（与 QuestSystemManager 冲突的旧任务系统）");

        if (Directory.Exists(Path.Combine(Application.dataPath, "Assets")))
            issues.Add("仍存在 Assets/Assets 嵌套重复目录");

        if (File.Exists(Path.Combine(Application.dataPath, "textures", "Node.mat")))
            issues.Add("仍存在 textures/Node.mat（包内新增材质）");

        string message = issues.Count == 0
            ? "未发现已知的错误导入残留。"
            : "发现以下残留：\n· " + string.Join("\n· ", issues);

        EditorUtility.DisplayDialog("unitypackage 导入检查", message, "确定");
    }
}
#endif
