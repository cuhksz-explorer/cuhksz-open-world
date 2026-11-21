#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 批量统一所有场景中的 InteractPrompt_Shared 为 480×320，并合并重复面板、重绑交互器。
/// </summary>
public static class InteractPromptAllScenesConfigurator
{
    const string MenuPath = "Tools/UI/Interaction Prompt/统一所有场景交互UI (480×320)";

    [MenuItem(MenuPath, false, 6)]
    public static void ApplyToAllScenesInProject()
    {
        if (!EditorUtility.DisplayDialog(
                "交互 UI 批量统一",
                "将处理 Assets/scene 下全部 .unity 场景：\n" +
                "· 合并重复的 InteractPrompt_Shared\n" +
                "· 面板尺寸统一为 480×320\n" +
                "· 重绑全部交互器 / 对话 / 赛车 HUD\n\n" +
                "未保存的当前场景修改请先保存。是否继续？",
                "继续",
                "取消"))
        {
            return;
        }

        string sceneRoot = Path.Combine(Application.dataPath, "scene");
        if (!Directory.Exists(sceneRoot))
        {
            EditorUtility.DisplayDialog("交互 UI", "未找到 Assets/scene 目录。", "确定");
            return;
        }

        string[] scenePaths = Directory.GetFiles(sceneRoot, "*.unity", SearchOption.AllDirectories);
        var originalSetup = EditorSceneManager.GetSceneManagerSetup();
        int changedScenes = 0;
        int removedPanels = 0;

        try
        {
            for (int i = 0; i < scenePaths.Length; i++)
            {
                string assetPath = ToAssetPath(scenePaths[i]);
                if (string.IsNullOrEmpty(assetPath) || assetPath.EndsWith(".bak.unity", System.StringComparison.OrdinalIgnoreCase))
                    continue;
                if (GameplayUiSceneExclusions.ShouldSkipScenePath(assetPath))
                    continue;

                EditorUtility.DisplayProgressBar(
                    "统一交互 UI",
                    assetPath,
                    scenePaths.Length <= 1 ? 1f : (float)i / scenePaths.Length);

                var scene = EditorSceneManager.OpenScene(assetPath, OpenSceneMode.Single);
                if (!scene.IsValid())
                    continue;

                removedPanels += ConfigureLoadedScene(scene);
                if (scene.isDirty)
                {
                    EditorSceneManager.SaveScene(scene);
                    changedScenes++;
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            if (originalSetup != null && originalSetup.Length > 0)
                EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
        }

        EditorUtility.DisplayDialog(
            "交互 UI 批量统一",
            $"已处理 {scenePaths.Length} 个场景文件。\n保存变更：{changedScenes} 个\n删除重复面板：{removedPanels} 套",
            "确定");
    }

    public static int ConfigureLoadedScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return 0;

        var panels = FindAllSharedPanelsInScene(scene);
        if (panels.Count == 0)
            return 0;

        GameObject canonical = InteractionPromptOneClickSetup.SelectBestCanonicalPanelPublic(panels);
        if (canonical == null)
            return 0;

        var panelRt = canonical.GetComponent<RectTransform>();
        var nameTmp = InteractionPromptOneClickSetup.FindTmpInPanelPublic(canonical.transform, "InteractNameText");
        var typeTmp = InteractionPromptOneClickSetup.FindTmpInPanelPublic(canonical.transform, "InteractTypeText");
        if (panelRt == null || nameTmp == null || typeTmp == null)
            return 0;

        InteractPromptSharedStyle.ApplyIfNeeded(panelRt);
        InteractionPromptOneClickSetup.BindAllConsumersEditorPublic(panelRt, nameTmp, typeTmp);

        int removed = 0;
        for (int i = 0; i < panels.Count; i++)
        {
            if (panels[i] == null || panels[i] == canonical)
                continue;
            Undo.DestroyObjectImmediate(panels[i]);
            removed++;
        }

        EnsureFollower(canonical);
        EditorSceneManager.MarkSceneDirty(scene);
        return removed;
    }

    static void EnsureFollower(GameObject panelGo)
    {
        var follower = panelGo.GetComponent<InteractPromptSharedScreenFollower>();
        if (follower == null)
            follower = panelGo.AddComponent<InteractPromptSharedScreenFollower>();
        follower.CaptureDefaultLayout();
    }

    static List<GameObject> FindAllSharedPanelsInScene(Scene scene)
    {
        var list = new List<GameObject>(2);
        if (!scene.IsValid() || !scene.isLoaded)
            return list;

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
            CollectSharedPanelsRecursive(roots[i].transform, list);
        return list;
    }

    static void CollectSharedPanelsRecursive(Transform root, List<GameObject> list)
    {
        if (root == null)
            return;
        if (string.Equals(root.name, InteractPromptSharedUiLocator.PanelObjectName, System.StringComparison.Ordinal))
            list.Add(root.gameObject);
        for (int i = 0; i < root.childCount; i++)
            CollectSharedPanelsRecursive(root.GetChild(i), list);
    }

    static string ToAssetPath(string fullPath)
    {
        fullPath = fullPath.Replace('\\', '/');
        string dataPath = Application.dataPath.Replace('\\', '/');
        if (!fullPath.StartsWith(dataPath, System.StringComparison.OrdinalIgnoreCase))
            return null;
        return "Assets" + fullPath.Substring(dataPath.Length);
    }
}
#endif
