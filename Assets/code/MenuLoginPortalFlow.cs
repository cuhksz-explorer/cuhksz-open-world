using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// 登录菜单流程（无传送门实体版本）：
/// 背景镜头循环巡轨 → 点击屏幕/按钮弹出登录 → 登录成功后暂停镜头
/// → 下方弹出「点击进入游戏」提示（UI 可选）→ 按 P（可改）切换账号/登出 → 自动或点击后调用 GameServerSync.EnterWorldFromServerSave()。
/// 请在 GameServerSync 上勾选 deferSpawnFetchUntilEnterWorld（可避免登录后立刻跳转）。
/// </summary>
public class MenuLoginPortalFlow : MonoBehaviour
{
    public static MenuLoginPortalFlow ActiveInstance { get; private set; }

    enum Phase
    {
        IntroCinematic,
        LoginForm,
        NicknameSetup,
        /// <summary>Loading 场景：已存 token，正在请求服务器校验。</summary>
        ValidatingStoredToken,
        ReadyToEnter,
        EnteringWorld,
    }

    [Header("镜头巡轨")]
    [SerializeField] IntroDollyCamera dolly;

    [Header("界面")]
    [Tooltip("登录面板根物体（含 GameServerAuthUI）；初始可隐藏，由本脚本在点击后显示")]
    [SerializeField] GameObject loginPanelRoot;

    [Tooltip("可选：全屏透明按钮，点击后打开登录面板（不填则用鼠标/触摸检测）")]
    [SerializeField] Button tapToOpenLoginButton;

    [Header("登录提示（底部居中）")]
    [Tooltip("是否在 Intro 阶段自动创建『点击任意处登录』提示（不拦截点击）。")]
    [SerializeField] bool autoCreateLoginHintUI = true;

    [SerializeField] string loginHintText = "点击任意处登录";
    [SerializeField] int loginHintFontSize = 48;
    [SerializeField] Color loginHintColor = new Color(0.08f, 0.08f, 0.08f, 0.85f);
    [SerializeField] int loginHintCanvasSortOrder = 32001;
    [SerializeField] float loginHintFromBottomPixels = 70f;

    GameObject _loginHintRoot;
    Text _loginHintLabel;

    [Header("进入提示（无传送门）")]
    [Tooltip("登录成功后显示的提示根物体（例如 UI 文本：点击进入游戏）。可选。")]
    [SerializeField] GameObject enterGameHintRoot;

    [Header("注册后昵称 / 角色设置")]
    [Tooltip("首次注册时使用的「角色选择 + 昵称」合并面板。不填则尝试在同物体或场景中查找。")]
    [SerializeField] PlayerCharacterRegisterUI characterRegisterUI;
    [Tooltip("可选：昵称设置面板根物体（包含 InputField + 确认按钮）。不填则自动生成兜底面板。")]
    [SerializeField] GameObject nicknameSetupPanelRoot;
    [SerializeField] InputField nicknameInputField;
    [SerializeField] Button nicknameConfirmButton;
    [SerializeField] Button nicknameRandomButton;
    [SerializeField] Text nicknameStatusText;
    [SerializeField] bool autoCreateNicknameSetupFallbackUI = true;
    Sprite _nicknameRandomDiceSprite;

    [Header("进入提示兜底")]
    [Tooltip("如果 enterGameHintRoot 为空，则自动生成一个兜底提示。")]
    [SerializeField] bool autoCreateEnterHintFallbackUI = true;
    [SerializeField] string enterHintText = "点击进入游戏";
    [SerializeField] int enterHintFontSize = 46;
    [SerializeField] Color enterHintColor = new Color(1f, 1f, 1f, 0.92f);
    [SerializeField] int enterHintCanvasSortOrder = 32002;
    [SerializeField] float enterHintFromBottomPixels = 110f;

    GameObject _enterHintRoot;
    Text _enterHintLabel;
    Text _enterHintLogoutLineLabel;
    Text _resolvedSwitchAccountHint;

    [Header("登录成功提示（顶部滑入）")]
    [Tooltip("登录成功后从屏幕顶部滑下提示条（用户名 + 已经登录成功）。")]
    [SerializeField] bool showLoginSuccessBanner = true;
    [SerializeField] LguHubLoginSuccessBanner loginSuccessBanner;

    LguHubLoginSuccessBanner _loginSuccessBannerRuntime;

    [Tooltip("为 true：显示提示后自动开始加载；为 false：需要用户再点击一次才开始加载。")]
    [SerializeField] bool autoEnterAfterLogin = true;

    [Tooltip("autoEnterAfterLogin 为 true 时的延迟（秒）。")]
    [SerializeField] float autoEnterDelaySeconds = 0f;

    [Header("服务器")]
    [SerializeField] GameServerSync serverSync;

    [Header("LoadingScene 专用行为")]
    [Tooltip("在这些场景中：开场直接显示登录面板，登录成功后需用户任意输入（鼠标/键盘）才进入游戏。")]
    [SerializeField] string[] forceShowLoginPanelScenes = { "Loadingscene" };
    [SerializeField] bool requireAnyInputToEnterInForceShowScenes = true;

    [Tooltip("若进入菜单时本地已有 token，直接进入「传送门就绪」阶段（跳过登录表单）")]
    [SerializeField] bool skipLoginIfAlreadyLoggedIn = true;
     
    [Header("切换账号（准备进入游戏阶段）")]
    [Tooltip("按此键登出并回到登录面板（不会用按钮，避免与「点击进入游戏」的鼠标判定冲突）。")]
    [SerializeField] KeyCode switchAccountKey = KeyCode.P;
    [SerializeField] string switchAccountHintLine = "切换账号 / 登出 (P)";
    [SerializeField] int switchAccountHintFontSize = 28;
    [SerializeField] float switchAccountHintFromBottomPixels = 200f;
    [Tooltip("可选：场景里专门显示上一行文案的 Text；物体名也可为 SwitchAccountHint。不填时仅在自动生成进入提示 UI 时创建第二行。")]
    [SerializeField] Text switchAccountHintLabel;

     
    [Tooltip("Intro 阶段显示鼠标便于点击")]
    [SerializeField] bool showCursorDuringIntro = true;

    [Header("调试")]
    [SerializeField] bool debugLog = false;

    [Header("进入世界兜底")]
    [Tooltip("点击进入后，若在此秒数内仍未离开 LoadingScene，则触发兜底。")]
    [SerializeField] float enterWorldTimeoutSeconds = 4f;
    [Tooltip("超时后自动进入该场景（为空则仅恢复到可重试状态）。")]
    [SerializeField] string fallbackWorldSceneName = "";

    [Header("Audio Listener 守卫（LoadingScene）")]
    [Tooltip("在 LoadingScene 中只保留一个 AudioListener，避免重复报警和音频异常。")]
    [SerializeField] bool enforceSingleAudioListenerInForceScene = true;
    [SerializeField] float audioListenerCheckInterval = 0.5f;

    Phase _phase = Phase.IntroCinematic;
    bool _isForceShowLoginScene;
    bool _pendingFirstRegisterProfile;
    bool _awaitingWelcomeSlidesAfterRegister;
    float _nextAudioListenerCheckTime;
    Coroutine _enterWorldWatchdog;
    Coroutine _validateStoredLoginCoroutine;
    int _lastSwitchAccountLogoutFrame = -1;
    static int _blockEnterWorldInputFrame = -1;

    /// <summary>本帧已处理切换账号热键时，禁止「任意键进入游戏」。</summary>
    public static bool ShouldBlockEnterWorldInputThisFrame =>
        _blockEnterWorldInputFrame == Time.frameCount;

    void Awake()
    {
        ActiveInstance = this;
        EnsureServerSyncReference();

        if (tapToOpenLoginButton != null)
            tapToOpenLoginButton.onClick.AddListener(OnTapToOpenLogin);

        GameServerSync.OnAuthSuccess += OnAuthSuccess;
        GameServerSync.OnRegisterSuccess += OnRegisterSuccess;
        GameServerSync.OnServerSpawnSyncLockEnded += OnServerSpawnSyncLockEnded;

        if (autoCreateLoginHintUI)
            EnsureLoginHintUIBuilt();

        if (autoCreateEnterHintFallbackUI && enterGameHintRoot == null)
            EnsureEnterHintFallbackUIBuilt();
        if (autoCreateNicknameSetupFallbackUI && nicknameSetupPanelRoot == null && characterRegisterUI == null)
            EnsureNicknameSetupFallbackUIBuilt();

        if (nicknameConfirmButton != null)
            nicknameConfirmButton.onClick.AddListener(OnNicknameConfirmClicked);
        if (nicknameRandomButton != null)
            nicknameRandomButton.onClick.AddListener(OnNicknameRandomClicked);
        ConfigureRandomNicknameButtonVisual();

    }

    void OnEnable()
    {
        Loc.OnLanguageChanged += OnLanguageChanged;
        RefreshLocalizedUiTexts();
    }

    void OnDisable()
    {
        Loc.OnLanguageChanged -= OnLanguageChanged;
    }

    void OnLanguageChanged() => RefreshLocalizedUiTexts();

    void RefreshLocalizedUiTexts()
    {
        if (_loginHintLabel != null)
            _loginHintLabel.text = Loc.Choose(loginHintText, "Tap anywhere to log in");
        if (_enterHintLabel != null)
            _enterHintLabel.text = Loc.Choose(enterHintText, "Click to enter game");
        ApplySwitchAccountHintLineText(_resolvedSwitchAccountHint);
        ApplySwitchAccountHintLineText(_enterHintLogoutLineLabel);
        if (nicknameStatusText != null && _phase == Phase.NicknameSetup
            && string.IsNullOrEmpty(nicknameInputField != null ? nicknameInputField.text : ""))
            nicknameStatusText.text = Loc.Choose("请设置你的游戏昵称（仅首次注册需要）", "Set your in-game nickname (first registration only)");
    }

    void OnDestroy()
    {
        Loc.OnLanguageChanged -= OnLanguageChanged;
        if (ActiveInstance == this)
            ActiveInstance = null;

        GameServerSync.OnAuthSuccess -= OnAuthSuccess;
        GameServerSync.OnRegisterSuccess -= OnRegisterSuccess;
        GameServerSync.OnServerSpawnSyncLockEnded -= OnServerSpawnSyncLockEnded;
        if (tapToOpenLoginButton != null)
            tapToOpenLoginButton.onClick.RemoveListener(OnTapToOpenLogin);
        if (nicknameConfirmButton != null)
            nicknameConfirmButton.onClick.RemoveListener(OnNicknameConfirmClicked);
        if (nicknameRandomButton != null)
            nicknameRandomButton.onClick.RemoveListener(OnNicknameRandomClicked);
    }

    void OnServerSpawnSyncLockEnded()
    {
        if (this == null) return;
        // 如果我们请求进入世界后，仍然停在 Loadingscene，则允许用户再次尝试（避免卡死）。
        if (!_isForceShowLoginScene) return;
        if (_phase != Phase.EnteringWorld) return;

        if (IsForceShowLoginScene(SceneManager.GetActiveScene().name))
        {
            // 停留在加载场景，说明本次 EnterWorld 没成功：回到 ReadyToEnter，让用户可以再点/再输一次。
            EnterReadyToEnterPhase();
        }
    }

    void Start()
    {
        EnsureServerSyncReference();
        EnsureCharacterRegisterUiReference();
        _isForceShowLoginScene = IsForceShowLoginScene(SceneManager.GetActiveScene().name);

        if (_isForceShowLoginScene)
        {
            ClientVersionUpdateGate.EnsureExists();
            GameplayUiHotkeyGuard.DismissDontDestroyOnLoadPanelsForLoginFlowScene();
        }

        UiLifecycleSafe.SetActiveIfAlive(loginPanelRoot, false);
        UiLifecycleSafe.SetActiveIfAlive(enterGameHintRoot, false);
        UiLifecycleSafe.SetActiveIfAlive(nicknameSetupPanelRoot, false);

        if (showCursorDuringIntro)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        if (_isForceShowLoginScene)
        {
            if (skipLoginIfAlreadyLoggedIn && serverSync != null && serverSync.IsLoggedIn)
            {
                _phase = Phase.ValidatingStoredToken;
                UiLifecycleSafe.SetActiveIfAlive(loginPanelRoot, false);
                if (tapToOpenLoginButton != null)
                    UiLifecycleSafe.SetActiveIfAlive(tapToOpenLoginButton.gameObject, false);
                _validateStoredLoginCoroutine = StartCoroutine(CoValidateStoredLoginOnLoad());
            }
            else
            {
                // LoadingScene：无有效本地 token 时进入即显示登录窗口
                _phase = Phase.LoginForm;
                ForceKeepLoginPanelVisibleInForceScene();
                if (tapToOpenLoginButton != null)
                    UiLifecycleSafe.SetActiveIfAlive(tapToOpenLoginButton.gameObject, false);
            }
        }
        else if (skipLoginIfAlreadyLoggedIn && serverSync != null && serverSync.IsLoggedIn)
        {
            EnterReadyToEnterPhase();
        }
        else
        {
            _phase = Phase.IntroCinematic;
        }

        SyncLoginHintVisibility();
        SyncEnterHintVisibility();

        if (debugLog)
        {
            string panelState = loginPanelRoot != null ? loginPanelRoot.activeSelf.ToString() : "null";
            string enterHintState = enterGameHintRoot != null ? "set" : "null";
            Debug.Log("[MenuLoginPortalFlow] Start phase=" + _phase +
                      " IsLoggedIn=" + (serverSync != null && serverSync.IsLoggedIn) +
                      " loginPanelRoot=" + panelState +
                      " enterHint=" + enterHintState);
        }
    }

    void Update()
    {
        if (this == null) return;
        // 场景往返后引用可能丢失：周期性重连 GameServerSync，避免“第一次能进，后面找不到”。
        if (serverSync == null)
            EnsureServerSyncReference();

        // 切换账号热键必须优先于「任意键进入游戏」，避免同帧 P 既登出又进关卡。
        if (TryHandleSwitchAccountLogoutKey())
            return;

        if (_isForceShowLoginScene && _phase == Phase.LoginForm && !_pendingFirstRegisterProfile)
            UpdateLoginPanelVisibilityInForceScene();
        if (_isForceShowLoginScene && enforceSingleAudioListenerInForceScene)
            TryEnforceSingleAudioListenerPeriodic();

        SyncLoginHintVisibility();
        SyncEnterHintVisibility();

        if (_phase == Phase.IntroCinematic)
        {
            // 需求是“点击任意处登录”，这里不再过滤“点到 UI 上”的情况。
            if (AnyPrimaryClickDown())
                OnTapToOpenLogin();
            return;
        }

        if (_phase == Phase.NicknameSetup)
            return;

        if (NewUserWelcomeTextSlides.IsPlaying)
            return;

        if (_phase == Phase.ReadyToEnter)
        {
            // LoadingScene（登录后）要求：鼠标/键盘任意输入均可进入（P 登出已在上面单独处理）
            bool isEnterInput = _isForceShowLoginScene ? AnyEnterInputDown() : AnyPrimaryClickDown();
            if (isEnterInput)
                EnterWorld();
        }
    }

    void LateUpdate()
    {
        if (this == null) return;
        if (!_isForceShowLoginScene) return;

        if (!DetectSwitchAccountKeyThisFrame())
            return;

        // 输入框聚焦时 Update 可能漏检 P；若同帧已误触发 EnterWorld，在此撤回并改为登出
        if (_phase == Phase.EnteringWorld)
        {
            if (_enterWorldWatchdog != null)
            {
                StopCoroutine(_enterWorldWatchdog);
                _enterWorldWatchdog = null;
            }

            OnSwitchAccountLogout();
            return;
        }

        if (_lastSwitchAccountLogoutFrame == Time.frameCount)
            return;

        TryHandleSwitchAccountLogoutKey();
    }

    /// <summary>
    /// P 切换账号。Loading 场景（forceShow）：除未登录开场镜头外均可登出；
    /// 其它场景：已登录且在登录成功/准备进入/进入世界阶段均可登出。
    /// </summary>
    public bool TryHandleSwitchAccountLogoutKey()
    {
        if (!DetectSwitchAccountKeyThisFrame())
            return false;

        if (!CanSwitchAccountLogoutNow())
            return false;

        MarkSwitchAccountHotkeyConsumed();
        OnSwitchAccountLogout();
        return true;
    }

    bool CanSwitchAccountLogoutNow()
    {
        if (_phase == Phase.IntroCinematic)
        {
            EnsureServerSyncReference();
            if (serverSync == null || !serverSync.IsLoggedIn)
                return false;
            return IsLoginPanelVisibleForSwitchAccount();
        }

        if (_isForceShowLoginScene)
        {
            if (GameplayUiHotkeyGuard.IsAnyInputFieldFocused())
                return false;

            EnsureServerSyncReference();
            if (serverSync == null || !serverSync.IsLoggedIn)
                return false;

            return _phase == Phase.LoginForm
                   || _phase == Phase.ValidatingStoredToken
                   || _phase == Phase.ReadyToEnter
                   || _phase == Phase.EnteringWorld;
        }

        EnsureServerSyncReference();
        if (serverSync == null || !serverSync.IsLoggedIn)
            return false;

        return _phase == Phase.LoginForm
               || _phase == Phase.ValidatingStoredToken
               || _phase == Phase.ReadyToEnter
               || _phase == Phase.EnteringWorld;
    }

    bool IsLoginPanelVisibleForSwitchAccount()
    {
        if (loginPanelRoot == null || !loginPanelRoot.scene.IsValid())
            TryResolveLoginPanelInActiveScene();
        if (loginPanelRoot == null || !loginPanelRoot.activeInHierarchy)
            return false;

        var auth = loginPanelRoot.GetComponent<GameServerAuthUI>();
        return auth == null || !auth.IsHiddenByCloseButton;
    }

    bool IsLoginPanelVisible()
    {
        EnsureLoginPanelRootResolved();
        if (loginPanelRoot == null || !loginPanelRoot.activeInHierarchy)
            return false;

        var auth = loginPanelRoot.GetComponent<GameServerAuthUI>();
        return auth == null || !auth.IsHiddenByCloseButton;
    }

    void EnsureLoginPanelRootResolved()
    {
        if (loginPanelRoot != null && loginPanelRoot.scene.IsValid())
            return;
        TryResolveLoginPanelInActiveScene();
    }

    static bool AnyPrimaryClickDown()
    {
        if (Input.GetMouseButtonDown(0))
            return true;
        if (Input.touchCount > 0)
        {
            var t = Input.GetTouch(0);
            if (t.phase == TouchPhase.Began)
                return true;
        }

        return false;
    }

    bool AnyEnterInputDown()
    {
        if (ShouldBlockEnterWorldInputThisFrame)
            return false;
        if (DetectSwitchAccountKeyThisFrame())
            return false;
        if (GameplayUiHotkeyGuard.IsAnyInputFieldFocused())
            return false;

        if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2))
            return true;
        if (Input.touchCount > 0)
        {
            var t = Input.GetTouch(0);
            if (t.phase == TouchPhase.Began)
                return true;
        }

        if (!Input.anyKeyDown)
            return false;

        // 不用裸 anyKeyDown 直接进世界：排除切换账号键（含输入框吞掉 GetKeyDown 的边缘情况）
        if (Input.GetKey(switchAccountKey))
            return false;

        return true;
    }

    /// <summary>检测切换账号热键；输入框聚焦时 Unity 可能吞掉 GetKeyDown，需用 inputString 兜底。</summary>
    bool DetectSwitchAccountKeyThisFrame()
    {
        if (Input.GetKeyDown(switchAccountKey))
            return true;
        if (switchAccountKey != KeyCode.P)
            return false;
        if (!GameplayUiHotkeyGuard.IsAnyInputFieldFocused())
            return false;

        string typed = Input.inputString;
        return typed == "p" || typed == "P";
    }

    /// <summary>供 GameServerAuthUI 等调用，与流程内检测逻辑一致。</summary>
    public static bool IsSwitchAccountKeyPressedThisFrame()
    {
        var flow = ActiveInstance != null ? ActiveInstance : FindObjectOfType<MenuLoginPortalFlow>(true);
        if (flow != null)
            return flow.DetectSwitchAccountKeyThisFrame();

        if (Input.GetKeyDown(KeyCode.P))
            return true;
        if (!GameplayUiHotkeyGuard.IsAnyInputFieldFocused())
            return false;

        string typed = Input.inputString;
        return typed == "p" || typed == "P";
    }

    static void MarkSwitchAccountHotkeyConsumed() => _blockEnterWorldInputFrame = Time.frameCount;

    void OnTapToOpenLogin()
    {
        if (_phase != Phase.IntroCinematic)
            return;

        _phase = Phase.LoginForm;

        var auth = loginPanelRoot != null ? loginPanelRoot.GetComponent<GameServerAuthUI>() : null;
        if (auth != null && auth.IsHiddenByCloseButton)
            auth.RestorePanelAfterClose();
        else
            UiLifecycleSafe.SetActiveIfAlive(loginPanelRoot, true);

        // 兜底：如果登录面板有 CanvasGroup 被其它脚本淡出过，确保可见且可交互
        if (UiLifecycleSafe.IsSafeToTouch(loginPanelRoot))
        {
            var cg = loginPanelRoot.GetComponentInChildren<CanvasGroup>(true);
            if (cg != null)
            {
                cg.alpha = 1f;
                cg.interactable = true;
                cg.blocksRaycasts = true;
            }
        }
        if (debugLog)
            Debug.Log($"[MenuLoginPortalFlow] OpenLogin -> phase={_phase} loginPanelRoot.active={(loginPanelRoot != null && loginPanelRoot.activeSelf)}");

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void OnRegisterSuccess(string username)
    {
        _pendingFirstRegisterProfile = true;
        _awaitingWelcomeSlidesAfterRegister = true;
        _phase = Phase.NicknameSetup;
        HideLoginPanelCompletely();
        if (debugLog)
            Debug.Log("[MenuLoginPortalFlow] 新账号注册成功，进入角色/昵称设置: " + username);
    }

    void OnAuthSuccess(string username, string token)
    {
        if (this == null) return;
        if (_lastSwitchAccountLogoutFrame == Time.frameCount)
            return;
        if (_phase != Phase.LoginForm && _phase != Phase.ValidatingStoredToken && !_pendingFirstRegisterProfile)
            return;

        if (_pendingFirstRegisterProfile)
        {
            HideLoginPanelCompletely();
            EnterNicknameSetupPhase();
            return;
        }

        StartCoroutine(CoHandlePostRegisterNicknameGate());
    }

    System.Collections.IEnumerator CoValidateStoredLoginOnLoad()
    {
        try
        {
            EnsureServerSyncReference();
            if (serverSync == null)
            {
                _phase = Phase.LoginForm;
                ForceKeepLoginPanelVisibleInForceScene();
                if (tapToOpenLoginButton != null)
                    UiLifecycleSafe.SetActiveIfAlive(tapToOpenLoginButton.gameObject, false);
                SyncLoginHintVisibility();
                SyncEnterHintVisibility();
                yield break;
            }

            bool ok = false;
            yield return StartCoroutine(serverSync.CoValidateStoredToken(v => ok = v));

            if (this == null) yield break;

            if (!ok)
            {
                _phase = Phase.LoginForm;
                ForceKeepLoginPanelVisibleInForceScene();
                if (tapToOpenLoginButton != null)
                    UiLifecycleSafe.SetActiveIfAlive(tapToOpenLoginButton.gameObject, false);
            }

            SyncLoginHintVisibility();
            SyncEnterHintVisibility();
        }
        finally
        {
            _validateStoredLoginCoroutine = null;
        }
    }

    void HideLoginPanelCompletely()
    {
        UiLifecycleSafe.SetActiveIfAlive(loginPanelRoot, false);
        if (loginPanelRoot != null)
        {
            var auth = loginPanelRoot.GetComponent<GameServerAuthUI>();
            if (auth != null)
                auth.HideForCharacterSetup();
        }
    }

    void UpdateLoginPanelVisibilityInForceScene()
    {
        if (loginPanelRoot == null || !loginPanelRoot.scene.IsValid())
            TryResolveLoginPanelInActiveScene();

        if (loginPanelRoot == null)
            return;

        var auth = loginPanelRoot.GetComponent<GameServerAuthUI>();
        if (auth != null && auth.IsHiddenByCloseButton)
        {
            if (AnyEnterInputDown())
                auth.RestorePanelAfterClose();
            return;
        }

        ForceKeepLoginPanelVisibleInForceScene();
    }

    void ForceKeepLoginPanelVisibleInForceScene()
    {
        if (loginPanelRoot == null)
            return;

        EnsureLoginPanelCanvasScale();

        if (!loginPanelRoot.activeSelf)
            UiLifecycleSafe.SetActiveIfAlive(loginPanelRoot, true);

        if (UiLifecycleSafe.IsSafeToTouch(loginPanelRoot))
        {
            var cg = loginPanelRoot.GetComponentInChildren<CanvasGroup>(true);
            if (cg != null)
            {
                cg.alpha = 1f;
                cg.interactable = true;
                cg.blocksRaycasts = true;
            }
        }

        UiLifecycleSafe.SetActiveIfAlive(enterGameHintRoot, false);
        UiLifecycleSafe.SetActiveIfAlive(_enterHintRoot, false);
        HideLoginSuccessBanner();
    }

    void EnsureLoginPanelCanvasScale()
    {
        if (loginPanelRoot == null)
            return;

        var canvas = loginPanelRoot.GetComponentInParent<Canvas>(true);
        if (canvas == null)
            return;

        var rt = canvas.transform as RectTransform;
        if (rt == null)
            return;

        if (rt.localScale.sqrMagnitude < 0.01f)
            rt.localScale = Vector3.one;
    }

    void TryResolveLoginPanelInActiveScene()
    {
        var roots = SceneManager.GetActiveScene().GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            var ts = roots[i].GetComponentsInChildren<Transform>(true);
            for (int j = 0; j < ts.Length; j++)
            {
                if (ts[j] != null && string.Equals(ts[j].name, "GameServer_LoginPanel", System.StringComparison.Ordinal))
                {
                    loginPanelRoot = ts[j].gameObject;
                    if (debugLog)
                        Debug.Log("[MenuLoginPortalFlow] Rebound loginPanelRoot to active-scene GameServer_LoginPanel.");
                    return;
                }
            }
        }
    }

    void EnterReadyToEnterPhase()
    {
        _phase = Phase.ReadyToEnter;

        UiLifecycleSafe.SetActiveIfAlive(loginPanelRoot, false);
        UiLifecycleSafe.SetActiveIfAlive(nicknameSetupPanelRoot, false);
        if (characterRegisterUI != null)
            characterRegisterUI.Hide();

        if (tapToOpenLoginButton != null)
            UiLifecycleSafe.SetActiveIfAlive(tapToOpenLoginButton.gameObject, false);

        if (dolly != null)
            dolly.SetPaused(true);
        UiLifecycleSafe.SetActiveIfAlive(enterGameHintRoot, true);
        if (UiLifecycleSafe.IsSafeToTouch(enterGameHintRoot))
        {
            var cg = enterGameHintRoot.GetComponentInChildren<CanvasGroup>(true);
            if (cg != null)
            {
                cg.alpha = 1f;
                cg.interactable = true;
                cg.blocksRaycasts = false;
            }
        }

        if (debugLog)
        {
            string hintState = enterGameHintRoot != null ? "set" : "null";
            Debug.Log("[MenuLoginPortalFlow] EnterReadyToEnterPhase -> enterGameHintRoot=" + hintState +
                      " autoEnterAfterLogin=" + autoEnterAfterLogin);
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        bool shouldAutoEnter = autoEnterAfterLogin;
        if (_isForceShowLoginScene && requireAnyInputToEnterInForceShowScenes)
            shouldAutoEnter = false;

        if (shouldAutoEnter)
        {
            if (autoEnterDelaySeconds <= 0f)
                EnterWorld();
            else
                StartCoroutine(CoAutoEnter());
        }

        SetSwitchAccountHintVisible(true);
        ShowLoginSuccessBannerIfNeeded();
    }

    void ShowLoginSuccessBannerIfNeeded()
    {
        if (!showLoginSuccessBanner)
            return;

        EnsureServerSyncReference();
        string username = serverSync != null ? serverSync.SavedUsername : "";
        if (string.IsNullOrWhiteSpace(username))
            return;

        EnsureLoginSuccessBanner();
        if (_loginSuccessBannerRuntime != null)
            _loginSuccessBannerRuntime.Show(username);
    }

    void EnsureLoginSuccessBanner()
    {
        if (!showLoginSuccessBanner)
            return;

        if (loginSuccessBanner != null)
        {
            _loginSuccessBannerRuntime = loginSuccessBanner;
            return;
        }

        if (_loginSuccessBannerRuntime != null)
            return;

        var go = new GameObject("LoginSuccessBanner");
        go.transform.SetParent(transform, false);
        _loginSuccessBannerRuntime = go.AddComponent<LguHubLoginSuccessBanner>();
    }

    void HideLoginSuccessBanner(bool immediate = true)
    {
        if (_loginSuccessBannerRuntime != null)
            _loginSuccessBannerRuntime.Hide(immediate);
        if (loginSuccessBanner != null && loginSuccessBanner != _loginSuccessBannerRuntime)
            loginSuccessBanner.Hide(immediate);
    }

    System.Collections.IEnumerator CoHandlePostRegisterNicknameGate()
    {
        EnsureServerSyncReference();
        if (serverSync == null)
        {
            EnterReadyToEnterPhase();
            yield break;
        }

        bool done = false;
        string nickname = "";
        string characterId = "";
        yield return StartCoroutine(serverSync.CoFetchMeProfile(
            (_, nick, charId) =>
            {
                nickname = (nick ?? "").Trim();
                characterId = (charId ?? "").Trim();
                done = true;
            },
            _ => { done = true; }));

        if (!done)
        {
            if (_pendingFirstRegisterProfile)
                EnterNicknameSetupPhase();
            else
                EnterReadyToEnterPhase();
            yield break;
        }

        if (string.IsNullOrEmpty(nickname) || string.IsNullOrEmpty(characterId))
            EnterNicknameSetupPhase();
        else
        {
            CompleteFirstRegisterFlow();
        }
    }

    void CompleteFirstRegisterFlow()
    {
        _pendingFirstRegisterProfile = false;
        EnsureServerSyncReference();
        string username = serverSync != null ? serverSync.SavedUsername : "";

        if (_awaitingWelcomeSlidesAfterRegister && NewUserWelcomeTextSlides.ShouldPlay(username))
        {
            _awaitingWelcomeSlidesAfterRegister = false;
            _phase = Phase.NicknameSetup;
            HideLoginPanelCompletely();
            UiLifecycleSafe.SetActiveIfAlive(enterGameHintRoot, false);
            UiLifecycleSafe.SetActiveIfAlive(_enterHintRoot, false);
            UiLifecycleSafe.SetActiveIfAlive(nicknameSetupPanelRoot, false);
            if (characterRegisterUI != null)
                characterRegisterUI.Hide();
            SetSwitchAccountHintVisible(false);
            if (dolly != null)
                dolly.SetPaused(true);

            NewUserWelcomeTextSlides.Play(username, EnterReadyToEnterPhase);
            if (debugLog)
                Debug.Log("[MenuLoginPortalFlow] 新账号欢迎文字，完成后进入 ReadyToEnter。");
            return;
        }

        _awaitingWelcomeSlidesAfterRegister = false;
        EnterReadyToEnterPhase();
    }

    void EnterNicknameSetupPhase()
    {
        _phase = Phase.NicknameSetup;
        HideLoginPanelCompletely();
        EnsureCharacterRegisterUiReference();
        if (characterRegisterUI != null && characterRegisterUI.HasAvailableOptions())
        {
            UiLifecycleSafe.SetActiveIfAlive(enterGameHintRoot, false);
            UiLifecycleSafe.SetActiveIfAlive(_enterHintRoot, false);
            UiLifecycleSafe.SetActiveIfAlive(nicknameSetupPanelRoot, false);
            SetSwitchAccountHintVisible(false);
            if (dolly != null)
                dolly.SetPaused(true);

            characterRegisterUI.Show(() => CompleteFirstRegisterFlow());
            return;
        }

        if (debugLog)
            Debug.LogWarning("[MenuLoginPortalFlow] 角色选择 UI 不可用，回退到仅昵称面板。");
        EnterLegacyNicknameSetupPhase();
    }

    void EnterLegacyNicknameSetupPhase()
    {
        _phase = Phase.NicknameSetup;
        UiLifecycleSafe.SetActiveIfAlive(loginPanelRoot, false);
        UiLifecycleSafe.SetActiveIfAlive(enterGameHintRoot, false);
        UiLifecycleSafe.SetActiveIfAlive(_enterHintRoot, false);
        SetSwitchAccountHintVisible(false);

        if (nicknameSetupPanelRoot == null && autoCreateNicknameSetupFallbackUI)
            EnsureNicknameSetupFallbackUIBuilt();
        UiLifecycleSafe.SetActiveIfAlive(nicknameSetupPanelRoot, true);
        ConfigureRandomNicknameButtonVisual();
        if (nicknameInputField != null)
            nicknameInputField.text = "";
        if (nicknameStatusText != null)
            nicknameStatusText.text = Loc.Choose("请设置你的游戏昵称（仅首次注册需要）", "Set your in-game nickname (first registration only)");
        if (nicknameConfirmButton != null)
            nicknameConfirmButton.interactable = true;
        if (nicknameRandomButton != null)
            nicknameRandomButton.interactable = true;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void OnNicknameConfirmClicked()
    {
        if (_phase != Phase.NicknameSetup)
            return;
        EnsureServerSyncReference();
        if (serverSync == null)
            return;

        string nick = nicknameInputField != null ? nicknameInputField.text.Trim() : "";
        if (string.IsNullOrEmpty(nick))
        {
            if (nicknameStatusText != null)
                nicknameStatusText.text = Loc.Choose("昵称不能为空", "Nickname cannot be empty");
            return;
        }

        if (nick.Length > 24)
        {
            if (nicknameStatusText != null)
                nicknameStatusText.text = Loc.Choose("昵称最多 24 个字符", "Nickname max 24 characters");
            return;
        }

        if (nicknameConfirmButton != null)
            nicknameConfirmButton.interactable = false;
        if (nicknameRandomButton != null)
            nicknameRandomButton.interactable = false;
        if (nicknameStatusText != null)
            nicknameStatusText.text = Loc.Choose("保存昵称中...", "Saving nickname...");

        StartCoroutine(CoSubmitNicknameAndContinue(nick));
    }

    void OnNicknameRandomClicked()
    {
        if (_phase != Phase.NicknameSetup)
            return;
        string nick = GenerateRandomNickname();
        if (nicknameInputField != null)
            nicknameInputField.text = nick;
        if (nicknameStatusText != null)
            nicknameStatusText.text = Loc.Choose("已生成随机昵称，可直接确认或继续修改", "Random nickname generated — confirm or edit");
    }

    static string GenerateRandomNickname()
    {
        return GenerateRandomNicknamePublic();
    }

    public static string GenerateRandomNicknamePublic()
    {
        string[] left =
        {
            "清风", "星河", "小鹿", "白桃", "夜雨",
            "青柠", "流萤", "云朵", "晚星", "墨竹",
            "初雪", "朝露", "山海", "锦鲤", "月影",
        };
        string[] right =
        {
            "旅人", "同学", "行者", "学长", "学姐",
            "探险家", "守望者", "追梦人", "冒险者", "研究员",
            "玩家", "观察员", "训练生", "指挥官", "守护者",
        };
        string[] mid =
        {
            "", "", "的", "小", "超", "阿",
        };

        int a = Random.Range(0, left.Length);
        int b = Random.Range(0, right.Length);
        int m = Random.Range(0, mid.Length);
        int n = Random.Range(10, 999);
        return left[a] + mid[m] + right[b] + n;
    }

    void ConfigureRandomNicknameButtonVisual()
    {
        if (nicknameRandomButton == null)
            return;
        var rt = nicknameRandomButton.GetComponent<RectTransform>();
        if (rt != null)
            rt.sizeDelta = new Vector2(64f, 64f);

        var image = nicknameRandomButton.GetComponent<Image>();
        if (image != null)
        {
            if (_nicknameRandomDiceSprite == null)
                _nicknameRandomDiceSprite = CreateDiceSprite();
            image.sprite = _nicknameRandomDiceSprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.color = Color.white;
        }

        var txt = nicknameRandomButton.GetComponentInChildren<Text>(true);
        if (txt != null)
            txt.gameObject.SetActive(false);
    }

    static Sprite CreateDiceSprite()
    {
        const int size = 96;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        var clear = new Color32(0, 0, 0, 0);
        var white = new Color32(245, 245, 245, 255);
        var black = new Color32(28, 28, 28, 255);
        var border = new Color32(95, 95, 95, 255);
        var px = new Color32[size * size];
        for (int i = 0; i < px.Length; i++)
            px[i] = clear;

        int margin = 7;
        for (int y = margin; y < size - margin; y++)
        for (int x = margin; x < size - margin; x++)
            px[y * size + x] = white;

        for (int y = margin; y < size - margin; y++)
        {
            px[y * size + margin] = border;
            px[y * size + (size - margin - 1)] = border;
        }
        for (int x = margin; x < size - margin; x++)
        {
            px[margin * size + x] = border;
            px[(size - margin - 1) * size + x] = border;
        }

        DrawDiceDot(px, size, size / 2, size / 2, 10, black);
        DrawDiceDot(px, size, 28, 28, 8, black);
        DrawDiceDot(px, size, 68, 68, 8, black);

        tex.SetPixels32(px);
        tex.Apply(false, false);
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 96f);
    }

    static void DrawDiceDot(Color32[] px, int size, int cx, int cy, int radius, Color32 color)
    {
        int r2 = radius * radius;
        for (int y = -radius; y <= radius; y++)
        for (int x = -radius; x <= radius; x++)
        {
            if (x * x + y * y > r2)
                continue;
            int pxX = cx + x;
            int pxY = cy + y;
            if (pxX < 0 || pxX >= size || pxY < 0 || pxY >= size)
                continue;
            px[pxY * size + pxX] = color;
        }
    }

    System.Collections.IEnumerator CoSubmitNicknameAndContinue(string nick)
    {
        bool ok = false;
        string err = "";
        yield return StartCoroutine(serverSync.CoSetMyNickname(
            nick,
            _ => ok = true,
            e => err = e ?? Loc.Choose("保存失败", "Save failed")));

        if (!ok)
        {
            if (nicknameStatusText != null)
                nicknameStatusText.text = Loc.F("保存失败：{0}", "Save failed: {0}", err);
            if (nicknameConfirmButton != null)
                nicknameConfirmButton.interactable = true;
            if (nicknameRandomButton != null)
                nicknameRandomButton.interactable = true;
            yield break;
        }

        if (nicknameStatusText != null)
            nicknameStatusText.text = Loc.Choose("昵称设置成功", "Nickname saved");
        CompleteFirstRegisterFlow();
    }

    System.Collections.IEnumerator CoAutoEnter()
    {
        yield return new WaitForSeconds(autoEnterDelaySeconds);
        if (this == null) yield break;
        EnterWorld();
    }

    void EnterWorld()
    {
        if (ShouldBlockEnterWorldInputThisFrame)
            return;
        if (DetectSwitchAccountKeyThisFrame())
            return;
        if (_phase != Phase.ReadyToEnter)
            return;
        if (NewUserWelcomeTextSlides.IsPlaying)
            return;

        ClientVersionUpdateGate.EnsureExists();
        if (!ClientVersionUpdateGate.IsReady)
        {
            if (debugLog)
                Debug.Log("[MenuLoginPortalFlow] 等待资源热更新完成后再进入游戏。");
            return;
        }

        EnsureServerSyncReference();
        if (serverSync == null)
        {
            Debug.LogWarning("[MenuLoginPortalFlow] 未找到 GameServerSync，无法进入世界。");
            return;
        }

        _phase = Phase.EnteringWorld;
        SetSwitchAccountHintVisible(false);
        HideLoginSuccessBanner(immediate: false);
        if (debugLog)
            Debug.Log($"[MenuLoginPortalFlow] EnterWorld -> IsLoggedIn={(serverSync != null && serverSync.IsLoggedIn)}");
        if (_enterWorldWatchdog != null)
            StopCoroutine(_enterWorldWatchdog);
        _enterWorldWatchdog = StartCoroutine(CoEnterWorldWatchdog());
        serverSync.EnterWorldFromServerSave();
    }

    System.Collections.IEnumerator CoEnterWorldWatchdog()
    {
        float deadline = Time.realtimeSinceStartup + Mathf.Max(1f, enterWorldTimeoutSeconds);
        while (Time.realtimeSinceStartup < deadline)
        {
            // 已离开 LoadingScene，说明成功进入世界，结束看门狗
            if (!IsForceShowLoginScene(SceneManager.GetActiveScene().name))
            {
                _enterWorldWatchdog = null;
                yield break;
            }

            yield return null;
        }

        // 超时仍在 LoadingScene：兜底处理，避免卡死
        if (_phase == Phase.EnteringWorld && IsForceShowLoginScene(SceneManager.GetActiveScene().name))
        {
            if (this == null) yield break;
            if (!string.IsNullOrWhiteSpace(fallbackWorldSceneName))
            {
                var target = fallbackWorldSceneName.Trim();
                if (Application.CanStreamedLevelBeLoaded(target))
                {
                    if (debugLog)
                        Debug.Log("[MenuLoginPortalFlow] EnterWorld timeout -> load fallback scene: " + target);
                    SceneManager.LoadScene(target);
                    _enterWorldWatchdog = null;
                    yield break;
                }
            }

            if (debugLog)
                Debug.LogWarning("[MenuLoginPortalFlow] EnterWorld timeout -> fallback scene not set/invalid, return ReadyToEnter.");
            EnterReadyToEnterPhase();
        }

        _enterWorldWatchdog = null;
    }

    void TryEnforceSingleAudioListenerPeriodic()
    {
        if (Time.unscaledTime < _nextAudioListenerCheckTime)
            return;
        _nextAudioListenerCheckTime = Time.unscaledTime + Mathf.Max(0.2f, audioListenerCheckInterval);

        // 与 TeleportSceneLoadOverlay 的监听器冲突时，会关掉加载视频所需的 Listener，造成断音。
        if (TeleportSceneLoadOverlay.IsShowing)
            return;

        var listeners = Object.FindObjectsOfType<AudioListener>(true);
        if (listeners == null || listeners.Length <= 1)
            return;

        AudioListener keep = null;
        if (dolly != null)
            keep = dolly.GetComponent<AudioListener>();
        if (keep == null && Camera.main != null)
            keep = Camera.main.GetComponent<AudioListener>();
        if (keep == null)
        {
            for (int i = 0; i < listeners.Length; i++)
            {
                if (listeners[i] != null && listeners[i].enabled)
                {
                    keep = listeners[i];
                    break;
                }
            }
        }
        if (keep == null)
            keep = listeners[0];

        for (int i = 0; i < listeners.Length; i++)
        {
            var al = listeners[i];
            if (al == null) continue;
            var g = al.gameObject;
            if (!g.scene.IsValid() || !g.scene.isLoaded) continue;
            al.enabled = al == keep;
        }
    }

    void SyncLoginHintVisibility()
    {
        if (!autoCreateLoginHintUI || _loginHintRoot == null)
            return;
        if (!UiLifecycleSafe.IsSafeToTouch(_loginHintRoot))
            return;

        bool shouldShow = _phase == Phase.IntroCinematic;
        if (_loginHintRoot.activeSelf != shouldShow)
            UiLifecycleSafe.SetActiveIfAlive(_loginHintRoot, shouldShow);
    }

    void SyncEnterHintVisibility()
    {
        if (enterGameHintRoot != null)
        {
            if (!UiLifecycleSafe.IsSafeToTouch(enterGameHintRoot))
                return;
            bool shouldShow = _phase == Phase.ReadyToEnter || _phase == Phase.EnteringWorld;
            if (enterGameHintRoot.activeSelf != shouldShow)
                UiLifecycleSafe.SetActiveIfAlive(enterGameHintRoot, shouldShow);
            return;
        }

        if (!autoCreateEnterHintFallbackUI || _enterHintRoot == null)
            return;
        if (!UiLifecycleSafe.IsSafeToTouch(_enterHintRoot))
            return;

        bool shouldShowFallback = _phase == Phase.ReadyToEnter || _phase == Phase.EnteringWorld;
        if (_enterHintRoot.activeSelf != shouldShowFallback)
            UiLifecycleSafe.SetActiveIfAlive(_enterHintRoot, shouldShowFallback);
    }

    void EnsureEnterHintFallbackUIBuilt()
    {
        if (_enterHintRoot != null) return;

        _enterHintRoot = new GameObject("EnterHintUI_Fallback");
        _enterHintRoot.transform.SetParent(transform, false);

        var canvas = _enterHintRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = enterHintCanvasSortOrder;

        var scaler = _enterHintRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        // 不加 GraphicRaycaster：该提示只负责展示

        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(_enterHintRoot.transform, false);
        _enterHintLabel = labelGo.AddComponent<Text>();
        _enterHintLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _enterHintLabel.text = Loc.Choose(enterHintText, "Click to enter game");
        _enterHintLabel.fontSize = enterHintFontSize;
        _enterHintLabel.alignment = TextAnchor.LowerCenter;
        _enterHintLabel.color = enterHintColor;

        var rt = _enterHintLabel.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, enterHintFromBottomPixels);
        rt.sizeDelta = new Vector2(900f, 120f);

        UiLifecycleSafe.SetActiveIfAlive(_enterHintRoot, false);

        EnsureEnterHintLogoutLineBuilt();
    }

    void EnsureEnterHintLogoutLineBuilt()
    {
        if (_enterHintLogoutLineLabel != null)
            return;
        if (_enterHintRoot == null || _enterHintLabel == null)
            return;

        var logoutLineGo = new GameObject("SwitchAccountHint");
        logoutLineGo.transform.SetParent(_enterHintRoot.transform, false);
        var logoutHit = logoutLineGo.AddComponent<Image>();
        logoutHit.color = new Color(1f, 1f, 1f, 0.001f);
        logoutHit.raycastTarget = true;
        var logoutBtn = logoutLineGo.AddComponent<Button>();
        logoutBtn.targetGraphic = logoutHit;
        logoutBtn.onClick.AddListener(RequestSwitchAccountLogout);

        var logoutLabelGo = new GameObject("Label");
        logoutLabelGo.transform.SetParent(logoutLineGo.transform, false);
        _enterHintLogoutLineLabel = logoutLabelGo.AddComponent<Text>();
        _enterHintLogoutLineLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        ApplySwitchAccountHintLineText(_enterHintLogoutLineLabel);
        _enterHintLogoutLineLabel.fontSize = switchAccountHintFontSize;
        _enterHintLogoutLineLabel.alignment = TextAnchor.LowerCenter;
        _enterHintLogoutLineLabel.color = new Color(enterHintColor.r, enterHintColor.g, enterHintColor.b, enterHintColor.a * 0.85f);
        _enterHintLogoutLineLabel.raycastTarget = false;

        var labelRt = _enterHintLogoutLineLabel.rectTransform;
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = Vector2.zero;
        labelRt.offsetMax = Vector2.zero;

        var rt = logoutLineGo.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, switchAccountHintFromBottomPixels);
        rt.sizeDelta = new Vector2(900f, 80f);

        UiLifecycleSafe.SetActiveIfAlive(logoutLineGo, false);
    }

    void ApplySwitchAccountHintLineText(Text t)
    {
        if (t == null) return;
        t.text = Loc.Choose(switchAccountHintLine, "Switch account / Log out (P)");
    }

    void ResolveSwitchAccountHintTarget()
    {
        if (_resolvedSwitchAccountHint)
            return;

        if (switchAccountHintLabel != null)
        {
            _resolvedSwitchAccountHint = switchAccountHintLabel;
            return;
        }

        if (enterGameHintRoot != null && UiLifecycleSafe.IsSafeToTouch(enterGameHintRoot))
        {
            var tr = enterGameHintRoot.transform.Find("SwitchAccountHint");
            if (tr != null)
                _resolvedSwitchAccountHint = tr.GetComponent<Text>();
        }

        if (_resolvedSwitchAccountHint == null && _enterHintLogoutLineLabel != null)
            _resolvedSwitchAccountHint = _enterHintLogoutLineLabel;
    }

    void SetSwitchAccountHintVisible(bool visible)
    {
        EnsureEnterHintLogoutLineBuilt();
        ResolveSwitchAccountHintTarget();
        if (_resolvedSwitchAccountHint != null)
        {
            ApplySwitchAccountHintLineText(_resolvedSwitchAccountHint);
            UiLifecycleSafe.SetActiveIfAlive(_resolvedSwitchAccountHint.gameObject, visible);
        }
    }

    void EnsureLoginHintUIBuilt()
    {
        if (_loginHintRoot != null)
            return;

        var existing = transform.Find("LoginHintUI");
        if (existing != null)
        {
            _loginHintRoot = existing.gameObject;
            _loginHintLabel = _loginHintRoot.GetComponentInChildren<Text>(true);
            return;
        }

        _loginHintRoot = new GameObject("LoginHintUI");
        _loginHintRoot.transform.SetParent(transform, false);

        var canvas = _loginHintRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = loginHintCanvasSortOrder;

        var scaler = _loginHintRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        // 不加 GraphicRaycaster：尽量保证该提示不会被 EventSystem 当作“点击到 UI”。

        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(_loginHintRoot.transform, false);
        _loginHintLabel = labelGo.AddComponent<Text>();
        _loginHintLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _loginHintLabel.text = Loc.Choose(loginHintText, "Tap anywhere to log in");
        _loginHintLabel.fontSize = loginHintFontSize;
        _loginHintLabel.alignment = TextAnchor.LowerCenter;
        _loginHintLabel.color = loginHintColor;

        var rt = _loginHintLabel.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, loginHintFromBottomPixels);
        rt.sizeDelta = new Vector2(900f, 120f);
    }

    void EnsureNicknameSetupFallbackUIBuilt()
    {
        if (nicknameSetupPanelRoot != null)
            return;

        var root = new GameObject("NicknameSetupUI");
        root.transform.SetParent(transform, false);
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32010;
        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        root.AddComponent<GraphicRaycaster>();

        var panel = new GameObject("Panel");
        panel.transform.SetParent(root.transform, false);
        var panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(760f, 320f);
        var panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.65f);

        var titleGo = new GameObject("Title");
        titleGo.transform.SetParent(panel.transform, false);
        var title = titleGo.AddComponent<Text>();
        title.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        title.text = Loc.Choose("设置昵称", "Set Nickname");
        title.fontSize = 40;
        title.color = Color.white;
        title.alignment = TextAnchor.MiddleCenter;
        var titleRt = title.rectTransform;
        titleRt.anchorMin = new Vector2(0.5f, 1f);
        titleRt.anchorMax = new Vector2(0.5f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.anchoredPosition = new Vector2(0f, -26f);
        titleRt.sizeDelta = new Vector2(560f, 56f);

        var inputGo = new GameObject("NicknameInput");
        inputGo.transform.SetParent(panel.transform, false);
        var inputImage = inputGo.AddComponent<Image>();
        inputImage.color = new Color(1f, 1f, 1f, 0.95f);
        var inputRt = inputGo.GetComponent<RectTransform>();
        inputRt.anchorMin = new Vector2(0.5f, 0.5f);
        inputRt.anchorMax = new Vector2(0.5f, 0.5f);
        inputRt.pivot = new Vector2(0.5f, 0.5f);
        inputRt.anchoredPosition = new Vector2(-44f, 20f);
        inputRt.sizeDelta = new Vector2(440f, 64f);

        var input = inputGo.AddComponent<InputField>();
        var textGo = new GameObject("Text");
        textGo.transform.SetParent(inputGo.transform, false);
        var inputText = textGo.AddComponent<Text>();
        inputText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        inputText.fontSize = 30;
        inputText.color = new Color(0.1f, 0.1f, 0.1f, 1f);
        inputText.alignment = TextAnchor.MiddleLeft;
        var inputTextRt = inputText.rectTransform;
        inputTextRt.anchorMin = Vector2.zero;
        inputTextRt.anchorMax = Vector2.one;
        inputTextRt.offsetMin = new Vector2(16f, 8f);
        inputTextRt.offsetMax = new Vector2(-16f, -8f);

        var placeholderGo = new GameObject("Placeholder");
        placeholderGo.transform.SetParent(inputGo.transform, false);
        var placeholder = placeholderGo.AddComponent<Text>();
        placeholder.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        placeholder.fontSize = 26;
        placeholder.text = Loc.Choose("输入昵称（1-24字符）", "Enter nickname (1–24 chars)");
        placeholder.color = new Color(0.45f, 0.45f, 0.45f, 0.85f);
        placeholder.alignment = TextAnchor.MiddleLeft;
        var placeholderRt = placeholder.rectTransform;
        placeholderRt.anchorMin = Vector2.zero;
        placeholderRt.anchorMax = Vector2.one;
        placeholderRt.offsetMin = new Vector2(16f, 8f);
        placeholderRt.offsetMax = new Vector2(-16f, -8f);

        input.textComponent = inputText;
        input.placeholder = placeholder;
        nicknameInputField = input;

        var statusGo = new GameObject("Status");
        statusGo.transform.SetParent(panel.transform, false);
        var status = statusGo.AddComponent<Text>();
        status.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        status.fontSize = 24;
        status.color = new Color(1f, 0.95f, 0.75f, 0.95f);
        status.alignment = TextAnchor.MiddleCenter;
        status.text = Loc.Choose("请设置你的游戏昵称（仅首次注册需要）", "Set your in-game nickname (first registration only)");
        var statusRt = status.rectTransform;
        statusRt.anchorMin = new Vector2(0.5f, 0.5f);
        statusRt.anchorMax = new Vector2(0.5f, 0.5f);
        statusRt.pivot = new Vector2(0.5f, 0.5f);
        statusRt.anchoredPosition = new Vector2(0f, -44f);
        statusRt.sizeDelta = new Vector2(640f, 46f);
        nicknameStatusText = status;

        var btnGo = new GameObject("ConfirmButton");
        btnGo.transform.SetParent(panel.transform, false);
        var btnImage = btnGo.AddComponent<Image>();
        btnImage.color = new Color(0.1f, 0.72f, 0.36f, 1f);
        var btn = btnGo.AddComponent<Button>();
        var btnRt = btnGo.GetComponent<RectTransform>();
        btnRt.anchorMin = new Vector2(0.5f, 0.5f);
        btnRt.anchorMax = new Vector2(0.5f, 0.5f);
        btnRt.pivot = new Vector2(0.5f, 0.5f);
        btnRt.anchoredPosition = new Vector2(0f, -118f);
        btnRt.sizeDelta = new Vector2(220f, 56f);

        var btnTextGo = new GameObject("Text");
        btnTextGo.transform.SetParent(btnGo.transform, false);
        var btnText = btnTextGo.AddComponent<Text>();
        btnText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        btnText.text = Loc.Choose("确认昵称", "Confirm Nickname");
        btnText.fontSize = 28;
        btnText.color = Color.white;
        btnText.alignment = TextAnchor.MiddleCenter;
        var btnTextRt = btnText.rectTransform;
        btnTextRt.anchorMin = Vector2.zero;
        btnTextRt.anchorMax = Vector2.one;
        btnTextRt.offsetMin = Vector2.zero;
        btnTextRt.offsetMax = Vector2.zero;
        nicknameConfirmButton = btn;

        var randomBtnGo = new GameObject("RandomButton");
        randomBtnGo.transform.SetParent(panel.transform, false);
        var randomBtnImage = randomBtnGo.AddComponent<Image>();
        randomBtnImage.color = Color.white;
        var randomBtn = randomBtnGo.AddComponent<Button>();
        var randomRt = randomBtnGo.GetComponent<RectTransform>();
        randomRt.anchorMin = new Vector2(0.5f, 0.5f);
        randomRt.anchorMax = new Vector2(0.5f, 0.5f);
        randomRt.pivot = new Vector2(0.5f, 0.5f);
        randomRt.anchoredPosition = new Vector2(250f, 20f);
        randomRt.sizeDelta = new Vector2(64f, 64f);

        var randomBtnTextGo = new GameObject("Text");
        randomBtnTextGo.transform.SetParent(randomBtnGo.transform, false);
        var randomBtnText = randomBtnTextGo.AddComponent<Text>();
        randomBtnText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        randomBtnText.text = "";
        randomBtnText.fontSize = 16;
        randomBtnText.color = Color.white;
        randomBtnText.alignment = TextAnchor.MiddleCenter;
        var randomBtnTextRt = randomBtnText.rectTransform;
        randomBtnTextRt.anchorMin = Vector2.zero;
        randomBtnTextRt.anchorMax = Vector2.one;
        randomBtnTextRt.offsetMin = Vector2.zero;
        randomBtnTextRt.offsetMax = Vector2.zero;
        nicknameRandomButton = randomBtn;

        nicknameSetupPanelRoot = root;
        nicknameConfirmButton.onClick.AddListener(OnNicknameConfirmClicked);
        nicknameRandomButton.onClick.AddListener(OnNicknameRandomClicked);
        ConfigureRandomNicknameButtonVisual();
        UiLifecycleSafe.SetActiveIfAlive(nicknameSetupPanelRoot, false);
    }

    [ContextMenu("Create/Refresh Login Hint UI")]
    void CreateOrRefreshLoginHintUI()
    {
        autoCreateLoginHintUI = true;
        EnsureLoginHintUIBuilt();

        if (_loginHintLabel != null)
        {
            _loginHintLabel.text = Loc.Choose(loginHintText, "Tap anywhere to log in");
            _loginHintLabel.fontSize = loginHintFontSize;
            _loginHintLabel.color = loginHintColor;
        }

        UiLifecycleSafe.SetActiveIfAlive(_loginHintRoot, true);
    }

    bool IsForceShowLoginScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName) || forceShowLoginPanelScenes == null)
            return false;

        for (int i = 0; i < forceShowLoginPanelScenes.Length; i++)
        {
            var n = forceShowLoginPanelScenes[i];
            if (!string.IsNullOrEmpty(n) &&
                string.Equals(n.Trim(), sceneName, System.StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    void EnsureCharacterRegisterUiReference()
    {
        if (characterRegisterUI != null)
            return;
        characterRegisterUI = GetComponent<PlayerCharacterRegisterUI>();
        if (characterRegisterUI != null)
            return;
        characterRegisterUI = FindObjectOfType<PlayerCharacterRegisterUI>(true);
        if (characterRegisterUI != null)
            return;
        if (Application.isPlaying)
            characterRegisterUI = gameObject.AddComponent<PlayerCharacterRegisterUI>();
    }

    void EnsureServerSyncReference()
    {
        if (serverSync != null && serverSync == GameServerSync.Instance)
            return;

        serverSync = GameServerSync.Instance;
        if (serverSync != null)
            return;

        serverSync = FindObjectOfType<GameServerSync>(true);
    }

    public void RequestSwitchAccountLogout()
    {
        OnSwitchAccountLogout();
    }

    /// <summary>供 GameServerSync 等常驻对象在 Loading 场景轮询 P（与 Update 逻辑一致）。</summary>
    public static bool TryPollSwitchAccountHotkey()
    {
        var flow = ActiveInstance != null ? ActiveInstance : FindObjectOfType<MenuLoginPortalFlow>(true);
        if (flow == null)
            return false;
        return flow.TryHandleSwitchAccountLogoutKey();
    }

    void OnSwitchAccountLogout()
    {
        if (_lastSwitchAccountLogoutFrame == Time.frameCount)
            return;
        _lastSwitchAccountLogoutFrame = Time.frameCount;
        MarkSwitchAccountHotkeyConsumed();

        if (_validateStoredLoginCoroutine != null)
        {
            StopCoroutine(_validateStoredLoginCoroutine);
            _validateStoredLoginCoroutine = null;
        }

        if (_enterWorldWatchdog != null)
        {
            StopCoroutine(_enterWorldWatchdog);
            _enterWorldWatchdog = null;
        }

        NewUserWelcomeTextSlides.ForceStopIfPlaying();

        EnsureServerSyncReference();
        serverSync?.Logout();

        _phase = Phase.LoginForm;
        _pendingFirstRegisterProfile = false;
        _awaitingWelcomeSlidesAfterRegister = false;
        UiLifecycleSafe.SetActiveIfAlive(enterGameHintRoot, false);
        UiLifecycleSafe.SetActiveIfAlive(_enterHintRoot, false);
        UiLifecycleSafe.SetActiveIfAlive(nicknameSetupPanelRoot, false);
        if (characterRegisterUI != null)
            characterRegisterUI.Hide();
        SetSwitchAccountHintVisible(false);
        HideLoginSuccessBanner();

        EnsureLoginPanelRootResolved();
        EnsureLoginPanelCanvasScale();
        if (loginPanelRoot != null)
        {
            var auth = loginPanelRoot.GetComponent<GameServerAuthUI>();
            if (auth != null)
                auth.RestorePanelAfterClose();
            else
                UiLifecycleSafe.SetActiveIfAlive(loginPanelRoot, true);
        }

        if (UiLifecycleSafe.IsSafeToTouch(loginPanelRoot))
        {
            var cg = loginPanelRoot.GetComponentInChildren<CanvasGroup>(true);
            if (cg != null)
            {
                cg.alpha = 1f;
                cg.interactable = true;
                cg.blocksRaycasts = true;
            }
        }

        if (_isForceShowLoginScene)
        {
            if (tapToOpenLoginButton != null)
                UiLifecycleSafe.SetActiveIfAlive(tapToOpenLoginButton.gameObject, false);
            ForceKeepLoginPanelVisibleInForceScene();
        }
        else if (tapToOpenLoginButton != null)
        {
            UiLifecycleSafe.SetActiveIfAlive(tapToOpenLoginButton.gameObject, true);
        }

        if (dolly != null)
            dolly.SetPaused(false);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        SyncLoginHintVisibility();
        SyncEnterHintVisibility();
    }

    /// <summary>
    /// 登录界面「离线游玩」：不写服务器，进厂仍走 EnterWorld → EnterWorldFromServerSave。
    /// </summary>
    public void RequestOfflinePlay()
    {
        if (_phase != Phase.LoginForm && _phase != Phase.IntroCinematic)
            return;

        EnsureServerSyncReference();
        if (serverSync == null)
        {
            Debug.LogWarning("[MenuLoginPortalFlow] 未找到 GameServerSync，无法离线游玩。");
            return;
        }

        serverSync.BeginOfflineGuestSession(GameServerSync.OfflineDefaultWorldScene);

        ClientVersionUpdateGate.EnsureExists()?.BeginUpdate();

        ReleaseLoginInputFocus();
        EnterReadyToEnterPhase();

        if (debugLog)
            Debug.Log("[MenuLoginPortalFlow] RequestOfflinePlay -> ReadyToEnter (undergarden fallback)");
    }

    static void ReleaseLoginInputFocus()
    {
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }
}
