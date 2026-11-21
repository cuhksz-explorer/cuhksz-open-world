#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// 烘焙场景光照，恢复/生成建筑静态阴影（配合 Mixed + Shadowmask）。
/// </summary>
public static class SceneLightingBakeMenu
{
    const string BakeActiveMenu = "Tools/场景配置/重新烘焙当前场景光照(建筑阴影)";
    const string BakeAllMenu = "Tools/场景配置/重新烘焙全部场景光照(建筑阴影)";

    [MenuItem(BakeActiveMenu, false, 30)]
    public static void BakeActiveScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("光照烘焙", "请先退出 Play Mode。", "确定");
            return;
        }

        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || string.IsNullOrEmpty(scene.path))
        {
            EditorUtility.DisplayDialog("光照烘焙", "请先打开要烘焙的场景。", "确定");
            return;
        }

        if (!PrepareSceneForBake(scene))
            return;

        Lightmapping.BakeAsync();
        EditorUtility.DisplayDialog(
            "光照烘焙",
            $"已开始烘焙：{scene.name}\n\n完成后建筑会恢复烘焙阴影，同时方向光(Mixed)仍可在运行时叠加动态阴影。\n请在 Lighting 窗口查看进度。",
            "确定");
    }

    [MenuItem(BakeAllMenu, false, 31)]
    public static void BakeAllScenes()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("光照烘焙", "请先退出 Play Mode。", "确定");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        string[] guids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/scene" });
        int prepared = 0;
        string activePath = SceneManager.GetActiveScene().path;

        try
        {
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(path))
                    continue;
                if (path.EndsWith("Loadingscene.scene", System.StringComparison.OrdinalIgnoreCase))
                    continue;

                EditorUtility.DisplayProgressBar("准备光照烘焙", path, (float)i / guids.Length);
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                if (PrepareSceneForBake(scene, saveScene: true))
                    prepared++;
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            if (!string.IsNullOrEmpty(activePath))
                EditorSceneManager.OpenScene(activePath, OpenSceneMode.Single);
        }

        var choice = EditorUtility.DisplayDialogComplex(
            "光照烘焙",
            $"已为 {prepared} 个场景写入 Mixed/Shadowmask 设置并标记静态物体。\n\n是否现在开始逐个烘焙？（耗时较长，可在 Lighting 窗口查看进度）",
            "开始烘焙",
            "取消",
            "仅保存设置");

        if (choice == 0)
        {
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(path) || path.EndsWith("Loadingscene.scene", System.StringComparison.OrdinalIgnoreCase))
                    continue;

                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                Lightmapping.BakeAsync();
                Debug.Log($"[LightingBake] started: {path}");
            }
        }
    }

    static bool PrepareSceneForBake(Scene scene, bool saveScene = false)
    {
        ConfigureRenderSettingsForMixedShadowmask();

        int staticCount = MarkEnvironmentStatic(scene);
        ConfigureDirectionalLights(scene);
        SceneTimeShadowSetupMenu.EnsureShadowSystemInScene(scene);
        OutdoorShadowQuality.ApplyForScene(scene);

        if (saveScene)
            EditorSceneManager.SaveScene(scene);

        Debug.Log($"[LightingBake] prepared scene={scene.name} staticRenderers={staticCount} shadowDistance={QualitySettings.shadowDistance}");
        return true;
    }

    static void ConfigureRenderSettingsForMixedShadowmask()
    {
        LightmapSettings.lightmapsMode = LightmapsMode.CombinedDirectional;

        var settings = Lightmapping.lightingSettings;
        if (settings == null)
        {
            settings = new LightingSettings();
            Lightmapping.lightingSettings = settings;
        }

        settings.bakedGI = true;
        settings.realtimeGI = false;
        settings.mixedBakeMode = MixedLightingMode.Shadowmask;
    }

    static int MarkEnvironmentStatic(Scene scene)
    {
        int count = 0;
        foreach (var renderer in Object.FindObjectsOfType<MeshRenderer>(true))
        {
            if (renderer == null || renderer.gameObject.scene != scene)
                continue;
            if (renderer.GetComponent<RectTransform>() != null)
                continue;
            if (renderer.GetComponent<SkinnedMeshRenderer>() != null)
                continue;
            if (renderer.gameObject.GetComponentInParent<Canvas>() != null)
                continue;

            var go = renderer.gameObject;
            var flags = GameObjectUtility.GetStaticEditorFlags(go);
            flags |= StaticEditorFlags.ContributeGI;
            flags |= StaticEditorFlags.BatchingStatic;
            flags |= StaticEditorFlags.OccludeeStatic;
            GameObjectUtility.SetStaticEditorFlags(go, flags);

            renderer.receiveShadows = true;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            count++;
        }
        return count;
    }

    static void ConfigureDirectionalLights(Scene scene)
    {
        foreach (var light in Object.FindObjectsOfType<Light>(true))
        {
            if (light == null || light.gameObject.scene != scene || light.type != LightType.Directional)
                continue;
            if (light.name.IndexOf("moon", System.StringComparison.OrdinalIgnoreCase) >= 0)
                continue;

            light.shadows = LightShadows.Soft;
            LightBakeCompat.SetMixedBakeTypeIfSupported(light);
            RenderSettings.sun = light;
        }
    }
}
#endif
