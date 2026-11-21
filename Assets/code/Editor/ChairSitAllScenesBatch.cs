#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 跨场景批量为所有椅子（bench / chair / seat / stool / sofa / couch / 椅 / 凳 ...）添加坐下交互。
/// 自动遍历 Assets/scene 下所有 .unity 场景，逐一打开 → setup → 保存 → 关闭。
/// </summary>
public static class ChairSitAllScenesBatch
{
    [MenuItem("Tools/Chair Sit/5 一键：所有场景批量添加坐下交互", false, 4)]
    [MenuItem("Tools/椅子交互/一键：所有场景批量添加坐下交互", false, 4)]
    public static void RunAllScenes()
    {
        if (!ChairSitEditorGuard.EnsureNotPlaying())
            return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        if (!EditorUtility.DisplayDialog(
                "椅子交互 · 全场景批量",
                "将依次打开 Assets/scene 下所有 .unity 场景，为每个场景里的 bench/chair/seat/sofa/凳/椅 等物体加上坐下交互。\n\n该操作会修改并保存这些场景文件，确认继续？",
                "继续", "取消"))
            return;

        string activeScenePath = SceneManager.GetActiveScene().path;
        var scenePaths = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/scene" }))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (p.EndsWith(".unity") && !p.Contains("/Library/"))
                scenePaths.Add(p);
        }

        int totalChairs = 0;
        int touchedScenes = 0;
        int processed = 0;

        try
        {
            foreach (string p in scenePaths)
            {
                processed++;
                if (EditorUtility.DisplayCancelableProgressBar(
                        "椅子交互 · 全场景批量",
                        $"({processed}/{scenePaths.Count}) 处理 {Path.GetFileName(p)}",
                        (float)processed / Mathf.Max(1, scenePaths.Count)))
                {
                    Debug.Log("[椅子交互] 用户取消批量。");
                    break;
                }

                var scene = EditorSceneManager.OpenScene(p, OpenSceneMode.Single);
                int found = CountAndSetupInScene(scene);
                if (found > 0)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    totalChairs += found;
                    touchedScenes++;
                    Debug.Log($"[椅子交互] {Path.GetFileName(p)} 配置 {found} 个椅子。");
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            if (!string.IsNullOrEmpty(activeScenePath) && File.Exists(activeScenePath))
                EditorSceneManager.OpenScene(activeScenePath, OpenSceneMode.Single);
        }

        EditorUtility.DisplayDialog(
            "椅子交互 · 全场景批量",
            $"完成！\n\n扫描场景：{scenePaths.Count}\n修改场景：{touchedScenes}\n配置椅子：{totalChairs}",
            "确定");
    }

    static int CountAndSetupInScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return 0;

        int count = 0;
        foreach (var root in scene.GetRootGameObjects())
            count += SetupRecursive(root.transform);
        return count;
    }

    static int SetupRecursive(Transform t)
    {
        if (t == null)
            return 0;
        if (InteractableChairSetup.IsGeneratedChairHelperName(t.name))
            return 0;

        int count = 0;
        if (InteractableChairSetup.IsBenchName(t.name))
        {
            if (SetupOne(t.gameObject))
                count++;
        }

        int childCountBeforeSetup = t.childCount;
        for (int i = 0; i < childCountBeforeSetup; i++)
            count += SetupRecursive(t.GetChild(i));
        return count;
    }

    static bool SetupOne(GameObject go)
    {
        if (go == null)
            return false;

        var chair = go.GetComponent<InteractableChair>();
        bool created = false;
        if (chair == null)
        {
            chair = Undo.AddComponent<InteractableChair>(go);
            created = true;
        }

        chair.displayName = ClassifyDisplayName(go.name);
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

        InteractableChairSetup.AutoPlaceSeatAnchor(go.transform, seat, approach);
        sit.SetPositionAndRotation(seat.position, seat.rotation);
        chair.seatAnchor = seat;
        chair.sitPoint = sit;
        chair.approachPoint = approach;
        EditorUtility.SetDirty(chair);
        return created;
    }

    static string ClassifyDisplayName(string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return "座位";
        string lower = raw.ToLowerInvariant();
        if (lower.Contains("sofa") || lower.Contains("couch") || lower.Contains("沙发")) return "沙发";
        if (lower.Contains("stool") || lower.Contains("凳")) return "凳子";
        if (lower.Contains("bench") || lower.Contains("长椅")) return "长椅";
        if (lower.Contains("chair") || lower.Contains("seat") || lower.Contains("椅")) return "椅子";
        return "座位";
    }
}
#endif
