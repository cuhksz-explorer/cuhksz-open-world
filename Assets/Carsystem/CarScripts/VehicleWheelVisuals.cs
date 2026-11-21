using UnityEngine;

/// <summary>
/// 车辆车轮视觉系统
/// 挂载位置：车辆根物体（与 Rigidbody、CarController 同一 GameObject）
/// </summary>
public class VehicleWheelVisuals : MonoBehaviour
{
    [Header("Wheel Collider（物理车轮）")]
    public WheelCollider wheelColliderFL;
    public WheelCollider wheelColliderFR;
    public WheelCollider wheelColliderRL;
    public WheelCollider wheelColliderRR;

    [Header("视觉轮子模型（Transform）")]
    public Transform wheelVisualFL;
    public Transform wheelVisualFR;
    public Transform wheelVisualRL;
    public Transform wheelVisualRR;

    [Header("参数")]
    // Prefab 中 WheelCollider.Radius = 0.5，必须与此一致
    [Tooltip("必须与 WheelCollider Inspector 中的 Radius 保持一致（当前 Prefab = 0.5）")]
    public float wheelRadius = 0.5f;

    [Tooltip("在 Scene 视图显示触地射线（绿=触地，红=悬空）")]
    public bool showDebugRays = true;

    void LateUpdate()
    {
        ApplyWheelPose(wheelColliderFL, wheelVisualFL);
        ApplyWheelPose(wheelColliderFR, wheelVisualFR);
        ApplyWheelPose(wheelColliderRL, wheelVisualRL);
        ApplyWheelPose(wheelColliderRR, wheelVisualRR);
    }

    void ApplyWheelPose(WheelCollider col, Transform vis)
    {
        if (col == null || vis == null) return;

        // GetWorldPose 已包含：悬挂压缩位移 + 车轮滚动旋转 + 转向旋转
        // 直接赋值，不做任何二次欧拉角运算（会引发万向锁导致前轮抽搐）
        col.GetWorldPose(out Vector3 pos, out Quaternion rot);
        vis.position = pos;
        vis.rotation = rot;

        if (showDebugRays)
        {
            Vector3 origin = vis.position + Vector3.up * 0.1f;
            bool hit = Physics.Raycast(origin, Vector3.down, wheelRadius + 0.2f);
            Debug.DrawRay(origin, Vector3.down * (wheelRadius + 0.2f), hit ? Color.green : Color.red);
        }
    }

    void OnDrawGizmosSelected()
    {
        Transform[] visuals = { wheelVisualFL, wheelVisualFR, wheelVisualRL, wheelVisualRR };
        foreach (var vis in visuals)
        {
            if (vis == null) continue;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(vis.position, wheelRadius);
        }
    }
}