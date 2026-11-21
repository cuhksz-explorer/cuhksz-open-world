using UnityEngine;

[RequireComponent(typeof(Camera))]
public class FirstPersonCamera : MonoBehaviour
{
    [Header("Player References")]
    public GameObject playerObject;
    public Transform cameraHolder;

    [Header("Field Of View")]
    [Range(60f, 120f)] public float fieldOfView = 85f;

    [Header("Look Settings")]
    public float horizontalSensitivity = 2f;
    public float verticalSensitivity = 2f;
    public float maxLookUpAngle = 85f;
    public float maxLookDownAngle = -60f;

    [Header("Eye Rig")]
    [Tooltip("第一人称视点相对玩家本地坐标的前向偏移（米），避免疾跑/加速时看见头部模型")]
    public float eyeForwardOffsetLocal = PlayerCharacterCameraUtility.DefaultEyeForwardOffsetLocal;

    /// <summary>?????????????????????????????? R/E/T ?????????</summary>
    public bool suppressMouseLook;

    /// <summary>
    /// ? true?????????????????????? Y ?????? false?????????????? CameraHolder ? Y?????�I???
    /// </summary>
    public bool applyMouseYawToPlayerBody = true;

    /// <summary>
    /// ? true ????? VehicleController ??????????????????????????? Transform??
    /// </summary>
    public bool vehicleDrivingDecoupledFromPlayer;

    /// <summary>???????????????????? up??? null ??????? Y??</summary>
    public Transform vehicleYawAxisReference;

    [Tooltip("??????????????/????????????? PlayerMovement.rotationSpeed * deltaTime ????")]
    public bool matchPlayerMovementMouseScalingWhileVehicleDecoupled = true;

    [Tooltip("?? PlayerMovement.rotationSpeed ?????????????????????")]
    public float pairedRotationSpeed = 15f;

    Transform playerTransform;
    float xRotation;
    Camera mainCamera;
    bool isActive;
    bool isCursorLocked;
    int _rigRestoreHoldFrames;
    int _mouseLookWarmupFrames;
    Vector3 _standingEyeLocalPos;
    float _crouchEyeBlend;
    float _crouchCameraHeightRatio = 1f;

    public struct WalkingRigSnapshot
    {
        public Vector3 holderLocalPos;
        public Quaternion holderLocalRot;
        public Vector3 holderLocalScale;
        public Vector3 cameraLocalPos;
        public float pitch;
        public float playerYaw;
    }

    public bool IsActive => isActive;
    public bool IsRendering => mainCamera != null && mainCamera.enabled;
    public bool IsCursorLocked => isCursorLocked;
    public bool IsPreservingRestoredRig => _rigRestoreHoldFrames > 0;

    public void SetCursorLocked(bool locked)
    {
        bool wasLocked = isCursorLocked;
        isCursorLocked = locked;
        GameplayCursorLock.SetLocked(locked);
        UpdateCursorState();
        if (locked && !wasLocked)
            WarmupMouseLookInput();
    }

    void Awake()
    {
        mainCamera = GetComponent<Camera>();
    }

    void Start()
    {
        if (mainCamera == null)
            mainCamera = GetComponent<Camera>();
        mainCamera.fieldOfView = fieldOfView;
        mainCamera.enabled = false;

        if (playerObject == null)
        {
            PlayerMovement pm = FindObjectOfType<PlayerMovement>();
            playerObject = pm ? pm.gameObject : null;
        }

        if (playerObject != null)
            playerTransform = playerObject.transform;

        if (cameraHolder == null && playerTransform != null)
        {
            GameObject holder = new GameObject("Auto_CameraHolder");
            cameraHolder = holder.transform;
            cameraHolder.SetParent(playerTransform, false);
            transform.SetParent(cameraHolder, false);
            transform.localPosition = Vector3.zero;
            ApplyWalkingEyeRigFromCharacter();
        }

        if (playerObject == null)
        {
            Debug.LogError("[FirstPersonCamera] ��?????????");
            enabled = false;
            return;
        }

        if (cameraHolder != null && playerTransform != null)
            cameraHolder.SetParent(playerTransform, false);

        SanitizeWalkingRigHierarchy();
        ApplyWalkingEyeRigFromCharacter();
    }

    public void EnableRenderingOnly()
    {
        if (mainCamera == null)
            mainCamera = GetComponent<Camera>();
        isActive = true;
        if (mainCamera != null)
            mainCamera.enabled = true;
    }

    public void Activate()
    {
        if (mainCamera == null)
            mainCamera = GetComponent<Camera>();
        bool wasLocked = isCursorLocked && GameplayCursorLock.IsLocked && Cursor.lockState == CursorLockMode.Locked;
        isActive = true;
        suppressMouseLook = false;
        if (mainCamera != null)
            mainCamera.enabled = true;
        isCursorLocked = true;
        EnsureGameplayReady();
        AdoptCurrentViewAsWalkingLookState();
        GameplayCursorLock.LockForGameplayView();
        if (!wasLocked)
            WarmupMouseLookInput();
    }

    public void Deactivate()
    {
        isActive = false;
        if (mainCamera != null)
            mainCamera.enabled = false;
        isCursorLocked = false;
        UpdateCursorState();
    }

    public void EnsureGameplayReady()
    {
        EnsureGameplayReady(playerObject);
    }

    public void EnsureGameplayReady(GameObject playerGo)
    {
        if (playerGo != null)
        {
            playerObject = playerGo;
            playerTransform = playerGo.transform;
        }

        if (IsPreservingRestoredRig)
            return;

        SanitizeWalkingRigHierarchy();
        ApplyWalkingEyeRigFromCharacter();
        ReconcileWalkingControlState();
    }

    public void ReconcileWalkingControlState()
    {
        if (!isActive)
            return;

        if (GameplayCursorLock.IsLocked)
        {
            isCursorLocked = true;
            UpdateCursorState();
        }
    }

    public WalkingRigSnapshot CaptureWalkingRigSnapshot()
    {
        if (playerObject != null)
            playerTransform = playerObject.transform;

        SyncPitchFromTransform();

        return new WalkingRigSnapshot
        {
            holderLocalPos = cameraHolder != null ? cameraHolder.localPosition : Vector3.zero,
            holderLocalRot = cameraHolder != null ? cameraHolder.localRotation : Quaternion.identity,
            holderLocalScale = cameraHolder != null ? cameraHolder.localScale : Vector3.one,
            cameraLocalPos = transform.localPosition,
            pitch = xRotation,
            playerYaw = playerTransform != null ? playerTransform.eulerAngles.y : 0f
        };
    }

    public void RestoreWalkingRigSnapshot(WalkingRigSnapshot snap, bool restorePlayerYaw = true)
    {
        if (mainCamera == null)
            mainCamera = GetComponent<Camera>();

        if (playerObject != null)
            playerTransform = playerObject.transform;

        if (restorePlayerYaw && playerTransform != null)
            playerTransform.rotation = Quaternion.Euler(0f, snap.playerYaw, 0f);

        if (cameraHolder != null && playerTransform != null)
        {
            if (cameraHolder.parent != playerTransform)
                cameraHolder.SetParent(playerTransform, false);

            cameraHolder.localPosition = snap.holderLocalPos;
            cameraHolder.localRotation = snap.holderLocalRot;
            cameraHolder.localScale = snap.holderLocalScale.sqrMagnitude > 0.0001f
                ? snap.holderLocalScale
                : Vector3.one;
        }

        xRotation = snap.pitch;
        transform.localPosition = snap.cameraLocalPos;
        transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        _rigRestoreHoldFrames = Mathf.Max(_rigRestoreHoldFrames, 8);
    }

    public void ExtendRigRestoreHold(int frames)
    {
        _rigRestoreHoldFrames = Mathf.Max(_rigRestoreHoldFrames, frames);
    }

    /// <summary>????????????/????????? CameraHolder ?????????��???��?��?</summary>
    public void SnapRigAfterWorldTeleport()
    {
        if (playerObject != null)
            playerTransform = playerObject.transform;

        SanitizeWalkingRigHierarchy();
    }

    public void SnapRigToPlayerHeadAfterTeleport(bool preservePitch = true)
    {
        SnapRigAfterWorldTeleport();
    }

    public void ApplyWalkingLookFromWorldDirection(Vector3 worldLook)
    {
        if (worldLook.sqrMagnitude < 1e-8f)
            return;

        worldLook.Normalize();
        if (playerTransform != null)
        {
            Vector3 flat = new Vector3(worldLook.x, 0f, worldLook.z);
            if (flat.sqrMagnitude > 1e-8f)
            {
                playerTransform.rotation = Quaternion.LookRotation(flat.normalized, Vector3.up);
                if (!vehicleDrivingDecoupledFromPlayer && applyMouseYawToPlayerBody && cameraHolder != null)
                    cameraHolder.localRotation = Quaternion.identity;
            }
        }

        float horiz = Mathf.Sqrt(worldLook.x * worldLook.x + worldLook.z * worldLook.z);
        float pitchDeg = Mathf.Atan2(worldLook.y, Mathf.Max(horiz, 1e-6f)) * Mathf.Rad2Deg;
        xRotation = Mathf.Clamp(pitchDeg, maxLookDownAngle, maxLookUpAngle);
        transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
    }

    public void RestoreWalkingCameraRigAfterVehicleExit(
        Transform player,
        Vector3 holderLocalPos,
        Quaternion holderLocalRot,
        Vector3 fullWorldLookDir)
    {
        if (player == null || cameraHolder == null) return;
        if (fullWorldLookDir.sqrMagnitude < 1e-10f) return;

        fullWorldLookDir.Normalize();
        Vector3 horiz = new Vector3(fullWorldLookDir.x, 0f, fullWorldLookDir.z);
        if (horiz.sqrMagnitude < 1e-8f)
            horiz = new Vector3(player.forward.x, 0f, player.forward.z);
        horiz.Normalize();

        player.rotation = Quaternion.LookRotation(horiz, Vector3.up);

        if (cameraHolder.parent != player)
            cameraHolder.SetParent(player, true);

        cameraHolder.localPosition = holderLocalPos;
        cameraHolder.localRotation = holderLocalRot;

        Vector3 localForward = Quaternion.Inverse(player.rotation) * fullWorldLookDir;
        float xz = Mathf.Sqrt(localForward.x * localForward.x + localForward.z * localForward.z);
        float pitchDeg = Mathf.Atan2(localForward.y, Mathf.Max(xz, 1e-8f)) * Mathf.Rad2Deg;
        xRotation = Mathf.Clamp(pitchDeg, maxLookDownAngle, maxLookUpAngle);
        transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
    }

    public void ResetWalkingPivotsAfterVehicleExit(
        float playerWorldYawDegrees,
        Transform player,
        Vector3 holderLocalPos,
        Quaternion holderLocalRot)
    {
        if (player != null)
            player.rotation = Quaternion.Euler(0f, playerWorldYawDegrees, 0f);
        if (cameraHolder != null)
        {
            if (player != null && cameraHolder.parent != player)
                cameraHolder.SetParent(player, true);
            cameraHolder.localPosition = holderLocalPos;
            cameraHolder.localRotation = holderLocalRot;
        }

        xRotation = 0f;
        transform.localRotation = Quaternion.identity;
    }

    void LateUpdate()
    {
        if (_rigRestoreHoldFrames > 0)
            _rigRestoreHoldFrames--;

        if (!isActive) return;
        if (!vehicleDrivingDecoupledFromPlayer && playerTransform == null) return;
        if (vehicleDrivingDecoupledFromPlayer && cameraHolder == null) return;
        if (GameServerSync.BlocksInputForServerSpawn) return;
        GameplayCameraInputGate.SyncModalUiCameraInput();
        if (GameplayUiHotkeyGuard.BlocksCameraMouseLook())
            return;

        if (GameplayCameraCursorRouting.ShouldHandleCursorForFirstPerson(this))
        {
            GameplayCursorLock.TryRelockOnPrimaryClick();
            isCursorLocked = GameplayCursorLock.IsLocked;
        }

        if (isCursorLocked)
        {
            if (_mouseLookWarmupFrames > 0)
            {
                _mouseLookWarmupFrames--;
                ConsumeMouseLookAxes();
                return;
            }
            HandleMouseLook();
        }
    }

    void Update()
    {
        if (!isActive) return;
        if (GameServerSync.BlocksInputForServerSpawn) return;
        GameplayCameraInputGate.SyncModalUiCameraInput();
        if (GameplayUiHotkeyGuard.BlocksCameraMouseLook())
            return;

        if (GameplayCameraCursorRouting.ShouldHandleCursorForFirstPerson(this)
            && GameplayCursorLock.TryToggleEscape())
        {
            isCursorLocked = GameplayCursorLock.IsLocked;
            return;
        }

        isCursorLocked = GameplayCursorLock.IsLocked;
    }

    void HandleMouseLook()
    {
        if (suppressMouseLook) return;

        float mouseX;
        float mouseY;
        if (vehicleDrivingDecoupledFromPlayer && matchPlayerMovementMouseScalingWhileVehicleDecoupled)
        {
            float s = pairedRotationSpeed * Mathf.Max(Time.deltaTime, 0.0001f);
            mouseX = Input.GetAxis("Mouse X") * s;
            mouseY = Input.GetAxis("Mouse Y") * s;
        }
        else
        {
            mouseX = Input.GetAxis("Mouse X") * horizontalSensitivity;
            mouseY = Input.GetAxis("Mouse Y") * verticalSensitivity;
        }

        if (vehicleDrivingDecoupledFromPlayer)
        {
            if (cameraHolder != null)
            {
                Vector3 axis = vehicleYawAxisReference != null
                    ? vehicleYawAxisReference.up
                    : Vector3.up;
                if (axis.sqrMagnitude < 1e-8f)
                    axis = Vector3.up;
                axis.Normalize();
                cameraHolder.Rotate(axis, mouseX, Space.World);
            }
        }
        else if (ChairSitState.IsSitting)
        {
            // 坐下时只抬/低头，不旋转玩家身体（避免人物在椅子上滑动）。
        }
        else if (!applyMouseYawToPlayerBody)
        {
            if (cameraHolder != null)
                cameraHolder.Rotate(0f, mouseX, 0f, Space.Self);
        }
        else if (playerTransform != null)
            playerTransform.Rotate(Vector3.up * mouseX);
        else if (cameraHolder != null)
            cameraHolder.Rotate(0f, mouseX, 0f, Space.Self);

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, maxLookDownAngle, maxLookUpAngle);
        transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
    }

    void SanitizeWalkingRigHierarchy()
    {
        if (playerObject != null)
            playerTransform = playerObject.transform;
        if (cameraHolder == null || playerTransform == null)
            return;

        if (cameraHolder.parent != playerTransform)
            cameraHolder.SetParent(playerTransform, false);

        if (cameraHolder.localScale.sqrMagnitude < 0.0001f
            || Mathf.Abs(cameraHolder.localScale.x - 1f) > 0.01f
            || Mathf.Abs(cameraHolder.localScale.y - 1f) > 0.01f
            || Mathf.Abs(cameraHolder.localScale.z - 1f) > 0.01f)
        {
            cameraHolder.localScale = Vector3.one;
        }

        Vector3 holderLocal = cameraHolder.localPosition;
        if (holderLocal.sqrMagnitude > 9f || holderLocal.y < 0.5f || holderLocal.y > 4f)
            ApplyWalkingEyeRigFromCharacter();
        else
            transform.localPosition = Vector3.zero;
    }

    void ApplyWalkingEyeRigFromCharacter()
    {
        if (cameraHolder == null || playerTransform == null)
            return;

        _standingEyeLocalPos = ResolveStandingEyeLocalPosition(1.8f);
        var crouchEye = ResolveStandingEyeLocalPosition(1f);
        ApplyEyeLocalPosition(_standingEyeLocalPos, crouchEye, _crouchEyeBlend, _crouchCameraHeightRatio);
        transform.localPosition = Vector3.zero;
    }

    /// <summary>由 PlayerMovement 在蹲下时调用，平滑降低第一人称视点。</summary>
    public void ApplyCrouchEyeBlend(
        float blend01,
        float standControllerHeight,
        float crouchControllerHeight,
        float heightRatio = 1f)
    {
        if (cameraHolder == null || playerTransform == null)
            return;
        if (IsPreservingRestoredRig)
            return;

        _crouchEyeBlend = Mathf.Clamp01(blend01);
        _crouchCameraHeightRatio = Mathf.Max(0.1f, heightRatio);

        float standH = standControllerHeight > 0.01f ? standControllerHeight : 1.8f;
        float crouchH = crouchControllerHeight > 0.01f ? crouchControllerHeight : 1f;
        var standEye = ResolveStandingEyeLocalPosition(standH);
        var crouchEye = ResolveStandingEyeLocalPosition(crouchH);
        ApplyEyeLocalPosition(standEye, crouchEye, _crouchEyeBlend, _crouchCameraHeightRatio);
    }

    Vector3 ResolveStandingEyeLocalPosition(float controllerHeight)
    {
        if (PlayerCharacterCameraUtility.TryGetEyeLocalPositionOnPlayer(
                playerTransform,
                controllerHeight,
                out Vector3 localEye,
                eyeForwardOffsetLocal))
        {
            return localEye;
        }

        float forward = eyeForwardOffsetLocal > 0f
            ? eyeForwardOffsetLocal
            : PlayerCharacterCameraUtility.DefaultEyeForwardOffsetLocal;
        return new Vector3(0f, controllerHeight > 0.01f ? controllerHeight * 0.9f : 1.8f, forward);
    }

    void ApplyEyeLocalPosition(Vector3 standEyeLocal, Vector3 crouchEyeLocal, float crouchBlend01, float heightRatio)
    {
        float t = Mathf.Clamp01(crouchBlend01) * Mathf.Clamp01(heightRatio);
        cameraHolder.localPosition = Vector3.Lerp(standEyeLocal, crouchEyeLocal, t);
    }

    void AdoptCurrentViewAsWalkingLookState()
    {
        if (vehicleDrivingDecoupledFromPlayer || !applyMouseYawToPlayerBody)
        {
            SyncPitchFromTransform();
            return;
        }

        Vector3 look = transform.forward;
        if (look.sqrMagnitude < 1e-8f)
        {
            SyncPitchFromTransform();
            return;
        }

        ApplyWalkingLookFromWorldDirection(look);
    }

    void SyncPitchFromTransform()
    {
        float pitch = transform.localEulerAngles.x;
        if (pitch > 180f)
            pitch -= 360f;
        xRotation = pitch;
    }

    void WarmupMouseLookInput()
    {
        _mouseLookWarmupFrames = Mathf.Max(_mouseLookWarmupFrames, 2);
        ConsumeMouseLookAxes();
    }

    static void ConsumeMouseLookAxes()
    {
        Input.GetAxis("Mouse X");
        Input.GetAxis("Mouse Y");
    }

    void UpdateCursorState()
    {
        CursorLockMode targetLock = isCursorLocked ? CursorLockMode.Locked : CursorLockMode.None;
        bool targetVisible = !isCursorLocked;
        if (Cursor.lockState == targetLock && Cursor.visible == targetVisible)
            return;

        Cursor.lockState = targetLock;
        Cursor.visible = targetVisible;
    }
}
