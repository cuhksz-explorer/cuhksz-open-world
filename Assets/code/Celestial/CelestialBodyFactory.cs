using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 为 SunSimulation 的 sun / moon 物体创建或升级真实感视觉网格。
/// </summary>
public static class CelestialBodyFactory
{
    const float DefaultVisualScale = 50f;

    public static void UpgradeSunVisual(Transform sunTransform, float scale = DefaultVisualScale)
    {
        if (sunTransform == null)
            return;

        var visual = GetOrAddVisual(sunTransform, CelestialBodyVisual.BodyKind.Sun);
        visual.Configure(CelestialBodyVisual.BodyKind.Sun, scale);

        // 保留场景里对 sunlight.mat 的引用时，仍优先使用运行时 Celestial 材质。
        var legacyRenderer = sunTransform.GetComponent<MeshRenderer>();
        if (legacyRenderer != null && legacyRenderer.sharedMaterial != null)
        {
            var matName = legacyRenderer.sharedMaterial.name;
            if (matName.Contains("sunlight") || matName.Contains("Default"))
                visual.ApplyMaterialSettings();
        }
    }

    public static void UpgradeMoonVisual(Transform moonTransform, float scale = DefaultVisualScale)
    {
        if (moonTransform == null)
            return;

        var visual = GetOrAddVisual(moonTransform, CelestialBodyVisual.BodyKind.Moon);
        visual.Configure(CelestialBodyVisual.BodyKind.Moon, scale);
    }

    public static Transform EnsureMoonForSunSimulation(SunSimulation sim, Scene scene)
    {
        if (sim == null)
            return null;

        if (sim.moon != null)
        {
            UpgradeMoonVisual(sim.moon, ReadScale(sim.moon));
            return sim.moon;
        }

        var moonGo = new GameObject("moon");
        if (scene.IsValid())
            SceneManager.MoveGameObjectToScene(moonGo, scene);
        moonGo.transform.SetParent(sim.transform.parent, false);

        var moonLight = moonGo.AddComponent<Light>();
        moonLight.type = LightType.Point;
        moonLight.range = 10000f;
        moonLight.shadows = LightShadows.None;

        sim.moon = moonGo.transform;
        UpgradeMoonVisual(sim.moon, DefaultVisualScale);
        return sim.moon;
    }

    public static void UpgradeSunSimulation(SunSimulation sim)
    {
        if (sim == null)
            return;

        UpgradeSunVisual(sim.transform, ReadScale(sim.transform));
        EnsureMoonForSunSimulation(sim, sim.gameObject.scene);
    }

    static CelestialBodyVisual GetOrAddVisual(Transform target, CelestialBodyVisual.BodyKind kind)
    {
        var visual = target.GetComponent<CelestialBodyVisual>();
        if (visual == null)
            visual = target.gameObject.AddComponent<CelestialBodyVisual>();
        visual.Kind = kind;
        return visual;
    }

    static float ReadScale(Transform t)
    {
        float s = t.localScale.x;
        if (s < 1f)
            s = DefaultVisualScale;
        return s;
    }
}
