using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 在两点之间生成一堵“空气墙”（Box Trigger）。
/// 当角色穿过该墙时，直接传送到目标场景。
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(BoxCollider))]
public class AirWallSceneTeleporter : MonoBehaviour
{
    [Header("空气墙范围（两点）")]
    [Tooltip("墙起点")]
    public Transform pointA;
    [Tooltip("墙终点")]
    public Transform pointB;
    [Tooltip("墙高度（Y）")]
    public float wallHeight = 3f;
    [Tooltip("墙厚度（法线方向）")]
    public float wallThickness = 0.4f;

    [Header("传送设置")]
    [Tooltip("进入空气墙后要切换到的场景名（需在 Build Settings 中）")]
    public string targetSceneName = "MainScene";
    [Tooltip("触发后冷却秒数，防止同帧重复触发")]
    public float triggerCooldownSeconds = 1f;
    [Tooltip("只允许玩家触发（Player 标签或挂有 PlayerMovement）")]
    public bool playerOnly = true;

    [Header("调试")]
    public bool autoRebuildColliderInEditor = true;
    public bool debugLog = false;
    public Color gizmoColor = new Color(0.2f, 0.8f, 1f, 0.35f);

    BoxCollider _box;
    float _nextTriggerTime;

    void Awake()
    {
        EnsureCollider();
        RebuildWallCollider();
    }

    void OnEnable()
    {
        EnsureCollider();
        RebuildWallCollider();
    }

    void OnValidate()
    {
        wallHeight = Mathf.Max(0.1f, wallHeight);
        wallThickness = Mathf.Max(0.05f, wallThickness);
        triggerCooldownSeconds = Mathf.Max(0f, triggerCooldownSeconds);

        if (autoRebuildColliderInEditor)
        {
            EnsureCollider();
            RebuildWallCollider();
        }
    }

    void Update()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying && autoRebuildColliderInEditor)
            RebuildWallCollider();
#endif
    }

    void EnsureCollider()
    {
        if (_box == null)
            _box = GetComponent<BoxCollider>();
        if (_box == null)
            _box = gameObject.AddComponent<BoxCollider>();
        _box.isTrigger = true;
    }

    void RebuildWallCollider()
    {
        if (_box == null || pointA == null || pointB == null)
            return;

        Vector3 a = pointA.position;
        Vector3 b = pointB.position;
        Vector3 dir = b - a;
        float length = dir.magnitude;
        if (length < 0.01f)
            return;

        Vector3 center = (a + b) * 0.5f;
        transform.position = center;

        // 仅绕 Y 轴旋转，墙体垂直于地面
        Vector3 dirXZ = new Vector3(dir.x, 0f, dir.z);
        if (dirXZ.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(dirXZ.normalized, Vector3.up);

        // BoxCollider 本地 z 作为“长度”
        _box.center = new Vector3(0f, wallHeight * 0.5f, 0f);
        _box.size = new Vector3(wallThickness, wallHeight, length);
    }

    void OnTriggerEnter(Collider other)
    {
        if (!Application.isPlaying)
            return;

        if (Time.time < _nextTriggerTime)
            return;

        if (playerOnly && !IsPlayerCollider(other))
            return;

        if (string.IsNullOrWhiteSpace(targetSceneName))
        {
            Debug.LogWarning("[AirWallSceneTeleporter] targetSceneName 为空，无法传送。", this);
            return;
        }

        _nextTriggerTime = Time.time + triggerCooldownSeconds;
        if (debugLog)
            Debug.Log($"[AirWallSceneTeleporter] 触发传送 -> {targetSceneName}", this);

        SceneManager.LoadScene(targetSceneName);
    }

    bool IsPlayerCollider(Collider col)
    {
        if (col == null) return false;
        if (col.CompareTag("Player")) return true;
        if (col.GetComponentInParent<PlayerMovement>() != null) return true;
        return false;
    }

    void OnDrawGizmos()
    {
        if (pointA == null || pointB == null)
            return;

        Gizmos.color = gizmoColor;
        Vector3 a = pointA.position;
        Vector3 b = pointB.position;
        Gizmos.DrawLine(a, b);

        Vector3 center = (a + b) * 0.5f + Vector3.up * (wallHeight * 0.5f);
        Vector3 size = new Vector3(wallThickness, wallHeight, Vector3.Distance(a, b));
        Matrix4x4 old = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
        Gizmos.DrawCube(new Vector3(0f, wallHeight * 0.5f, 0f), size);
        Gizmos.matrix = old;
    }
}
