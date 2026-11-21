using UnityEngine;

public class WideViewCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform target;
    public Rigidbody targetRigidbody;

    [Header("Camera Range")]
    public float baseHeight = 20f;
    public float baseHorizontalDistance = 12f;
    [Range(60, 130)] public float fieldOfView = 100f;
    [Range(1f, 1.5f)] public float forwardRangeMultiplier = 1.3f;
    [Range(0.5f, 1f)] public float backwardRangeMultiplier = 0.8f;

    [Header("Vertical Angles")]
    [Range(20, 60)] public float defaultDownAngle = 45f;
    [Range(-30, 10)] public float minVerticalAngle = -10f;
    [Range(40, 80)] public float maxVerticalAngle = 60f;

    [Header("Mouse / Follow")]
    public float smoothSpeed = 10f;
    public float mouseSensitivity = 2f;
    public bool enableMouseRotation = true;
    public bool requireRightClick = true;
    public bool rotateTargetWithMouse = true;
    public bool autoFollowMovementDirection = true;
    public float followDirectionSpeed = 5f;
    public float mouseInputTimeout = 1f;

    [Header("Teleport")]
    public float teleportThreshold = 10f; // Distance threshold for instant camera snap after teleport

    private Camera mainCamera;
    private float currentXRotation;
    private float currentYRotation;
    private Vector3 lastTargetPos;
    private float lastMouseInputTime;

    FirstPersonCamera _boundFirstPerson;

    public void BindFirstPersonCamera(FirstPersonCamera fps)
    {
        _boundFirstPerson = fps;
        if (fps != null && fps.playerObject != null && target == null)
        {
            target = fps.playerObject.transform;
            targetRigidbody = target.GetComponent<Rigidbody>();
        }
    }

    public void SyncFromFirstPersonView(FirstPersonCamera fps, bool rotatePlayerYaw = true)
    {
        if (fps == null)
            return;

        BindFirstPersonCamera(fps);
        if (!VehicleController.IsAnyVehicleDriven && fps.playerObject != null)
            target = fps.playerObject.transform;
        if (target == null)
            return;
        targetRigidbody = target.GetComponent<Rigidbody>();

        if (rotatePlayerYaw)
            currentXRotation = target.eulerAngles.y;
        else
            currentXRotation = fps.playerObject != null
                ? fps.playerObject.transform.eulerAngles.y
                : target.eulerAngles.y;

        currentYRotation = Mathf.Clamp(-fps.transform.localEulerAngles.x, minVerticalAngle, maxVerticalAngle);
        lastTargetPos = target.position;
        transform.position = CalculateCameraPosition();
    }

    public void ApplyOrbitToFirstPersonView(FirstPersonCamera fps)
    {
        if (fps == null)
            return;

        Vector3 look = transform.forward;
        if (target != null)
        {
            Vector3 toTarget = target.position - transform.position;
            if (toTarget.sqrMagnitude > 1e-6f)
                look = toTarget.normalized;
        }

        fps.ApplyWalkingLookFromWorldDirection(look);
    }

    public void ApplyFirstPersonStyleMouseLookSettings()
    {
        enableMouseRotation = true;
        requireRightClick = false;
    }

    public void AlignOrbitToWorldFacing(Quaternion worldFacing, bool rotatePlayerYaw)
    {
        if (target == null)
            return;

        Vector3 flatFwd = worldFacing * Vector3.forward;
        flatFwd.y = 0f;
        if (flatFwd.sqrMagnitude < 1e-6f)
            return;

        if (rotatePlayerYaw)
            target.rotation = Quaternion.LookRotation(flatFwd.normalized, Vector3.up);

        currentXRotation = target.eulerAngles.y;
        lastTargetPos = target.position;
        transform.position = CalculateCameraPosition();
    }

    public void SetVehicleDrivingTarget(Transform vehicleTarget)
    {
        if (vehicleTarget != null)
            target = vehicleTarget;
    }

    public void ClearVehicleDrivingTarget()
    {
        if (SceneTeleportHelper.TryFindLocalPlayer(out var player))
            target = player.transform;
    }

    private void Start()
    {
        mainCamera = GetComponent<Camera>();
        mainCamera.fieldOfView = fieldOfView;
        ApplyFirstPersonStyleMouseLookSettings();

        DetachFromFollowTargetIfNeeded();

        if (target != null)
        {
            currentXRotation = target.eulerAngles.y;
            currentYRotation = defaultDownAngle;
            lastTargetPos = target.position;
            lastMouseInputTime = Time.time;
            // ???????????????????????????????????????????????????????????????????????????????????????????????????????
            transform.position = CalculateCameraPosition();
        }

        if (target != null && targetRigidbody == null)
        {
            targetRigidbody = target.GetComponent<Rigidbody>();
        }
    }

    void DetachFromFollowTargetIfNeeded()
    {
        if (target == null || transform.parent == null)
            return;

        if (transform.parent == target || transform.IsChildOf(target))
            transform.SetParent(null, true);
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            Debug.LogError("WideViewCamera: target is not assigned.");
            return;
        }

        if (!GameplayCameraCursorRouting.ShouldHandleCursorForThirdPerson(this))
            return;

        GameplayCameraInputGate.SyncModalUiCameraInput();
        if (GameplayUiHotkeyGuard.BlocksCameraMouseLook())
            return;

        GameplayCursorLock.TryToggleEscape();
        GameplayCursorLock.TryRelockOnPrimaryClick();

        // ????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????
        if (Vector3.Distance(target.position, lastTargetPos) > teleportThreshold)
        {
            lastTargetPos = target.position; // ??????????????????????????????????????????????????????????????????????Lerp
            transform.position = CalculateCameraPosition(); // ??????????????????????????????????????
        }
        else
        {
            // ??????????????????????????????????????????????????????????????????????????????????????????????????????
            lastTargetPos = Vector3.Lerp(lastTargetPos, target.position, smoothSpeed * Time.deltaTime);
        }

        bool isMouseActive = HandleMouseInput();

        if (autoFollowMovementDirection && !isMouseActive && Time.time - lastMouseInputTime > mouseInputTimeout)
        {
            FollowMovementDirection();
        }

        // ????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????
        if (Vector3.Distance(target.position, lastTargetPos) <= teleportThreshold)
        {
            Vector3 targetPos = CalculateCameraPosition();
            transform.position = Vector3.Lerp(transform.position, targetPos, smoothSpeed * Time.deltaTime);
        }

        // ????????????????????????????????????????????????????????????????????????????????????????????????????????????
        Quaternion targetRot = Quaternion.LookRotation(lastTargetPos - transform.position);
        transform.rotation = Quaternion.Lerp(transform.rotation, targetRot, smoothSpeed * Time.deltaTime);
    }

    // ????????????????????????????????????????...
    private bool HandleMouseInput()
    {
        if (!enableMouseRotation) return false;
        if (!GameplayCursorLock.IsLocked) return false;

        bool isRightClickPressed = !requireRightClick || Input.GetMouseButton(1);
        if (!isRightClickPressed) return false;

        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        if (Mathf.Abs(mouseX) > 0.01f || Mathf.Abs(mouseY) > 0.01f)
        {
            currentXRotation += mouseX;
            currentYRotation -= mouseY;
            currentYRotation = Mathf.Clamp(currentYRotation, minVerticalAngle, maxVerticalAngle);
            ApplyTargetYawFromCamera();
            lastMouseInputTime = Time.time;
            return true;
        }

        return false;
    }

    void ApplyTargetYawFromCamera()
    {
        if (!rotateTargetWithMouse || target == null || VehicleController.IsAnyVehicleDriven)
            return;

        Vector3 forward = Quaternion.Euler(0f, currentXRotation, 0f) * Vector3.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude > 0.0001f)
            target.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
    }

    private void FollowMovementDirection()
    {
        Vector3 movementDir = GetMovementDirection();
        if (movementDir.sqrMagnitude < 0.01f) return;

        float targetAngle = Mathf.Atan2(movementDir.x, movementDir.z) * Mathf.Rad2Deg;
        currentXRotation = Mathf.LerpAngle(currentXRotation, targetAngle, followDirectionSpeed * Time.deltaTime);
    }

    private Vector3 GetMovementDirection()
    {
        Vector3 dir = Vector3.forward;

        if (targetRigidbody != null)
        {
            dir = targetRigidbody.velocity;
            dir.y = 0;
            if (dir.sqrMagnitude > 0.1f)
            {
                dir.Normalize();
                return dir;
            }
        }

        dir = target.forward;
        dir.y = 0;
        dir.Normalize();
        return dir;
    }

    private Vector3 CalculateCameraPosition()
    {
        float angleFactor = Mathf.Cos((currentXRotation - GetMovementDirectionAngle()) * Mathf.Deg2Rad);
        float rangeMultiplier = Mathf.Lerp(backwardRangeMultiplier, forwardRangeMultiplier, (angleFactor + 1) / 2f);
        float actualHorizontalDistance = baseHorizontalDistance * rangeMultiplier;

        float heightAdjustment = Mathf.Lerp(0.8f, 1.2f, (currentYRotation - minVerticalAngle) / (maxVerticalAngle - minVerticalAngle));
        float actualHeight = baseHeight * heightAdjustment;

        Quaternion horizontalRot = Quaternion.Euler(0, currentXRotation, 0);
        Vector3 horizontalOffset = horizontalRot * Vector3.back * actualHorizontalDistance;

        float verticalOffset = actualHeight * Mathf.Lerp(1f, 0.7f, currentYRotation / maxVerticalAngle);

        return lastTargetPos + horizontalOffset + Vector3.up * verticalOffset;
    }

    private float GetMovementDirectionAngle()
    {
        Vector3 dir = GetMovementDirection();
        return Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
    }

    // ??????????????????????MapSystem????????????????????????????????????????????????????????????????????????????????????????????????
    public void SetTargetAndTeleport(Transform newTarget)
    {
        if (newTarget == null) return;

        // 1. ??????????????????????????????????????????
        target = newTarget;
        if (targetRigidbody == null)
            targetRigidbody = target.GetComponent<Rigidbody>();

        // 2. ??????????????????????????????????????????????????????????????????????????????????????????????????
        lastTargetPos = target.position;

        // 3. ????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????
        transform.position = CalculateCameraPosition();

        // 4. ??????????????????????????????????????????????????
        Quaternion targetRot = Quaternion.LookRotation(target.position - transform.position);
        transform.rotation = targetRot;

        Debug.Log("WideViewCamera: orbit aligned to world facing.");
    }

    /// <summary>??/?? UI ?????????????????????</summary>
    public void ResumeGameplayFollowAfterExternalControl()
    {
        if (!VehicleController.IsAnyVehicleDriven)
        {
            if (_boundFirstPerson != null && _boundFirstPerson.playerObject != null)
                target = _boundFirstPerson.playerObject.transform;
            else if (target == null && SceneTeleportHelper.TryFindLocalPlayer(out GameObject player))
                target = player.transform;
        }
        if (target == null)
            return;

        targetRigidbody = target.GetComponent<Rigidbody>();
        lastTargetPos = target.position;
        lastMouseInputTime = Time.time;
        enableMouseRotation = true;
        requireRightClick = false;

        Vector3 camToTarget = target.position - transform.position;
        Vector3 flatBack = transform.position - target.position;
        flatBack.y = 0f;
        if (flatBack.sqrMagnitude > 0.05f)
        {
            flatBack.Normalize();
            currentXRotation = Mathf.Atan2(-flatBack.x, -flatBack.z) * Mathf.Rad2Deg;
        }
        else if (target.forward.sqrMagnitude > 0.01f)
        {
            Vector3 fwd = target.forward;
            fwd.y = 0f;
            fwd.Normalize();
            currentXRotation = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;
        }

        float horiz = new Vector2(camToTarget.x, camToTarget.z).magnitude;
        if (horiz > 0.01f)
        {
            float pitch = Mathf.Atan2(camToTarget.y, horiz) * Mathf.Rad2Deg;
            currentYRotation = Mathf.Clamp(pitch, minVerticalAngle, maxVerticalAngle);
        }

        transform.position = CalculateCameraPosition();
        Vector3 lookDir = lastTargetPos - transform.position;
        if (lookDir.sqrMagnitude > 1e-6f)
            transform.rotation = Quaternion.LookRotation(lookDir, Vector3.up);

        enabled = true;
    }

    // ????????????????Gizmos????????????????????????
    private void OnDrawGizmosSelected()
    {
        if (target != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(transform.position, target.position);

            Gizmos.color = new Color(0, 0.5f, 1, 0.2f);
            if (mainCamera == null) mainCamera = GetComponent<Camera>();
            Vector3 fovDirection = target.position - transform.position;
            Gizmos.DrawFrustum(transform.position, fieldOfView, fovDirection.magnitude * 1.5f, 0.1f, mainCamera.aspect);

            Gizmos.color = Color.red;
            Vector3 forwardDir = target.forward;
            forwardDir.y = 0;
            Gizmos.DrawRay(target.position, forwardDir * 3f);

            Gizmos.color = Color.green;
            Vector3 moveDir = GetMovementDirection();
            Gizmos.DrawRay(target.position, moveDir * 4f);

            Gizmos.color = new Color(1, 0.5f, 0, 0.1f);
            Gizmos.DrawWireSphere(target.position, baseHorizontalDistance);
        }
    }
}
