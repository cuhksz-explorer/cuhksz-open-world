using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 账号登录 UI：参考弹窗布局（LGU EXPLORER 顶栏 + 进入游戏 + 注册/忘记密码 + 协议勾选）。
/// </summary>
public class GameServerAuthUI : MonoBehaviour
{
    public enum AuthPage
    {
        Login,
        Register,
    }

    [Header("服务器")]
    [SerializeField] GameServerSync serverSync;

    [Header("LGUHUB 双页")]
    [SerializeField] bool useLguHubTwoPageLayout = true;
    [SerializeField] GameObject loginPageRoot;
    [SerializeField] GameObject registerPageRoot;
    [SerializeField] TMP_InputField usernameInput;
    [SerializeField] TMP_InputField passwordInput;
    [SerializeField] TMP_InputField registerUsernameInput;
    [SerializeField] TMP_InputField registerPasswordInput;
    [SerializeField] Button loginButton;
    [SerializeField] Button registerSubmitButton;
    [SerializeField] Button goToRegisterButton;
    [SerializeField] Button backToLoginButton;
    [SerializeField] Button closePanelButton;
    [SerializeField] Button forgotPasswordButton;
    [SerializeField] Button offlinePlayButton;
    [SerializeField] Button mobileQuickLoginPlaceholder;
    [SerializeField] Toggle agreementToggle;
    [SerializeField] Button userAgreementLink;
    [SerializeField] Button privacyPolicyLink;
    [SerializeField] LguHubAgreementLineClick agreementLineClick;
    [SerializeField] GameObject panelFooterRoot;
    [SerializeField] TMP_Text statusText;
    [SerializeField] TMP_Text registerStatusText;

    [Header("兼容旧版单页")]
    [SerializeField] Button registerButton;
    [SerializeField] Button logoutButton;

    [Header("LoadingScene")]
    [SerializeField] bool hideLogoutButton = true;

    AuthPage _page = AuthPage.Login;
    bool _lguHubLayoutReady;
    bool _hiddenByCloseButton;

    public bool IsHiddenByCloseButton => _hiddenByCloseButton;

    void Awake()
    {
        EnsureServerSyncReference();
        EnsureLguHubLayout(forceRebuild: false);
        RefreshButtonBindings();
        GameServerSync.OnAuthSuccess += OnAuthSuccess;
        GameServerSync.OnAuthFailure += OnAuthFailure;
    }

    void OnEnable()
    {
        Loc.OnLanguageChanged += OnLanguageChanged;
        EnsureServerSyncReference();
        EnsureLguHubLayout(forceRebuild: false);
        EnsureOfflinePlayButton();
        LguHubAuthUILayout.RefreshLocalizedTexts(transform);
        UiCanvasOverlayFix.EnsureOverlayCanvases(transform);
        if (loginPageRoot != null)
            ShowPage(AuthPage.Login);
        SyncFooterVisibility();
        SyncLogoutVisibility();
        RefreshButtonBindings();
    }

    void OnDisable()
    {
        Loc.OnLanguageChanged -= OnLanguageChanged;
    }

    void OnDestroy()
    {
        Loc.OnLanguageChanged -= OnLanguageChanged;
        GameServerSync.OnAuthSuccess -= OnAuthSuccess;
        GameServerSync.OnAuthFailure -= OnAuthFailure;
        UnwireButtons();
    }

    void OnLanguageChanged()
    {
        LguHubAuthUILayout.RefreshLocalizedTexts(transform);
        RefreshStatus();
    }

    void Start()
    {
        RefreshStatus();
        SyncLogoutVisibility();
    }

    void Update()
    {
        if (!gameObject.activeInHierarchy || _hiddenByCloseButton)
            return;

        if (!MenuLoginPortalFlow.IsSwitchAccountKeyPressedThisFrame())
            return;

        var flow = MenuLoginPortalFlow.ActiveInstance != null
            ? MenuLoginPortalFlow.ActiveInstance
            : Object.FindObjectOfType<MenuLoginPortalFlow>(true);
        if (flow != null)
        {
            if (flow.TryHandleSwitchAccountLogoutKey())
                return;
        }

        EnsureServerSyncReference();
        if (serverSync != null && serverSync.IsLoggedIn)
            OnLogoutClicked();
    }

    public void ConfigureLguHubPages(
        GameServerSync sync,
        GameObject loginPage,
        GameObject registerPage,
        TMP_InputField loginUser,
        TMP_InputField loginPass,
        TMP_InputField regUser,
        TMP_InputField regPass,
        Button loginBtn,
        Button registerBtn,
        Button goRegisterLink,
        Button backLoginLink,
        TMP_Text loginStatus,
        TMP_Text regStatus,
        TMP_Text _loginTitle,
        TMP_Text _registerTitle)
    {
        var loginUi = new LguHubAuthUILayout.BuiltLoginUi
        {
            usernameInput = loginUser,
            passwordInput = loginPass,
            enterGameButton = loginBtn,
            goToRegisterButton = goRegisterLink,
            statusText = loginStatus
        };
        var regUi = new LguHubAuthUILayout.BuiltRegisterUi
        {
            usernameInput = regUser,
            passwordInput = regPass,
            registerButton = registerBtn,
            backToLoginButton = backLoginLink,
            statusText = regStatus
        };
        ConfigureReferenceLayout(sync, loginPage, registerPage, loginUi, regUi, _loginTitle);
    }

    public void ConfigureReferenceLayout(
        GameServerSync sync,
        GameObject loginPage,
        GameObject registerPage,
        LguHubAuthUILayout.BuiltLoginUi loginUi,
        LguHubAuthUILayout.BuiltRegisterUi regUi,
        TMP_Text _brandTitle)
    {
        serverSync = sync;
        loginPageRoot = loginPage;
        registerPageRoot = registerPage;
        usernameInput = loginUi.usernameInput;
        passwordInput = loginUi.passwordInput;
        loginButton = loginUi.enterGameButton;
        goToRegisterButton = loginUi.goToRegisterButton;
        forgotPasswordButton = loginUi.forgotPasswordButton;
        offlinePlayButton = loginUi.offlinePlayButton;
        closePanelButton = loginUi.closePanelButton;
        mobileQuickLoginPlaceholder = loginUi.mobileQuickLoginPlaceholder;
        agreementToggle = loginUi.agreementToggle;
        agreementLineClick = loginUi.agreementLineClick;
        userAgreementLink = loginUi.userAgreementLink;
        privacyPolicyLink = loginUi.privacyPolicyLink;
        statusText = loginUi.statusText;

        registerUsernameInput = regUi.usernameInput;
        registerPasswordInput = regUi.passwordInput;
        registerSubmitButton = regUi.registerButton;
        backToLoginButton = regUi.backToLoginButton;
        registerStatusText = regUi.statusText;

        panelFooterRoot = transform.Find("PanelFooter")?.gameObject;

        registerButton = null;
        logoutButton = null;

        RefreshButtonBindings();
        ShowPage(AuthPage.Login);
    }

    void RefreshButtonBindings()
    {
        UnwireButtons();
        WireButtons();
        SyncEnterButtonWithAgreement();
    }

    void WireButtons()
    {
        RebindLoginUiReferencesIfNeeded();

        if (loginButton != null)
            loginButton.onClick.AddListener(OnLoginClicked);
        if (registerSubmitButton != null)
            registerSubmitButton.onClick.AddListener(OnRegisterClicked);
        if (goToRegisterButton != null)
            goToRegisterButton.onClick.AddListener(() => ShowPage(AuthPage.Register));
        if (backToLoginButton != null)
            backToLoginButton.onClick.AddListener(() => ShowPage(AuthPage.Login));
        if (closePanelButton != null)
            closePanelButton.onClick.AddListener(OnClosePanelClicked);
        if (forgotPasswordButton != null)
            forgotPasswordButton.onClick.AddListener(OnForgotPasswordClicked);
        if (offlinePlayButton != null)
            offlinePlayButton.onClick.AddListener(OnOfflinePlayClicked);
        if (agreementToggle != null)
            agreementToggle.onValueChanged.AddListener(OnAgreementToggleChanged);
        if (userAgreementLink != null)
            userAgreementLink.onClick.AddListener(OnUserAgreementLinkClicked);
        if (privacyPolicyLink != null)
            privacyPolicyLink.onClick.AddListener(OnPrivacyPolicyLinkClicked);
        if (agreementLineClick != null)
            agreementLineClick.LinkClicked += OnAgreementLineLinkClicked;

        if (registerButton != null)
            registerButton.onClick.AddListener(OnRegisterClicked);
        if (logoutButton != null)
            logoutButton.onClick.AddListener(OnLogoutClicked);
    }

    void UnwireButtons()
    {
        if (loginButton != null)
            loginButton.onClick.RemoveListener(OnLoginClicked);
        if (registerSubmitButton != null)
            registerSubmitButton.onClick.RemoveListener(OnRegisterClicked);
        if (goToRegisterButton != null)
            goToRegisterButton.onClick.RemoveAllListeners();
        if (backToLoginButton != null)
            backToLoginButton.onClick.RemoveAllListeners();
        if (closePanelButton != null)
            closePanelButton.onClick.RemoveListener(OnClosePanelClicked);
        if (forgotPasswordButton != null)
            forgotPasswordButton.onClick.RemoveListener(OnForgotPasswordClicked);
        if (offlinePlayButton != null)
            offlinePlayButton.onClick.RemoveListener(OnOfflinePlayClicked);
        if (agreementToggle != null)
            agreementToggle.onValueChanged.RemoveListener(OnAgreementToggleChanged);
        if (userAgreementLink != null)
            userAgreementLink.onClick.RemoveListener(OnUserAgreementLinkClicked);
        if (privacyPolicyLink != null)
            privacyPolicyLink.onClick.RemoveListener(OnPrivacyPolicyLinkClicked);
        if (agreementLineClick != null)
            agreementLineClick.LinkClicked -= OnAgreementLineLinkClicked;
        if (registerButton != null)
            registerButton.onClick.RemoveListener(OnRegisterClicked);
        if (logoutButton != null)
            logoutButton.onClick.RemoveListener(OnLogoutClicked);
    }

    public void ShowPage(AuthPage page)
    {
        _page = page;
        if (loginPageRoot != null)
            loginPageRoot.SetActive(page == AuthPage.Login);
        if (registerPageRoot != null)
            registerPageRoot.SetActive(page == AuthPage.Register);
        SyncFooterVisibility();
        ClearStatus();
    }

    void SyncFooterVisibility()
    {
        if (panelFooterRoot != null)
            panelFooterRoot.SetActive(_page == AuthPage.Login);
    }

    public void HideForCharacterSetup()
    {
        gameObject.SetActive(false);
    }

    public void OnClosePanelClicked()
    {
        _hiddenByCloseButton = true;
        gameObject.SetActive(false);
    }

    public void RestorePanelAfterClose()
    {
        _hiddenByCloseButton = false;
        gameObject.SetActive(true);
        if (loginPageRoot != null)
            ShowPage(AuthPage.Login);

        var cg = GetComponent<CanvasGroup>();
        if (cg != null)
        {
            cg.alpha = 1f;
            cg.interactable = true;
            cg.blocksRaycasts = true;
        }
    }

    void OnForgotPasswordClicked()
    {
        SetStatus(Loc.Choose("忘记密码功能即将开放", "Forgot password coming soon"), false);
    }

    void OnOfflinePlayClicked()
    {
        var flow = MenuLoginPortalFlow.ActiveInstance != null
            ? MenuLoginPortalFlow.ActiveInstance
            : Object.FindObjectOfType<MenuLoginPortalFlow>(true);
        if (flow == null)
        {
            SetStatus(Loc.Choose("未找到登录流程", "Login flow not found"), false);
            return;
        }

        SetStatus(Loc.Choose("离线模式：准备进入…", "Offline mode: preparing…"), false);
        flow.RequestOfflinePlay();
    }

    void OnAgreementToggleChanged(bool _)
    {
        SyncEnterButtonWithAgreement();
        if (agreementToggle != null && agreementToggle.isOn)
            ClearStatus();
    }

    void SyncEnterButtonWithAgreement()
    {
        if (loginButton == null)
            return;
        bool agreed = agreementToggle == null || agreementToggle.isOn;
        loginButton.interactable = agreed;

        if (loginButton.targetGraphic is Image img)
        {
            img.color = agreed
                ? LguHubAuthUILayout.PrimaryButtonBg
                : new Color(0.55f, 0.55f, 0.55f, 1f);
        }
    }

    void OnUserAgreementLinkClicked()
    {
        LguHubAuthLegalDialog.Show(
            transform,
            LguHubLegalDocuments.UserAgreementTitle,
            LguHubLegalDocuments.UserAgreementBody);
    }

    void OnPrivacyPolicyLinkClicked()
    {
        LguHubAuthLegalDialog.Show(
            transform,
            LguHubLegalDocuments.PrivacyPolicyTitle,
            LguHubLegalDocuments.PrivacyPolicyBody);
    }

    void OnAgreementLineLinkClicked(string linkId)
    {
        if (string.Equals(linkId, "user", System.StringComparison.OrdinalIgnoreCase))
            OnUserAgreementLinkClicked();
        else if (string.Equals(linkId, "privacy", System.StringComparison.OrdinalIgnoreCase))
            OnPrivacyPolicyLinkClicked();
    }

    void OnRegisterClicked()
    {
        EnsureServerSyncReference();
        if (serverSync == null)
        {
            SetStatus(Loc.Choose("未找到 GameServerSync", "GameServerSync not found"), true);
            return;
        }

        var u = GetUsernameForPage(AuthPage.Register);
        var p = GetPasswordForPage(AuthPage.Register);
        if (string.IsNullOrEmpty(u) || string.IsNullOrEmpty(p))
        {
            SetStatus(Loc.Choose("请输入账号和密码", "Please enter username and password"), true);
            return;
        }

        SetStatus(Loc.Choose("注册中…", "Registering…"), true);
        serverSync.Register(u, p);
    }

    void OnLoginClicked()
    {
        EnsureServerSyncReference();
        if (serverSync == null)
        {
            SetStatus(Loc.Choose("未找到 GameServerSync", "GameServerSync not found"), false);
            return;
        }

        if (agreementToggle == null || !agreementToggle.isOn)
        {
            SetStatus(Loc.Choose("请先勾选同意用户协议与隐私政策", "Please agree to the Terms and Privacy Policy"), false);
            return;
        }

        var u = GetUsernameForPage(AuthPage.Login);
        var p = GetPasswordForPage(AuthPage.Login);
        if (string.IsNullOrEmpty(u) || string.IsNullOrEmpty(p))
        {
            SetStatus(Loc.Choose("请输入账号和密码", "Please enter username and password"), false);
            return;
        }

        SetStatus(Loc.Choose("登录中…", "Logging in…"), false);
        serverSync.Login(u, p);
    }

    void OnLogoutClicked()
    {
        var flow = Object.FindObjectOfType<MenuLoginPortalFlow>(true);
        if (flow != null)
        {
            flow.RequestSwitchAccountLogout();
            return;
        }

        EnsureServerSyncReference();
        serverSync?.Logout();
        SetStatus(Loc.Choose("已登出", "Logged out"), false);
    }

    void OnAuthSuccess(string username, string token)
    {
        ReleaseInputFieldFocus();
        SetStatus(Loc.F("已登录：{0}", "Logged in: {0}", username), _page == AuthPage.Register);
    }

    void ReleaseInputFieldFocus()
    {
        if (usernameInput != null)
            usernameInput.DeactivateInputField();
        if (passwordInput != null)
            passwordInput.DeactivateInputField();
        if (registerUsernameInput != null)
            registerUsernameInput.DeactivateInputField();
        if (registerPasswordInput != null)
            registerPasswordInput.DeactivateInputField();
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

    void OnAuthFailure(string message)
    {
        SetStatus(Loc.F("失败：{0}", "Failed: {0}", message), _page == AuthPage.Register);
    }

    string GetUsernameForPage(AuthPage page)
    {
        if (page == AuthPage.Register && registerUsernameInput != null)
            return registerUsernameInput.text.Trim();
        return usernameInput != null ? usernameInput.text.Trim() : "";
    }

    string GetPasswordForPage(AuthPage page)
    {
        if (page == AuthPage.Register && registerPasswordInput != null)
            return registerPasswordInput.text;
        return passwordInput != null ? passwordInput.text : "";
    }

    void SetStatus(string msg, bool onRegisterPage)
    {
        if (onRegisterPage)
        {
            if (registerStatusText != null)
                registerStatusText.text = msg;
        }
        else if (statusText != null)
            statusText.text = msg;

        Debug.Log($"[GameServerAuthUI] {msg}");
    }

    void ClearStatus()
    {
        if (statusText != null)
            statusText.text = "";
        if (registerStatusText != null)
            registerStatusText.text = "";
    }

    void RefreshStatus()
    {
        EnsureServerSyncReference();
        if (serverSync == null) return;
        if (serverSync.IsLoggedIn)
            SetStatus(Loc.F("已登录（本地）：{0}", "Logged in (local): {0}", serverSync.SavedUsername), false);
    }

    void SyncLogoutVisibility()
    {
        if (logoutButton == null) return;
        logoutButton.gameObject.SetActive(!hideLogoutButton);
    }

    void EnsureServerSyncReference()
    {
        if (serverSync != null) return;
        serverSync = GameServerSync.Instance;
        if (serverSync != null) return;
        serverSync = FindObjectOfType<GameServerSync>(true);
    }

    void RebindLoginUiReferencesIfNeeded()
    {
        if (loginPageRoot == null)
            loginPageRoot = transform.Find("LoginPage")?.gameObject;
        if (registerPageRoot == null)
            registerPageRoot = transform.Find("RegisterPage")?.gameObject;

        var ui = new LguHubAuthUILayout.BuiltLoginUi
        {
            usernameInput = usernameInput,
            passwordInput = passwordInput,
            enterGameButton = loginButton,
            goToRegisterButton = goToRegisterButton,
            forgotPasswordButton = forgotPasswordButton,
            offlinePlayButton = offlinePlayButton,
            closePanelButton = closePanelButton,
            mobileQuickLoginPlaceholder = mobileQuickLoginPlaceholder,
            agreementToggle = agreementToggle,
            agreementLineClick = agreementLineClick,
            userAgreementLink = userAgreementLink,
            privacyPolicyLink = privacyPolicyLink,
            statusText = statusText,
            brandTitle = null
        };
        LguHubAuthUILayout.TryResolveLoginUiReferences(transform, ref ui);

        usernameInput = ui.usernameInput;
        passwordInput = ui.passwordInput;
        loginButton = ui.enterGameButton;
        goToRegisterButton = ui.goToRegisterButton;
        forgotPasswordButton = ui.forgotPasswordButton;
        offlinePlayButton = ui.offlinePlayButton;
        closePanelButton = ui.closePanelButton;
        mobileQuickLoginPlaceholder = ui.mobileQuickLoginPlaceholder;
        agreementToggle = ui.agreementToggle;
        agreementLineClick = ui.agreementLineClick;
        userAgreementLink = ui.userAgreementLink;
        privacyPolicyLink = ui.privacyPolicyLink;
        statusText = ui.statusText;

        if (ui.brandTitle != null)
            ui.brandTitle.text = LguHubAuthUILayout.BrandTitleText;

        EnsureOfflinePlayButton();
    }

    void EnsureOfflinePlayButton()
    {
        if (offlinePlayButton != null)
            return;

        var loginPage = loginPageRoot != null ? loginPageRoot.transform : transform.Find("LoginPage");
        if (loginPage == null)
            return;

        offlinePlayButton = LguHubAuthUILayout.FindDeep(loginPage, "Btn_OfflinePlay")?.GetComponent<Button>();
        if (offlinePlayButton == null)
            offlinePlayButton = LguHubAuthUILayout.EnsureOfflinePlayButton(loginPage);

        if (offlinePlayButton != null)
        {
            offlinePlayButton.onClick.RemoveListener(OnOfflinePlayClicked);
            offlinePlayButton.onClick.AddListener(OnOfflinePlayClicked);
        }
    }

    void EnsureLguHubLayout(bool forceRebuild = false)
    {
        if (!useLguHubTwoPageLayout)
            return;

        LguHubAuthUILayout.PruneStrayPanelChildren(transform);
        LguHubAuthUILayout.PruneDuplicatePages(transform, loginPageRoot, registerPageRoot);

        if (LguHubAuthUILayout.CountAuthPageChildren(transform) > 2)
        {
            LguHubAuthUILayout.ClearAllPanelChildren(transform);
            _lguHubLayoutReady = false;
        }

        var loginPageT = transform.Find("LoginPage");
        if (loginPageT != null)
        {
            if (loginPageT.Find("Box_BelowLogin") != null || loginPageT.Find("Title") != null)
                _lguHubLayoutReady = false;
            if (LguHubAuthUILayout.FindDeep(loginPageT, "AgreementLine") == null)
                _lguHubLayoutReady = false;
            if (LguHubAuthUILayout.FindDeep(loginPageT, "UsernameField/BorderLeft") == null)
                _lguHubLayoutReady = false;
            var checkmark = LguHubAuthUILayout.FindDeep(loginPageT, "AgreementToggle/CircleBackground/Checkmark");
            if (checkmark == null || checkmark.GetComponent<Image>() == null)
                _lguHubLayoutReady = false;
        }

        if (transform.Find("PanelHeader") == null)
            _lguHubLayoutReady = false;

        if (!forceRebuild && _lguHubLayoutReady && IsLguHubLayoutBoundAndValid())
        {
            ApplyCenteredPanelRect();
            return;
        }

        if (!forceRebuild && IsLguHubLayoutBoundAndValid())
        {
            _lguHubLayoutReady = true;
            ApplyCenteredPanelRect();
            return;
        }

        LguHubAuthUILayout.ApplyToPanel(transform, this, serverSync);
        _lguHubLayoutReady = true;
        _hiddenByCloseButton = false;
    }

    bool IsLguHubLayoutBoundAndValid()
    {
        if (loginPageRoot == null || registerPageRoot == null
            || usernameInput == null || passwordInput == null || loginButton == null)
            return false;

        if (loginPageRoot.transform.parent != transform
            || registerPageRoot.transform.parent != transform)
            return false;

        return LguHubAuthUILayout.HasValidLguHubPageLayout(transform);
    }

    void ApplyCenteredPanelRect()
    {
        var rt = GetComponent<RectTransform>();
        if (rt != null)
            LguHubAuthUILayout.ApplyReferenceModalPanel(rt);
    }
}
