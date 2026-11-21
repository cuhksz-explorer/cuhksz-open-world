using UnityEngine;

/// <summary>
/// 不可见的阻挡墙：玩家（CharacterController）无法穿过。
/// 在 Inspector 中直接调整 Transform 与 BoxCollider 的 center/size，或使用两点模式自动生成墙体。
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public class AirWall : MonoBehaviour
{
    public enum SizingMode
    {
        /// <summary>手动调整 Transform 与 BoxCollider。</summary>
        Manual,
        /// <summary>根据 pointA、pointB 自动计算位置、旋转与长度。</summary>
        TwoPoint
    }

    [Header("尺寸模式")]
    public SizingMode sizingMode = SizingMode.Manual;

    [Header("两点模式（仅 TwoPoint 生效）")]
    [Tooltip("墙起点")]
    public Transform pointA;
    [Tooltip("墙终点")]
    public Transform pointB;
    [Tooltip("墙高度")]
    public float wallHeight = 3f;
    [Tooltip("墙厚度（法线方向）")]
    public float wallThickness = 0.3f;

    [Header("编辑器")]
    public bool autoRebuildInEditor = true;
    public Color gizmoColor = new Color(1f, 0.35f, 0.2f, 0.35f);

    BoxCollider _box;

    void Awake()
    {
        EnsureCollider();
        ApplySizing();
    }

    void OnEnable()
    {
        EnsureCollider();
        ApplySizing();
    }

    void OnValidate()
    {
        wallHeight = Mathf.Max(0.1f, wallHeight);
        wallThickness = Mathf.Max(0.05f, wallThickness);

        EnsureCollider();
        if (autoRebuildInEditor || Application.isPlaying)
            ApplySizing();
    }

    void Update()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying && autoRebuildInEditor && sizingMode == SizingMode.TwoPoint)
            ApplySizing();
#endif
    }

    void EnsureCollider()
    {
        if (_box == null)
            _box = GetComponent<BoxCollider>();
        if (_box == null)
            _box = gameObject.AddComponent<BoxCollider>();

        _box.isTrigger = false;
    }

    /// <summary>按当前模式刷新碰撞体（TwoPoint 模式下根据两点重建）。</summary>
    public void ApplySizing()
    {
        EnsureCollider();
        if (sizingMode != SizingMode.TwoPoint)
            return;

        if (_box == null || pointA == null || pointB == null)
            return;

        Vector3 a = pointA.position;
        Vector3 b = pointB.position;
        Vector3 dir = b - a;
        float length = dir.magnitude;
        if (length < 0.01f)
            return;

        transform.position = (a + b) * 0.5f;

        Vector3 dirXZ = new Vector3(dir.x, 0f, dir.z);
        if (dirXZ.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(dirXZ.normalized, Vector3.up);

        _box.center = new Vector3(0f, wallHeight * 0.5f, 0f);
        _box.size = new Vector3(wallThickness, wallHeight, length);
    }

    /// <summary>手动模式下设置 BoxCollider 尺寸（局部空间）。</summary>
    public void SetManualSize(Vector3 center, Vector3 size)
    {
        sizingMode = SizingMode.Manual;
        EnsureCollider();
        _box.center = center;
        _box.size = size;
    }

    void OnDrawGizmosSelected()
    {
        DrawGizmo(true);
    }

    void OnDrawGizmos()
    {
        DrawGizmo(false);
    }

    void DrawGizmo(bool selected)
    {
        var box = _box != null ? _box : GetComponent<BoxCollider>();
        if (box == null)
            return;

        Color color = gizmoColor;
        if (!selected)
            color.a *= 0.45f;
        Gizmos.color = color;

        Matrix4x4 old = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(box.center, box.size);
        Gizmos.matrix = old;

        if (sizingMode == SizingMode.TwoPoint && pointA != null && pointB != null)
        {
            Gizmos.color = new Color(1f, 0.85f, 0.2f, selected ? 0.9f : 0.5f);
            Gizmos.DrawSphere(pointA.position, 0.15f);
            Gizmos.DrawSphere(pointB.position, 0.15f);
            Gizmos.DrawLine(pointA.position, pointB.position);
        }
    }
}
