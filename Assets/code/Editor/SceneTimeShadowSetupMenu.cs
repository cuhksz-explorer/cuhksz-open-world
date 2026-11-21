#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// 批量为 Assets/scene 内玩法场景补齐时间阴影控制器（与运行时 Bootstrap 逻辑一致）。
/// </summary>
public static class SceneTimeShadowSetupMenu
{
    const string MenuPath = "Tools/场景配置/为全部场景补齐时间阴影系统";

    [MenuItem(MenuPath, false, 20)]
    public static void ApplyToAllProjectScenes()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("时间阴影", "请先退出 Play Mode。", "确定");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        string[] sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/scene" });
        int updated = 0;
        var activePath = SceneManager.GetActiveScene().path;

        try
        {
            for (int i = 0; i < sceneGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(sceneGuids[i]);
                if (string.IsNullOrEmpty(path))
                    continue;
                if (path.EndsWith("Loadingscene.scene", System.StringComparison.OrdinalIgnoreCase))
                    continue;

                EditorUtility.DisplayProgressBar("补齐时间阴影", path, (float)i / sceneGuids.Length);
                if (ApplyToSceneAsset(path))
                    updated++;
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            if (!string.IsNullOrEmpty(activePath))
                EditorSceneManager.OpenScene(activePath, OpenSceneMode.Single);
        }

        EditorUtility.DisplayDialog("时间阴影", $"已处理 {sceneGuids.Length} 个场景，更新 {updated} 个。", "确定");
    }

    [MenuItem("Tools/场景配置/为当前场景启用建筑实时阴影", false, 21)]
    public static void ApplyBuildingShadowsToActiveScene()
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
            return;

        BuildingRealtimeShadowSupport.ResetProcessedScenes();
        int before = CountMeshRenderers(scene);
        BuildingRealtimeShadowSupport.ApplyToScene(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorUtility.DisplayDialog("建筑实时阴影", $"已在当前场景处理建筑网格（共扫描约 {before} 个 MeshRenderer）。\n运行时仍会再次应用以确保生效。", "确定");
    }

    static bool ApplyToSceneAsset(string scenePath)
    {
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        bool changed = EnsureShadowSystemInScene(scene);
        if (changed)
            EditorSceneManager.SaveScene(scene);
        return changed;
    }

    public static bool EnsureShadowSystemInScene(Scene scene)
    {
        bool changed = false;

        var sunSim = FindInScene<SunSimulation>(scene);
        if (sunSim == null)
        {
            sunSim = CreateSunSimulationEditor(scene);
            changed = true;
        }
        else
        {
            sunSim.EnsureRuntimeDefaults();
            CelestialBodyFactory.UpgradeSunSimulation(sunSim);
        }

        var shadow = FindInScene<TimeBasedShadowController>(scene);
        if (shadow == null)
        {
            CreateShadowControllerEditor(scene, sunSim);
            changed = true;
        }
        else
        {
            if (shadow.sunSimulationObject == null)
            {
                shadow.sunSimulationObject = sunSim.gameObject;
                EditorUtility.SetDirty(shadow);
                changed = true;
            }
        }

        var dir = FindDirectionalLight(scene);
        if (dir != null && RenderSettings.sun != dir)
        {
            RenderSettings.sun = dir;
            changed = true;
        }

        if (dir != null && dir.shadows == LightShadows.None)
        {
            dir.shadows = LightShadows.Soft;
            EditorUtility.SetDirty(dir);
            changed = true;
        }

        if (dir != null && LightBakeCompat.SupportsLightmapBakeType &&
            LightBakeCompat.GetLightmapBakeType(dir) != LightmapBakeType.Mixed)
        {
            LightBakeCompat.SetMixedBakeTypeIfSupported(dir);
            EditorUtility.SetDirty(dir);
            changed = true;
        }

        BuildingRealtimeShadowSupport.ResetProcessedScenes();
        BuildingRealtimeShadowSupport.ApplyToScene(scene);
        if (changed)
            EditorSceneManager.MarkSceneDirty(scene);
        return changed;
    }

    static SunSimulation CreateSunSimulationEditor(Scene scene)
    {
        var root = new GameObject("TimeLightingRoot");
        SceneManager.MoveGameObjectToScene(root, scene);

        var center = new GameObject("SunCenter");
        center.transform.SetParent(root.transform, false);

        var sunGo = new GameObject("sun");
        sunGo.transform.SetParent(root.transform, false);
        var sunLight = sunGo.AddComponent<Light>();
        sunLight.type = LightType.Point;
        sunLight.range = 10000f;
        sunLight.shadows = LightShadows.None;

        var sim = sunGo.AddComponent<SunSimulation>();
        sim.centerObject = center.transform;
        sim.dayLengthInSeconds = 60f;
        sim.currentTimeOfDay = 8f;
        sim.celestialVisualScale = 50f;
        sim.EnsureRuntimeDefaults();
        CelestialBodyFactory.UpgradeSunSimulation(sim);
        Undo.RegisterCreatedObjectUndo(root, "Create TimeLightingRoot");
        return sim;
    }

    static void CreateShadowControllerEditor(Scene scene, SunSimulation sunSim)
    {
        var dirLight = FindDirectionalLight(scene);
        if (dirLight == null)
        {
            var dlGo = new GameObject("Directional Light");
            SceneManager.MoveGameObjectToScene(dlGo, scene);
            dirLight = dlGo.AddComponent<Light>();
            dirLight.type = LightType.Directional;
            dirLight.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        Light moonLight = null;
        foreach (var light in Object.FindObjectsOfType<Light>(true))
        {
            if (light != null && light.gameObject.scene == scene && light.type == LightType.Directional &&
                light.name.IndexOf("moon", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                moonLight = light;
                break;
            }
        }

        if (moonLight == null)
        {
            var moonGo = new GameObject("moonlight");
            SceneManager.MoveGameObjectToScene(moonGo, scene);
            moonLight = moonGo.AddComponent<Light>();
            moonLight.type = LightType.Directional;
            moonLight.enabled = false;
            moonLight.shadows = LightShadows.Soft;
        }

        var shadowGo = new GameObject("shadow");
        SceneManager.MoveGameObjectToScene(shadowGo, scene);
        var ctrl = shadowGo.AddComponent<TimeBasedShadowController>();
        ctrl.sunSimulationObject = sunSim.gameObject;
        ctrl.sunLight = dirLight;
        ctrl.moonLight = moonLight;
        Undo.RegisterCreatedObjectUndo(shadowGo, "Create shadow controller");
    }

    static Light FindDirectionalLight(Scene scene)
    {
        Light best = null;
        foreach (var light in Object.FindObjectsOfType<Light>(true))
        {
            if (light == null || light.gameObject.scene != scene || light.type != LightType.Directional)
                continue;
            if (light.name.IndexOf("moon", System.StringComparison.OrdinalIgnoreCase) >= 0)
                continue;
            if (best == null || light.intensity > best.intensity)
                best = light;
        }
        return best;
    }

    static T FindInScene<T>(Scene scene) where T : Component
    {
        foreach (var item in Object.FindObjectsOfType<T>(true))
        {
            if (item != null && item.gameObject.scene == scene)
                return item;
        }
        return null;
    }

    static int CountMeshRenderers(Scene scene)
    {
        int count = 0;
        foreach (var r in Object.FindObjectsOfType<MeshRenderer>(true))
        {
            if (r != null && r.gameObject.scene == scene)
                count++;
        }
        return count;
    }
}
#endif
