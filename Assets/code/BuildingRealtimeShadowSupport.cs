using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// 让静态建筑参与实时阴影（投射+接收），并保留 Lightmap/Probe 间接光。
/// </summary>
public static class BuildingRealtimeShadowSupport
{
    static readonly System.Collections.Generic.HashSet<int> ProcessedScenes =
        new System.Collections.Generic.HashSet<int>();

    public static void ApplyToScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return;
        if (scene.name == "DontDestroyOnLoad")
            return;
        if (ProcessedScenes.Contains(scene.handle))
            return;

        OutdoorShadowQuality.ApplyForScene(scene);

        int updated = 0;
        foreach (var root in scene.GetRootGameObjects())
            updated += ApplyRecursive(root.transform);

        ProcessedScenes.Add(scene.handle);
        if (updated > 0)
            Debug.Log($"[BuildingShadow] scene={scene.name} shadowDistance={QualitySettings.shadowDistance:F0} updated={updated}");
    }

    public static void ResetProcessedScenes()
    {
        ProcessedScenes.Clear();
    }

    static int ApplyRecursive(Transform node)
    {
        if (node == null)
            return 0;

        int count = 0;
        if (ShouldProcess(node.gameObject))
        {
            var renderer = node.GetComponent<MeshRenderer>();
            if (renderer != null && ApplyToRenderer(renderer))
                count++;

            var terrain = node.GetComponent<Terrain>();
            if (terrain != null && ApplyToTerrain(terrain))
                count++;
        }

        for (int i = 0; i < node.childCount; i++)
            count += ApplyRecursive(node.GetChild(i));

        return count;
    }

    static bool ShouldProcess(GameObject go)
    {
        if (go == null || !go.activeInHierarchy)
            return false;
        if (go.GetComponent<RectTransform>() != null)
            return false;
        if (go.GetComponent<Canvas>() != null)
            return false;
        if (go.GetComponent<ParticleSystem>() != null)
            return false;
        if (go.GetComponent<SkinnedMeshRenderer>() != null)
            return false;

        if (go.layer == LayerMask.NameToLayer("UI"))
            return false;

        string n = go.name;
        if (n.IndexOf("Canvas", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return false;
        if (n.IndexOf("UI", System.StringComparison.OrdinalIgnoreCase) >= 0 && go.GetComponent<MeshRenderer>() == null)
            return false;

        return true;
    }

    static bool ApplyToRenderer(MeshRenderer renderer)
    {
        if (renderer == null)
            return false;

        bool changed = false;

        if (!renderer.receiveShadows)
        {
            renderer.receiveShadows = true;
            changed = true;
        }

        if (renderer.shadowCastingMode == ShadowCastingMode.Off)
        {
            renderer.shadowCastingMode = ShadowCastingMode.On;
            changed = true;
        }

        // 保留 Lightmap，避免大楼失去烘焙明暗；动态阴影靠方向光 + Shadowmask/实时阴影叠加。
        if (renderer.lightProbeUsage == LightProbeUsage.Off)
        {
            renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
            changed = true;
        }

        return changed;
    }

    static bool ApplyToTerrain(Terrain terrain)
    {
        if (terrain == null)
            return false;

        if (terrain.shadowCastingMode == ShadowCastingMode.On)
            return false;

        terrain.shadowCastingMode = ShadowCastingMode.On;
        return true;
    }
}

/// <summary>
/// 开放场景需要更远的阴影距离，否则只有近处人物有影、远处大楼无影。
/// </summary>
public static class OutdoorShadowQuality
{
    const float MinShadowDistance = 320f;
    const int MinShadowCascades = 4;

    static readonly string[] OutdoorKeywords =
    {
        "garden", "corridor", "library", "college", "ling", "cec", "campus",
        "dorm", "bus", "mini", "shaw", "muse", "eight", "classroom", "room",
        "hall", "computer", "discussion", "learning", "teaching", "lecture"
    };

    public static bool IsLargeScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
            return false;
        if (sceneName.IndexOf("Loading", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return false;

        string lower = sceneName.ToLowerInvariant();
        for (int i = 0; i < OutdoorKeywords.Length; i++)
        {
            if (lower.IndexOf(OutdoorKeywords[i], System.StringComparison.Ordinal) >= 0)
                return true;
        }
        return false;
    }

    public static void ApplyForScene(Scene scene)
    {
        if (!scene.IsValid() || !IsLargeScene(scene.name))
            return;

        if (QualitySettings.shadowDistance < MinShadowDistance)
            QualitySettings.shadowDistance = MinShadowDistance;

        if (QualitySettings.shadowCascades < MinShadowCascades)
            QualitySettings.shadowCascades = MinShadowCascades;

        QualitySettings.shadowProjection = ShadowProjection.StableFit;
        QualitySettings.shadowNearPlaneOffset = Mathf.Min(QualitySettings.shadowNearPlaneOffset, 2f);
    }
}
