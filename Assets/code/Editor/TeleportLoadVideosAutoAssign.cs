using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

/// <summary>
/// 将 scene/Materials 下「11月25日」系列 mp4 批量写入所有传送相关组件的随机视频数组。
/// </summary>
public static class TeleportLoadVideosAutoAssign
{
    const string MaterialsFolder = "Assets/scene/Materials";

    /// <summary>每组按顺序尝试：半角括号、全角括号文件名。</summary>
    static readonly string[][] NameVariants =
    {
        new[] { "11月25日" },
        new[] { "11月25日(1)", "11月25日（1）" },
        new[] { "11月25日(2)", "11月25日（2）" },
        new[] { "11月25日(3)", "11月25日（3）" },
    };

    [MenuItem("Tools/传送/自动绑定 11月25日 加载视频（全场景+预制体）")]
    public static void Run()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        if (!TryBuildClipArray(out VideoClip[] clips, out string hint))
        {
            EditorUtility.DisplayDialog("传送加载视频", hint, "确定");
            return;
        }

        int touched = 0;
        string originalPath = SceneManager.GetActiveScene().path;

        try
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Scene"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.StartsWith("Assets/") || !path.EndsWith(".unity", System.StringComparison.OrdinalIgnoreCase))
                    continue;

                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                int sceneTouched = AssignInLoadedScene(clips);
                if (sceneTouched > 0)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    touched += sceneTouched;
                    EditorSceneManager.SaveScene(scene);
                }
            }

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.StartsWith("Assets/") || !path.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase))
                    continue;

                touched += AssignInPrefab(path, clips);
            }
        }
        finally
        {
            if (!string.IsNullOrEmpty(originalPath))
            {
                try
                {
                    EditorSceneManager.OpenScene(originalPath, OpenSceneMode.Single);
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[TeleportLoadVideos] 无法重新打开原场景: {originalPath}\n{e.Message}");
                }
            }
        }

        AssetDatabase.SaveAssets();
        string msg = $"已写入 {touched} 个组件（TeleportButton / MapSystem / 公交车传送）。\n{hint}";
        EditorUtility.DisplayDialog("传送加载视频", msg, "确定");
        Debug.Log($"[TeleportLoadVideos] 绑定 {clips.Length} 个 VideoClip，共 {touched} 处。{hint}");
    }

    static bool TryBuildClipArray(out VideoClip[] clips, out string message)
    {
        message = "";
        var list = new List<VideoClip>();
        var missing = new List<string>();

        foreach (string[] variants in NameVariants)
        {
            VideoClip found = LoadClipByCandidateNames(variants);

            if (found != null)
                list.Add(found);
            else
                missing.Add(variants[0]);
        }

        clips = list.ToArray();
        if (clips.Length == 0)
        {
            message = $"未在 {MaterialsFolder} 找到任何匹配的 .mp4。";
            return false;
        }

        if (missing.Count > 0)
            message = $"缺少文件（已跳过）: {string.Join(", ", missing)}";

        string foundNames = (clips != null && clips.Length > 0)
            ? string.Join(", ", System.Array.ConvertAll(clips, c => c != null ? c.name : "NULL"))
            : "";
        if (!string.IsNullOrWhiteSpace(foundNames))
            message = string.IsNullOrWhiteSpace(message) ? $"已找到：{foundNames}" : $"{message}\n已找到：{foundNames}";

        return true;
    }

    static VideoClip LoadClipByCandidateNames(string[] candidateNames)
    {
        if (candidateNames == null || candidateNames.Length == 0)
            return null;

        // 1) 精确路径加载（最准确）
        for (int i = 0; i < candidateNames.Length; i++)
        {
            string name = candidateNames[i];
            if (string.IsNullOrWhiteSpace(name))
                continue;

            string rel = $"{MaterialsFolder}/{name}.mp4";
            var clip = AssetDatabase.LoadAssetAtPath<VideoClip>(rel);
            if (clip != null)
                return clip;
        }

        // 2) 名称兜底搜索（处理全角/半角括号、或 Unity 导入资源名差异）
        for (int i = 0; i < candidateNames.Length; i++)
        {
            string name = candidateNames[i];
            if (string.IsNullOrWhiteSpace(name))
                continue;

            string[] guids = AssetDatabase.FindAssets($"t:VideoClip {name}", new[] { MaterialsFolder });
            for (int k = 0; k < guids.Length; k++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[k]);
                if (string.IsNullOrEmpty(path))
                    continue;
                if (!path.EndsWith(".mp4", System.StringComparison.OrdinalIgnoreCase))
                    continue;

                var clip = AssetDatabase.LoadAssetAtPath<VideoClip>(path);
                if (clip != null)
                    return clip;
            }
        }

        return null;
    }

    static bool IsSwitchController(GameObject go)
    {
        if (go == null)
            return false;

        Transform t = go.transform;
        while (t != null)
        {
            if (!string.IsNullOrEmpty(t.name) &&
                t.name.StartsWith("switchcontroller", System.StringComparison.OrdinalIgnoreCase))
                return true;
            t = t.parent;
        }

        return false;
    }

    static int AssignInLoadedScene(VideoClip[] clips)
    {
        int n = 0;
        foreach (TeleportButton tb in Object.FindObjectsOfType<TeleportButton>(true))
        {
            if (ApplyTeleportButton(tb, clips))
                n++;
        }

        foreach (MapSystem ms in Object.FindObjectsOfType<MapSystem>(true))
        {
            if (ApplyMapSystem(ms, clips))
                n++;
        }

        foreach (SpawnMoveRotateDisappearSceneSwitch bus in Object.FindObjectsOfType<SpawnMoveRotateDisappearSceneSwitch>(true))
        {
            if (!IsSwitchController(bus.gameObject))
                continue;
            if (ApplyBus(bus, clips))
                n++;
        }

        return n;
    }

    static int AssignInPrefab(string prefabPath, VideoClip[] clips)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        int n = 0;
        try
        {
            foreach (TeleportButton tb in root.GetComponentsInChildren<TeleportButton>(true))
            {
                if (ApplyTeleportButton(tb, clips))
                    n++;
            }

            foreach (MapSystem ms in root.GetComponentsInChildren<MapSystem>(true))
            {
                if (ApplyMapSystem(ms, clips))
                    n++;
            }

            foreach (SpawnMoveRotateDisappearSceneSwitch bus in root.GetComponentsInChildren<SpawnMoveRotateDisappearSceneSwitch>(true))
            {
                if (!IsSwitchController(bus.gameObject))
                    continue;
                if (ApplyBus(bus, clips))
                    n++;
            }

            if (n > 0)
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        return n;
    }

    static bool ApplyTeleportButton(TeleportButton tb, VideoClip[] clips)
    {
        Undo.RecordObject(tb, "Bind teleport load videos");
        var so = new SerializedObject(tb);
        so.Update();
        if (!AssignVariantArray(so, "loadVideoClipVariants", clips))
            return false;
        AssignFallback(so, "loadVideoClip", clips);
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(tb);
        return true;
    }

    static bool ApplyMapSystem(MapSystem ms, VideoClip[] clips)
    {
        Undo.RecordObject(ms, "Bind teleport load videos");
        var so = new SerializedObject(ms);
        so.Update();
        if (!AssignVariantArray(so, "teleportVideoClipVariants", clips))
            return false;
        AssignFallback(so, "teleportVideoClip", clips);
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(ms);
        return true;
    }

    static bool ApplyBus(SpawnMoveRotateDisappearSceneSwitch bus, VideoClip[] clips)
    {
        Undo.RecordObject(bus, "Bind teleport load videos");
        var so = new SerializedObject(bus);
        so.Update();

        // 你之前提到「公交站 useLoadVideo 没有自动勾选」：
        // 写入数组/备用视频时不要因为某个字段找不到而直接返回，改为尽可能把能写的都写进去。
        AssignVariantArray(so, "loadVideoClipVariants", clips);
        AssignFallback(so, "loadVideoClip", clips);

        bool anyClip = false;
        if (clips != null)
        {
            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i] != null)
                {
                    anyClip = true;
                    break;
                }
            }
        }

        SerializedProperty useProp = so.FindProperty("useLoadVideo");
        if (useProp != null && useProp.propertyType == SerializedPropertyType.Boolean && anyClip)
            useProp.boolValue = true;

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(bus);
        return true;
    }

    static bool AssignVariantArray(SerializedObject so, string propName, VideoClip[] clips)
    {
        SerializedProperty p = so.FindProperty(propName);
        if (p == null || !p.isArray)
            return false;

        p.arraySize = clips.Length;
        for (int i = 0; i < clips.Length; i++)
            p.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];

        return true;
    }

    static void AssignFallback(SerializedObject so, string propName, VideoClip[] clips)
    {
        if (clips == null || clips.Length == 0 || clips[0] == null)
            return;
        SerializedProperty p = so.FindProperty(propName);
        if (p != null)
            p.objectReferenceValue = clips[0];
    }
}
