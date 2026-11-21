using UnityEngine;

/// <summary>
/// 将 KriptoFX 液态水调成接近静态水池：关闭波纹模拟，减弱 Gerstner/UV 动画与折射扭曲。
/// </summary>
public static class LiquidWaterCalmSettings
{
    static readonly int DirectionId = Shader.PropertyToID("_Direction");
    static readonly int FoamDirectionId = Shader.PropertyToID("_FoamDirection");
    static readonly int GSpeedId = Shader.PropertyToID("_GSpeed");
    static readonly int GAmplitudeId = Shader.PropertyToID("_GAmplitude");
    static readonly int GFrequencyId = Shader.PropertyToID("_GFrequency");
    static readonly int GSteepnessId = Shader.PropertyToID("_GSteepness");
    static readonly int DistortionId = Shader.PropertyToID("_Distortion");
    static readonly int DistortionVertId = Shader.PropertyToID("_DistortionVert");
    static readonly int EdgeDistortionId = Shader.PropertyToID("_EdgeDistortion");
    static readonly int TexturesScaleId = Shader.PropertyToID("_TexturesScale");
    static readonly int FoamIntensityId = Shader.PropertyToID("_FoamIntensity");

    /// <summary>平静外观 + 踩踏波纹（推荐默认）。</summary>
    public static void Apply(GameObject liquidWaterRoot)
    {
        ApplyCalmVisuals(liquidWaterRoot);
        LiquidWaterInteractiveRipplesUtility.Apply(liquidWaterRoot);
    }

    /// <summary>仅平静外观，完全无波纹模拟。</summary>
    public static void ApplyCalmOnly(GameObject liquidWaterRoot)
    {
        ApplyCalmVisuals(liquidWaterRoot);
        foreach (var ripples in liquidWaterRoot.GetComponentsInChildren<WaterRipples>(true))
            ripples.enabled = false;
        Shader.DisableKeyword("ripples_on");
    }

    public static void ApplyCalmVisuals(GameObject liquidWaterRoot)
    {
        if (liquidWaterRoot == null)
            return;

        foreach (var postFx in liquidWaterRoot.GetComponentsInChildren<UnderwaterPostEffects>(true))
        {
            postFx.UseSunShafts = false;
            postFx.enabled = false;
        }

        Transform waterSurface = FindWaterSurface(liquidWaterRoot.transform);
        if (waterSurface == null)
            return;

        foreach (var t in liquidWaterRoot.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "WaterLightWaves")
                t.gameObject.SetActive(false);
        }

        var renderer = waterSurface.GetComponent<Renderer>();
        if (renderer == null)
            return;

        var mat = renderer.material;
        mat.SetVector(DirectionId, Vector4.zero);
        mat.SetVector(FoamDirectionId, Vector4.zero);
        mat.SetVector(GSpeedId, Vector4.zero);
        mat.SetVector(GAmplitudeId, new Vector4(0.002f, 0.002f, 0.002f, 0.002f));
        mat.SetVector(GFrequencyId, new Vector4(0.5f, 0.5f, 0.5f, 0.5f));
        mat.SetVector(GSteepnessId, new Vector4(0.02f, 0.02f, 0.02f, 0.02f));
        mat.SetFloat(DistortionId, 50f);
        mat.SetFloat(DistortionVertId, 0f);
        mat.SetFloat(EdgeDistortionId, 0.5f);
        mat.SetFloat(TexturesScaleId, 40f);
        mat.SetFloat(FoamIntensityId, 0.05f);
    }

    static Transform FindWaterSurface(Transform root)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name != "Water")
                continue;
            if (t.GetComponent<MeshRenderer>() != null && t.GetComponent<MeshFilter>() != null)
                return t;
        }

        var mr = root.GetComponentInChildren<MeshRenderer>(true);
        return mr != null ? mr.transform : null;
    }
}
