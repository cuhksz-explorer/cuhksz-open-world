#if UNITY_EDITOR
using System.IO;
using GameQuestSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 将所有场景中 QuestSystemUI 简要任务栏的布局/字号统一为 undergarden（下园）标准。
/// </summary>
public static class QuestBriefPanelStyleSync
{
    const string MenuPath = "Tools/场景配置/统一下园简要任务栏样式";
    const string SceneFolder = "Assets/scene";

    [MenuItem(MenuPath, false, 11)]
    public static void SyncAllProjectScenes()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("统一样式", "请先退出 Play Mode。", "确定");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        var scenePaths = Directory.GetFiles(SceneFolder, "*.unity", SearchOption.TopDirectoryOnly);
        int updatedScenes = 0;
        int updatedUi = 0;
        var activePath = SceneManager.GetActiveScene().path;

        try
        {
            for (int i = 0; i < scenePaths.Length; i++)
            {
                var path = scenePaths[i].Replace('\\', '/');
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var uiList = Object.FindObjectsOfType<QuestSystemUI>(true);
                if (uiList.Length == 0)
                    continue;

                bool sceneDirty = false;
                for (int j = 0; j < uiList.Length; j++)
                {
                    var ui = uiList[j];
                    ui.EditorApplyUndergardenBriefPanelStyle();
                    EditorUtility.SetDirty(ui);
                    sceneDirty = true;
                    updatedUi++;
                }

                if (sceneDirty)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    updatedScenes++;
                }
            }
        }
        finally
        {
            if (!string.IsNullOrEmpty(activePath) && File.Exists(activePath))
                EditorSceneManager.OpenScene(activePath, OpenSceneMode.Single);
        }

        EditorUtility.DisplayDialog(
            "统一样式完成",
            $"已处理 {updatedScenes} 个场景，共 {updatedUi} 处 QuestSystemUI。\n简要栏已对齐 undergarden：标题/进度/简介三行、35 号字、400×400 面板。",
            "确定");
    }
}
#endif
