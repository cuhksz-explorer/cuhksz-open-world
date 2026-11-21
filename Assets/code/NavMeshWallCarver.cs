using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 墙/柱上的 MeshCollider 不会让 <see cref="NavMeshAgent"/> 绕行：代理只认烘焙出来的 NavMesh。
/// 在墙体根物体上挂本脚本（需带 Collider），会自动添加 <see cref="NavMeshObstacle"/> 并开启 <b>Carve</b>，
/// 在运行时从 NavMesh 上挖出不可走区域，避免 NPC 穿墙。
/// </summary>
[DisallowMultipleComponent]
public class NavMeshWallCarver : MonoBehaviour
{
    [Tooltip("进入场景时根据当前 Collider 配置障碍（静态墙可关改用手动 Apply）")]
    [SerializeField] bool configureOnAwake = true;

    [Tooltip("静态墙体勾选：只雕刻一次，更省性能")]
    [SerializeField] bool carveOnlyStationary = true;

    void Awake()
    {
        if (configureOnAwake)
            ApplyFromCollider();
    }

#if UNITY_EDITOR
    [ContextMenu("从 Collider 应用 NavMesh 障碍")]
    void EditorApply() => ApplyFromCollider();
#endif

    /// <summary>按 Collider 形状写入 NavMeshObstacle（盒体为主；胶囊墙用 CapsuleCollider）。</summary>
    public void ApplyFromCollider()
    {
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            Debug.LogWarning("[NavMeshWallCarver] 需要 Collider（Mesh / Box 等）。物体: " + gameObject.name, this);
            return;
        }

        NavMeshObstacle obs = GetComponent<NavMeshObstacle>();
        if (obs == null)
            obs = gameObject.AddComponent<NavMeshObstacle>();

        obs.carving = true;
        obs.carveOnlyStationary = carveOnlyStationary;

        switch (col)
        {
            case BoxCollider bc:
                obs.shape = NavMeshObstacleShape.Box;
                obs.center = bc.center;
                obs.size = bc.size;
                break;

            case MeshCollider mc:
                obs.shape = NavMeshObstacleShape.Box;
                if (mc.sharedMesh != null)
                {
                    Bounds b = mc.sharedMesh.bounds;
                    obs.center = b.center;
                    obs.size = b.size;
                }
                else
                {
                    FitBoxToWorldBounds(col, obs);
                }
                break;

            case CapsuleCollider cc:
                obs.shape = NavMeshObstacleShape.Capsule;
                obs.center = cc.center;
                obs.radius = cc.radius;
                obs.height = cc.height;
                break;

            default:
                obs.shape = NavMeshObstacleShape.Box;
                FitBoxToWorldBounds(col, obs);
                break;
        }
    }

    static void FitBoxToWorldBounds(Collider col, NavMeshObstacle obs)
    {
        Bounds wb = col.bounds;
        obs.center = col.transform.InverseTransformPoint(wb.center);
        Vector3 s = col.transform.lossyScale;
        obs.size = new Vector3(
            wb.size.x / Mathf.Max(0.001f, Mathf.Abs(s.x)),
            wb.size.y / Mathf.Max(0.001f, Mathf.Abs(s.y)),
            wb.size.z / Mathf.Max(0.001f, Mathf.Abs(s.z)));
    }
}
