#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 一键将静态水替换为 KriptoFX 液态水：贴合切割 mesh、平静外观、玩家踩踏波纹。
/// </summary>
public static class StaticWaterReplaceMenu
{
    const string WaterRoot = "Assets/KriptoFX/Water2.0/Prefabs/Water/PC/Unity5";

    [MenuItem("Tools/场景配置/一键布置平静踩踏水面", false, 40)]
    [MenuItem("Tools/环境/一键布置平静踩踏水面", false, 40)]
    [MenuItem("Tools/Quest System/环境/一键布置平静踩踏水面", false, 41)]
    [MenuItem("GameObject/场景配置/一键布置平静踩踏水面", false, 40)]
    [MenuItem("GameObject/环境/一键布置平静踩踏水面", false, 40)]
    public static void SetupCalmFootstepWaterOneClick()
    {
        SetupCalmFootstepWaterOneClick(LiquidWaterPerformanceTier.Quality);
    }

    [MenuItem("Tools/场景配置/一键布置高性能平静水面", false, 41)]
    [MenuItem("Tools/环境/一键布置高性能平静水面", false, 41)]
    [MenuItem("Tools/Quest System/环境/一键布置高性能平静水面", false, 42)]
    [MenuItem("GameObject/场景配置/一键布置高性能平静水面", false, 41)]
    [MenuItem("GameObject/环境/一键布置高性能平静水面", false, 41)]
    public static void SetupHighPerformanceCalmWaterOneClick()
    {
        SetupCalmFootstepWaterOneClick(LiquidWaterPerformanceTier.HighPerformance);
    }

    static void SetupCalmFootstepWaterOneClick(LiquidWaterPerformanceTier tier)
    {
        var selected = Selection.activeGameObject;
        if (selected == null)
        {
            EditorUtility.DisplayDialog(
                "平静踩踏水面",
                "请先在 Hierarchy 选中：\n· 原来的静态水物体，或\n· 已生成的 xxx_LiquidWater",
                "确定");
            return;
        }

        int restored = KriptoFXWaterRestoreMenu.TryRestoreMissingAssets(out string restoreError);
        if (restoreError != null)
        {
            EditorUtility.DisplayDialog("平静踩踏水面", restoreError, "确定");
            return;
        }

        if (restored > 0)
            AssetDatabase.Refresh();

        var liquidRoot = ResolveLiquidWaterRoot(selected);
        if (liquidRoot != null)
        {
            SetupExistingLiquidWater(liquidRoot, tier);
            return;
        }

        if (!TryGetStaticWaterMesh(selected, out MeshFilter sourceMesh, out Bounds bounds))
        {
            EditorUtility.DisplayDialog(
                "平静踩踏水面",
                "选中物体未找到可用水面 Mesh。\n请选中带 MeshRenderer 的静态水，或已生成的 LiquidWater。",
                "确定");
            return;
        }

        CreateAndSetupLiquidWater(selected, sourceMesh, bounds, restored, tier);
    }

    [MenuItem("Tools/场景配置/一键布置平静踩踏水面", true)]
    [MenuItem("Tools/场景配置/一键布置高性能平静水面", true)]
    [MenuItem("Tools/环境/一键布置平静踩踏水面", true)]
    [MenuItem("Tools/环境/一键布置高性能平静水面", true)]
    [MenuItem("Tools/Quest System/环境/一键布置平静踩踏水面", true)]
    [MenuItem("Tools/Quest System/环境/一键布置高性能平静水面", true)]
    [MenuItem("GameObject/场景配置/一键布置平静踩踏水面", true)]
    [MenuItem("GameObject/场景配置/一键布置高性能平静水面", true)]
    [MenuItem("GameObject/环境/一键布置平静踩踏水面", true)]
    [MenuItem("GameObject/环境/一键布置高性能平静水面", true)]
    public static bool ValidateOneClick() => Selection.activeGameObject != null;

    static void CreateAndSetupLiquidWater(GameObject target, MeshFilter sourceMesh, Bounds bounds, int restoredAssets, LiquidWaterPerformanceTier tier)
    {
        string prefabPath = ResolveWaterPrefabPath(target);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            EditorUtility.DisplayDialog(
                "平静踩踏水面",
                "未找到液态水 Prefab：\n" + prefabPath +
                "\n\n请确认已导入 Assets/KriptoFX/Water2.0。",
                "确定");
            return;
        }

        var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (instance == null)
        {
            EditorUtility.DisplayDialog("平静踩踏水面", "实例化失败。", "确定");
            return;
        }

        Undo.RegisterCreatedObjectUndo(instance, "Setup Calm Footstep Water");
        instance.name = target.name + "_LiquidWater";

        instance.transform.SetParent(target.transform, false);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;

        ConfigureLiquidWaterInstance(instance, sourceMesh, bounds);
        ApplyFullWaterSetup(instance, tier);
        HideStaticWaterRenderers(target, instance);

        EditorUtility.SetDirty(target);
        EditorUtility.SetDirty(instance);
        MarkSceneDirty(target);

        Selection.activeGameObject = instance;
        bool playerAdded = tier != LiquidWaterPerformanceTier.HighPerformance
            && LiquidWaterInteractiveRipplesUtility.EnsurePlayerEmitter();
        ShowDoneDialog(instance.name, prefabPath, restoredAssets, playerAdded, created: true, tier);
    }

    static void SetupExistingLiquidWater(GameObject liquidRoot, LiquidWaterPerformanceTier tier)
    {
        Undo.RegisterFullObjectHierarchyUndo(liquidRoot, "Setup Calm Footstep Water");

        var staticParent = liquidRoot.transform.parent;
        if (staticParent != null)
        {
            if (TryGetStaticWaterMesh(staticParent.gameObject, out MeshFilter sourceMesh, out Bounds bounds))
            {
                liquidRoot.transform.localPosition = Vector3.zero;
                liquidRoot.transform.localRotation = Quaternion.identity;
                liquidRoot.transform.localScale = Vector3.one;
                ConfigureLiquidWaterInstance(liquidRoot, sourceMesh, bounds);
            }
        }

        ApplyFullWaterSetup(liquidRoot, tier);
        EditorUtility.SetDirty(liquidRoot);
        MarkSceneDirty(liquidRoot);

        bool playerAdded = tier != LiquidWaterPerformanceTier.HighPerformance
            && LiquidWaterInteractiveRipplesUtility.EnsurePlayerEmitter();
        ShowDoneDialog(liquidRoot.name, null, 0, playerAdded, created: false, tier);
    }

    static void ApplyFullWaterSetup(GameObject liquidRoot, LiquidWaterPerformanceTier tier)
    {
        DisableInfiniteWaterFeatures(liquidRoot);

        var profile = liquidRoot.GetComponent<LiquidWaterPerformanceProfile>();
        if (profile == null)
            profile = Undo.AddComponent<LiquidWaterPerformanceProfile>(liquidRoot);
        profile.Tier = tier;

        if (liquidRoot.GetComponent<LiquidWaterRippleApplier>() == null
            && liquidRoot.GetComponent<LiquidWaterCalmApplier>() == null)
            Undo.AddComponent<LiquidWaterRippleApplier>(liquidRoot);

        LiquidWaterPerformanceSettings.Apply(liquidRoot, tier);
        if (tier != LiquidWaterPerformanceTier.HighPerformance)
            LiquidWaterInteractiveRipplesUtility.EnsurePlayerEmitter();
    }

    static void ShowDoneDialog(string waterName, string prefabPath, int restoredAssets, bool playerAdded, bool created, LiquidWaterPerformanceTier tier)
    {
        string msg = created
            ? $"已生成：{waterName}\n"
            : $"已配置：{waterName}\n";

        if (!string.IsNullOrEmpty(prefabPath))
            msg += $"Prefab：{prefabPath}\n";

        if (restoredAssets > 0)
            msg += $"已自动恢复 {restoredAssets} 个水面材质/贴图。\n";

        msg += "\n· 水面贴合原切割 mesh，默认平静\n";
        msg += "· 性能档位：" + TierLabel(tier) + "\n";
        if (tier == LiquidWaterPerformanceTier.HighPerformance)
            msg += "· 已关闭踩踏波纹与装饰投影（最省性能）\n";
        else
            msg += "· 玩家走上去会产生踩踏波纹\n";
        msg += playerAdded
            ? "· 已为玩家添加 PlayerWaterRippleEmitter\n"
            : "· 未找到玩家，请给主角加 Player 标签或 PlayerMovement 后重试\n";

        msg += "\n运行游戏从岸上走进水面即可测试。";

        EditorUtility.DisplayDialog("平静踩踏水面", msg, "确定");
    }

    static string TierLabel(LiquidWaterPerformanceTier tier)
    {
        switch (tier)
        {
            case LiquidWaterPerformanceTier.HighPerformance:
                return "高性能（无踩踏波纹）";
            case LiquidWaterPerformanceTier.Balanced:
                return "均衡";
            default:
                return "品质（默认）";
        }
    }

    static void MarkSceneDirty(GameObject anyInScene)
    {
        if (anyInScene != null && anyInScene.scene.IsValid())
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(anyInScene.scene);
    }

    static GameObject ResolveLiquidWaterRoot(GameObject go)
    {
        if (go == null)
            return null;
        if (go.name.EndsWith("_LiquidWater", StringComparison.Ordinal))
            return go;
        if (go.transform.parent != null && go.transform.parent.name.EndsWith("_LiquidWater", StringComparison.Ordinal))
            return go.transform.parent.gameObject;
        return null;
    }

    static bool TryGetStaticWaterMesh(GameObject go, out MeshFilter meshFilter, out Bounds bounds)
    {
        meshFilter = GetPrimaryMeshFilter(go);
        bounds = default;
        if (meshFilter == null || meshFilter.sharedMesh == null)
            return false;
        return TryGetStaticWaterBounds(go, out bounds);
    }

    static string ResolveWaterPrefabPath(GameObject target)
    {
        string n = target.name.ToLowerInvariant();
        if (n.Contains("river") || n.Contains("河"))
            return WaterRoot + "/River.prefab";
        if (n.Contains("sea") || n.Contains("海"))
            return WaterRoot + "/Sea.prefab";
        return WaterRoot + "/Pool.prefab";
    }

    static void ConfigureLiquidWaterInstance(GameObject instance, MeshFilter sourceMesh, Bounds targetBounds)
    {
        Transform waterSurface = FindWaterSurface(instance.transform);
        if (waterSurface == null)
        {
            FallbackScaleRoot(instance.transform, targetBounds);
            return;
        }

        var waterMf = waterSurface.GetComponent<MeshFilter>();
        if (waterMf != null)
            waterMf.sharedMesh = sourceMesh.sharedMesh;

        waterSurface.localPosition = sourceMesh.transform.localPosition;
        waterSurface.localRotation = sourceMesh.transform.localRotation;
        waterSurface.localScale = sourceMesh.transform.localScale;

        FitBoxColliderToMesh(waterSurface);
        FitEffectScales(instance.transform, waterSurface);

        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;
    }

    static void DisableInfiniteWaterFeatures(GameObject root)
    {
        foreach (var move in root.GetComponentsInChildren<MoveWaterToCamera>(true))
            move.enabled = false;

        // 切割静态水面用 Pool：实时反射依赖 Camera.main，uppergarden 无 MainCamera 时会 NRE 刷屏
        foreach (var reflection in root.GetComponentsInChildren<ReflectionCamera>(true))
            reflection.enabled = false;

        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "InfiniteWaterMesh")
                t.gameObject.SetActive(false);
        }
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

    static void FitBoxColliderToMesh(Transform waterSurface)
    {
        var col = waterSurface.GetComponent<BoxCollider>();
        var mf = waterSurface.GetComponent<MeshFilter>();
        if (col == null || mf == null || mf.sharedMesh == null)
            return;

        var lb = mf.sharedMesh.bounds;
        col.center = lb.center;
        col.size = new Vector3(
            Mathf.Max(lb.size.x, 0.1f),
            Mathf.Max(lb.size.y, 0.5f),
            Mathf.Max(lb.size.z, 0.1f));
    }

    static void FitEffectScales(Transform root, Transform waterSurface)
    {
        var mr = waterSurface.GetComponent<MeshRenderer>();
        if (mr == null)
            return;

        Bounds b = mr.bounds;
        float extentX = Mathf.Max(b.extents.x, 1f);
        float extentZ = Mathf.Max(b.extents.z, 1f);
        float ortho = Mathf.Max(extentX, extentZ);

        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "WaterLightWaves")
            {
                t.localScale = new Vector3(extentX * 2f, extentZ * 2f, 1f);
                continue;
            }

            if (t.name.StartsWith("Projector", StringComparison.Ordinal))
            {
                t.localScale = new Vector3(extentX * 0.1f, 1f, extentZ * 0.1f);
                var proj = t.GetComponent<Projector>();
                if (proj != null)
                    proj.orthographicSize = ortho;
            }
        }
    }

    static void FallbackScaleRoot(Transform instance, Bounds targetBounds)
    {
        instance.position = new Vector3(
            targetBounds.center.x,
            targetBounds.max.y,
            targetBounds.center.z);
        instance.localScale = new Vector3(
            Mathf.Max(targetBounds.size.x, 4f),
            1f,
            Mathf.Max(targetBounds.size.z, 4f));
    }

    static void HideStaticWaterRenderers(GameObject root, GameObject liquidInstance)
    {
        foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (liquidInstance != null && r.transform.IsChildOf(liquidInstance.transform))
                continue;
            r.enabled = false;
        }

        foreach (var r in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (liquidInstance != null && r.transform.IsChildOf(liquidInstance.transform))
                continue;
            r.enabled = false;
        }
    }

    static MeshFilter GetPrimaryMeshFilter(GameObject go)
    {
        var mf = go.GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
            return mf;

        MeshFilter best = null;
        float bestArea = 0f;
        foreach (var child in go.GetComponentsInChildren<MeshFilter>(true))
        {
            if (IsUnderLiquidWaterRoot(child.transform))
                continue;
            if (child.sharedMesh == null)
                continue;
            var rend = child.GetComponent<MeshRenderer>();
            if (rend != null && !rend.enabled)
                continue;
            float area = child.sharedMesh.bounds.size.sqrMagnitude;
            if (area > bestArea)
            {
                bestArea = area;
                best = child;
            }
        }

        return best;
    }

    static bool IsUnderLiquidWaterRoot(Transform t)
    {
        while (t != null)
        {
            if (t.name.EndsWith("_LiquidWater", StringComparison.Ordinal))
                return true;
            t = t.parent;
        }
        return false;
    }

    static bool TryGetStaticWaterBounds(GameObject go, out Bounds bounds)
    {
        bounds = new Bounds(go.transform.position, Vector3.zero);
        bool has = false;

        var primary = GetPrimaryMeshFilter(go);
        if (primary != null)
        {
            var rend = primary.GetComponent<Renderer>();
            if (rend != null && rend.enabled)
            {
                bounds = rend.bounds;
                return bounds.size.sqrMagnitude > 0.001f;
            }
        }

        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            if (!r.enabled)
                continue;
            if (has)
                bounds.Encapsulate(r.bounds);
            else
            {
                bounds = r.bounds;
                has = true;
            }
        }

        return has && bounds.size.sqrMagnitude > 0.001f;
    }
}
#endif
