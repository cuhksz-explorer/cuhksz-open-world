#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameQuestSystem;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Sketchfab 模型导入目录 + 剧情 NPC 外观配置（项目内 Supercyan 示例角色 / 用户下载的 FBX）。
/// </summary>
public static class StoryNpcSketchfabImportMenu
{
    const string StoryNpcFolder = "Assets/NPCs/Story";
    const float UserModelSizeScale = 2f;
    const float UserModelPitchDegrees = -90f;

    [MenuItem("Tools/Quest System/剧情NPC/更新镜像类NPC为主角虚化模型", false, 91)]
    [MenuItem("Tools/场景配置/剧情NPC/更新镜像类NPC为主角虚化模型", false, 91)]
    public static void ApplyAllMirrorPlayerNpcModels()
    {
        int applied = 0;
        applied += ApplyPlayerGhostNpcModel("镜像林渡") ? 1 : 0;
        applied += ApplyPlayerGhostNpcModel("镜像") ? 1 : 0;
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog(
            "镜像类 NPC",
            applied > 0
                ? $"已更新 {applied} 个镜像 NPC（主角模型 + 虚化）。"
                : "未找到可更新的镜像 NPC 预制体。",
            "确定");
    }

    [MenuItem("Tools/Quest System/剧情NPC/更新镜像林渡为主角虚化模型", false, 92)]
    [MenuItem("Tools/场景配置/剧情NPC/更新镜像林渡为主角虚化模型", false, 92)]
    public static void ApplyMirrorLinduNpcModel()
    {
        if (ApplyPlayerGhostNpcModel("镜像林渡"))
        {
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("镜像林渡", "已替换为主角模型并启用虚化效果。", "确定");
        }
    }

    [MenuItem("Tools/Quest System/剧情NPC/更新镜像为主角虚化模型", false, 93)]
    [MenuItem("Tools/场景配置/剧情NPC/更新镜像为主角虚化模型", false, 93)]
    public static void ApplyMirrorNpcModel()
    {
        if (ApplyPlayerGhostNpcModel("镜像"))
        {
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("镜像", "已替换为主角模型并启用虚化效果。", "确定");
        }
    }

    static bool ApplyPlayerGhostNpcModel(string displayName)
    {
        string prefabPath = StoryNpcFolder + "/" + displayName + ".prefab";
        if (!File.Exists(prefabPath))
            return false;

        var playerModel = StoryNpcVisualUtility.LoadModelAsset(StoryNpcUserModelMapping.PlayerModelFbxPath);
        if (playerModel == null)
        {
            Debug.LogWarning("[StoryNpc] 未找到主角 FBX：" + StoryNpcUserModelMapping.PlayerModelFbxPath);
            return false;
        }

        var temp = UnityEngine.Object.Instantiate(playerModel);
        temp.name = displayName + "_ModelSource";
        try
        {
            StoryNpcVisualUtility.ApplyCharacterToStoryNpcPrefab(
                prefabPath,
                temp,
                scale: 1f,
                groundOffsetY: 0f,
                targetHeightMeters: 1.68f * UserModelSizeScale,
                yawOffsetDegrees: 180f,
                pitchOffsetDegrees: UserModelPitchDegrees,
                displayName: displayName);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(temp);
        }

        return true;
    }

    [MenuItem("Tools/Quest System/剧情NPC/一键导入 NPC 角色模型", false, 90)]
    [MenuItem("Tools/场景配置/剧情NPC/一键导入 NPC 角色模型", false, 90)]
    [MenuItem("Tools/场景配置/一键导入 NPC 角色模型", false, 90)]
    [MenuItem("Tools/剧情NPC/一键导入 NPC 角色模型", false, 90)]
    public static void ApplyUserProvidedNpcModels()
    {
        ApplyAllUserProvidedNpcModels();
        EditorUtility.DisplayDialog("剧情NPC模型", "已写入剧情 NPC 预制体。请打开 undergarden 等场景查看。", "确定");
    }

    /// <summary>BatchMode: -executeMethod StoryNpcSketchfabImportMenu.ApplyAllUserProvidedNpcModels</summary>
    public static void ApplyAllUserProvidedNpcModels()
    {
        AssetDatabase.Refresh();

        var playerModel = StoryNpcVisualUtility.LoadModelAsset(StoryNpcUserModelMapping.PlayerModelFbxPath);
        if (playerModel == null)
            Debug.LogWarning("[StoryNpc] 未找到玩家 FBX：" + StoryNpcUserModelMapping.PlayerModelFbxPath);

        int applied = 0;
        int skipped = 0;
        foreach (var prefabPath in Directory.GetFiles(StoryNpcFolder, "*.prefab"))
        {
            string assetPath = prefabPath.Replace('\\', '/');
            string displayName = Path.GetFileNameWithoutExtension(assetPath);
            if (StoryNpcVisualUtility.ShouldSkipStoryNpc(displayName))
            {
                skipped++;
                continue;
            }

            GameObject source = null;
            float height = ResolveUserModelTargetHeight(displayName);

            if (StoryNpcUserModelMapping.UsesPlayerModel(displayName))
                source = playerModel;
            else if (StoryNpcUserModelMapping.TryResolveGlbPath(displayName, out string glbPath))
            {
                source = StoryNpcVisualUtility.LoadModelAsset(glbPath);
                if (source == null)
                    Debug.LogWarning("[StoryNpc] 无法加载模型：" + glbPath + " → " + displayName);
            }

            if (source == null)
            {
                skipped++;
                continue;
            }

            var temp = UnityEngine.Object.Instantiate(source);
            temp.name = displayName + "_ModelSource";
            try
            {
                StoryNpcVisualUtility.ApplyCharacterToStoryNpcPrefab(
                    assetPath,
                    temp,
                    scale: 1f,
                    groundOffsetY: 0f,
                    targetHeightMeters: height * UserModelSizeScale,
                    yawOffsetDegrees: 180f,
                    pitchOffsetDegrees: UserModelPitchDegrees);
                applied++;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(temp);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[StoryNpc] 已应用模型 {applied} 个，跳过 {skipped} 个。");
    }

    static float ResolveUserModelTargetHeight(string displayName)
    {
        if (StoryNpcUserModelMapping.UsesPlayerModel(displayName))
            return 1.68f;
        if (displayName.Contains("阿姨") || displayName.Contains("教授") || displayName.Contains("老师"))
            return displayName.Contains("阿姨") ? 1.62f : 1.72f;
        if (StoryNpcVisualUtility.LooksLikeFemaleNpc(displayName))
            return 1.68f;
        return 1.78f;
    }

    [MenuItem("Tools/Quest System/剧情NPC/创建 Sketchfab 导入目录", false, 120)]
    [MenuItem("Tools/场景配置/剧情NPC/创建 Sketchfab 导入目录", false, 120)]
    public static void CreateSketchfabImportFolder()
    {
        EnsureFolder("Assets/Imports");
        EnsureFolder(StoryNpcVisualUtility.SketchfabImportRoot);
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog(
            "Sketchfab 导入目录",
            "已创建：\n" + StoryNpcVisualUtility.SketchfabImportRoot +
            "\n\n请将 Sketchfab 下载的 .fbx / .glb 解压后放入该目录，再执行「从导入目录配置到剧情NPC」。",
            "确定");
    }

    [MenuItem("Tools/Quest System/剧情NPC/一键应用 Supercyan 示例角色到剧情NPC", false, 121)]
    [MenuItem("Tools/场景配置/剧情NPC/一键应用 Supercyan 示例角色到剧情NPC", false, 121)]
    public static void ApplySupercyanToAllStoryNpcs()
    {
        var male = AssetDatabase.LoadAssetAtPath<GameObject>(StoryNpcVisualUtility.SupercyanMalePrefab);
        var alt = AssetDatabase.LoadAssetAtPath<GameObject>(StoryNpcVisualUtility.CharacterQPrefab);
        if (male == null)
        {
            EditorUtility.DisplayDialog("剧情NPC外观", "未找到 Supercyan 男性预制体。", "确定");
            return;
        }

        int applied = 0;
        int skipped = 0;
        foreach (var path in Directory.GetFiles(StoryNpcFolder, "*.prefab"))
        {
            string assetPath = path.Replace('\\', '/');
            string name = Path.GetFileNameWithoutExtension(assetPath);
            if (StoryNpcVisualUtility.ShouldSkipStoryNpc(name))
            {
                skipped++;
                continue;
            }

            var prefab = name == "周砚" || name == "赵承勋" ? (alt != null ? alt : male) : male;
            float scale = StoryNpcVisualUtility.ResolveDefaultScale(name);
            StoryNpcVisualUtility.ApplyCharacterToStoryNpcPrefab(assetPath, prefab, scale);
            applied++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog(
            "剧情NPC外观",
            $"已为 {applied} 个剧情 NPC 替换胶囊模型。\n跳过 {skipped} 个（触发点/镜像等）。\n\n女性角色暂用略缩小比例的男性示例体，可从 Sketchfab 导入后单独替换。",
            "确定");
    }

    [MenuItem("Tools/Quest System/剧情NPC/从 Sketchfab 导入目录配置到剧情NPC", false, 122)]
    [MenuItem("Tools/场景配置/剧情NPC/从 Sketchfab 导入目录配置到剧情NPC", false, 122)]
    public static void ApplyImportedModelsFromFolder()
    {
        var models = StoryNpcVisualUtility.FindImportedModelAssetPaths();
        if (models.Count == 0)
        {
            EditorUtility.DisplayDialog(
                "Sketchfab 导入",
                "导入目录中没有模型。\n请先执行「创建 Sketchfab 导入目录」，登录 Sketchfab 下载 FBX/GLB 后放入该文件夹。",
                "确定");
            return;
        }

        var npcPrefabs = Directory.GetFiles(StoryNpcFolder, "*.prefab")
            .Select(p => p.Replace('\\', '/'))
            .Where(p => !StoryNpcVisualUtility.ShouldSkipStoryNpc(Path.GetFileNameWithoutExtension(p)))
            .OrderBy(p => p)
            .ToList();

        int applied = 0;
        var unmatchedModels = new List<string>(models);
        foreach (var npcPath in npcPrefabs)
        {
            string npcName = Path.GetFileNameWithoutExtension(npcPath);
            string matched = FindBestModelForNpc(npcName, unmatchedModels);
            if (matched == null)
                continue;

            var source = AssetDatabase.LoadAssetAtPath<GameObject>(matched);
            if (source == null)
                continue;

            var instanceRoot = InstantiateModelForNpc(source);
            if (instanceRoot == null)
                continue;

            try
            {
                StoryNpcVisualUtility.ApplyCharacterToStoryNpcPrefab(npcPath, instanceRoot, 1f);
                applied++;
                unmatchedModels.Remove(matched);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instanceRoot);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog(
            "Sketchfab 导入",
            $"已按文件名匹配并配置 {applied} 个剧情 NPC。\n未匹配的模型 {unmatchedModels.Count} 个（可重命名 FBX 为角色名后重试）。",
            "确定");
    }

    [MenuItem("Tools/Quest System/剧情NPC/打开 Sketchfab 推荐模型清单", false, 123)]
    public static void OpenRecommendedList()
    {
        string path = StoryNpcVisualUtility.SketchfabImportRoot + "/RecommendedModels.md";
        if (!File.Exists(path))
            CreateSketchfabImportFolder();
        EditorUtility.OpenWithDefaultApp(path);
    }

    static GameObject InstantiateModelForNpc(GameObject source)
    {
        if (source == null)
            return null;

        string path = AssetDatabase.GetAssetPath(source);
        if (!string.IsNullOrEmpty(path) && path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
        {
            var temp = new GameObject("_StoryNpcModelTemp");
            var child = UnityEngine.Object.Instantiate(source, temp.transform);
            child.name = Path.GetFileNameWithoutExtension(path);
            return temp;
        }

        return UnityEngine.Object.Instantiate(source);
    }

    static string FindBestModelForNpc(string npcDisplayName, List<string> modelPaths)
    {
        if (string.IsNullOrWhiteSpace(npcDisplayName) || modelPaths == null || modelPaths.Count == 0)
            return null;

        string normalizedNpc = NormalizeMatchKey(npcDisplayName);
        string best = null;
        int bestScore = 0;
        for (int i = 0; i < modelPaths.Count; i++)
        {
            string fileName = Path.GetFileNameWithoutExtension(modelPaths[i]);
            string normalizedFile = NormalizeMatchKey(fileName);
            int score = 0;
            if (normalizedFile == normalizedNpc)
                score = 100;
            else if (normalizedFile.Contains(normalizedNpc) || normalizedNpc.Contains(normalizedFile))
                score = 60;
            else if (ShareEnoughChars(normalizedNpc, normalizedFile))
                score = 30;

            if (score > bestScore)
            {
                bestScore = score;
                best = modelPaths[i];
            }
        }

        return bestScore >= 30 ? best : null;
    }

    static string NormalizeMatchKey(string s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return "";
        s = s.Trim();
        foreach (var ch in new[] { '（', '）', '(', ')', ' ', '_', '-', '·' })
            s = s.Replace(ch.ToString(), "");
        return s.ToLowerInvariant();
    }

    static bool ShareEnoughChars(string a, string b)
    {
        if (a.Length < 2 || b.Length < 2)
            return false;
        int shared = 0;
        foreach (char c in a)
        {
            if (b.IndexOf(c) >= 0)
                shared++;
        }

        return shared >= 2;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;
        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        string name = Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }
}
#endif
