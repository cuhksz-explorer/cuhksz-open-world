#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 为长椅/椅子一键添加 <see cref="InteractableChair"/> 与坐姿点。
/// </summary>
public static class InteractableChairSetup
{
    [MenuItem("Tools/Chair Sit/3 Setup All Benches In Scene", false, 2)]
    [MenuItem("Tools/椅子交互/为场景中所有 bench 添加坐下交互", false, 2)]
    public static void AddChairInteractionAllBenchesMenu() => AddChairInteractionAllBenches();

    [MenuItem("Tools/Chair Sit/4 Setup Selected Object", false, 3)]
    [MenuItem("Tools/椅子交互/为选中物体添加坐下交互", false, 3)]
    public static void AddChairInteractionSelectionPublic() => AddChairInteractionSelection();

    [MenuItem("GameObject/Chair Sit/Add Sit To Selected", false, 0)]
    static void AddChairInteractionContext() => AddChairInteractionSelection();

    static void AddChairInteractionSelection()
    {
        if (!ChairSitEditorGuard.EnsureNotPlaying())
            return;
        var selected = Selection.gameObjects;
        if (selected == null || selected.Length == 0)
        {
            EditorUtility.DisplayDialog("椅子交互", "请先在 Hierarchy 中选中长椅（如 bench (2)）。", "确定");
            return;
        }

        int count = 0;
        foreach (var go in selected)
        {
            if (go == null)
                continue;
            if (SetupBench(go))
                count++;
        }

        MarkSceneDirty();
        EditorUtility.DisplayDialog("椅子交互", $"已为 {count} 个物体配置坐下交互。\n请微调 ChairSitPoint 位置，保存场景（Ctrl+S）后运行测试。", "确定");
    }

    public static void AddChairInteractionAllBenches()
    {
        if (!ChairSitEditorGuard.EnsureNotPlaying())
            return;
        int count = 0;
        for (int si = 0; si < SceneManager.sceneCount; si++)
        {
            var scene = SceneManager.GetSceneAt(si);
            if (!scene.isLoaded)
                continue;
            foreach (var root in scene.GetRootGameObjects())
                count += SetupBenchesRecursive(root.transform);
        }

        MarkSceneDirty();
        EditorUtility.DisplayDialog(
            "椅子交互",
            count > 0
                ? $"已为场景中 {count} 个 bench 配置坐下交互。\n请保存场景（Ctrl+S）。"
                : "未找到名称以 bench 开头的物体。请先保存场景，或手动选中长椅后使用「为选中物体添加坐下交互」。",
            "确定");
    }

    static int SetupBenchesRecursive(Transform t)
    {
        int count = 0;
        if (t == null)
            return 0;

        if (IsGeneratedChairHelperName(t.name))
            return 0;

        if (IsBenchName(t.name) && SetupBench(t.gameObject))
            count++;

        int childCountBeforeSetup = t.childCount;
        for (int i = 0; i < childCountBeforeSetup; i++)
            count += SetupBenchesRecursive(t.GetChild(i));

        return count;
    }

    static readonly string[] s_chairKeywords = new[]
    {
        "bench", "chair", "seat", "stool", "sofa", "couch", "armchair", "pew",
        "椅", "凳", "沙发", "座椅"
    };

    public static bool IsBenchName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
            return false;
        if (IsGeneratedChairHelperName(objectName))
            return false;

        string lower = objectName.ToLowerInvariant();
        if (IsExcluded(lower))
            return false;

        foreach (var kw in s_chairKeywords)
        {
            if (lower.Contains(kw.ToLowerInvariant()) || objectName.Contains(kw))
                return true;
        }
        return false;
    }

    public static bool IsGeneratedChairHelperName(string objectName)
    {
        return objectName == "SeatAnchor"
            || objectName == "SeatAnchor_Runtime"
            || objectName == "ChairSitPoint"
            || objectName == "ChairApproachPoint"
            || objectName == "ChairApproachPoint_Runtime"
            || objectName == "ChairSitBlocker";
    }

    /// <summary>排除车内座椅、安全带、模型术语等容易误命中的名称。</summary>
    static bool IsExcluded(string lowerName)
    {
        return lowerName.Contains("seatbelt")
            || lowerName.Contains("backseat")
            || lowerName.Contains("benchmark")
            || lowerName.Contains("chairperson")
            || lowerName.Contains("seater_car")
            || lowerName.Contains("car_seat")
            || lowerName.Contains("driverseat")
            || lowerName.Contains("driver_seat");
    }

    static bool SetupBench(GameObject go)
    {
        if (go == null)
            return false;

        var chair = go.GetComponent<InteractableChair>();
        if (chair == null)
            chair = Undo.AddComponent<InteractableChair>(go);

        chair.displayName = IsBenchName(go.name) ? "长椅" : go.name;
        chair.interactDistance = 2.8f;

        Transform seat = go.transform.Find("SeatAnchor");
        if (seat == null)
        {
            var seatGo = new GameObject("SeatAnchor");
            Undo.RegisterCreatedObjectUndo(seatGo, "Seat Anchor");
            seatGo.transform.SetParent(go.transform, false);
            seat = seatGo.transform;
        }

        Transform sit = go.transform.Find("ChairSitPoint");
        if (sit == null)
        {
            var sitGo = new GameObject("ChairSitPoint");
            Undo.RegisterCreatedObjectUndo(sitGo, "Chair Sit Point");
            sitGo.transform.SetParent(go.transform, false);
            sit = sitGo.transform;
        }

        Transform approach = go.transform.Find("ChairApproachPoint");
        if (approach == null)
        {
            var ap = new GameObject("ChairApproachPoint");
            Undo.RegisterCreatedObjectUndo(ap, "Chair Approach Point");
            ap.transform.SetParent(go.transform, false);
            approach = ap.transform;
        }

        AutoPlaceSeatAnchor(go.transform, seat, approach);
        sit.SetPositionAndRotation(seat.position, seat.rotation);
        chair.seatAnchor = seat;
        chair.sitPoint = sit;
        chair.approachPoint = approach;

        EnsureBlockingBoxCollider(go);

        EditorUtility.SetDirty(chair);
        Debug.Log($"[椅子交互] 已配置：{GetHierarchyPath(go.transform)}", go);
        return true;
    }

    /// <summary>
    /// 根据长椅所有 Renderer 包围盒自动生成座位锚点：
    /// - SeatAnchor：座位中心，Forward 指向椅子正面（短边方向），修正模型 Pivot/Forward 混乱。
    /// - ApproachPoint：SeatAnchor 正前方的地面站位。
    /// </summary>
    public static void AutoPlaceSeatAnchor(Transform benchRoot, Transform seatAnchor, Transform approachPoint = null)
    {
        AutoPlaceSitPoint(benchRoot, seatAnchor, approachPoint);
    }

    public static void AutoPlaceSitPoint(Transform benchRoot, Transform sitPoint, Transform approachPoint = null)
    {
        if (benchRoot == null || sitPoint == null)
            return;

        var renderers = benchRoot.GetComponentsInChildren<Renderer>(true);
        if (!ChairSitSeatPose.TryComputeFromRenderers(
                benchRoot,
                renderers,
                out Vector3 seatPos,
                out Quaternion seatRot,
                out Vector3 approachPos))
        {
            sitPoint.localPosition = Vector3.zero;
            sitPoint.localRotation = Quaternion.identity;
            if (approachPoint != null)
            {
                approachPoint.localPosition = new Vector3(0f, 0f, 0.7f);
                approachPoint.localRotation = Quaternion.identity;
            }
            return;
        }

        sitPoint.position = seatPos;
        sitPoint.rotation = seatRot;

        if (approachPoint != null)
        {
            approachPoint.position = approachPos;
            approachPoint.rotation = seatRot;
        }
    }

    /// <summary>
    /// 给椅子根加一个覆盖整体 bounds 的 BoxCollider（trigger=false），用于挡住玩家穿过。
    /// 已有同名 BoxCollider（ChairSitBlocker_*）时只更新尺寸。
    /// </summary>
    static void EnsureBlockingBoxCollider(GameObject go)
    {
        if (go == null)
            return;

        var renderers = go.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
            return;

        Bounds world = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            world.Encapsulate(renderers[i].bounds);

        Transform blockerT = go.transform.Find("ChairSitBlocker");
        GameObject blocker;
        if (blockerT == null)
        {
            blocker = new GameObject("ChairSitBlocker");
            Undo.RegisterCreatedObjectUndo(blocker, "Chair Sit Blocker");
            blocker.transform.SetParent(go.transform, false);
            blocker.layer = go.layer;
        }
        else
        {
            blocker = blockerT.gameObject;
        }

        var box = blocker.GetComponent<BoxCollider>();
        if (box == null)
            box = Undo.AddComponent<BoxCollider>(blocker);
        box.isTrigger = false;

        // 把世界 bounds 转回 blocker 局部空间。
        Vector3 localCenter = blocker.transform.InverseTransformPoint(world.center);
        Vector3 worldSize = world.size;
        Vector3 localScale = blocker.transform.lossyScale;
        Vector3 localSize = new Vector3(
            worldSize.x / Mathf.Max(0.0001f, Mathf.Abs(localScale.x)),
            worldSize.y / Mathf.Max(0.0001f, Mathf.Abs(localScale.y)),
            worldSize.z / Mathf.Max(0.0001f, Mathf.Abs(localScale.z)));

        // 略向外扩 0.05m 防止侧面缝隙穿过
        localSize += new Vector3(0.1f, 0.05f, 0.1f);

        box.center = localCenter;
        box.size = localSize;
        EditorUtility.SetDirty(box);
    }

    static string GetHierarchyPath(Transform t)
    {
        if (t == null)
            return "";
        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }

    static void MarkSceneDirty()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.IsValid())
            EditorSceneManager.MarkSceneDirty(scene);
    }
}
#endif
