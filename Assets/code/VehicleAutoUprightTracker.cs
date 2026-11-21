using UnityEngine;

/// <summary>
/// 自动扶正追踪：车辆出现倒扣/大幅侧翻时，平滑拉回 upright，防止“车头朝下/底朝天”。
/// 可挂在任意载具根节点。
/// </summary>
public class VehicleAutoUprightTracker : MonoBehaviour
{
    [Header("触发阈值")]
    [Tooltip("transform.up 与世界 Up 点积低于此值时开始扶正；0 表示大于 90 度才扶正")]
    [Range(-1f, 1f)] public float startUprightDotThreshold = 0.2f;
    [Tooltip("扶正到该点积以上视为稳定，停止连续拉正")]
    [Range(-1f, 1f)] public float stopUprightDotThreshold = 0.9f;

    [Header("扶正参数")]
    [Tooltip("扶正旋转速度（度/秒）")]
    public float uprightAngularSpeed = 200f;
    [Tooltip("扶正时额外抬升，避免与地面卡住")]
    public float raiseWhenUpright = 0.15f;
    [Tooltip("是否每帧尝试把车辆“贴地且立正”")]
    public bool continuousGroundSnap = false;
    public LayerMask groundLayers = ~0;

    [Header("调试")]
    public bool debugLog = false;

    bool _isRecovering;

    void LateUpdate()
    {
        float dot = Vector3.Dot(transform.up, Vector3.up);
        if (!_isRecovering && dot < startUprightDotThreshold)
        {
            _isRecovering = true;
            if (debugLog) Debug.Log("[VehicleUpright] 检测到翻车，开始扶正。");
        }

        if (!_isRecovering && !continuousGroundSnap)
            return;

        Quaternion target = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            target,
            uprightAngularSpeed * Time.deltaTime);

        if (TryGetGroundY(transform.position, out float gy))
        {
            Vector3 p = transform.position;
            if (p.y < gy + raiseWhenUpright)
            {
                p.y = gy + raiseWhenUpright;
                transform.position = p;
            }
        }

        if (_isRecovering)
        {
            float nd = Vector3.Dot(transform.up, Vector3.up);
            if (nd >= stopUprightDotThreshold)
            {
                _isRecovering = false;
                if (debugLog) Debug.Log("[VehicleUpright] 扶正完成。");
            }
        }
    }

    public void ForceUprightNow(float extraRaise = 0.1f)
    {
        transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        if (TryGetGroundY(transform.position, out float gy))
        {
            var p = transform.position;
            p.y = gy + Mathf.Max(0f, extraRaise);
            transform.position = p;
        }
        _isRecovering = false;
    }

    bool TryGetGroundY(Vector3 pos, out float groundY)
    {
        Vector3 from = pos + Vector3.up * 30f;
        if (Physics.Raycast(from, Vector3.down, out RaycastHit hit, 100f, groundLayers, QueryTriggerInteraction.Ignore))
        {
            groundY = hit.point.y;
            return true;
        }
        groundY = pos.y;
        return false;
    }
}
