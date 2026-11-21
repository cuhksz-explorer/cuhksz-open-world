using UnityEngine;

/// <summary>
/// 登录/标题界面的背景镜头用：沿坐标路点匀速巡航并循环。
/// 可选「到达路点后先停下来缓慢转向，再前往下一个目标点」，让转动更顺滑，不要生硬“瞬转”。
/// </summary>
public class IntroDollyCamera : MonoBehaviour
{
    [Tooltip("要移动的物体（留空则用本物体，一般为摄像机）")]
    [SerializeField] Transform target;

    [Header("路径（坐标）")]
    [Tooltip("按行驶顺序排列；至少 2 个点")]
    [SerializeField] Vector3[] waypointPositions;

    [Tooltip("与 waypointPositions 同长度时生效：指定每个路点“到站后的期望朝向”（用于转向）。长度不一致则自动朝向前进方向。")]
    [SerializeField] Vector3[] waypointEulerAngles;

    [Tooltip("转向：true 时优先使用 waypointEulerAngles（若缺失则自动朝向）；false 则始终朝向前进方向")]
    [SerializeField] bool useWaypointRotation = true;

    [Header("运动")]
    [Tooltip("沿路径移动速度（世界单位/秒）")]
    [SerializeField] float moveSpeed = 8f;

    [Tooltip("调用 SetPaused(true) 时是否先平滑减速再完全停下。")]
    [SerializeField] bool smoothStopOnPause = true;
    [Tooltip("平滑减速到完全停止所用时间（秒）。")]
    [SerializeField] float smoothStopDuration = 1.2f;

    [Tooltip("闭合路线：最后一点连回第一点")]
    [SerializeField] bool closedLoop = true;

    [Header("平滑转向")]
    [Tooltip("到达每个路点后：先停住缓慢转向，再开始移动到下一个点。建议开启。")]
    [SerializeField] bool pauseAtWaypointsForRotation = true;

    [Tooltip("每次转向持续时间（秒）。为 0 则相当于瞬转。")]
    [SerializeField] float waypointTurnDuration = 0.45f;

    [SerializeField] bool playOnEnable = true;

    float[] _segmentLengths;
    int _segmentCount;
    int _seg;
    float _segT;
    bool _paused;

    bool _isTurning;
    float _turnElapsed;
    float _turnFromT;
    Quaternion _turnFromRot;
    Quaternion _turnToRot;
    float _pendingMoveDistance;
    bool _pauseRequested;
    float _pauseElapsed;

    void OnEnable()
    {
        if (playOnEnable)
            SetPaused(false);
    }

    void Start()
    {
        if (target == null)
            target = transform;
        RebuildPath();
        SnapToPathStart();
    }

    void Update()
    {
        if (target == null || _segmentCount <= 0 || moveSpeed <= 0f)
            return;

        if (_paused && !_pauseRequested)
            return;

        // 转向期间不移动，保证“到点转完再走”。
        if (_isTurning)
        {
            TickTurning();
            return;
        }

        float effectiveSpeed = moveSpeed;
        if (_pauseRequested && smoothStopOnPause && smoothStopDuration > 0.01f)
        {
            _pauseElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_pauseElapsed / smoothStopDuration);
            float factor = 1f - t;
            effectiveSpeed *= factor;

            if (t >= 1f - 0.0001f)
            {
                _pauseRequested = false;
                _paused = true;
                effectiveSpeed = 0f;
            }
        }

        if (_paused && !_pauseRequested)
            return;

        if (effectiveSpeed <= 0f)
            return;

        _pendingMoveDistance += effectiveSpeed * Time.deltaTime;

        while (!_isTurning && _pendingMoveDistance > 0.0001f)
        {
            AdvanceMoveOnce();
        }
    }

    public void SetPaused(bool paused)
    {
        if (!paused)
        {
            _paused = false;
            _pauseRequested = false;
            _pauseElapsed = 0f;
            return;
        }

        if (smoothStopOnPause && smoothStopDuration > 0.01f)
        {
            _pauseRequested = true;
            _pauseElapsed = 0f;
        }
        else
        {
            _paused = true;
            _pauseRequested = false;
        }
    }

    public bool IsPaused => _paused;

    /// <summary>在编辑器改坐标后可在运行时调用以重建长度表。</summary>
    [ContextMenu("Rebuild Path")]
    public void RebuildPath()
    {
        if (waypointPositions == null || waypointPositions.Length < 2)
        {
            _segmentLengths = null;
            _segmentCount = 0;
            return;
        }

        int n = waypointPositions.Length;
        _segmentCount = closedLoop ? n : n - 1;
        _segmentLengths = new float[_segmentCount];
        for (int s = 0; s < _segmentCount; s++)
        {
            int i0 = s;
            int i1 = closedLoop ? (s + 1) % n : s + 1;
            _segmentLengths[s] = Vector3.Distance(waypointPositions[i0], waypointPositions[i1]);
        }
    }

    void SnapToPathStart()
    {
        if (waypointPositions == null || waypointPositions.Length < 2 || target == null)
            return;

        _seg = 0;
        _segT = 0f;
        _isTurning = false;
        _pendingMoveDistance = 0f;
        // 起始点朝向与第一段方向/路点朝向保持一致
        ApplyPoseMoving();
    }

    void AdvanceMoveOnce()
    {
        if (_segmentCount <= 0 || _seg < 0 || _seg >= _segmentCount)
            return;

        float len = _segmentLengths[_seg];
        if (len < 0.0001f)
        {
            // 相邻点太近：直接认为到达终点并进入转向/下一段
            _segT = 1f;
            ApplyPoseMoving();
            HandleArriveAtWaypointAndTurnIfNeeded();
            return;
        }

        float remainAlongSeg = len * (1f - _segT);

        if (_pendingMoveDistance < remainAlongSeg)
        {
            _segT += _pendingMoveDistance / len;
            _pendingMoveDistance = 0f;
            ApplyPoseMoving();
            return;
        }

        // 到达本段终点
        _pendingMoveDistance -= remainAlongSeg;
        _segT = 1f;
        ApplyPoseMoving();
        HandleArriveAtWaypointAndTurnIfNeeded();
    }

    void HandleArriveAtWaypointAndTurnIfNeeded()
    {
        if (pauseAtWaypointsForRotation && waypointTurnDuration > 0.0001f)
        {
            int n = waypointPositions.Length;
            int fromIndex = _seg;
            int toIndex = closedLoop ? (_seg + 1) % n : _seg + 1;

            // 如果是开放路线的最后一个点：没有下一段方向，直接结束移动
            if (!closedLoop && toIndex >= n - 1)
            {
                _pendingMoveDistance = 0f;
                // 保持到站朝向
                ApplyPoseMoving();
                return;
            }

            _turnFromRot = target.rotation;
            _turnToRot = GetExpectedRotationAtWaypointIndex(toIndex);
            float angle = Quaternion.Angle(_turnFromRot, _turnToRot);
            if (angle > 0.1f)
            {
                _isTurning = true;
                _turnElapsed = 0f;
                TickTurning(); // 先推进到 0->小量，避免一帧延迟
                return;
            }
        }

        // 直接进入下一段
        AdvanceToNextSegmentOrStop();
    }

    void AdvanceToNextSegmentOrStop()
    {
        _seg++;
        _segT = 0f;

        if (_seg >= _segmentCount)
        {
            if (closedLoop)
            {
                _seg = 0;
            }
            else
            {
                _seg = _segmentCount - 1;
                _segT = 1f;
                _pendingMoveDistance = 0f;
            }
        }

        ApplyPoseMoving();
    }

    void TickTurning()
    {
        if (!_isTurning || target == null) return;

        if (waypointTurnDuration <= 0.0001f)
        {
            target.rotation = _turnToRot;
            _isTurning = false;
            AdvanceToNextSegmentOrStop();
            return;
        }

        _turnElapsed += Time.deltaTime;
        float t = Mathf.Clamp01(_turnElapsed / waypointTurnDuration);
        target.rotation = Quaternion.Slerp(_turnFromRot, _turnToRot, t);

        if (t >= 1f - 0.0001f)
        {
            _isTurning = false;
            // 转完之后继续用剩余 pendingMoveDistance 推进
            AdvanceToNextSegmentOrStop();
        }
    }

    Quaternion GetExpectedRotationAtWaypointIndex(int waypointIndex)
    {
        int n = waypointPositions != null ? waypointPositions.Length : 0;
        if (n < 2) return target.rotation;
        if (waypointIndex < 0 || waypointIndex >= n) return target.rotation;

        if (useWaypointRotation && waypointEulerAngles != null && waypointEulerAngles.Length == n)
        {
            return Quaternion.Euler(waypointEulerAngles[waypointIndex]);
        }

        // 自动朝向：朝向“下一点 - 当前点”
        int nextIndex;
        if (closedLoop)
            nextIndex = (waypointIndex + 1) % n;
        else
            nextIndex = waypointIndex + 1;

        if (!closedLoop && nextIndex >= n) return target.rotation;

        Vector3 dir = waypointPositions[nextIndex] - waypointPositions[waypointIndex];
        if (dir.sqrMagnitude < 0.0001f) return target.rotation;
        return Quaternion.LookRotation(dir.normalized, Vector3.up);
    }

    void ApplyPoseMoving()
    {
        if (waypointPositions == null || waypointPositions.Length < 2 || target == null)
            return;

        int n = waypointPositions.Length;
        int i0 = Mathf.Clamp(_seg, 0, n - 1);
        int i1 = closedLoop ? (_seg + 1) % n : Mathf.Min(_seg + 1, n - 1);
        Vector3 p0 = waypointPositions[i0];
        Vector3 p1 = waypointPositions[i1];

        target.position = Vector3.Lerp(p0, p1, _segT);

        // 移动期间保持当前段的朝向（转向由到点流程控制）。
        target.rotation = GetExpectedRotationAtWaypointIndex(i0);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (waypointPositions == null || waypointPositions.Length < 2) return;
        Gizmos.color = Color.cyan;
        int n = waypointPositions.Length;
        int max = closedLoop ? n : n - 1;
        for (int s = 0; s < max; s++)
        {
            int i0 = s;
            int i1 = closedLoop ? (s + 1) % n : s + 1;
            Gizmos.DrawLine(waypointPositions[i0], waypointPositions[i1]);
        }
    }
#endif
}
