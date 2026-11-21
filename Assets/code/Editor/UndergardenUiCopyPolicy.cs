#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// undergarden UI 复制策略：哪些根节点/分支不应被一键配置复制到其它场景。
/// </summary>
public static class UndergardenUiCopyPolicy
{
    static readonly HashSet<string> ExcludedTeleportCanvasRoots = BuildExcludedTeleportCanvasRoots();

    static HashSet<string> BuildExcludedTeleportCanvasRoots()
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 1; i <= 8; i++)
            set.Add($"Canvas ({i})");
        return set;
    }

    /// <summary>
    /// 是否跳过复制（Canvas 传送点、MapSystem 及其 map 窗口、传送点列表等）。
    /// </summary>
    public static bool ShouldSkipUiCopy(GameObject go)
    {
        if (go == null)
            return true;

        if (IsExcludedTeleportCanvasRoot(go.name))
            return true;

        return IsMapSystemRelated(go);
    }

    public static bool ShouldSkipUiCopy(Transform t) => ShouldSkipUiCopy(t != null ? t.gameObject : null);

    /// <summary>Canvas (1)～Canvas (8) 为 undergarden 地图传送点，不参与一键 UI 配置。</summary>
    public static bool IsExcludedTeleportCanvasRoot(string objectName)
    {
        return !string.IsNullOrEmpty(objectName) && ExcludedTeleportCanvasRoots.Contains(objectName);
    }

    public static bool IsExcludedTeleportCanvasRoot(GameObject go)
    {
        return go != null && IsExcludedTeleportCanvasRoot(go.name);
    }

    public static bool IsExcludedTeleportCanvasRoot(Transform t)
    {
        return t != null && IsExcludedTeleportCanvasRoot(t.name);
    }

    public static bool IsMapSystemRelated(GameObject go)
    {
        if (go == null)
            return false;

        if (go.GetComponent<MapSystem>() != null)
            return true;

        if (string.Equals(go.name, "MAPCONTROL", StringComparison.OrdinalIgnoreCase))
            return true;

        if (go.GetComponentInParent<MapSystem>(true) != null)
            return true;

        // undergarden 中 MapSystem.mapWindow 名为 map；各场景需自行配置大地图。
        if (string.Equals(go.name, "map", StringComparison.OrdinalIgnoreCase) &&
            go.GetComponentInParent<MapSystem>(true) != null)
            return true;

        return false;
    }
}
#endif
