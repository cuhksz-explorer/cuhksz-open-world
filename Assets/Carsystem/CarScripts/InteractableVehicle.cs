using System.Collections;
using System.Collections.Generic;
using Mirror;
using TMPro;
using UnityEngine;

public class InteractableVehicle : MonoBehaviour
{
    [Header("车辆组件引用")]
    public Carbilili carController;
    public CarCamera carCamera;
    public Rigidbody carRigidbody;
    public CarSystemVehicleHUD vehicleHud;

    [Header("交互")]
    public string vehicleDisplayName = "载具";
    public KeyCode interactKey = KeyCode.F;
    public KeyCode exitKey = KeyCode.Escape;
    public const float DefaultInteractDistance = 7f;
    public const float DefaultDistanceBuffer = 1.5f;

    [Tooltip("水平距离 ≤ interactDistance + distanceBuffer 时可显示提示并按 F 上车（默认约 8.5m）")]
    public float interactDistance = DefaultInteractDistance;
    public float distanceBuffer = DefaultDistanceBuffer;
    public int interactPriority = 50;

    [Header("状态")]
    public bool isOccupied;

    static int _carSystemDrivingCount;
    /// <summary>本地玩家是否正在驾驶 Carsystem 车辆（供 CameraSwitcher 等屏蔽 V 切换）。</summary>
    public static bool IsAnyCarSystemVehicleDriven => _carSystemDrivingCount > 0;

    PlayerMovement currentPlayer;
    PlayerMovement cachedPlayer;
    Camera previousMainCamera;
    CarSystemVehicleNetworkProxy networkProxy;
    CarSystemVehicleFlipRescue flipRescue;
    VehicleHUD legacyHud;
    float nextEnterAllowedTime;
    bool waitingDriveAuthority;
    TextMeshProUGUI sharedInteractNameTmp;
    TextMeshProUGUI sharedInteractTypeTmp;
    int sharedInteractOfferGroupId;

    // 上车时只隐藏角色“模型/渲染”，保留根物体与 Transform 供任务/小地图等系统继续更新。
    Transform _playerOriginalParent;
    bool _playerOriginalActiveSelf;
    CharacterController _playerCharacterController;
    bool _playerCharacterControllerEnabled;
    Renderer[] _playerRenderers;
    bool[] _playerRendererEnabled;
    Animator[] _playerAnimators;
    bool[] _playerAnimatorEnabled;
    CameraSwitcher _sceneCameraSwitcher;
    bool _preDriveWasFirstPerson;

    void Awake()
    {
        isOccupied = false;

        if (carController == null)
            carController = GetComponent<Carbilili>();
        if (carCamera == null)
            carCamera = GetComponentInChildren<CarCamera>(true);
        if (carRigidbody == null)
            carRigidbody = GetComponent<Rigidbody>();
        if (networkProxy == null)
            networkProxy = GetComponent<CarSystemVehicleNetworkProxy>();
        if (flipRescue == null)
            flipRescue = GetComponent<CarSystemVehicleFlipRescue>();

        if (carController != null)
        {
            carController.SetLocalInputEnabled(false);
            carController.enabled = false;
        }

        if (carCamera != null)
        {
            carCamera.enabled = false;
            if (carCamera.cam == null)
                carCamera.cam = carCamera.GetComponent<Camera>();
            if (carCamera.cam != null)
                carCamera.cam.enabled = false;
        }

        ResolveLegacyHud();
        ResolveSharedInteractPromptRefs();
    }

    void Update()
    {
        bool occupiedByOther = networkProxy != null && networkProxy.IsOccupiedByOther();

        if (isOccupied)
        {
            UpdateLegacyHudSpeed();

            if (Input.GetKeyDown(exitKey))
                ExitVehicle();
            return;
        }

        if (occupiedByOther || Time.time < nextEnterAllowedTime)
            return;

        PlayerMovement player = GetActivePlayer();
        if (player == null || !player.gameObject.activeInHierarchy)
            return;

        float distance = Vector3.Distance(player.transform.position, transform.position);
        bool inRange = distance <= interactDistance + distanceBuffer;
        if (!inRange)
            return;

        if (UIInteractionState.BlocksWorldPrompt)
            return;

        if (sharedInteractOfferGroupId != 0)
        {
            InteractPromptSharedLabelFrame.Offer(
                sharedInteractOfferGroupId,
                distance,
                vehicleDisplayName,
                FormatEnterTypeLine(),
                transform.position);
        }
        else
        {
            ResolveLegacyHud();
            if (legacyHud != null)
                legacyHud.OfferPrompt(distance, vehicleDisplayName, FormatEnterTypeLine());
        }

        if (GameplayUiHotkeyGuard.ShouldBlockGameplayHotkeys()
            || GameplayUiHotkeyGuard.BlocksGameplayBecauseMultiplayerUi())
            return;

        if (Input.GetKeyDown(interactKey))
        {
            float claimDistance = distance;
            InteractionNearestTriggerGate.Submit(interactKey, claimDistance, GetInstanceID(), () =>
            {
                if (!this || !enabled || isOccupied)
                    return;
                if (networkProxy != null && networkProxy.IsOccupiedByOther())
                    return;

                PlayerMovement latestPlayer = GetActivePlayer();
                if (latestPlayer == null || !latestPlayer.gameObject.activeInHierarchy)
                    return;

                float latestDistance = Vector3.Distance(latestPlayer.transform.position, transform.position);
                if (latestDistance > interactDistance + distanceBuffer)
                    return;

                Interact(latestPlayer);
            }, interactPriority);
        }
    }

    PlayerMovement GetActivePlayer()
    {
        if (cachedPlayer != null)
            return cachedPlayer;
        cachedPlayer = FindObjectOfType<PlayerMovement>(true);
        return cachedPlayer;
    }

    string FormatEnterTypeLine() => $"[{interactKey}] 上车";

    string BuildDrivingControlsText()
    {
        return
            "<b>操控方式</b>\n\n" +
            "W  —  前进\n" +
            "S  —  刹车 / 倒车\n" +
            "A  —  左转\n" +
            "D  —  右转\n" +
            $"{exitKey} — 下车\n" +
            "F2 — 翻车自救";
    }

    public void Interact(PlayerMovement player)
    {
        if (isOccupied || player == null)
            return;

        currentPlayer = player;
        cachedPlayer = player;

        if (networkProxy != null)
        {
            networkProxy.TryBeginDrive();
            if (NetworkClient.active && !networkProxy.CanLocalSimulateVehicle())
            {
                if (!waitingDriveAuthority)
                    StartCoroutine(CoWaitDriveAuthorityThenEnter());
                return;
            }
        }

        EnterVehicleInternal();
    }

    IEnumerator CoWaitDriveAuthorityThenEnter()
    {
        waitingDriveAuthority = true;
        float deadline = Time.realtimeSinceStartup + 1.5f;
        while (Time.realtimeSinceStartup < deadline)
        {
            if (isOccupied)
            {
                waitingDriveAuthority = false;
                yield break;
            }
            if (networkProxy == null || !NetworkClient.active)
                break;
            if (networkProxy.CanLocalSimulateVehicle())
            {
                waitingDriveAuthority = false;
                EnterVehicleInternal();
                yield break;
            }
            yield return null;
        }

        waitingDriveAuthority = false;
        Debug.LogWarning("[InteractableVehicle] 等待载具驾驶权限超时，已取消本次上车。");
    }

    void EnterVehicleInternal()
    {
        if (isOccupied || currentPlayer == null)
            return;

        isOccupied = true;
        _carSystemDrivingCount++;
        UIInteractionState.SetNpcShopOpen(false);
        UIInteractionState.SetDrivingHudActive(true);

        if (_sceneCameraSwitcher == null)
            _sceneCameraSwitcher = FindObjectOfType<CameraSwitcher>(true);
        if (_sceneCameraSwitcher != null)
        {
            _preDriveWasFirstPerson = _sceneCameraSwitcher.IsFirstPersonActive;
            _sceneCameraSwitcher.SuppressWalkingFirstPersonForVehicle();
        }

        currentPlayer.isMovementPaused = true;
        CacheAndHidePlayerModel();
        ParentPlayerToVehicleAndKeepPose();

        if (carRigidbody != null)
            carRigidbody.WakeUp();

        if (carController != null)
        {
            carController.enabled = true;
            carController.SetLocalInputEnabled(true);
        }

        SwitchToVehicleCamera();
        ApplyTargetGuideVehicleCamera();

        if (flipRescue == null)
            flipRescue = GetComponent<CarSystemVehicleFlipRescue>();
        if (flipRescue != null)
            flipRescue.CaptureSnapshotNow();

        ResolveLegacyHud();
        if (legacyHud != null)
        {
            legacyHud.EnterDriving();
            legacyHud.UpdateControlsContent(BuildDrivingControlsText());
            UpdateLegacyHudSpeed();
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void ExitVehicle()
    {
        if (!isOccupied || currentPlayer == null)
            return;

        if (networkProxy != null)
            networkProxy.TryEndDrive();

        if (carController != null)
        {
            carController.SetLocalInputEnabled(false, true);
            carController.enabled = false;
        }

        if (carRigidbody != null)
        {
            carRigidbody.velocity = Vector3.zero;
            carRigidbody.angularVelocity = Vector3.zero;
        }

        ClearTargetGuideVehicleCamera();
        SwitchBackToPlayerCamera();

        Vector3 exitSide = transform.position + transform.right * -2.5f;
        Vector3 rayOrigin = exitSide + Vector3.up * 5f;
        float exitY = transform.position.y + 0.1f;

        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 15f))
            exitY = hit.point.y + 0.1f;

        Vector3 exitPos = new Vector3(exitSide.x, exitY, exitSide.z);

        RestorePlayerModelAndInput();
        UnparentPlayerFromVehicle();
        currentPlayer.Teleport(exitPos);
        currentPlayer.transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        currentPlayer.isMovementPaused = false;

        if (_carSystemDrivingCount > 0)
            _carSystemDrivingCount--;

        if (_sceneCameraSwitcher != null)
            _sceneCameraSwitcher.RestoreWalkingViewAfterVehicle(_preDriveWasFirstPerson);

        UIInteractionState.SetDrivingHudActive(false);
        if (legacyHud != null)
            legacyHud.ExitDriving();

        isOccupied = false;
        waitingDriveAuthority = false;
        nextEnterAllowedTime = Time.time + 0.25f;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        currentPlayer = null;
    }

    void CacheAndHidePlayerModel()
    {
        if (currentPlayer == null)
            return;

        _playerOriginalActiveSelf = currentPlayer.gameObject.activeSelf;
        _playerOriginalParent = currentPlayer.transform.parent;

        // 禁用角色控制，避免被车辆跟随/物理影响到
        _playerCharacterController = currentPlayer.GetComponent<CharacterController>();
        if (_playerCharacterController != null)
        {
            _playerCharacterControllerEnabled = _playerCharacterController.enabled;
            _playerCharacterController.enabled = false;
        }

        // 只隐藏渲染与动画，保留根 Transform 供 UI/任务系统持续追踪
        _playerRenderers = currentPlayer.GetComponentsInChildren<Renderer>(true);
        _playerRendererEnabled = new bool[_playerRenderers.Length];
        for (int i = 0; i < _playerRenderers.Length; i++)
        {
            var r = _playerRenderers[i];
            if (r == null) continue;
            _playerRendererEnabled[i] = r.enabled;
            r.enabled = false;
        }

        _playerAnimators = currentPlayer.GetComponentsInChildren<Animator>(true);
        _playerAnimatorEnabled = new bool[_playerAnimators.Length];
        for (int i = 0; i < _playerAnimators.Length; i++)
        {
            var a = _playerAnimators[i];
            if (a == null) continue;
            _playerAnimatorEnabled[i] = a.enabled;
            a.enabled = false;
        }
    }

    void ParentPlayerToVehicleAndKeepPose()
    {
        if (currentPlayer == null)
            return;

        // worldPositionStays=true：上车瞬间不跳位置；随后车辆移动时，角色 Transform 会一起跟随。
        currentPlayer.transform.SetParent(transform, worldPositionStays: true);
    }

    void RestorePlayerModelAndInput()
    {
        if (currentPlayer == null)
            return;

        if (_playerCharacterController != null)
            _playerCharacterController.enabled = _playerCharacterControllerEnabled;

        // 恢复渲染/动画
        if (_playerRenderers != null && _playerRendererEnabled != null)
        {
            for (int i = 0; i < _playerRenderers.Length; i++)
            {
                var r = _playerRenderers[i];
                if (r == null) continue;
                bool en = (i < _playerRendererEnabled.Length) ? _playerRendererEnabled[i] : true;
                r.enabled = en;
            }
        }

        if (_playerAnimators != null && _playerAnimatorEnabled != null)
        {
            for (int i = 0; i < _playerAnimators.Length; i++)
            {
                var a = _playerAnimators[i];
                if (a == null) continue;
                bool en = (i < _playerAnimatorEnabled.Length) ? _playerAnimatorEnabled[i] : true;
                a.enabled = en;
            }
        }

        if (!currentPlayer.gameObject.activeSelf && _playerOriginalActiveSelf)
            currentPlayer.gameObject.SetActive(true);
    }

    void UnparentPlayerFromVehicle()
    {
        if (currentPlayer == null)
            return;
        if (_playerOriginalParent == null)
            currentPlayer.transform.SetParent(null, worldPositionStays: true);
        else
            currentPlayer.transform.SetParent(_playerOriginalParent, worldPositionStays: true);
    }

    public void ApplyRemoteDriveState(Vector3 linearVelocity, Vector3 angularVelocity)
    {
        if (networkProxy != null && networkProxy.IsLocalDriver)
            return;

        if (carController != null)
        {
            carController.SetLocalInputEnabled(false);
            carController.enabled = false;
            carController.ApplyRemoteState(linearVelocity, angularVelocity);
        }
        else if (carRigidbody != null)
        {
            carRigidbody.velocity = linearVelocity;
            carRigidbody.angularVelocity = angularVelocity;
        }
    }

    void SwitchToVehicleCamera()
    {
        if (carCamera == null)
            return;

        if (carCamera.cam == null)
            carCamera.cam = carCamera.GetComponent<Camera>();
        if (carCamera.cam == null)
            carCamera.cam = carCamera.GetComponentInChildren<Camera>(true);
        if (carCamera.cam == null)
            return;

        previousMainCamera = Camera.main;
        if (previousMainCamera != null && previousMainCamera != carCamera.cam)
            previousMainCamera.enabled = false;

        carCamera.enabled = true;
        carCamera.cam.enabled = true;
    }

    void ApplyTargetGuideVehicleCamera()
    {
        Camera guideCam = ResolveVehicleGuideCamera();
        if (guideCam != null)
            TargetGuideUI.SetVehicleCameraOverride(guideCam);
    }

    void ClearTargetGuideVehicleCamera()
    {
        TargetGuideUI.ClearVehicleCameraOverride(ResolveVehicleGuideCamera());
    }

    Camera ResolveVehicleGuideCamera()
    {
        if (carCamera == null)
            return null;
        if (carCamera.cam == null)
            carCamera.cam = carCamera.GetComponent<Camera>();
        if (carCamera.cam == null)
            carCamera.cam = carCamera.GetComponentInChildren<Camera>(true);
        return carCamera.cam;
    }

    void OnDisable()
    {
        if (isOccupied)
            ClearTargetGuideVehicleCamera();
    }

    void SwitchBackToPlayerCamera()
    {
        if (carCamera != null)
        {
            if (carCamera.cam != null)
                carCamera.cam.enabled = false;
            carCamera.enabled = false;
        }

        if (previousMainCamera != null)
            previousMainCamera.enabled = true;
    }

    void ResolveLegacyHud()
    {
        if (legacyHud != null)
            return;

        legacyHud = FindObjectOfType<VehicleHUD>(true);
        if (legacyHud == null)
        {
            var go = new GameObject("VehicleHUD");
            legacyHud = go.AddComponent<VehicleHUD>();
        }
    }

    void ResolveSharedInteractPromptRefs()
    {
        if (sharedInteractOfferGroupId != 0)
            return;

        InteractPromptSharedUiLocator.TryFindShared(out _, out sharedInteractNameTmp, out sharedInteractTypeTmp);
        sharedInteractOfferGroupId = InteractPromptSharedLabelFrame.ResolveOfferGroupId(sharedInteractNameTmp, sharedInteractTypeTmp);
    }

    void UpdateLegacyHudSpeed()
    {
        if (!isOccupied || legacyHud == null || carRigidbody == null)
            return;

        Vector3 flat = carRigidbody.velocity;
        flat.y = 0f;
        legacyHud.UpdateSpeed(flat.magnitude * 3.6f);
    }
}