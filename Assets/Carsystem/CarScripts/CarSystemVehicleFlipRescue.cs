using Mirror;
using UnityEngine;

/// <summary>
/// Carsystem 翻车自救：驾驶中持续记录「最后一次稳定姿态」，按 F2 恢复到该姿态（位置 + 朝向 + 清零速度）。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class CarSystemVehicleFlipRescue : MonoBehaviour
{
    [Header("输入")]
    public KeyCode rescueKey = KeyCode.F2;

    [Header("稳定姿态采样")]
    [Tooltip("transform.up 与世界 Up 点积高于此值时才更新快照。")]
    [Range(0.5f, 1f)]
    public float stableUpDotThreshold = 0.82f;
    [Tooltip("角速度低于此值（度/秒）才更新快照。")]
    public float maxAngularSpeedDegForSnapshot = 120f;
    [Tooltip("无快照时扶正后相对地面的抬升。")]
    public float groundClearance = 0.15f;
    public LayerMask groundLayers = ~0;

    [Header("调试")]
    public bool debugLog;

    Rigidbody _rb;
    InteractableVehicle _vehicle;
    Carbilili _carController;
    VehicleAutoUprightTracker _uprightTracker;

    bool _hasSnapshot;
    Vector3 _snapshotPosition;
    Quaternion _snapshotRotation;

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _vehicle = GetComponent<InteractableVehicle>();
        _carController = GetComponent<Carbilili>();
        _uprightTracker = GetComponent<VehicleAutoUprightTracker>();
    }

    void FixedUpdate()
    {
        if (!CanRecordSnapshot())
            return;
        TryRecordStableSnapshot();
    }

    void Update()
    {
        if (!IsLocalDriverOccupying())
            return;

        if (GameplayUiHotkeyGuard.ShouldBlockGameplayHotkeys()
            || GameplayUiHotkeyGuard.BlocksGameplayBecauseMultiplayerUi())
            return;

        if (Input.GetKeyDown(rescueKey))
            PerformFlipRescue();
    }

    /// <summary>上车时可调用，确保至少有一份可用快照。</summary>
    public void CaptureSnapshotNow()
    {
        _snapshotPosition = transform.position;
        _snapshotRotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        _hasSnapshot = true;
    }

    public void PerformFlipRescue()
    {
        if (!IsLocalDriverOccupying())
            return;

        if (_hasSnapshot)
            RestoreSnapshot();
        else
            UprightInPlace();

        if (_carController != null)
            _carController.StopImmediately();

        if (_rb != null)
        {
            _rb.velocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.WakeUp();
        }

        if (debugLog)
            Debug.Log($"[CarSystemVehicleFlipRescue] {name} 已执行 F2 自救。", this);
    }

    bool CanRecordSnapshot()
    {
        if (_vehicle == null || !_vehicle.isOccupied)
            return false;
        return IsLocalDriverOccupying();
    }

    void TryRecordStableSnapshot()
    {
        float upDot = Vector3.Dot(transform.up, Vector3.up);
        if (upDot < stableUpDotThreshold)
            return;

        if (_rb != null)
        {
            float angDeg = _rb.angularVelocity.magnitude * Mathf.Rad2Deg;
            if (angDeg > maxAngularSpeedDegForSnapshot)
                return;
        }

        _snapshotPosition = transform.position;
        _snapshotRotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        _hasSnapshot = true;
    }

    void RestoreSnapshot()
    {
        Vector3 pos = _snapshotPosition;
        if (TryGetGroundY(pos, out float gy))
            pos.y = Mathf.Max(pos.y, gy + groundClearance);

        ApplyPose(pos, _snapshotRotation);
    }

    void UprightInPlace()
    {
        if (_uprightTracker != null)
        {
            _uprightTracker.ForceUprightNow(groundClearance);
            CaptureSnapshotNow();
            return;
        }

        Vector3 pos = transform.position;
        Quaternion rot = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        if (TryGetGroundY(pos, out float gy))
            pos.y = gy + groundClearance;
        ApplyPose(pos, rot);
        CaptureSnapshotNow();
    }

    void ApplyPose(Vector3 position, Quaternion rotation)
    {
        if (_rb != null)
        {
            _rb.velocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.MovePosition(position);
            _rb.MoveRotation(rotation);
        }
        else
        {
            transform.SetPositionAndRotation(position, rotation);
        }
    }

    bool TryGetGroundY(Vector3 worldPos, out float groundY)
    {
        groundY = worldPos.y;
        Vector3 origin = worldPos + Vector3.up * 3f;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 12f, groundLayers, QueryTriggerInteraction.Ignore))
        {
            groundY = hit.point.y;
            return true;
        }
        return false;
    }

    bool IsLocalDriverOccupying()
    {
        if (_vehicle == null || !_vehicle.isOccupied)
            return false;

        var proxy = GetComponent<CarSystemVehicleNetworkProxy>();
        if (proxy == null || !NetworkClient.active)
            return true;
        return proxy.IsLocalDriver;
    }
}
