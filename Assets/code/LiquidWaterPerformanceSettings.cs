using UnityEngine;

/// <summary>
/// 按档位降低 KriptoFX 液态水 CPU/GPU 开销（波纹模拟、投影、折射相关组件）。
/// </summary>
public static class LiquidWaterPerformanceSettings
{
    public static void Apply(GameObject liquidWaterRoot, LiquidWaterPerformanceTier tier)
    {
        if (liquidWaterRoot == null)
            return;

        LiquidWaterCalmSettings.ApplyCalmVisuals(liquidWaterRoot);
        DisableInfiniteWaterFeatures(liquidWaterRoot);

        switch (tier)
        {
            case LiquidWaterPerformanceTier.HighPerformance:
                LiquidWaterCalmSettings.ApplyCalmOnly(liquidWaterRoot);
                SetDecorationProjectorsEnabled(liquidWaterRoot, false);
                SetProjectorMatricesUpdating(liquidWaterRoot, false);
                DisableDistortionAndBloom(liquidWaterRoot);
                ApplyHighPerformanceShaderTuning(liquidWaterRoot);
                break;

            case LiquidWaterPerformanceTier.Balanced:
                ApplyInteractiveRipples(liquidWaterRoot, displacementResolution: 64, updateFps: 15);
                SetDecorationProjectorsEnabled(liquidWaterRoot, false);
                SetProjectorMatricesUpdating(liquidWaterRoot, false);
                TuneDistortionAndBloom(liquidWaterRoot, renderTextureFactor: 0.33f);
                break;

            default:
                LiquidWaterInteractiveRipplesUtility.Apply(liquidWaterRoot);
                SetDecorationProjectorsEnabled(liquidWaterRoot, true);
                SetProjectorMatricesUpdating(liquidWaterRoot, true);
                break;
        }
    }

    static void ApplyInteractiveRipples(GameObject root, int displacementResolution, int updateFps)
    {
        foreach (var ripples in root.GetComponentsInChildren<WaterRipples>(true))
        {
            ripples.enabled = true;
            ripples.Speed = 0.12f;
            ripples.Damping = 0.035f;
            ripples.UpdateFPS = updateFps;
            ripples.DisplacementResolution = displacementResolution;
            ripples.Multithreading = true;
            ripples.UseProjectedWaves = false;
            ripples.UseSmoothWaves = true;
        }

        foreach (var creator in root.GetComponentsInChildren<RippleCreator>(true))
            creator.enabled = false;

        foreach (var reflection in root.GetComponentsInChildren<ReflectionCamera>(true))
            reflection.enabled = false;

        EnsureWaterTriggerCollider(root);
    }

    static void EnsureWaterTriggerCollider(GameObject root)
    {
        Transform surface = FindWaterSurface(root.transform);
        if (surface == null)
            return;

        var col = surface.GetComponent<BoxCollider>();
        if (col == null)
            col = surface.gameObject.AddComponent<BoxCollider>();

        col.isTrigger = true;
        col.enabled = true;
    }

    static void DisableInfiniteWaterFeatures(GameObject root)
    {
        foreach (var move in root.GetComponentsInChildren<MoveWaterToCamera>(true))
            move.enabled = false;

        foreach (var reflection in root.GetComponentsInChildren<ReflectionCamera>(true))
            reflection.enabled = false;

        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "InfiniteWaterMesh")
                t.gameObject.SetActive(false);
        }
    }

    /// <summary>焦散/池底波纹等装饰投影，与踩踏波纹无关。</summary>
    static void SetDecorationProjectorsEnabled(GameObject root, bool enabled)
    {
        foreach (var projector in root.GetComponentsInChildren<Projector>(true))
            projector.enabled = enabled;

        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "Caustic" || t.name == "ProjectorWaves" || t.name == "ProjectorCausticScale")
                t.gameObject.SetActive(enabled);
        }
    }

    static void SetProjectorMatricesUpdating(GameObject root, bool canUpdate)
    {
        foreach (var matrix in root.GetComponentsInChildren<ProjectorMatrix>(true))
            matrix.CanUpdate = canUpdate;
    }

    static void DisableDistortionAndBloom(GameObject root)
    {
        foreach (var distortion in root.GetComponentsInChildren<Water_DistortionAndBloom>(true))
            distortion.enabled = false;
    }

    static void TuneDistortionAndBloom(GameObject root, float renderTextureFactor)
    {
        foreach (var distortion in root.GetComponentsInChildren<Water_DistortionAndBloom>(true))
        {
            distortion.enabled = true;
            distortion.RenderTextureResolutoinFactor = Mathf.Clamp(renderTextureFactor, 0.2f, 1f);
        }
    }

    static void ApplyHighPerformanceShaderTuning(GameObject root)
    {
        Transform surface = FindWaterSurface(root.transform);
        if (surface == null)
            return;

        var renderer = surface.GetComponent<Renderer>();
        if (renderer == null)
            return;

        var mat = renderer.material;
        mat.SetFloat(Shader.PropertyToID("_Distortion"), 120f);
        mat.SetFloat(Shader.PropertyToID("_EdgeDistortion"), 0.2f);
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
