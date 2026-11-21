#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 为场景中的 sun / moon 挂载真实感天体视觉，并更新材质引用。
/// </summary>
public static class CelestialBodySceneUpgrade
{
    const string MenuPath = "Tools/场景配置/升级太阳月亮真实感模型";

    [MenuItem(MenuPath, false, 22)]
    public static void UpgradeAllOutdoorScenes()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("天体视觉", "请先退出 Play Mode。", "确定");
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

                EditorUtility.DisplayProgressBar("升级天体视觉", path, (float)i / sceneGuids.Length);
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                if (UpgradeScene(scene))
                {
                    EditorSceneManager.SaveScene(scene);
                    updated++;
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            if (!string.IsNullOrEmpty(activePath))
                EditorSceneManager.OpenScene(activePath, OpenSceneMode.Single);
        }

        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("天体视觉", $"已升级 {updated} 个场景的太阳/月亮视觉。", "确定");
    }

    [MenuItem("Tools/场景配置/升级当前场景太阳月亮", false, 23)]
    public static void UpgradeActiveSceneMenu()
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
            return;
        if (UpgradeScene(scene))
            EditorSceneManager.MarkSceneDirty(scene);
    }

    public static bool UpgradeScene(Scene scene)
    {
        bool changed = false;
        foreach (var sim in Object.FindObjectsOfType<SunSimulation>(true))
        {
            if (sim == null || sim.gameObject.scene != scene)
                continue;

            sim.EnsureRuntimeDefaults();
            changed = true;

            var sunVisual = sim.GetComponent<CelestialBodyVisual>();
            if (sunVisual != null)
                EditorUtility.SetDirty(sunVisual);

            if (sim.moon != null)
            {
                var moonVisual = sim.moon.GetComponent<CelestialBodyVisual>();
                if (moonVisual != null)
                    EditorUtility.SetDirty(moonVisual);
            }

            EditorUtility.SetDirty(sim);
        }

        return changed;
    }
}
#endif
