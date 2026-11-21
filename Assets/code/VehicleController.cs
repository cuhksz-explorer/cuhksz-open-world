using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Multiplayer;
using Mirror;

/// <summary>
/// 载具：W/S/A/D 驾驶；驾驶中仅使用 <see cref="drivingThirdPersonCamera"/> 对齐 <see cref="thirdPersonViewMount"/>。
/// 上下车时的走路视角由 <see cref="CameraSwitcher"/> 负责（若存在）；本类不引用第一人称相机脚本。
/// </summary>
[DefaultExecutionOrder(500)]
public class VehicleController : MonoBehaviour
{
    [Header("载具运动")]
    public float acceleration = 6f;
    public float brakeForce = 10f;
    [Tooltip("松开 W/S 时，目标速度向 0 靠拢的速率（越大停车越快）")]
    public float coastStopDeceleration = 18f;
    [Tooltip("倒车时油门加速度（通常小于 acceleration，倒车更慢）")]
    public float reverseAcceleration = 2.5f;
    [Tooltip("前进中按住 S 的刹车强度（越大刹停越快）")]
    public float forwardBrakeDeceleration = 32f;
    public float steerSpeed = 55f;
    public float velocityAlignAcceleration = 28f;

    [Header("正面碰撞")]
    [Tooltip("与障碍物法线夹角：车头正面撞上时法线大致与车头相反，点积为负；绝对值越大越算正面")]
    [Range(-1f, 0f)]
    public float frontCollisionNormalDotMax = -0.35f;
    [Tooltip("仅当沿车头方向速度超过此值时才触发正面急停，避免轻微刮擦清零")]
    public float frontCollisionMinForwardSpeed = 0.8f;
    [Tooltip("正面碰撞时是否把水平速度立刻清零（否则仅清标量速度，由 Align 慢慢收）")]
    public bool frontCollisionZeroHorizontalVelocity = true;

    [Header("防车头穿模（CharacterController 常比模型小，需额外探测）")]
    [Tooltip("车头附近的世界探测起点（本地空间，+Z 一般为车头方向）")]
    public Vector3 frontProbeLocalPosition = new Vector3(0f, 0.5f, 2.2f);
    [Tooltip("倒车时从车尾附近探测（本地 -Z）")]
    public Vector3 rearProbeLocalPosition = new Vector3(0f, 0.5f, -1.5f);
    [Tooltip("探测球半径，略大更易提前挡住穿模")]
    public float wallProbeRadius = 0.35f;
    [Tooltip("碰到墙时保留的间隙，避免贴脸抖动")]
    public float wallProbeSkin = 0.06f;
    [Tooltip("参与探测的层；默认全层，若误撞自己可调掉车身所在层")]
    public LayerMask wallProbeLayers = ~0;

    [Header("顶墙时速度与仪表")]
    [Tooltip("车头前方有此距离内的障碍时，不按 W 继续拉高 _currentSpeed（仪表与目标速度一致）")]
    public float forwardBlockedProbeDistance = 0.55f;
    [Tooltip("本帧沿车头预期前进速度与实际位移比值低于此且按住 W 时，认为顶墙并清零车头方向速度分量")]
    [Range(0.05f, 0.9f)]
    public float stuckAgainstWallMoveFraction = 0.22f;
    [Tooltip("参与顶墙位移判断的最小预期前进速度（m/s），避免极低速误判")]
    public float stuckAgainstWallMinIntendedForward = 0.35f;
    [Tooltip("满足顶墙判定后需持续这么久才清零速度，避免瞬时误判。")]
    public float stuckResetGraceSeconds = 0.18f;

    [Header("速度表（HUD）")]
    [Tooltip("示数 =（水平速度换算的 km/h）× 本系数。表显比体感偏快时调小（如 0.8）；偏慢则调大；1 为不修正。")]
    [Min(0.001f)]
    public float speedometerDisplayScale = 1f;

    [Header("转向平滑")]
    [Tooltip("转向输入从 0→±1 的平滑速率（越大越灵敏，越小越丝滑；推荐 5~10）")]
    public float steerSmoothSpeed = 7f;
    [Tooltip("超过此速度（m/s）后转向逐渐变迟缓，模拟高速转向阻力；设 0 则不限速")]
    public float steerSpeedDampingThreshold = 9f;
    [Tooltip("高速时转向保留的最低比例（0 = 完全锁死，1 = 不减速）")]
    [Range(0f, 1f)]
    public float steerMinFraction = 0.4f;
    [Tooltip("车速低于此值（m/s）时禁止转向，避免静止漂移")]
    public float steerMinSpeedRequired = 0.3f;

    [Header("驾驶逻辑融合（兼容旧版）")]
    [Tooltip("启用后保持旧版逻辑：仅在 W/S 油门输入时允许转向。关闭后使用新版平滑转向+高速阻尼。")]
    public bool useLegacySteerWhenThrottleOnly = true;

    [Header("脱离卡点 / 救援")]
    [Tooltip("按住此键达到确认时间后，将车辆传送回出生位置（默认 R）")]
    public KeyCode rescueKey = KeyCode.R;
    [Tooltip("触发救援需持续按住的时间（秒）；设 0 则即按即触发")]
    public float rescueHoldSeconds = 1.5f;
    [Tooltip("救援目标点；留空则使用载具在 Start() 时记录的初始位置/朝向")]
    public Transform spawnPoint;
    [Tooltip("是否启用长按救援功能（兼容旧版手感时可关闭）。")]
    public bool enableRescueFeature = true;

    [Header("载具相机 — 请手动绑定")]
    [Tooltip("驾驶中使用的第三人称 Camera（可留空，会尝试用 CameraSwitcher 的第三人称）")]
    public Camera drivingThirdPersonCamera;

    [Tooltip("第三人称相机挂载点（推荐为车身子物体）")]
    public Transform thirdPersonViewMount;

    [Tooltip("走路视角切换器；用于上车时关第一人称、下车时恢复上车前的人称")]
    public CameraSwitcher sceneCameraSwitcher;

    [Tooltip("第三人称上的 WideViewCamera 等，驾驶时会暂时关闭，避免与车位移冲突")]
    public bool disableThirdPersonFollowScriptsWhileDriving = true;

    [Header("隐藏玩家")]
    public Vector3 playerHiddenLocalPosition = new Vector3(0f, -500f, 0f);
    public Transform playerVisualRoot;

    [Header("下车")]
    public Transform exitPoint;
    public Vector3 defaultExitOffset = new Vector3(-2f, 0.5f, 0f);

    [Header("玩家引用")]
    public PlayerMovement playerMovement;
    public Transform playerModelTransform;
    public Animator playerAnimator;

    [Header("交互")]
    public float interactDistance = 4f;
    public float distanceBuffer = 0.5f;
    public string vehicleDisplayName = "载具";
    public string interactionTypeName = "上车";
    public KeyCode enterKey = KeyCode.F;
    [Tooltip("为 true 时靠近提示走共用 TMP（与对话同一套字体与位置）；未绑定下方字段且允许自动查找失败时再用车载 HUD 自带提示")]
    public bool useSharedInteractPrompt = true;

    [Tooltip("对话面板上的 InteractNameText（或等价 TMP）；与 CanvasInteractionUIManager 绑定的应是同一引用")]
    public TextMeshProUGUI sharedInteractNameText;

    [Tooltip("对话面板上的 InteractTypeText（显示 [F] 上车 等）")]
    public TextMeshProUGUI sharedInteractTypeText;

    [Tooltip("仅当上面名字/类型 TMP 有任一留空时，按场景物体名 InteractPrompt_Shared 尝试补齐；全手动绑定时可关掉避免查找")]
    public bool autoFindSharedInteractPromptIfUnassigned = true;

    [Header("场景可见性")]
    [Tooltip("若勾选：进场景时不会自动把本车及未激活的父节点设为激活。默认不勾选——解决车在 Hierarchy 里为灰色（整物体关闭）导致看不见且脚本不运行。")]
    [SerializeField] bool skipAutoActivateHierarchyOnSceneLoad;

    float _currentSpeed;
    float _verticalVel;
    bool _isDriving;

    float _driveYaw;
    float _initialEulerX;
    float _initialEulerZ;
    Vector3 _horizontalVelocity;

    CharacterController _cc;
    VehicleNetworkProxy _networkProxy;

    Transform _origPlayerParent;
    VehicleHUD _hud;

    bool _tpDrivingUseVehicleLocalOffset;
    Vector3 _tpDrivingOffsetLocal;
    Quaternion _tpDrivingRotLocal;

    Camera _tpCam;
    Transform _tpOrigParent;
    Vector3 _tpOrigLocalPos;
    Quaternion _tpOrigLocalRot;
    WideViewCamera _tpWideView;

    // thirdPersonViewMount 若不在车身子层级，上车时临时挂到车身下；下车时恢复原父级
    Transform _mountOrigParent;
    Vector3 _mountOrigLocalPos;
    Quaternion _mountOrigLocalRot;
    bool _mountWasReparented;
    bool _tpWideViewEnabledBeforeDrive;
    MonoBehaviour[] _tpExtraFollowScripts;
    bool[] _tpExtraWereEnabled;

    bool _preDriveWasFirstPerson;
    bool _waitingDriveAuthority;

    HealthBarUI _healthBarUi;
    StaminaBarUI _staminaBarUi;

    TextMeshProUGUI _sharedInteractNameTmp;
    TextMeshProUGUI _sharedInteractTypeTmp;
    int _sharedInteractOfferGroupId;

    readonly List<Renderer> _renderersBuf = new List<Renderer>(32);
    readonly List<bool> _rendererWasEnabled = new List<bool>(32);

    static readonly RaycastHit[] _wallProbeHits = new RaycastHit[16];
    float _stuckAgainstWallTimer;
    float _steerSmoothed;      // 平滑后的转向输入 [-1, 1]
    float _rescueHoldTimer;    // 救援按键持续时间
    bool  _prevDrivingForward; // 上一帧 W 键状态，方向切换时重置卡墙计时器
    Vector3    _spawnPosition;
    Quaternion _spawnRotation;

    public bool IsDriving => _isDriving;
    public float CurrentSpeedKmh
    {
        get
        {
            Vector3 h = _horizontalVelocity;
            h.y = 0f;
            return h.magnitude * 3.6f * speedometerDisplayScale;
        }
    }

    static int _drivingCount;
    static bool _warnedMissingCameraSwitcher;
    public static bool IsAnyVehicleDriven => _drivingCount > 0;

    static bool _sceneLoadHooked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void RegisterVehicleSceneVisibilityHook()
    {
        if (_sceneLoadHooked) return;
        _sceneLoadHooked = true;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoadedEnsureVehiclesActive;
    }

    static void OnSceneLoadedEnsureVehiclesActive(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        ActivateInactiveHierarchyForVehiclesInScene(scene);
        VehicleActivateNextFrameHost.Schedule(() => ActivateInactiveHierarchyForVehiclesInScene(scene));
    }

    /// <summary>
    /// 遍历当前已加载的所有场景，激活其中带 VehicleController 的物体及其未激活父链。
    /// </summary>
    public static void ActivateInactiveHierarchyForAllVehiclesImmediate()
    {
        for (int si = 0; si < UnityEngine.SceneManagement.SceneManager.sceneCount; si++)
            ActivateInactiveHierarchyForVehiclesInScene(UnityEngine.SceneManagement.SceneManager.GetSceneAt(si));
    }

    /// <summary>
    /// 仅处理指定场景：激活带 VehicleController 的物体及其向上未激活的父链，使 Awake/Start 能跑、Hierarchy 不再整棵灰色。
    /// </summary>
    public static void ActivateInactiveHierarchyForVehiclesInScene(UnityEngine.SceneManagement.Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded) return;
        var roots = scene.GetRootGameObjects();
        for (int r = 0; r < roots.Length; r++)
        {
            var vcs = roots[r].GetComponentsInChildren<VehicleController>(true);
            for (int i = 0; i < vcs.Length; i++)
            {
                var vc = vcs[i];
                if (vc == null || vc.skipAutoActivateHierarchyOnSceneLoad) continue;
                ActivateInactiveAncestorsAndSelf(vc.transform);
            }
        }
    }

    static void ActivateInactiveAncestorsAndSelf(Transform leaf)
    {
        if (leaf == null) return;
        var chain = new List<GameObject>(8);
        Transform t = leaf;
        while (t != null)
        {
            if (!t.gameObject.activeSelf)
                chain.Add(t.gameObject);
            t = t.parent;
        }
        for (int i = chain.Count - 1; i >= 0; i--)
            chain[i].SetActive(true);
    }

    void Awake()
    {
        EnsureCharacterControllerOnThisObject();
        _networkProxy = GetComponent<VehicleNetworkProxy>();
    }

    void EnsureCharacterControllerOnThisObject()
    {
        _cc = GetComponent<CharacterController>();
        if (_cc == null)
        {
            _cc = gameObject.AddComponent<CharacterController>();
            var renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds b = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                    b.Encapsulate(renderers[i].bounds);

                _cc.center = transform.InverseTransformPoint(b.center);
                _cc.height = Mathf.Max(b.size.y, 0.5f);
                float minXZ = Mathf.Min(b.size.x, b.size.z);
                _cc.radius = Mathf.Clamp(minXZ * 0.42f, 0.25f, minXZ * 0.48f);
            }
            else
            {
                _cc.height = 1.5f;
                _cc.radius = 1f;
                _cc.center = new Vector3(0, 0.75f, 0);
            }

            _cc.slopeLimit = 45f;
            _cc.stepOffset = 0.4f;
            _cc.skinWidth = 0.08f;
        }
    }

    void OnEnable() => Loc.OnLanguageChanged += OnLanguageChanged;

    void OnDisable() => Loc.OnLanguageChanged -= OnLanguageChanged;

    void Start()
    {
        if (!playerMovement)
            playerMovement = FindObjectOfType<PlayerMovement>(true);
        if (playerMovement && !playerModelTransform)
            playerModelTransform = playerMovement.transform;
        if (playerMovement)
            _playerCC = playerMovement.GetComponent<CharacterController>();

        _hud = FindObjectOfType<VehicleHUD>(true);
        if (!_hud)
        {
            var go = new GameObject("VehicleHUD");
            _hud = go.AddComponent<VehicleHUD>();
        }

        ResolveSharedInteractPromptRefs();

        if (!sceneCameraSwitcher)
            sceneCameraSwitcher = FindObjectOfType<CameraSwitcher>(true);

        _healthBarUi = FindObjectOfType<HealthBarUI>(true);
        _staminaBarUi = FindObjectOfType<StaminaBarUI>(true);

        // 进场景后车身网格仍应可见：纠正误挂到车身下的玩家根节点，并恢复被误关的车身 Renderer
        RecoverPlayerRootIfWronglyParentedToVehicle();
        RestoreVehicleBodyRenderersAfterLoad();

        // 记录初始出生位置/朝向，供救援功能使用
        if (spawnPoint != null)
        {
            _spawnPosition = spawnPoint.position;
            _spawnRotation = spawnPoint.rotation;
        }
        else
        {
            _spawnPosition = transform.position;
            _spawnRotation = transform.rotation;
        }
    }

    void SetWalkingCombatBarsVisible(bool visible)
    {
        if (_healthBarUi != null)
            _healthBarUi.SetBarVisible(visible);
        if (_staminaBarUi != null)
            _staminaBarUi.SetBarVisible(visible);
    }

    CharacterController _playerCC;

    void ResolveSharedInteractPromptRefs()
    {
        _sharedInteractNameTmp = null;
        _sharedInteractTypeTmp = null;
        _sharedInteractOfferGroupId = 0;
        if (!useSharedInteractPrompt)
            return;

        _sharedInteractNameTmp = sharedInteractNameText;
        _sharedInteractTypeTmp = sharedInteractTypeText;

        if (autoFindSharedInteractPromptIfUnassigned &&
            (_sharedInteractNameTmp == null || _sharedInteractTypeTmp == null))
        {
            RectTransform panelIgnored = null;
            InteractPromptSharedUiLocator.TryAutoBind(
                ref panelIgnored,
                ref _sharedInteractNameTmp,
                ref _sharedInteractTypeTmp);
        }

        if (_sharedInteractNameTmp != null)
            _sharedInteractOfferGroupId = _sharedInteractNameTmp.GetInstanceID();
        else if (_sharedInteractTypeTmp != null)
            _sharedInteractOfferGroupId = _sharedInteractTypeTmp.GetInstanceID();
    }

    void OnLanguageChanged()
    {
        if (_isDriving && _hud != null)
            _hud.UpdateControlsContent(BuildDrivingControlsText());
    }

    string GetVehicleDisplayName()
    {
        if (string.IsNullOrEmpty(vehicleDisplayName) || vehicleDisplayName == "载具")
            return Loc.Choose("载具", "Vehicle");
        return vehicleDisplayName;
    }

    string GetInteractionTypeName()
    {
        if (string.IsNullOrEmpty(interactionTypeName) || interactionTypeName == "上车")
            return Loc.Choose("上车", "Enter");
        return interactionTypeName;
    }

    string FormatVehicleInteractTypeLine()
    {
        string label = GetInteractionTypeName();
        if (string.IsNullOrEmpty(label))
            return "";
        return $"[{enterKey}] {label}";
    }

    void Update()
    {
        if (GameServerSync.BlocksInputForServerSpawn) return;

        if (_isDriving)
        {
            ProcessDriving();
            SyncThirdPersonVehicleCamera();
            if (_hud) _hud.UpdateSpeed(CurrentSpeedKmh);

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                InteractionEscGate.SuppressFpsEscForThisFrame();
                ExitVehicle();
            }
            return;
        }

        if (!playerModelTransform || IsAnyVehicleDriven) return;

        float dist = Vector3.Distance(playerModelTransform.position, transform.position);
        bool inRange = dist <= interactDistance + distanceBuffer;

        // 提示文案须独立于热键屏蔽：否则联机/商城等挡住 Gameplay 时整段 return，共用 TMP 永远没有 Offer，看起来像「门控/优先级弄没了提示」。
        if (inRange)
        {
            if (_sharedInteractOfferGroupId != 0)
                InteractPromptSharedLabelFrame.Offer(
                    _sharedInteractOfferGroupId,
                    dist,
                    GetVehicleDisplayName(),
                    FormatVehicleInteractTypeLine(),
                    transform.position);
            else if (_hud)
                _hud.OfferPrompt(dist, GetVehicleDisplayName(), FormatVehicleInteractTypeLine());
        }

        if (GameplayUiHotkeyGuard.ShouldBlockGameplayHotkeys()
            || GameplayUiHotkeyGuard.BlocksGameplayBecauseMultiplayerUi())
            return;

        if (inRange && Input.GetKeyDown(enterKey))
        {
            float claimDistance = dist;
            InteractionNearestTriggerGate.Submit(enterKey, claimDistance, GetInstanceID(), () =>
            {
                if (!this || !enabled || _isDriving || !playerModelTransform || IsAnyVehicleDriven)
                    return;
                float nowDist = Vector3.Distance(playerModelTransform.position, transform.position);
                if (nowDist > interactDistance + distanceBuffer)
                    return;
                if (_networkProxy != null && _networkProxy.IsOccupiedByOther())
                    return;
                EnterVehicle();
            }, priority: 50);
        }
    }

    void LateUpdate()
    {
        if (_isDriving)
        {
            SyncThirdPersonVehicleCamera();
            return;
        }

        // 无对话管理器时仍刷新共用提示；与 CanvasInteractionUIManager 同帧多次 Apply 有互斥保护
        if (useSharedInteractPrompt &&
            (_sharedInteractNameTmp != null || _sharedInteractTypeTmp != null))
            InteractPromptSharedLabelFrame.ApplyBestToSharedTmp(_sharedInteractNameTmp, _sharedInteractTypeTmp);
    }

    bool IsWalkingFirstPersonCameraObject(Camera c)
    {
        if (c == null || sceneCameraSwitcher == null || sceneCameraSwitcher.firstPersonCamera == null)
            return false;
        return c.transform == sceneCameraSwitcher.firstPersonCamera.transform;
    }

    void ResolveDrivingCameras()
    {
        if (!sceneCameraSwitcher)
            sceneCameraSwitcher = FindObjectOfType<CameraSwitcher>(true);

        if (!drivingThirdPersonCamera && sceneCameraSwitcher != null && sceneCameraSwitcher.thirdPersonCamera != null)
            drivingThirdPersonCamera = sceneCameraSwitcher.thirdPersonCamera;

        if (!drivingThirdPersonCamera)
        {
            foreach (var c in FindObjectsOfType<Camera>(true))
            {
                if (c == null) continue;
                if (IsWalkingFirstPersonCameraObject(c))
                    continue;
                drivingThirdPersonCamera = c;
                break;
            }
        }

        _tpCam = drivingThirdPersonCamera;
    }

    void EnterVehicle()
    {
        if (_networkProxy != null)
        {
            _networkProxy.TryBeginDrive();
            // 联机时等待 authority/驾驶者状态就绪，再进入驾驶态；否则本地能动但其他端不同步。
            if (NetworkClient.active && !_networkProxy.CanLocalSimulateVehicle())
            {
                if (!_waitingDriveAuthority)
                    StartCoroutine(CoWaitDriveAuthorityThenEnter());
                return;
            }
        }
        EnsureCharacterControllerOnThisObject();
        ResolveDrivingCameras();

        if (_tpCam == null || thirdPersonViewMount == null)
        {
            Debug.LogError("[VehicleController] 请绑定 drivingThirdPersonCamera（或可解析的第三人称 Camera）与 thirdPersonViewMount。");
            return;
        }

        _isDriving = true;
        _drivingCount++;
        _currentSpeed = 0f;
        _steerSmoothed = 0f;
        _rescueHoldTimer = 0f;
        _prevDrivingForward = false;

        Vector3 e = transform.eulerAngles;
        _initialEulerX = e.x;
        _initialEulerZ = e.z;
        _driveYaw = e.y;
        _horizontalVelocity = Vector3.zero;

        _preDriveWasFirstPerson = sceneCameraSwitcher == null || sceneCameraSwitcher.IsFirstPersonActive;

        _origPlayerParent = playerModelTransform.parent;

        if (playerMovement) playerMovement.enabled = false;
        if (_playerCC) _playerCC.enabled = false;
        if (playerAnimator) playerAnimator.enabled = false;

        SnapshotThirdPersonCameraForWalkingRestore();

        playerModelTransform.SetParent(transform);
        playerModelTransform.localPosition = playerHiddenLocalPosition;
        playerModelTransform.localRotation = Quaternion.identity;

        HidePlayerVisuals();

        CacheThirdPersonFollowScripts();
        WideViewBindVehicleTarget();
        EnsureMountIsUnderVehicle();
        ApplyDrivingThirdPersonCamera();

        if (sceneCameraSwitcher != null)
            sceneCameraSwitcher.SuppressWalkingFirstPersonForVehicle();

        SetWalkingCombatBarsVisible(false);

        if (_hud) _hud.EnterDriving();

        // 更新操控面板：包含救援键提示
        if (_hud != null)
        {
            _hud.UpdateControlsContent(BuildDrivingControlsText());
        }
    }

    string BuildDrivingControlsText()
    {
        string content =
            Loc.Choose("<b>操控方式</b>\n\n", "<b>Controls</b>\n\n") +
            Loc.Choose("W  —  前进\n", "W  —  Forward\n") +
            Loc.Choose("S  —  倒车\n", "S  —  Reverse\n") +
            Loc.Choose("A  —  左转\n", "A  —  Turn Left\n") +
            Loc.Choose("D  —  右转\n", "D  —  Turn Right\n") +
            Loc.Choose("Esc — 下车", "Esc — Exit");

        if (!enableRescueFeature)
            return content;

        string rescueLabel = rescueHoldSeconds > 0.1f
            ? Loc.F("长按 {0}（{1:F1}s）", "Hold {0} ({1:F1}s)", rescueKey, rescueHoldSeconds)
            : rescueKey.ToString();
        return content + "\n" + rescueLabel + Loc.Choose(" — 救援回出生点", " — Rescue to spawn");
    }

    System.Collections.IEnumerator CoWaitDriveAuthorityThenEnter()
    {
        _waitingDriveAuthority = true;
        float deadline = Time.realtimeSinceStartup + 1.5f;
        while (Time.realtimeSinceStartup < deadline)
        {
            if (_isDriving)
            {
                _waitingDriveAuthority = false;
                yield break;
            }
            if (_networkProxy == null || !NetworkClient.active)
                break;
            if (_networkProxy.CanLocalSimulateVehicle())
            {
                _waitingDriveAuthority = false;
                EnterVehicle();
                yield break;
            }
            yield return null;
        }
        _waitingDriveAuthority = false;
        Debug.LogWarning("[VehicleController] 等待载具驾驶权限超时，已取消本次上车。");
    }

    void WideViewBindVehicleTarget()
    {
        if (_tpWideView != null)
            _tpWideView.SetVehicleDrivingTarget(transform);
    }

    void WideViewClearVehicleTarget()
    {
        if (_tpWideView != null)
            _tpWideView.ClearVehicleDrivingTarget();
    }

    void SnapshotThirdPersonCameraForWalkingRestore()
    {
        if (!_tpCam) return;
        _tpOrigParent = _tpCam.transform.parent;
        _tpOrigLocalPos = _tpCam.transform.localPosition;
        _tpOrigLocalRot = _tpCam.transform.localRotation;
    }

    void CacheThirdPersonFollowScripts()
    {
        _tpWideView = null;
        if (!_tpCam) return;
        _tpWideView = _tpCam.GetComponent<WideViewCamera>();
        _tpWideViewEnabledBeforeDrive = _tpWideView != null && _tpWideView.enabled;

        var orbit = _tpCam.GetComponents<MonoBehaviour>();
        var list = new List<MonoBehaviour>();
        foreach (var m in orbit)
        {
            if (m == null) continue;
            if (m is WideViewCamera) continue;
            var n = m.GetType().Name;
            if (n.Contains("Follow") || n.Contains("Orbit") || n == "CameraFollow" || n == "HoverCamera")
                list.Add(m);
        }
        _tpExtraFollowScripts = list.ToArray();
        _tpExtraWereEnabled = new bool[_tpExtraFollowScripts.Length];
        for (int i = 0; i < _tpExtraFollowScripts.Length; i++)
            _tpExtraWereEnabled[i] = _tpExtraFollowScripts[i].enabled;
    }

    /// <summary>
    /// 若 thirdPersonViewMount 不在车身子层级，临时挂到车身下，使其跟车一起移动。
    /// </summary>
    void EnsureMountIsUnderVehicle()
    {
        _mountWasReparented = false;
        if (!thirdPersonViewMount) return;
        if (IsUnderTransform(thirdPersonViewMount, transform)) return;

        _mountOrigParent = thirdPersonViewMount.parent;
        _mountOrigLocalPos = thirdPersonViewMount.localPosition;
        _mountOrigLocalRot = thirdPersonViewMount.localRotation;

        thirdPersonViewMount.SetParent(transform, true);
        _mountWasReparented = true;
    }

    /// <summary>
    /// 下车时：把挂载点 Cube 的世界位置设为相机最后的位姿（这样第二次上车时 Cube 就在正确位置），然后恢复原父级。
    /// </summary>
    void RestoreMountAfterDrive()
    {
        if (!thirdPersonViewMount) return;

        // 先把 Cube 移到相机此刻的世界位置（下车前最后一帧 Sync 写入的位姿）
        if (_tpCam != null)
            thirdPersonViewMount.SetPositionAndRotation(_tpCam.transform.position, _tpCam.transform.rotation);

        if (_mountWasReparented)
        {
            thirdPersonViewMount.SetParent(_mountOrigParent, true);
            _mountWasReparented = false;
        }
    }

    void ApplyDrivingThirdPersonCamera()
    {
        if (!_tpCam || !thirdPersonViewMount) return;

        RefreshDrivingThirdPersonFollowCache();
        SetThirdPersonFollowEnabled(false);

        // 先脱离父级，再立即把相机对准挂载点（不依赖快照的 localPos，否则第二次上车会用到旧位置）
        _tpCam.transform.SetParent(null, true);
        _tpCam.transform.SetPositionAndRotation(thirdPersonViewMount.position, thirdPersonViewMount.rotation);

        _tpCam.enabled = true;
        SyncThirdPersonVehicleCamera();

        if (sceneCameraSwitcher)
            sceneCameraSwitcher.RefreshGameplayAudioListeners();
    }

    void RefreshDrivingThirdPersonFollowCache()
    {
        if (!thirdPersonViewMount)
        {
            _tpDrivingUseVehicleLocalOffset = false;
            return;
        }

        if (IsUnderTransform(thirdPersonViewMount, transform))
        {
            _tpDrivingUseVehicleLocalOffset = false;
            return;
        }

        _tpDrivingUseVehicleLocalOffset = true;
        _tpDrivingOffsetLocal = transform.InverseTransformPoint(thirdPersonViewMount.position);
        _tpDrivingRotLocal = Quaternion.Inverse(transform.rotation) * thirdPersonViewMount.rotation;
    }

    void SyncThirdPersonVehicleCamera()
    {
        if (!_tpCam || !thirdPersonViewMount) return;

        Transform ct = _tpCam.transform;
        if (ct.parent != null)
            ct.SetParent(null, true);

        if (IsUnderTransform(thirdPersonViewMount, transform))
        {
            ct.SetPositionAndRotation(thirdPersonViewMount.position, thirdPersonViewMount.rotation);
            return;
        }

        if (_tpDrivingUseVehicleLocalOffset)
        {
            ct.SetPositionAndRotation(
                transform.TransformPoint(_tpDrivingOffsetLocal),
                transform.rotation * _tpDrivingRotLocal);
            return;
        }

        RefreshDrivingThirdPersonFollowCache();
        if (_tpDrivingUseVehicleLocalOffset)
        {
            ct.SetPositionAndRotation(
                transform.TransformPoint(_tpDrivingOffsetLocal),
                transform.rotation * _tpDrivingRotLocal);
        }
        else if (IsUnderTransform(thirdPersonViewMount, transform))
        {
            ct.SetPositionAndRotation(thirdPersonViewMount.position, thirdPersonViewMount.rotation);
        }
    }

    static bool IsUnderTransform(Transform t, Transform ancestor)
    {
        if (t == null || ancestor == null) return false;
        for (Transform p = t; p != null; p = p.parent)
        {
            if (p == ancestor) return true;
        }
        return false;
    }

    /// <summary>
    /// 场景里若曾把 PlayerMovement 根误挂在车身下且当前未在驾驶，拖回场景根，避免碰撞体在而模型像「消失」。
    /// </summary>
    void RecoverPlayerRootIfWronglyParentedToVehicle()
    {
        if (_isDriving || playerMovement == null) return;
        Transform root = playerMovement.transform;
        if (!IsUnderTransform(root, transform)) return;
        root.SetParent(null, true);
    }

    /// <summary>
    /// 重新打开车身上的 Renderer（不含玩家子层级），修复上车隐藏逻辑误伤或异常切场景后的状态。
    /// </summary>
    void RestoreVehicleBodyRenderersAfterLoad()
    {
        if (_isDriving) return;
        var rs = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < rs.Length; i++)
        {
            var r = rs[i];
            if (r == null) continue;
            if (playerModelTransform != null && IsUnderTransform(r.transform, playerModelTransform))
                continue;
            if (!r.enabled)
                r.enabled = true;
        }
    }

    void SetThirdPersonFollowEnabled(bool enabled)
    {
        if (!enabled && !disableThirdPersonFollowScriptsWhileDriving && !_isDriving)
            return;

        if (_tpWideView != null)
        {
            if (enabled)
                _tpWideView.enabled = _tpWideViewEnabledBeforeDrive;
            else
                _tpWideView.enabled = false;
        }

        if (_tpExtraFollowScripts == null) return;
        for (int i = 0; i < _tpExtraFollowScripts.Length; i++)
        {
            if (_tpExtraFollowScripts[i] == null) continue;
            if (enabled)
                _tpExtraFollowScripts[i].enabled = _tpExtraWereEnabled[i];
            else
                _tpExtraFollowScripts[i].enabled = false;
        }
    }

    void ExitVehicle()
    {
        if (_networkProxy != null)
            _networkProxy.TryEndDrive();
        _isDriving = false;
        _drivingCount--;
        _currentSpeed = 0f;
        _horizontalVelocity = Vector3.zero;

        Vector3 exitPos = exitPoint
            ? exitPoint.position
            : transform.TransformPoint(defaultExitOffset);

        RestoreAllCamerasAfterDrive();

        playerModelTransform.SetParent(_origPlayerParent);

        ShowPlayerVisuals();

        if (_playerCC) _playerCC.enabled = true;
        if (playerMovement)
        {
            playerMovement.enabled = true;
            playerMovement.Teleport(exitPos);
        }

        playerModelTransform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

        if (playerAnimator) playerAnimator.enabled = true;

        SetWalkingCombatBarsVisible(true);

        if (_hud) _hud.ExitDriving();
    }

    /// <summary>
    /// 将车辆传送回初始出生位置并重置一切运动状态。
    /// 由 <see cref="rescueKey"/> 长按触发，解决撞墙卡模无法倒车的问题。
    /// </summary>
    void RescueToSpawn()
    {
        Vector3    targetPos = spawnPoint != null ? spawnPoint.position : _spawnPosition;
        Quaternion targetRot = spawnPoint != null ? spawnPoint.rotation : _spawnRotation;

        // CharacterController 必须先禁用才能瞬移，否则引擎会把位置拉回碰撞前
        bool ccWasEnabled = _cc != null && _cc.enabled;
        if (_cc != null) _cc.enabled = false;
        transform.SetPositionAndRotation(targetPos, targetRot);
        if (_cc != null && ccWasEnabled) _cc.enabled = true;

        // 重置全部运动状态
        _currentSpeed = 0f;
        _horizontalVelocity = Vector3.zero;
        _verticalVel = 0f;
        _stuckAgainstWallTimer = 0f;
        _steerSmoothed = 0f;
        _prevDrivingForward = false;

        Vector3 euler = targetRot.eulerAngles;
        _driveYaw       = euler.y;
        _initialEulerX  = euler.x;
        _initialEulerZ  = euler.z;

        Debug.Log("[VehicleController] 车辆已救援回出生点。");
    }

    void RestoreAllCamerasAfterDrive()
    {
        WideViewClearVehicleTarget();
        RestoreMountAfterDrive();

        if (_tpCam)
        {
            _tpCam.transform.SetParent(_tpOrigParent);
            _tpCam.transform.localPosition = _tpOrigLocalPos;
            _tpCam.transform.localRotation = _tpOrigLocalRot;
            SetThirdPersonFollowEnabled(true);

            if (_tpWideView != null && _tpWideView.enabled && _tpWideView.target != null)
                _tpWideView.SetTargetAndTeleport(_tpWideView.target);
        }

        if (sceneCameraSwitcher != null)
            sceneCameraSwitcher.RestoreWalkingViewAfterVehicle(_preDriveWasFirstPerson);
        else if (!_warnedMissingCameraSwitcher)
        {
            _warnedMissingCameraSwitcher = true;
            Debug.LogWarning("[VehicleController] 未配置 CameraSwitcher，下车后请自行确认走路时第一/三人称相机启用状态是否正确。");
        }
    }

    void HidePlayerVisuals()
    {
        _renderersBuf.Clear();
        _rendererWasEnabled.Clear();

        Renderer[] rs = playerVisualRoot
            ? playerVisualRoot.GetComponentsInChildren<Renderer>(true)
            : playerModelTransform.GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < rs.Length; i++)
        {
            var r = rs[i];
            if (!r) continue;
            // 仅隐藏「人物」子层级：若 playerVisualRoot 设得过大或层级与车身重叠，不要关掉车身 Mesh
            if (IsUnderTransform(r.transform, transform) &&
                (playerModelTransform == null || !IsUnderTransform(r.transform, playerModelTransform)))
                continue;

            _renderersBuf.Add(r);
            _rendererWasEnabled.Add(r.enabled);
            r.enabled = false;
        }
    }

    void ShowPlayerVisuals()
    {
        int n = Mathf.Min(_renderersBuf.Count, _rendererWasEnabled.Count);
        for (int i = 0; i < n; i++)
        {
            var r = _renderersBuf[i];
            if (r) r.enabled = _rendererWasEnabled[i];
        }
        _renderersBuf.Clear();
        _rendererWasEnabled.Clear();
    }

    void ProcessDriving()
    {
        bool w = Input.GetKey(KeyCode.W);
        bool s = Input.GetKey(KeyCode.S);
        if (w && s)
        {
            w = false;
            s = false;
        }

        // W↔S 方向切换时重置卡墙计时，避免反向误判
        if (w != _prevDrivingForward)
        {
            _stuckAgainstWallTimer = 0f;
            _prevDrivingForward = w;
        }

        bool throttle = w || s;

        // 顶墙探测用本帧开始时的航向（与原版一致：先油门再转向）
        Vector3 probeForward = Quaternion.Euler(0f, _driveYaw, 0f) * Vector3.forward;

        if (w)
        {
            if (IsForwardPathBlocked(probeForward))
            {
                if (_currentSpeed > 0f)
                    _currentSpeed = Mathf.MoveTowards(_currentSpeed, 0f, forwardBrakeDeceleration * Time.deltaTime);
                else if (_currentSpeed < 0f)
                    _currentSpeed += brakeForce * Time.deltaTime;
            }
            else if (_currentSpeed < 0f)
                _currentSpeed += brakeForce * Time.deltaTime;
            else
                _currentSpeed += acceleration * Time.deltaTime;
        }
        else if (s)
        {
            if (IsReversePathBlocked(probeForward))
            {
                if (_currentSpeed < 0f)
                    _currentSpeed = Mathf.MoveTowards(_currentSpeed, 0f, forwardBrakeDeceleration * Time.deltaTime);
                else if (_currentSpeed > 0f)
                    _currentSpeed -= forwardBrakeDeceleration * Time.deltaTime;
            }
            else if (_currentSpeed > 0f)
                _currentSpeed -= forwardBrakeDeceleration * Time.deltaTime;
            else
                _currentSpeed -= reverseAcceleration * Time.deltaTime;
        }
        else
        {
            _currentSpeed = Mathf.MoveTowards(_currentSpeed, 0f, coastStopDeceleration * Time.deltaTime);
        }

        if (useLegacySteerWhenThrottleOnly)
        {
            _steerSmoothed = 0f;
            if (throttle)
            {
                float steer = 0f;
                if (Input.GetKey(KeyCode.A)) steer -= 1f;
                if (Input.GetKey(KeyCode.D)) steer += 1f;
                float facing = _currentSpeed >= 0f ? 1f : -1f;
                _driveYaw += steer * steerSpeed * facing * Time.deltaTime;
            }
        }
        else
        {
            // ── 转向（平滑版：不要求踩油门，按实际速度方向决定左右，高速阻尼）──
            float steerTarget = 0f;
            if (Input.GetKey(KeyCode.A)) steerTarget -= 1f;
            if (Input.GetKey(KeyCode.D)) steerTarget += 1f;

            // 平滑插值：避免瞬间满舵
            _steerSmoothed = Mathf.MoveTowards(_steerSmoothed, steerTarget, steerSmoothSpeed * Time.deltaTime);

            float speedAbs = Mathf.Abs(_currentSpeed);
            if (speedAbs >= steerMinSpeedRequired && Mathf.Abs(_steerSmoothed) > 0.001f)
            {
                // 高速时降低转向灵敏度
                float speedT = steerSpeedDampingThreshold > 0.01f
                    ? Mathf.Clamp01(speedAbs / steerSpeedDampingThreshold)
                    : 0f;
                float dampedSteer = Mathf.Lerp(steerSpeed, steerSpeed * steerMinFraction, speedT);

                // 倒车时 AD 反转（以实际速度方向为准，不依赖按键）
                float facing = _currentSpeed >= 0f ? 1f : -1f;
                _driveYaw += _steerSmoothed * dampedSteer * facing * Time.deltaTime;
            }
        }

        // ── 救援：长按 rescueKey 返回出生点（可关闭以兼容旧版）──
        if (enableRescueFeature && Input.GetKey(rescueKey))
        {
            _rescueHoldTimer += Time.deltaTime;
            if (_rescueHoldTimer >= Mathf.Max(0f, rescueHoldSeconds))
            {
                _rescueHoldTimer = 0f;
                RescueToSpawn();
                return;
            }
        }
        else
        {
            _rescueHoldTimer = 0f;
        }

        transform.rotation = Quaternion.Euler(_initialEulerX, _driveYaw, _initialEulerZ);
        Vector3 flatForward = Quaternion.Euler(0f, _driveYaw, 0f) * Vector3.forward;

        Vector3 desiredHorizontal = flatForward * _currentSpeed;
        Vector3 hv = _horizontalVelocity;
        hv.y = 0f;
        desiredHorizontal.y = 0f;
        float align = Mathf.Max(0.01f, velocityAlignAcceleration) * Time.deltaTime;
        hv = Vector3.MoveTowards(hv, desiredHorizontal, align);
        _horizontalVelocity.x = hv.x;
        _horizontalVelocity.y = 0f;
        _horizontalVelocity.z = hv.z;

        Vector3 hvBeforeClamp = _horizontalVelocity;
        Vector3 moveVec = _horizontalVelocity * Time.deltaTime;

        if (_cc.isGrounded)
            _verticalVel = -2f;
        else
            _verticalVel += Physics.gravity.y * Time.deltaTime;

        moveVec.y = _verticalVel * Time.deltaTime;
        ClampMoveForWallContact(ref moveVec);
        Vector3 posBefore = transform.position;
        CollisionFlags moveFlags = _cc != null ? _cc.Move(moveVec) : CollisionFlags.None;
        SyncSpeedAfterMove(w, s, flatForward, hvBeforeClamp, posBefore, moveFlags);

        // 联机兜底同步：即使 authority 分配偶发失败，也持续把车体状态推给服务器/其他端。
        if (_networkProxy != null && _networkProxy.IsLocalDriver)
            _networkProxy.PushLocalDriveState(transform.position, transform.rotation, _horizontalVelocity, _currentSpeed);

    }

    bool IsForwardPathBlocked(Vector3 flatDir)
    {
        if (_cc == null) return false;
        flatDir.y = 0f;
        if (flatDir.sqrMagnitude < 1e-6f) return false;
        flatDir.Normalize();

        Vector3 origin = transform.TransformPoint(frontProbeLocalPosition);
        origin.y += _cc.stepOffset;

        float step = Mathf.Abs(_currentSpeed) * Time.deltaTime;
        float castDist = Mathf.Max(forwardBlockedProbeDistance, step + wallProbeRadius + wallProbeSkin * 2f);

        int n = Physics.SphereCastNonAlloc(
            origin,
            wallProbeRadius,
            flatDir,
            _wallProbeHits,
            castDist,
            wallProbeLayers,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < n; i++)
        {
            RaycastHit hit = _wallProbeHits[i];
            if (hit.collider == null) continue;
            if (ShouldIgnoreWallCollider(hit.collider))
                continue;
            return true;
        }

        return false;
    }

    bool IsReversePathBlocked(Vector3 flatDir)
    {
        if (_cc == null) return false;
        flatDir.y = 0f;
        if (flatDir.sqrMagnitude < 1e-6f) return false;
        flatDir.Normalize();
        Vector3 backDir = -flatDir;

        Vector3 origin = transform.TransformPoint(rearProbeLocalPosition);
        origin.y += _cc.stepOffset;

        float step = Mathf.Abs(_currentSpeed) * Time.deltaTime;
        float castDist = Mathf.Max(forwardBlockedProbeDistance, step + wallProbeRadius + wallProbeSkin * 2f);

        int n = Physics.SphereCastNonAlloc(
            origin,
            wallProbeRadius,
            backDir,
            _wallProbeHits,
            castDist,
            wallProbeLayers,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < n; i++)
        {
            RaycastHit hit = _wallProbeHits[i];
            if (hit.collider == null) continue;
            if (ShouldIgnoreWallCollider(hit.collider))
                continue;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 顶墙时 CC 可能几乎不位移，但 MoveTowards 仍把 <see cref="_horizontalVelocity"/> 拉满；按实际位移削掉沿车头分量并清零 <see cref="_currentSpeed"/>。
    /// </summary>
    void SyncSpeedAfterMove(bool w, bool s, Vector3 flatForward, Vector3 hvBeforeClamp, Vector3 posBefore, CollisionFlags moveFlags)
    {
        if (_cc == null) return;

        flatForward.y = 0f;
        if (flatForward.sqrMagnitude < 1e-6f) return;
        flatForward.Normalize();

        Vector3 delta = transform.position - posBefore;
        delta.y = 0f;
        float dt = Mathf.Max(Time.deltaTime, 1e-6f);
        float actualFwd = Vector3.Dot(delta, flatForward) / dt;
        float intendedFwd = Vector3.Dot(hvBeforeClamp, flatForward);

        float minI = stuckAgainstWallMinIntendedForward;
        float fr = stuckAgainstWallMoveFraction;

        bool stuckByMotion =
            (w && intendedFwd > minI && actualFwd < intendedFwd * fr)
            || (s && intendedFwd < -minI && actualFwd > intendedFwd * fr);

        bool sideHit = (moveFlags & CollisionFlags.CollidedSides) != 0;
        bool stuckBySides = sideHit
            && ((w && intendedFwd > minI && actualFwd < intendedFwd * fr)
                || (s && intendedFwd < -minI && actualFwd > intendedFwd * fr));

        if (!stuckByMotion && !stuckBySides)
        {
            _stuckAgainstWallTimer = 0f;
            return;
        }

        _stuckAgainstWallTimer += Time.deltaTime;
        if (_stuckAgainstWallTimer < Mathf.Max(0.05f, stuckResetGraceSeconds))
            return;

        float along = Vector3.Dot(_horizontalVelocity, flatForward);
        _horizontalVelocity -= flatForward * along;
        _currentSpeed = 0f;
        _stuckAgainstWallTimer = 0f;
    }

    /// <summary>
    /// 在 Move 前用球扫限制水平位移，避免 CC 胶囊小于车头模型时高速穿入静态网格。
    /// </summary>
    void ClampMoveForWallContact(ref Vector3 worldMove)
    {
        if (_cc == null) return;

        Vector3 h = new Vector3(worldMove.x, 0f, worldMove.z);
        float dist = h.magnitude;
        if (dist < 1e-6f) return;

        Vector3 dir = h / dist;

        Vector3 f = transform.forward;
        f.y = 0f;
        if (f.sqrMagnitude < 1e-6f) return;
        f.Normalize();

        Vector3 origin = Vector3.Dot(dir, f) >= 0f
            ? transform.TransformPoint(frontProbeLocalPosition)
            : transform.TransformPoint(rearProbeLocalPosition);
        origin.y += _cc.stepOffset;

        float castDist = dist + wallProbeRadius;
        int n = Physics.SphereCastNonAlloc(
            origin,
            wallProbeRadius,
            dir,
            _wallProbeHits,
            castDist,
            wallProbeLayers,
            QueryTriggerInteraction.Ignore);

        float best = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            RaycastHit hit = _wallProbeHits[i];
            if (hit.collider == null) continue;
            if (ShouldIgnoreWallCollider(hit.collider))
                continue;
            if (hit.distance < best)
                best = hit.distance;
        }

        if (best >= float.MaxValue - 1f) return;

        float allowed = Mathf.Max(0f, best - wallProbeSkin);
        if (allowed >= dist - 1e-4f) return;

        float scale = dist > 1e-6f ? allowed / dist : 0f;
        worldMove.x *= scale;
        worldMove.z *= scale;

        _horizontalVelocity.x *= scale;
        _horizontalVelocity.z *= scale;
    }

    bool ShouldIgnoreWallCollider(Collider col)
    {
        if (col == null) return true;
        if (col.isTrigger) return true;
        Transform ct = col.transform;
        if (ct == transform || ct.IsChildOf(transform))
            return true;
        if (ct.GetComponentInParent<PlayerMovement>() != null)
            return true;
        if (ct.GetComponentInParent<CoopNetworkPlayer>() != null)
            return true;
        return false;
    }

    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (!_isDriving || _cc == null || hit.controller != _cc) return;
        if (hit.collider != null && hit.collider.isTrigger) return;

        Vector3 f = transform.forward;
        f.y = 0f;
        if (f.sqrMagnitude < 1e-6f) return;
        f.Normalize();

        float forwardVel = Vector3.Dot(_horizontalVelocity, f);
        if (forwardVel < frontCollisionMinForwardSpeed && _currentSpeed < frontCollisionMinForwardSpeed)
            return;

        Vector3 n = hit.normal;
        n.y = 0f;
        if (n.sqrMagnitude < 1e-6f) return;
        n.Normalize();

        // 正面撞墙：表面法线大致与车头朝向相反（点积为负，且足够“正对”）
        float dot = Vector3.Dot(f, n);
        if (dot > frontCollisionNormalDotMax)
            return;

        _currentSpeed = 0f;
        if (frontCollisionZeroHorizontalVelocity)
        {
            _horizontalVelocity.x = 0f;
            _horizontalVelocity.z = 0f;
        }
    }

    void OnDestroy()
    {
        if (!_isDriving) return;
        _drivingCount--;

        // 尽量先把人物从车身下摘出，再恢复渲染（销毁顺序不确定时也能减少「车看不见」的残留状态）
        if (playerModelTransform != null)
        {
            if (_origPlayerParent != null)
                playerModelTransform.SetParent(_origPlayerParent, true);
            else if (IsUnderTransform(playerModelTransform, transform))
                playerModelTransform.SetParent(null, true);
        }

        if (_renderersBuf.Count > 0)
            ShowPlayerVisuals();

        RestoreAllCamerasAfterDrive();
        SetWalkingCombatBarsVisible(true);

        // 切场景时车身被卸掉但 EnterVehicle 曾关掉移动/CC；必须恢复，否则副机仍「卡在车里」无法走动。
        if (_playerCC != null && !_playerCC.enabled)
            _playerCC.enabled = true;
        if (playerMovement != null && !playerMovement.enabled)
            playerMovement.enabled = true;
        if (playerAnimator != null && !playerAnimator.enabled)
            playerAnimator.enabled = true;
        _isDriving = false;
    }

    /// <summary>远端回放载具状态，避免仅位置瞬移导致速度表与运动判定抖动。</summary>
    public void ApplyRemoteDriveState(Vector3 horizontalVelocity, float speed)
    {
        if (_isDriving) return;
        _horizontalVelocity = horizontalVelocity;
        _horizontalVelocity.y = 0f;
        _currentSpeed = speed;
    }

    void OnDrawGizmosSelected()
    {
        var root = transform;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(root.position, interactDistance);
        Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
        Gizmos.DrawWireSphere(root.position, interactDistance + distanceBuffer);

        Vector3 ep = exitPoint ? exitPoint.position : root.TransformPoint(defaultExitOffset);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(ep, 0.5f);

        if (thirdPersonViewMount)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(thirdPersonViewMount.position, 0.3f);
        }

        Gizmos.color = Color.red;
        Gizmos.DrawRay(root.position + Vector3.up, root.forward * 3f);
    }
}

/// <summary>延迟一帧再激活载具，避免同帧内其它 Start 又把车关掉。</summary>
internal sealed class VehicleActivateNextFrameHost : MonoBehaviour
{
    System.Action _action;

    internal static void Schedule(System.Action action)
    {
        if (action == null) return;
        var go = new GameObject(nameof(VehicleActivateNextFrameHost));
        var host = go.AddComponent<VehicleActivateNextFrameHost>();
        host._action = action;
    }

    System.Collections.IEnumerator Start()
    {
        yield return null;
        _action?.Invoke();
        Destroy(gameObject);
    }
}
