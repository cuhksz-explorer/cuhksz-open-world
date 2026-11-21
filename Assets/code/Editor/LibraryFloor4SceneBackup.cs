#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// libraryfloor4 场景备份与恢复（Reimport 前请先备份）。
/// </summary>
public static class LibraryFloor4SceneBackup
{
    public const string ScenePath = "Assets/scene/libraryfloor4.scene";
    public const string BackupFolder = "Assets/scene/Backups";

    const string MenuBackup = "Tools/场景/libraryfloor4 - 备份当前场景";
    const string MenuRestoreLatest = "Tools/场景/libraryfloor4 - 从最新备份恢复场景";
    const string MenuOpenBackupFolder = "Tools/场景/libraryfloor4 - 打开备份文件夹";

    [MenuItem(MenuBackup, false, 40)]
    public static void BackupFromMenu()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("libraryfloor4 备份", "请先退出 Play Mode。", "确定");
            return;
        }

        string path = BackupScene();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("libraryfloor4 备份", $"已备份到：\n{path}", "确定");
    }

    [MenuItem(MenuRestoreLatest, false, 41)]
    public static void RestoreLatestFromMenu()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("libraryfloor4 恢复", "请先退出 Play Mode。", "确定");
            return;
        }

        string latest = FindLatestBackup();
        if (string.IsNullOrEmpty(latest))
        {
            EditorUtility.DisplayDialog("libraryfloor4 恢复", $"未找到备份。\n目录：{BackupFolder}", "确定");
            return;
        }

        if (!EditorUtility.DisplayDialog(
                "libraryfloor4 恢复",
                $"将用备份覆盖当前场景：\n{latest}\n\n当前未保存的修改会丢失，是否继续？",
                "恢复",
                "取消"))
            return;

        RestoreFromBackup(latest);
        EditorUtility.DisplayDialog("libraryfloor4 恢复", "场景已恢复，请检查 Hierarchy。", "确定");
    }

    [MenuItem(MenuOpenBackupFolder, false, 42)]
    public static void OpenBackupFolderFromMenu()
    {
        EnsureBackupFolder();
        EditorUtility.RevealInFinder(Path.Combine(GetProjectRoot(), BackupFolder));
    }

    public static string BackupScene()
    {
        if (!File.Exists(GetAbsolutePath(ScenePath)))
            throw new FileNotFoundException($"场景不存在：{ScenePath}");

        EnsureBackupFolder();
        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string assetPath = $"{BackupFolder}/libraryfloor4_{stamp}.scene";
        string absDest = GetAbsolutePath(assetPath);

        if (EditorSceneManager.GetActiveScene().path.Replace('\\', '/') == ScenePath.Replace('\\', '/'))
            EditorSceneManager.SaveOpenScenes();

        File.Copy(GetAbsolutePath(ScenePath), absDest, overwrite: false);
        AssetDatabase.ImportAsset(assetPath);
        Debug.Log($"[libraryfloor4] 场景已备份：{assetPath}");
        return assetPath;
    }

    public static void RestoreFromBackup(string backupAssetPath)
    {
        string absBackup = GetAbsolutePath(backupAssetPath);
        if (!File.Exists(absBackup))
            throw new FileNotFoundException($"备份不存在：{backupAssetPath}");

        string absScene = GetAbsolutePath(ScenePath);
        File.Copy(absBackup, absScene, overwrite: true);
        AssetDatabase.ImportAsset(ScenePath);
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Debug.Log($"[libraryfloor4] 已从备份恢复：{backupAssetPath}");
    }

    static string FindLatestBackup()
    {
        string folder = GetAbsolutePath(BackupFolder);
        if (!Directory.Exists(folder))
            return null;

        string abs = Directory.GetFiles(folder, "libraryfloor4_*.scene")
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
            if (!AssetDatabase.IsValidFolder("Assets/scene"))
                return;
            AssetDatabase.CreateFolder("Assets/scene", "Backups");
        }
    }

    static string GetProjectRoot() => Path.GetDirectoryName(Application.dataPath);

    static string GetAbsolutePath(string assetPath) =>
        Path.Combine(GetProjectRoot(), assetPath.Replace('/', Path.DirectorySeparatorChar));
}
#endif
