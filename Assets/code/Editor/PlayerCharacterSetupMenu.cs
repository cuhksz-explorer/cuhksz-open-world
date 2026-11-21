#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

public static class PlayerCharacterSetupMenu
{
    const string MenuRoot = "Tools/Game Server/";

    [MenuItem(MenuRoot + "配置角色WASD动画（骨架+动作）", false, 122)]
    public static void ConfigureCharacterMovementOnly()
    {
        PlayerCharacterEditorShared.ConfigureAllCharacterPrefabsMovement();
        PlayerCharacterEditorShared.WireCatalogMovementController();
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog(
            "角色动画",
            "已为 3 个角色 prefab 配置 Humanoid Avatar + WASD Animator。\n" +
            "研一/博一 prefab 已强制重建，并与本科新生共用同一套 Humanoid 绑骨。\n\n" +
            "请重新运行游戏测试 WASD 动画。",
            "确定");
    }

    [MenuItem(MenuRoot + "导入队友NPC包并配置首次注册角色选择", false, 120)]
    public static void ImportAndConfigureCharacterSelection()
    {
        ImportUnityPackagesIfPresent();
        AssetDatabase.Refresh();

        PlayerCharacterEditorShared.EnsureFolder(PlayerCharacterEditorShared.PrefabRoot);
        PlayerCharacterEditorShared.EnsureMovementControllerInResources();
        var femalePrefab = EnsureCharacterPrefab(PlayerCharacterEditorShared.FemaleFbx, "hero_female", 1.1f);
        var malePrefabA = PlayerCharacterEditorShared.RebuildCharacterPrefab(
            PlayerCharacterEditorShared.MaleFbxA, "hero_male_a", 1f, forceRebuild: true);
        var malePrefabB = PlayerCharacterEditorShared.RebuildCharacterPrefab(
            PlayerCharacterEditorShared.MaleFbxB, "hero_male_b", 1f, forceRebuild: true);

        var catalog = AssetDatabase.LoadAssetAtPath<PlayerCharacterCatalog>(PlayerCharacterEditorShared.CatalogPath);
        if (catalog == null)
        {
            PlayerCharacterEditorShared.EnsureFolder(PlayerCharacterEditorShared.ResourcesRoot);
            catalog = ScriptableObject.CreateInstance<PlayerCharacterCatalog>();
            AssetDatabase.CreateAsset(catalog, PlayerCharacterEditorShared.CatalogPath);
        }

        var so = new SerializedObject(catalog);
        var optionsProp = so.FindProperty("options");
        optionsProp.arraySize = 3;
        WriteOption(optionsProp.GetArrayElementAtIndex(0), "hero_female", "本科新生", "本", "PlayerCharacters/hero_female", femalePrefab, 1.1f, 0.52f);
        WriteOption(optionsProp.GetArrayElementAtIndex(1), "hero_male_a", "研一新生", "研", "PlayerCharacters/hero_male_a", malePrefabA, 1.1f, 0.48f);
        WriteOption(optionsProp.GetArrayElementAtIndex(2), "hero_male_b", "博一新生", "博", "PlayerCharacters/hero_male_b", malePrefabB, 1.1f, 0.48f);
        so.FindProperty("movementAnimatorController").objectReferenceValue =
            PlayerCharacterEditorShared.LoadMovementControllerAsset();
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(catalog);

        try
        {
            WireLoadingScenePublic();
            WireGameServerNetworkPublic();
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("[PlayerCharacterSetup] 场景接线部分失败，运行时仍会自动兜底: " + ex.Message);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog(
            "角色选择",
            "已完成：\n" +
            "1. 导入/更新 NPC 模型\n" +
            "2. 生成 3 个角色 prefab（含骨架/动作）\n" +
            "3. 创建 PlayerCharacterCatalog\n" +
            "4. 接线 Loadingscene\n\n" +
            "下一步请执行：\n" +
            "Tools/场景配置/一键配置所有场景角色（骨架+动作+换模）",
            "确定");
    }

    public static void WireLoadingScenePublic() => WireLoadingScene();
    public static void WireGameServerNetworkPublic() => WireGameServerNetwork();

    static void ImportUnityPackagesIfPresent()
    {
        string srcDir = @"z:\微信\xwechat_files\wxid_iex8clusq8ee32_2373\msg\file\2026-05";
        ImportPackage(Path.Combine(srcDir, "npc2.unitypackage"), importScenes: false);
        ImportPackage(Path.Combine(srcDir, "npc unity.unitypackage"), importScenes: false);
    }

    static void ImportPackage(string path, bool importScenes)
    {
        if (!File.Exists(path))
            return;
        if (importScenes)
        {
            AssetDatabase.ImportPackage(path, false);
            return;
        }

        var tempDir = Path.Combine(Path.GetTempPath(), "npc_pkg_" + Path.GetFileNameWithoutExtension(path));
        if (Directory.Exists(tempDir))
            Directory.Delete(tempDir, true);
        Directory.CreateDirectory(tempDir);
        File.Copy(path, Path.Combine(tempDir, "pkg.unitypackage"), true);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "tar",
            Arguments = "-xf \"" + Path.Combine(tempDir, "pkg.unitypackage") + "\" -C \"" + tempDir + "\"",
            UseShellExecute = false,
            CreateNoWindow = true,
        })?.WaitForExit();

        foreach (var pathnameFile in Directory.GetFiles(tempDir, "pathname", SearchOption.AllDirectories))
        {
            string rel = File.ReadAllText(pathnameFile).Trim().Replace('/', Path.DirectorySeparatorChar);
            if (rel.StartsWith("Assets" + Path.DirectorySeparatorChar + "Scenes", System.StringComparison.OrdinalIgnoreCase))
                continue;
            string folder = Path.GetDirectoryName(pathnameFile);
            string asset = Path.Combine(folder, "asset");
            string meta = Path.Combine(folder, "asset.meta");
            if (!File.Exists(asset))
                continue;
            string dest = Path.Combine(Application.dataPath, rel.Substring("Assets".Length + 1));
            Directory.CreateDirectory(Path.GetDirectoryName(dest) ?? Application.dataPath);
            File.Copy(asset, dest, true);
            if (File.Exists(meta))
                File.Copy(meta, dest + ".meta", true);
        }
    }

    static GameObject EnsureCharacterPrefab(string fbxPath, string prefabName, float scale)
    {
        return PlayerCharacterEditorShared.RebuildCharacterPrefab(fbxPath, prefabName, scale);
    }

    static void WriteOption(
        SerializedProperty element,
        string id,
        string displayName,
        string avatarShortLabel,
        string resourcesPath,
        GameObject prefab,
        float worldScale,
        float previewScale)
    {
        element.FindPropertyRelative("id").stringValue = id;
        element.FindPropertyRelative("displayName").stringValue = displayName;
        element.FindPropertyRelative("avatarShortLabel").stringValue = avatarShortLabel;
        element.FindPropertyRelative("prefab").objectReferenceValue = prefab;
        element.FindPropertyRelative("resourcesPrefabPath").stringValue = resourcesPath;
        element.FindPropertyRelative("worldScale").floatValue = worldScale;
        element.FindPropertyRelative("previewScale").floatValue = previewScale;
        element.FindPropertyRelative("previewYawDegrees").floatValue = 0f;
        element.FindPropertyRelative("previewPitchDegrees").floatValue = 0f;
        element.FindPropertyRelative("previewOffset").vector3Value = Vector3.zero;

        string bio = "";
        switch (id)
        {
            case "hero_female":
                bio = "香港中文大学（深圳）本科新生，刚踏入大学校园。性格温和细心，善于观察与倾听，面对新环境既谨慎又充满好奇。身材纤瘦、齐肩黑发，穿着简洁清爽，整体给人干净柔和的第一印象。";
                break;
            case "hero_male_a":
                bio = "香港中文大学（深圳）硕士研究生新生，已具备一定专业基础。性格开朗果断，沟通直接，遇到任务喜欢迅速推进。身高偏高、身形挺拔，短发利落，常穿休闲得体的日常装扮，显得干练有朝气。";
                break;
            case "hero_male_b":
                bio = "香港中文大学（深圳）博士研究生新生，学术方向明确。性格沉稳内敛，思考深入，表达简练而有条理。中等身材、轮廓分明，穿搭偏简约正式，举止成熟克制，给人可靠稳重的印象。";
                break;
        }
        element.FindPropertyRelative("bio").stringValue = bio;
    }

    static void WireLoadingScene()
    {
        const string scenePath = "Assets/scene/Loadingscene.scene";
        if (!File.Exists(scenePath))
            return;

        var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);
        var flow = Object.FindObjectOfType<MenuLoginPortalFlow>();
        if (flow == null)
        {
            Debug.LogWarning("[PlayerCharacterSetup] Loadingscene 中未找到 MenuLoginPortalFlow。");
            return;
        }

        var ui = flow.GetComponent<PlayerCharacterRegisterUI>();
        if (ui == null)
            ui = Undo.AddComponent<PlayerCharacterRegisterUI>(flow.gameObject);

        var catalog = AssetDatabase.LoadAssetAtPath<PlayerCharacterCatalog>(PlayerCharacterEditorShared.CatalogPath);
        var uiSo = new SerializedObject(ui);
        uiSo.FindProperty("catalog").objectReferenceValue = catalog;
        uiSo.FindProperty("buildUiOnAwake").boolValue = true;
        uiSo.ApplyModifiedPropertiesWithoutUndo();

        var flowSo = new SerializedObject(flow);
        flowSo.FindProperty("characterRegisterUI").objectReferenceValue = ui;
        flowSo.FindProperty("autoCreateNicknameSetupFallbackUI").boolValue = false;
        flowSo.ApplyModifiedPropertiesWithoutUndo();

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
    }

    static void WireGameServerNetwork()
    {
        const string scenePath = "Assets/scene/Loadingscene.scene";
        var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);
        var sync = Object.FindObjectOfType<GameServerSync>();
        if (sync == null)
            return;

        if (sync.GetComponent<PlayerCharacterApplier>() == null)
            Undo.AddComponent<PlayerCharacterApplier>(sync.gameObject);

        var catalog = AssetDatabase.LoadAssetAtPath<PlayerCharacterCatalog>(PlayerCharacterEditorShared.CatalogPath);
        var applier = sync.GetComponent<PlayerCharacterApplier>();
        if (applier != null && catalog != null)
        {
            var so = new SerializedObject(applier);
            so.FindProperty("catalog").objectReferenceValue = catalog;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
    }
}
#endif
