#if UNITY_EDITOR
using System.Collections.Generic;
using System.Diagnostics;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 用「从根物体递归整棵 Transform 树」收集带 MeshFilter / SkinnedMeshRenderer 且有 sharedMesh 的节点，
/// 这样大模型拆出来的深层子物体会全部覆盖（不依赖 FindObjectsOfType 对嵌套实例的完整性）。
/// </summary>
public static class AddMeshCollidersToScene
{
    const string MenuRoot = "Tools/Physics/";

    const int SkipMeshVertexCount = 28000;

    const int SkipMeshTriangleCount = 20000;

    [MenuItem(MenuRoot + "Add Mesh Colliders (Convex)", false, 10)]
    static void AddConvex() => Run(convex: true);

    [MenuItem(MenuRoot + "Add Mesh Colliders (Non-Convex)", false, 11)]
    static void AddNonConvex() => Run(convex: false);

    [MenuItem(MenuRoot + "Add Mesh Colliders (Convex) To Selection Only", false, 20)]
    static void AddConvexSelection() => Run(convex: true, selectionOnly: true);

    static void Run(bool convex, bool selectionOnly = false)
    {
        int added = 0;
        int updated = 0;
        int unchanged = 0;
        int noMeshOnRenderer = 0;
        int skippedHeavyMesh = 0;
        int cancelledAt = -1;

        var targets = CollectTargets(selectionOnly);
        if (targets == null)
            return;

        int total = targets.Count;
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();

        try
        {
            for (int i = 0; i < total; i++)
            {
                var go = targets[i];
                if (go == null) continue;

                if (EditorUtility.DisplayCancelableProgressBar(
                        "Mesh Colliders",
                        $"处理中 {i + 1} / {total}  ({go.name})",
                        total > 0 ? (float)(i + 1) / total : 1f))
                {
                    cancelledAt = i + 1;
                    UnityEngine.Debug.LogWarning($"[AddMeshColliders] 用户取消，已处理到第 {cancelledAt} 个目标（共 {total} 个）。");
                    break;
                }

                var sw = Stopwatch.StartNew();
                EnsureMeshCollider(go, convex, ref added, ref updated, ref unchanged, ref noMeshOnRenderer, ref skippedHeavyMesh);
                sw.Stop();

                if (sw.ElapsedMilliseconds >= 5000)
                {
                    UnityEngine.Debug.LogWarning(
                        $"[AddMeshColliders] 单物体烘焙耗时 ≥5s（{sw.Elapsed.TotalSeconds:F1}s）：{GetScenePath(go)} — " +
                        "若经常如此，请降低 SkipMeshVertexCount / SkipMeshTriangleCount 或对该网格简化 / 用凸包。");
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            Undo.CollapseUndoOperations(undoGroup);
            Undo.SetCurrentGroupName("Add Mesh Colliders");
        }

        UnityEngine.Debug.Log(
            "[AddMeshColliders] 完成 — " +
            $"含网格物体数: {total}, 新增: {added}, 更新/补全: {updated}, 已有且正确: {unchanged}, " +
            $"无 Mesh: {noMeshOnRenderer}, 因网格过大跳过: {skippedHeavyMesh}" +
            (cancelledAt >= 0 ? $", 用户取消于: {cancelledAt}" : "") +
            $". Convex: {(convex ? "是" : "否")}" +
            $"{(selectionOnly ? " (仅选中子树)" : "")}"
        );
    }

    static string GetScenePath(GameObject go)
    {
        if (go == null) return "(null)";
        var t = go.transform;
        var stack = new List<string>();
        while (t != null)
        {
            stack.Add(t.name);
            t = t.parent;
        }

        stack.Reverse();
        return string.Join("/", stack);
    }

    static bool IsSceneInstance(GameObject go)
    {
        if (go == null) return false;
        if (EditorUtility.IsPersistent(go))
            return false;
        var scene = go.scene;
        return scene.IsValid() && scene.isLoaded;
    }

    static List<GameObject> CollectTargets(bool selectionOnly)
    {
        var set = new HashSet<GameObject>();

        if (selectionOnly)
        {
            if (Selection.gameObjects == null || Selection.gameObjects.Length == 0)
            {
                EditorUtility.DisplayDialog(
                    "Add Mesh Colliders",
                    "请先在 Hierarchy 中选中建模根物体（将递归其下所有子物体）。",
                    "确定");
                return null;
            }

            foreach (var root in Selection.gameObjects)
            {
                if (root == null) continue;
                CollectMeshesRecursive(root.transform, set);
            }
        }
        else
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var sc = SceneManager.GetSceneAt(i);
                if (!sc.isLoaded) continue;
                foreach (var root in sc.GetRootGameObjects())
                {
                    if (root != null)
                        CollectMeshesRecursive(root.transform, set);
                }
            }
        }

        return new List<GameObject>(set);
    }

    static void CollectMeshesRecursive(Transform t, HashSet<GameObject> set)
    {
        if (t == null) return;

        var go = t.gameObject;
        if (IsSceneInstance(go))
        {
            var mf = go.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
                set.Add(go);
            else
            {
                var smr = go.GetComponent<SkinnedMeshRenderer>();
                if (smr != null && smr.sharedMesh != null)
                    set.Add(go);
            }
        }

        for (int c = 0; c < t.childCount; c++)
            CollectMeshesRecursive(t.GetChild(c), set);
    }

    static Mesh ResolveMesh(GameObject go)
    {
        var mf = go.GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
            return mf.sharedMesh;

        var smr = go.GetComponent<SkinnedMeshRenderer>();
        if (smr != null && smr.sharedMesh != null)
            return smr.sharedMesh;

        return null;
    }

    static long CountTriangles(Mesh mesh)
    {
        if (mesh == null || mesh.subMeshCount <= 0)
            return 0;

        long n = 0;
        for (int s = 0; s < mesh.subMeshCount; s++)
            n += (long)mesh.GetIndexCount(s) / 3;
        return n;
    }

    static bool ShouldSkipHeavyMesh(Mesh mesh, GameObject go, ref int skippedHeavyMesh)
    {
        if (mesh == null) return false;

        int vc = mesh.vertexCount;
        long tris = CountTriangles(mesh);

        if (vc > SkipMeshVertexCount || tris > SkipMeshTriangleCount)
        {
            skippedHeavyMesh++;
            UnityEngine.Debug.LogWarning(
                $"[AddMeshColliders] 跳过（网格过大，避免长时间卡住）：{GetScenePath(go)} " +
                $"vertices={vc}, tris={tris}. 阈值: {SkipMeshVertexCount} verts / {SkipMeshTriangleCount} tris。");
            return true;
        }

        return false;
    }

    static void EnsureMeshCollider(
        GameObject go,
        bool convex,
        ref int added,
        ref int updated,
        ref int unchanged,
        ref int noMeshOnRenderer,
        ref int skippedHeavyMesh)
    {
        var mesh = ResolveMesh(go);
        if (mesh == null)
        {
            noMeshOnRenderer++;
            return;
        }

        if (ShouldSkipHeavyMesh(mesh, go, ref skippedHeavyMesh))
            return;

        var mc = go.GetComponent<MeshCollider>();
        if (mc == null)
        {
            mc = Undo.AddComponent<MeshCollider>(go);
            mc.sharedMesh = mesh;
            mc.convex = convex;
            EditorUtility.SetDirty(go);
            added++;
            return;
        }

        if (mc.sharedMesh != mesh || mc.convex != convex)
        {
            Undo.RecordObject(mc, "MeshCollider assign");
            mc.sharedMesh = mesh;
            mc.convex = convex;
            EditorUtility.SetDirty(mc);
            updated++;
            return;
        }

        unchanged++;
    }
}
#endif
