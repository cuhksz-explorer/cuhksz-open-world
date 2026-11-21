using UnityEngine;

/// <summary>
/// 挂在角色上：长按 F2 召唤/收回已绑定载具。
/// - 第一次长按：在角色周围 radius 米内选点并把载具传送过去（自动激活）
/// - 再次长按：隐藏载具（SetActive(false)）
/// </summary>
public class VehicleSummonByHoldF2 : MonoBehaviour
{
    public enum VehicleForwardAxis
    {
        PositiveZ,
        PositiveX
    }

    [Header("输入")]
    public KeyCode summonKey = KeyCode.F2;
    [Tooltip("长按触发秒数")]
    public float holdSeconds = 0.8f;

    [Header("载具引用")]
    [Tooltip("提前放在地图中的载具对象（可先禁用，召唤时会自动启用）")]
    public GameObject vehicleObject;

    [Header("召唤范围")]
    [Tooltip("开启后使用简化规则：保持载具当前旋转/缩放等参数不变，仅修改位置到角色附近固定偏移。")]
    public bool keepAllParamsAndOnlyMovePosition = true;
    [Tooltip("简化规则下，载具目标 X = 角色X + 此偏移（默认 5m）")]
    public float summonOffsetX = 5f;
    [Tooltip("简化规则下，载具目标 Z = 角色Z + 此偏移（默认 5m）")]
    public float summonOffsetZ = 5f;

    [Tooltip("在角色周围该半径内尝试找可落点，默认 5m")]
    public float summonRadius = 5f;
    [Tooltip("若找不到平地，使用角色前方该距离作为兜底点")]
    public float fallbackForwardDistance = 2.5f;
    [Tooltip("召唤点与角色的最小水平距离（米），避免直接落在脚下把车顶翻）")]
    public float minSummonDistance2D = 2f;
    [Tooltip("地面检测层")]
    public LayerMask groundLayers = ~0;
    [Tooltip("召唤后额外抬高，防止轻微穿地")]
    public float yOffset = 0.05f;
    [Tooltip("从角色头顶开始下落的高度（米）")]
    public float dropStartHeightAbovePlayer = 5f;
    [Tooltip("下落速度（米/秒）")]
    public float dropSpeed = 12f;
    [Tooltip("是否让召唤后的车辆 Yaw 对齐角色朝向")]
    public bool alignYawToPlayer = true;
    [Tooltip("车辆模型“车头前向”使用哪个本地轴。你当前车型应选 PositiveX。")]
    public VehicleForwardAxis vehicleForwardAxis = VehicleForwardAxis.PositiveX;
    [Tooltip("额外 Yaw 偏移（度），一般保持 0；仅在美术资源仍有偏差时微调。")]
    public float vehicleYawOffset = 0f;

    [Header("调试")]
    public bool debugLog = false;
    [Header("自动扶正")]
    [Tooltip("召唤后自动启用扶正追踪，避免车辆头朝下/底朝天")]
    public bool autoAddUprightTracker = true;

    float _holdTimer;
    bool _triggeredThisHold;
    Coroutine _coDrop;
    void Update()
    {
                // 驾驶 Carsystem 车辆时 F2 用于翻车自救，不与召唤载具冲突。
        if (InteractableVehicle.IsAnyCarSystemVehicleDriven)
            return;

        if (Input.GetKeyDown(summonKey))
        {
            _holdTimer = 0f;
            _triggeredThisHold = false;
        }

        if (!Input.GetKey(summonKey))
            return;

        if (_triggeredThisHold)
            return;

        _holdTimer += Time.unscaledDeltaTime;
        if (_holdTimer < holdSeconds)
            return;

        _triggeredThisHold = true;
        ToggleSummon();
    }

    void LateUpdate()
    {
        if (Input.GetKeyUp(summonKey))
        {
            _holdTimer = 0f;
            _triggeredThisHold = false;
        }
    }

    void ToggleSummon()
    {
        if (vehicleObject == null)
        {
            Debug.LogWarning("[VehicleSummon] vehicleObject 未绑定。");
            return;
        }

        // 已在场上 -> 隐藏
        if (vehicleObject.activeSelf)
        {
            var vc = vehicleObject.GetComponent<VehicleController>();
            if (vc != null && vc.IsDriving)
            {
                Debug.LogWarning("[VehicleSummon] 当前正在驾驶中，不能收回载具。");
                return;
            }

            vehicleObject.SetActive(false);
            if (_coDrop != null)
            {
                StopCoroutine(_coDrop);
                _coDrop = null;
            }
            if (debugLog) Debug.Log("[VehicleSummon] 载具已收回。");
            return;
        }

        // 未在场上 -> 召唤
        if (keepAllParamsAndOnlyMovePosition)
        {
            Vector3 target = new Vector3(
                transform.position.x + summonOffsetX,
                transform.position.y,
                transform.position.z + summonOffsetZ);

            vehicleObject.SetActive(true);
            vehicleObject.transform.position = target;

            if (debugLog) Debug.Log($"[VehicleSummon] 简化召唤：仅改位置到 {target}（旋转/缩放保持原样）");
            return;
        }

        Vector3 targetPos;
        if (!TryPickGroundPoint(transform.position, summonRadius, out targetPos))
            targetPos = transform.position + transform.forward * Mathf.Max(1f, fallbackForwardDistance);

        Vector3 groundTarget = targetPos;
        Quaternion spawnRot = GetSummonRotation();
        vehicleObject.SetActive(true);

        // 先应用旋转，再按“车底到 pivot”的偏移算真实落地高度，避免插地
        vehicleObject.transform.rotation = spawnRot;
        Vector3 probePos = new Vector3(
            groundTarget.x,
            transform.position.y + Mathf.Max(0.5f, dropStartHeightAbovePlayer),
            groundTarget.z);
        vehicleObject.transform.position = probePos;

        float bottomToPivot = GetBottomToPivotHeight();
        float endPivotY = groundTarget.y + yOffset + bottomToPivot;
        float startPivotY = Mathf.Max(
            transform.position.y + Mathf.Max(0.5f, dropStartHeightAbovePlayer),
            endPivotY + 0.8f);

        Vector3 startPos = new Vector3(groundTarget.x, startPivotY, groundTarget.z);
        Vector3 endPos = new Vector3(groundTarget.x, endPivotY, groundTarget.z);
        vehicleObject.transform.position = startPos;

        if (autoAddUprightTracker)
        {
            var tracker = vehicleObject.GetComponent<VehicleAutoUprightTracker>();
            if (tracker == null)
                tracker = vehicleObject.AddComponent<VehicleAutoUprightTracker>();
            tracker.groundLayers = groundLayers;
        }
        if (_coDrop != null)
            StopCoroutine(_coDrop);
        _coDrop = StartCoroutine(CoDropToGround(endPos));

        if (debugLog) Debug.Log($"[VehicleSummon] 载具从 {startPos} 下落到 {endPos}，bottomToPivot={bottomToPivot:F3}");
    }

    System.Collections.IEnumerator CoDropToGround(Vector3 endPos)
    {
        float speed = Mathf.Max(0.5f, dropSpeed);
        while (vehicleObject != null && vehicleObject.activeSelf)
        {
            Vector3 p = vehicleObject.transform.position;
            if (p.y <= endPos.y + 0.001f)
            {
                p.y = endPos.y;
                vehicleObject.transform.position = p;
                break;
            }
            p.y = Mathf.MoveTowards(p.y, endPos.y, speed * Time.deltaTime);
            vehicleObject.transform.position = p;
            yield return null;
        }
        _coDrop = null;
    }

    Quaternion GetSummonRotation()
    {
        Quaternion baseRot = vehicleObject != null ? vehicleObject.transform.rotation : transform.rotation;
        Vector3 baseForward = GetVehicleForwardFromRotation(baseRot);
        baseForward = Vector3.ProjectOnPlane(baseForward, Vector3.up).normalized;
        if (baseForward.sqrMagnitude < 0.0001f)
            baseForward = Vector3.forward;

        Vector3 desiredForward;
        if (alignYawToPlayer)
        {
            Vector3 fwd = transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.0001f)
                desiredForward = baseForward;
            else
                desiredForward = fwd.normalized;
        }
        else
        {
            desiredForward = baseForward;
        }

        float yawDelta = Vector3.SignedAngle(baseForward, desiredForward, Vector3.up);
        Quaternion rot = Quaternion.AngleAxis(yawDelta, Vector3.up) * baseRot;

        if (Mathf.Abs(vehicleYawOffset) > 0.001f)
            rot = Quaternion.AngleAxis(vehicleYawOffset, Vector3.up) * rot;
        return rot;
    }

    Vector3 GetVehicleForwardFromRotation(Quaternion rot)
    {
        if (vehicleForwardAxis == VehicleForwardAxis.PositiveX)
            return rot * Vector3.right;
        return rot * Vector3.forward;
    }

    bool TryPickGroundPoint(Vector3 center, float radius, out Vector3 point)
    {
        const int samples = 12;
        float bestDist = float.MaxValue;
        bool ok = false;
        Vector3 best = center;
        Vector2 centerXZ = new Vector2(center.x, center.z);
        float minD = Mathf.Max(0f, minSummonDistance2D);

        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)samples;
            float ang = t * Mathf.PI * 2f;
            Vector3 candidate = center + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * radius;
            if (!TryRaycastGround(candidate, out Vector3 hit))
                continue;

            // 仅按角色水平面（XZ）上的圆形半径判定，忽略 Y 高差。
            float d = Vector2.Distance(centerXZ, new Vector2(hit.x, hit.z));
            if (d < minD)
                continue;
            if (d < bestDist)
            {
                bestDist = d;
                best = hit;
                ok = true;
            }
        }

        // 不再优先脚下点，避免车辆贴身下落导致穿插/翻车；失败时由上层 fallbackForwardDistance 兜底。

        point = best;
        return ok;
    }

    bool TryRaycastGround(Vector3 around, out Vector3 hitPoint)
    {
        Vector3 from = around + Vector3.up * 30f;
        if (Physics.Raycast(from, Vector3.down, out RaycastHit hit, 100f, groundLayers, QueryTriggerInteraction.Ignore))
        {
            hitPoint = hit.point;
            return true;
        }
        hitPoint = around;
        return false;
    }

    float GetBottomToPivotHeight()
    {
        if (vehicleObject == null)
            return 0.5f;
        if (!TryGetWorldBounds(vehicleObject, out Bounds b))
            return 0.5f;
        return Mathf.Max(0f, vehicleObject.transform.position.y - b.min.y);
    }

    bool TryGetWorldBounds(GameObject obj, out Bounds bounds)
    {
        var colliders = obj.GetComponentsInChildren<Collider>(true);
        if (colliders.Length > 0)
        {
            bounds = colliders[0].bounds;
            for (int i = 1; i < colliders.Length; i++)
                bounds.Encapsulate(colliders[i].bounds);
            return true;
        }

        var renderers = obj.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length > 0)
        {
            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            return true;
        }

        bounds = default;
        return false;
    }
}
