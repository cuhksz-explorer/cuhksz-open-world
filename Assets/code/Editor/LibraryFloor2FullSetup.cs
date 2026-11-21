#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// libraryfloor2：批处理一键完成模型导入、undergarden UI/主角、外壳。
/// </summary>
public static class LibraryFloor2FullSetup
{
    const string MenuPath = "Tools/场景/libraryfloor2 - 一键完整配置（模型+UI+外壳）";

    [MenuItem(MenuPath, false, 48)]
    public static void RunAllFromMenu()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("libraryfloor2", "请先退出 Play Mode。", "确定");
            return;
        }

        try
        {
            RunAll();
            EditorUtility.DisplayDialog("libraryfloor2", "完整配置已完成。", "确定");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            EditorUtility.DisplayDialog("libraryfloor2", $"配置失败：{ex.Message}", "确定");
        }
    }

    /// <summary>BatchMode: -executeMethod LibraryFloor2FullSetup.RunAll</summary>
    public static void RunAll()
    {
        UndergardenSceneOneClickConfigurator.AddProjectScenesToBuildSettings();
        LibraryFloor2SceneSetup.ImportAndCreateScene();
        LibraryFloor2UndergardenUiSetup.ConfigureAndSave();

        try
        {
            LibraryFloor2ShellSetup.BuildAndSave();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[libraryfloor2] 外壳生成跳过：{ex.Message}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[libraryfloor2] 一键完整配置完成。");
    }
}
#endif
