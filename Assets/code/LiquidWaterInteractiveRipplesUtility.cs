using UnityEngine;

/// <summary>
/// 平静水面 + 仅在有物体踩踏时通过 WaterRipples 产生局部波纹（配合 PlayerWaterRippleEmitter）。
/// </summary>
public static class LiquidWaterInteractiveRipplesUtility
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoAttachPlayerEmitter()
    {
        EnsurePlayerEmitter();
    }

    public static void Apply(GameObject liquidWaterRoot)
    {
        if (liquidWaterRoot == null)
            return;

        foreach (var ripples in liquidWaterRoot.GetComponentsInChildren<WaterRipples>(true))
        {
            ripples.enabled = true;
            ripples.Speed = 0.12f;
            ripples.Damping = 0.035f;
            ripples.UpdateFPS = 24;
            ripples.DisplacementResolution = 96;
            ripples.Multithreading = true;
            ripples.UseProjectedWaves = false;
            ripples.UseSmoothWaves = true;
        }

        foreach (var creator in liquidWaterRoot.GetComponentsInChildren<RippleCreator>(true))
            creator.enabled = false;

        foreach (var reflection in liquidWaterRoot.GetComponentsInChildren<ReflectionCamera>(true))
            reflection.enabled = false;

        EnsureWaterTriggerCollider(liquidWaterRoot);
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

    public static bool EnsurePlayerEmitter()
    {
        if (!SceneTeleportHelper.TryFindLocalPlayer(out var player))
            return false;

        if (player.GetComponent<PlayerWaterRippleEmitter>() == null)
            player.AddComponent<PlayerWaterRippleEmitter>();

        return true;
    }
}
