#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// libraryfloor1 / 2 / 4 完整场景备份（整份 .scene，含 UI、Plane、NPC、建模摆放等全部对象）。
/// </summary>
public static class LibraryFloorsSceneBackup
{
    public const string BackupFolder = "Assets/scene/Backups";

    public static readonly string[] ScenePaths =
    {
        "Assets/scene/libraryfloor1.scene",
        "Assets/scene/libraryfloor2.scene",
        "Assets/scene/libraryfloor4.scene",
    };

    const string MenuBackupAll = "Tools/图书馆/备份三个场景（完整 scene 文件）";
    const string MenuRestoreAllLatest = "Tools/图书馆/从最新备份恢复三个场景";

    [MenuItem(MenuBackupAll, false, 30)]
    public static void BackupAllFromMenu()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("图书馆场景备份", "请先退出 Play Mode。", "确定");
            return;
        }

        var paths = BackupAllScenes();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog(
            "图书馆场景备份",
            "已备份 " + paths.Length + " 个完整场景到：\n" + BackupFolder + "\n\n" + string.Join("\n", paths),
            "确定");
    }

    [MenuItem(MenuRestoreAllLatest, false, 31)]
    public static void RestoreAllLatestFromMenu()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("图书馆场景恢复", "请先退出 Play Mode。", "确定");
            return;
        }

        if (!EditorUtility.DisplayDialog(
                "图书馆场景恢复",
                "将用各场景最新备份覆盖当前 libraryfloor1/2/4.scene。\n未保存修改会丢失。继续？",
                "恢复",
                "取消"))
            return;

        int n = RestoreAllLatest();
        EditorUtility.DisplayDialog("图书馆场景恢复", $"已恢复 {n} 个场景。", "确定");
    }

  /// <summary>BatchMode: -executeMethod LibraryFloorsSceneBackup.BackupAllScenesBatch</summary>
    public static void BackupAllScenesBatch()
    {
        BackupAllScenes();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    public static string[] BackupAllScenes()
    {
        var saved = new string[ScenePaths.Length];
        for (int i = 0; i < ScenePaths.Length; i++)
            saved[i] = BackupScene(ScenePaths[i]);
        return saved;
    }

    public static string BackupScene(string sceneAssetPath)
    {
        string absSrc = GetAbsolutePath(sceneAssetPath);
        if (!File.Exists(absSrc))
            throw new FileNotFoundException("场景不存在：" + sceneAssetPath);

        EnsureBackupFolder();
        string stem = Path.GetFileNameWithoutExtension(sceneAssetPath);
        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string assetPath = $"{BackupFolder}/{stem}_{stamp}.scene";
        string absDest = GetAbsolutePath(assetPath);

        if (EditorSceneManager.GetActiveScene().path.Replace('\\', '/') == sceneAssetPath.Replace('\\', '/'))
            EditorSceneManager.SaveOpenScenes();

        File.Copy(absSrc, absDest, overwrite: false);
        AssetDatabase.ImportAsset(assetPath);
        Debug.Log($"[LibraryFloorsBackup] 完整场景已备份：{assetPath}");
        return assetPath;
    }

    public static int RestoreAllLatest()
    {
        int count = 0;
        foreach (string scenePath in ScenePaths)
        {
            string latest = FindLatestBackup(scenePath);
            if (string.IsNullOrEmpty(latest))
            {
                Debug.LogWarning($"[LibraryFloorsBackup] 无备份：{scenePath}");
                continue;
            }

            RestoreFromBackup(scenePath, latest);
            count++;
        }

        return count;
    }

    public static void RestoreFromBackup(string sceneAssetPath, string backupAssetPath)
    {
        string absBackup = GetAbsolutePath(backupAssetPath);
        if (!File.Exists(absBackup))
            throw new FileNotFoundException("备份不存在：" + backupAssetPath);

        File.Copy(absBackup, GetAbsolutePath(sceneAssetPath), overwrite: true);
        AssetDatabase.ImportAsset(sceneAssetPath);
        Debug.Log($"[LibraryFloorsBackup] 已恢复 {sceneAssetPath} <- {backupAssetPath}");
    }

    public static string FindLatestBackup(string sceneAssetPath)
    {
        string stem = Path.GetFileNameWithoutExtension(sceneAssetPath);
        string folder = GetAbsolutePath(BackupFolder);
        if (!Directory.Exists(folder))
            return null;

        string abs = Directory.GetFiles(folder, stem + "_*.scene")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
        if (string.IsNullOrEmpty(abs))
            return null;

        string projectRoot = GetProjectRoot().Replace('\\', '/');
        return abs.Replace('\\', '/').Replace(projectRoot + "/", "");
    }

    static void EnsureBackupFolder()
    {
        string abs = GetAbsolutePath(BackupFolder);
        if (!Directory.Exists(abs))
            Directory.CreateDirectory(abs);

        if (!AssetDatabase.IsValidFolder(BackupFolder))
        {
            if (AssetDatabase.IsValidFolder("Assets/scene"))
                AssetDatabase.CreateFolder("Assets/scene", "Backups");
        }
    }

    static string GetProjectRoot() => Path.GetDirectoryName(Application.dataPath);

    static string GetAbsolutePath(string assetPath) =>
        Path.Combine(GetProjectRoot(), assetPath.Replace('/', Path.DirectorySeparatorChar));
}
#endif
