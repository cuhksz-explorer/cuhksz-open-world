using UnityEngine;

/// <summary>
/// 图书馆1楼：每个网格仅保留一个非凸 MeshCollider；Awake 时去重并补全缺失项。
/// </summary>
[DisallowMultipleComponent]
public class LibraryFloor1PhysicsBootstrap : MonoBehaviour
{
    [SerializeField] bool _setupOnAwake = true;

    void Awake()
    {
        if (_setupOnAwake)
            EnsureColliders(transform);
    }

    /// <returns>移除的重复碰撞体数量</returns>
    public static int RemoveDuplicateColliders(Transform root)
    {
        if (root == null)
            return 0;

        int removed = 0;
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            var mcs = t.GetComponents<MeshCollider>();
            if (mcs.Length <= 1)
                continue;

            MeshCollider keep = mcs[0];
            Mesh targetMesh = ResolveMesh(t.gameObject);
            if (targetMesh != null)
            {
                foreach (var mc in mcs)
                {
                    if (mc.sharedMesh == targetMesh)
                    {
                        keep = mc;
                        break;
                    }
                }
            }

            for (int i = 0; i < mcs.Length; i++)
            {
                if (mcs[i] == keep)
                    continue;
                if (Application.isPlaying)
                    Destroy(mcs[i]);
                else
                    DestroyImmediate(mcs[i]);
                removed++;
            }
        }

        return removed;
    }

    public static int EnsureColliders(Transform root)
    {
        if (root == null)
            return 0;

        int removed = RemoveDuplicateColliders(root);
        int added = 0;

        foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf == null || mf.sharedMesh == null)
                continue;
            if (ConfigureSingleCollider(mf.gameObject, mf.sharedMesh))
                added++;
        }

        foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (smr == null || smr.sharedMesh == null)
                continue;
            var go = smr.gameObject;
            if (go.GetComponent<MeshFilter>() != null)
                continue;
            if (ConfigureSingleCollider(go, smr.sharedMesh))
                added++;
        }

        if (removed > 0)
            Debug.Log($"[图书馆1楼] 已移除 {removed} 个重复 MeshCollider。");

        return added;
    }

    static bool ConfigureSingleCollider(GameObject go, Mesh mesh)
    {
        var mcs = go.GetComponents<MeshCollider>();
        MeshCollider mc;
        bool added = false;

        if (mcs.Length == 0)
        {
            mc = go.AddComponent<MeshCollider>();
            added = true;
        }
        else
        {
            mc = mcs[0];
            for (int i = 1; i < mcs.Length; i++)
            {
                if (Application.isPlaying)
                    Destroy(mcs[i]);
                else
                    DestroyImmediate(mcs[i]);
            }
        }

        mc.sharedMesh = mesh;
        mc.convex = false;
        go.isStatic = true;
        return added;
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
}
