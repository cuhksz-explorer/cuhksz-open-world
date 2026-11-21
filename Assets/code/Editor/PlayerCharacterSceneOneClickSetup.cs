#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 一键布置各场景 Player：隐藏烘焙的女主网格，改为运行时按账户所选角色（本科/研一/博一）动态加载。
/// </summary>
public static class PlayerCharacterSceneOneClickSetup
{
    const string MenuPath = "Tools/场景配置/一键布置场景可选主角";
    const string GameServerMenuPath = "Tools/Game Server/一键布置所有场景可选主角";

    static readonly string[] SkipSceneNameContains =
    {
        "Loadingscene",
        "MainMenu",
        "Benchmark",
        "Mirror/Examples",
    };

    static readonly string[] LegacyPlayerNameHints =
    {
        "自机角色女",
        "裸模",
    };

    [MenuItem(MenuPath, false, 4)]
    [MenuItem(GameServerMenuPath, false, 121)]
    public static void ConfigureAllScenes()
    {
        ConfigureAllScenes(skipConfirm: false);
    }

    public static void ConfigureAllScenes(bool skipConfirm, float progressBase = 0f, float progressSpan = 1f, bool suppressCompletionDialog = false)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("布置场景主角", "请先退出 Play Mode。", "确定");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        var scenePaths = CollectTargetScenePaths();
        if (scenePaths.Count == 0)
        {
            EditorUtility.DisplayDialog("布置场景主角", "未找到可处理的场景。", "确定");
            return;
        }

        if (!skipConfirm && !EditorUtility.DisplayDialog(
                "布置场景可选主角",
                $"将处理 {scenePaths.Count} 个场景：\n" +
                "• 标记 Player 根节点\n" +
                "• 添加 KeyInputController + Animator（WASD 骨架/动作）\n" +
                "• 保留场景烘焙模型（运行时换模成功后再隐藏）\n" +
                "• 运行时由 PlayerCharacterApplier 按用户所选角色加载 prefab\n\n" +
                "是否继续？",
                "继续",
                "取消"))
            return;

        string originalScene = SceneManager.GetActiveScene().path;
        var report = new StringBuilder();
        int configured = 0;
        int skipped = 0;
        int failed = 0;

        try
        {
            for (int i = 0; i < scenePaths.Count; i++)
            {
                string path = scenePaths[i];
                EditorUtility.DisplayProgressBar(
                    "布置场景可选主角",
                    path,
                    progressBase + progressSpan * ((float)i / scenePaths.Count));
                try
                {
                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    if (TryConfigureScene(scene, out string sceneNote))
                    {
                        EditorSceneManager.MarkSceneDirty(scene);
                        EditorSceneManager.SaveScene(scene);
                        configured++;
                        report.AppendLine("✓ " + path + " — " + sceneNote);
                    }
                    else
                    {
                        skipped++;
                        report.AppendLine("– " + path + " — " + sceneNote);
                    }
                }
                catch (Exception ex)
                {
                    failed++;
                    report.AppendLine("✗ " + path + " — " + ex.Message);
                    Debug.LogException(ex);
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            if (!string.IsNullOrEmpty(originalScene) && System.IO.File.Exists(originalScene))
                EditorSceneManager.OpenScene(originalScene, OpenSceneMode.Single);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[PlayerCharacterSceneSetup]\n" + report);
        if (!suppressCompletionDialog)
        {
            EditorUtility.DisplayDialog(
                "布置完成",
                $"已配置 {configured} 个场景，跳过 {skipped} 个，失败 {failed} 个。\n\n详见 Console。",
                "确定");
        }
    }

    [MenuItem(MenuPath + " (当前场景)", false, 5)]
    public static void ConfigureActiveScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("布置场景主角", "请先退出 Play Mode。", "确定");
            return;
        }

        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || string.IsNullOrEmpty(scene.path))
        {
            EditorUtility.DisplayDialog("布置场景主角", "请先保存并打开要处理的场景。", "确定");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        if (!TryConfigureScene(scene, out string note))
        {
            EditorUtility.DisplayDialog("布置场景主角", note, "确定");
            return;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorUtility.DisplayDialog("布置场景主角", "完成：" + note, "确定");
    }

    const string RestoreMenuPath = "Tools/场景配置/恢复所有场景Player可见（修复导入后隐形）";

    [MenuItem(RestoreMenuPath, false, 6)]
    public static void RestoreAllScenePlayerVisibility()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("恢复 Player 可见", "请先退出 Play Mode。", "确定");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        ConfigureAllScenes(skipConfirm: true, suppressCompletionDialog: true);
        EditorUtility.DisplayDialog(
            "恢复 Player 可见",
            "已重新处理所有场景：\n" +
            "• 恢复被隐藏的烘焙模型 Renderer\n" +
            "• 保留 WASD / Animator 配置\n\n" +
            "进入游戏后，换模成功时会自动隐藏旧模型。",
            "确定");
    }

    static List<string> CollectTargetScenePaths()
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var scene in EditorBuildSettings.scenes)
        {
            if (!scene.enabled || string.IsNullOrEmpty(scene.path))
                continue;
            if (ShouldSkipScenePath(scene.path))
                continue;
            if (seen.Add(scene.path))
                result.Add(scene.path);
        }

        const string sceneFolder = "Assets/scene";
        foreach (var guid in AssetDatabase.FindAssets("t:Scene", new[] { sceneFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path) || ShouldSkipScenePath(path))
                continue;
            if (seen.Add(path))
                result.Add(path);
        }

        result.Sort(StringComparer.OrdinalIgnoreCase);
        return result;
    }

    static bool ShouldSkipScenePath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return true;
        string normalized = path.Replace('\\', '/');
        for (int i = 0; i < SkipSceneNameContains.Length; i++)
        {
            if (normalized.IndexOf(SkipSceneNameContains[i], StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }
        return false;
    }

    static bool TryConfigureScene(Scene scene, out string note)
    {
        note = "";
        if (!scene.IsValid())
        {
            note = "场景无效";
            return false;
        }

        if (!TryFindPlayerRoot(scene, out GameObject player))
        {
            note = "未找到 Player / PlayerMovement / 女主根节点，已跳过";
            return false;
        }

        Undo.RegisterFullObjectHierarchyUndo(player, "Configure Player Character Scene Root");
        if (!player.CompareTag("Player"))
        {
            Undo.RecordObject(player, "Set Player Tag");
            player.tag = "Player";
        }

        if (player.GetComponent<PlayerCharacterSceneRoot>() == null)
            Undo.AddComponent<PlayerCharacterSceneRoot>(player);

        PlayerCharacterEditorShared.EnsureScenePlayerMovementComponents(player);

        RemoveEditorPreviewVisual(player.transform);
        int restored = RestoreLegacyPlayerRenderers(player.transform);

        note = restored > 0
            ? $"Player={player.name}，WASD+Animator 已配置，已恢复 {restored} 个烘焙 Renderer"
            : $"Player={player.name}，WASD+Animator 已配置（运行时换模）";
        return true;
    }

    static bool TryFindPlayerRoot(Scene scene, out GameObject player)
    {
        player = null;
        var roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            player = FindPlayerRecursive(roots[i].transform);
            if (player != null)
                return true;
        }
        return false;
    }

    static GameObject FindPlayerRecursive(Transform transform)
    {
        if (transform == null)
            return null;

        if (transform.CompareTag("Player"))
            return transform.gameObject;

        if (transform.GetComponent<PlayerMovement>() != null)
            return transform.gameObject;

        if (IsLegacyFemaleRootName(transform.name))
            return transform.gameObject;

        for (int i = 0; i < transform.childCount; i++)
        {
            var found = FindPlayerRecursive(transform.GetChild(i));
            if (found != null)
                return found;
        }

        return null;
    }

    static bool IsLegacyFemaleRootName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return false;
        for (int i = 0; i < LegacyPlayerNameHints.Length; i++)
        {
            if (name.IndexOf(LegacyPlayerNameHints[i], StringComparison.Ordinal) >= 0)
                return true;
        }
        return false;
    }

    static void RemoveEditorPreviewVisual(Transform player)
    {
        for (int i = player.childCount - 1; i >= 0; i--)
        {
            var child = player.GetChild(i);
            if (child == null)
                continue;
            if (child.name == "PlayerCharacterVisual" || child.GetComponent<PlayerCharacterVisualMarker>() != null)
                Undo.DestroyObjectImmediate(child.gameObject);
        }
    }

    static int RestoreLegacyPlayerRenderers(Transform player)
    {
        int count = 0;
        var renderers = player.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            var renderer = renderers[i];
            if (renderer == null || IsProtectedTransform(renderer.transform, player))
                continue;
            if (IsUnderRuntimeVisual(renderer.transform, player))
                continue;
            if (renderer.enabled)
                continue;
            Undo.RecordObject(renderer, "Restore Legacy Player Renderer");
            renderer.enabled = true;
            count++;
        }
        return count;
    }

    static bool IsUnderRuntimeVisual(Transform t, Transform playerRoot)
    {
        while (t != null && t != playerRoot)
        {
            if (string.Equals(t.name, "PlayerCharacterVisual", System.StringComparison.Ordinal))
                return true;
            if (t.GetComponent<PlayerCharacterVisualMarker>() != null)
                return true;
            t = t.parent;
        }
        return false;
    }

    static bool IsProtectedTransform(Transform t, Transform playerRoot)
    {
        while (t != null && t != playerRoot)
        {
            string n = t.name ?? "";
            if (n.IndexOf("Camera", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (t.GetComponent<Camera>() != null)
                return true;
            t = t.parent;
        }
        return false;
    }
}
#endif
