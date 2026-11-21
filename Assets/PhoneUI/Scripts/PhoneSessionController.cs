using DormMall;
using GameControls;
using GameQuestSystem;
using UnityEngine;
using UnityEngine.UI;

namespace PhoneUI
{
    /// <summary>
    /// 手机会话：菜单开关、移动暂停、光标、热键互斥；可选镜头呈现（Launcher 模式仅 UI）。
    /// </summary>
    public class PhoneSessionController : MonoBehaviour
    {
        public static PhoneSessionController Instance { get; private set; }
        public static bool BlocksGameplayInput => Instance != null && Instance._isOpen;
        public static bool IsPhoneOpen => Instance != null && Instance._isOpen;

        public static bool IsSettingsPageOpen =>
            Instance != null && Instance.IsSettingsOverlayOpen;

        public bool IsSettingsOverlayOpen =>
            _isOpen && _overlayApp == PhoneAppId.Settings
            && settingsPage != null && settingsPage.activeSelf;

        [Header("Panels")]
        public GameObject hudPanel;
        public GameObject phonePanel;
        public GameObject phoneShell;
        public GameObject settingsPage;
        public GameObject keyBindingsPage;

        [Header("Player")]
        public PlayerMovement playerMovement;
        public PhonePresentationController presentation;

        [Header("Grid")]
        public PhoneAppGrid appGrid;

        [Header("输入（参考 InventoryUI）")]
        public KeyCode toggleKey = KeyCode.F1;
        public bool enableToggleHotkey = true;

        [Header("调试")]
        public bool debugLog;

        [Header("Canvas")]
        public int phoneCanvasSortOrder = 3000;

        [Header("Mode")]
        [Tooltip("仅弹出九宫格菜单并跳转各系统 Panel，不切换第三人称镜头。")]
        public bool launcherOnly = false;
        [Tooltip("使用 Scene/Inspector 中手摆的 PhoneFrame 位置与尺寸；不运行时右对齐覆盖。")]
        public bool useManualLayout = false;

        bool _isOpen;
        int _phoneCanvasSortOrderSaved = -1;
        bool _movementPausedBefore;
        CursorLockMode _cursorLockBefore;
        bool _cursorVisibleBefore;
        FirstPersonCamera _fpsCamera;
        bool _fpsCursorLockedBefore;
        bool _fpsSuppressMouseLookBefore;
        bool _playerMovementEnabledBefore = true;
        bool _wasThirdPersonBeforePhoneUi;
        CameraSwitcher _menuCameraSwitcher;
        bool _notifyOpenSettingsQuestOnLauncherReady;
        bool _openSettingsWhenReady;
        bool _useMenuCameraForThisOpen;
        bool _endingPresentationForSettings;
        int _phoneLayoutFramesLeft;
        PhoneAppId _overlayApp = PhoneAppId.None;
        float _overlaySyncNextTime;

        public static bool IsConfigured(PhoneSessionController c) =>
            c != null && c.phonePanel != null && c.phoneShell != null;

        public static void ClaimInstance(PhoneSessionController candidate)
        {
            if (candidate == null || !IsConfigured(candidate))
                return;
            if (Instance != null && Instance != candidate && IsConfigured(Instance))
                return;
            if (Instance != null && Instance != candidate)
                Destroy(Instance);
            Instance = candidate;
        }

        public static PhoneSessionController FindBestInScene()
        {
            var all = Object.FindObjectsOfType<PhoneSessionController>(true);
            PhoneSessionController best = null;
            for (int i = 0; i < all.Length; i++)
            {
                var c = all[i];
                if (c == null || !IsConfigured(c))
                    continue;
                if (best == null)
                {
                    best = c;
                    continue;
                }
                if (best.phoneShell == null && c.phoneShell != null)
                    best = c;
            }
            return best;
        }

        void Awake()
        {
            if (!IsConfigured(this))
            {
                enabled = false;
                return;
            }

            if (Instance != null && Instance != this)
            {
                if (!IsConfigured(Instance))
                {
                    Destroy(Instance);
                    Instance = this;
                    return;
                }

                Debug.LogWarning("[PhoneSessionController] 重复实例，销毁: " + name);
                Destroy(this);
                return;
            }

            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        void OnEnable() => Loc.OnLanguageChanged += OnLanguageChanged;
        void OnDisable()
        {
            Loc.OnLanguageChanged -= OnLanguageChanged;
            if (!_isOpen)
                return;

            _isOpen = false;
            RestoreInputBlock();
            if (!launcherOnly && presentation != null && !_useMenuCameraForThisOpen && presentation.IsPresentationActive)
                presentation.EndPresentation(null);
        }

        void OnLanguageChanged()
        {
            PhoneUiLauncherSlots.Apply(this);
        }

        void Start()
        {
            if (presentation == null)
                presentation = GetComponent<PhonePresentationController>();
            if (presentation == null)
                presentation = GetComponentInChildren<PhonePresentationController>(true);

            ApplyDefaultPanelState();
            PhoneUiVisualRefresh.ApplyToSession(this);
        }

        void LateUpdate()
        {
            if (_isOpen && _overlayApp == PhoneAppId.None)
                EnsurePhoneUiCursor();

            if (_phoneLayoutFramesLeft <= 0 || !_isOpen || phoneShell == null || !phoneShell.activeSelf)
                return;
            _phoneLayoutFramesLeft--;
            PhoneUiLayout.ApplyLauncherHudLayout(this);
        }

        void Update()
        {
            if (_isOpen)
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    var questUi = Object.FindObjectOfType<QuestSystemUI>(true);
                    if (questUi != null && questUi.TryHandleEscapeClose())
                        return;

                    if (TryCloseOverlayAndShowLauncher())
                        return;
                    ClosePhone();
                    return;
                }

                SyncOverlayClosedExternally();
            }

            if (!enableToggleHotkey || !IsConfigured(this))
                return;
            if (!GameInput.GetKeyDown(GameControlAction.TogglePhone))
                return;

            if (_isOpen)
            {
                if (_overlayApp != PhoneAppId.None && TryCloseOverlayAndShowLauncher())
                    return;
                ClosePhone();
                if (debugLog)
                    Debug.Log($"[PhoneUI] 按下 {toggleKey}，手机已关闭");
                return;
            }

            if (!CanOpenPhoneFromHotkey())
                return;

            OpenPhone();
            if (debugLog)
                Debug.Log($"[PhoneUI] 按下 {toggleKey}，手机已打开");
        }

        public bool HasOverlayOpen => _overlayApp != PhoneAppId.None;

        public void EnterOverlay(PhoneAppId app)
        {
            _overlayApp = app;
            if (phoneShell != null)
                phoneShell.SetActive(false);
            if (settingsPage != null && app != PhoneAppId.Settings)
                settingsPage.SetActive(false);
            if (keyBindingsPage != null && app != PhoneAppId.Custom1)
                keyBindingsPage.SetActive(false);
        }

        public void ShowLauncher()
        {
            _overlayApp = PhoneAppId.None;
            if (settingsPage != null)
                settingsPage.SetActive(false);
            if (keyBindingsPage != null)
                keyBindingsPage.SetActive(false);
            if (phoneShell != null)
            {
                phoneShell.SetActive(true);
                PhoneUiLayout.ApplyLauncherHudLayout(this);
                PhoneUiChromeView.Ensure(this);
                PhoneUiLayout.ApplyLauncherHudLayout(this);
            }

            EnsurePhoneUiCursor();
        }

        /// <summary>手机仍打开时需保持光标解锁（子界面 ESC 返回后 Inventory/Quest 等会误恢复锁定）。</summary>
        public void EnsurePhoneUiCursor()
        {
            if (!_isOpen)
                return;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (_fpsCamera == null)
                _fpsCamera = FindFirstPersonCameraForMenuUi();
            if (_fpsCamera != null)
            {
                _fpsCamera.SetCursorLocked(false);
                _fpsCamera.suppressMouseLook = true;
            }
        }

        public bool TryCloseOverlayAndShowLauncher()
        {
            if (_overlayApp == PhoneAppId.None)
                return false;

            switch (_overlayApp)
            {
                case PhoneAppId.Settings:
                    if (settingsPage != null && settingsPage.activeSelf)
                    {
                        CloseSettingsPage();
                        return true;
                    }
                    break;
                case PhoneAppId.Map:
                {
                    var map = Object.FindObjectOfType<MapSystem>(true);
                    if (map != null && map.IsMapWindowOpen)
                    {
                        map.HideMap();
                        ShowLauncher();
                        return true;
                    }
                    break;
                }
                case PhoneAppId.Quest:
                {
                    var questUi = Object.FindObjectOfType<QuestSystemUI>(true);
                    if (questUi != null && questUi.IsFullPanelOpen)
                    {
                        questUi.CloseFullPanelFromPhone();
                        ShowLauncher();
                        return true;
                    }
                    break;
                }
                case PhoneAppId.Shop:
                    if (DormMallUI.IsAnyMainShopPanelOpenInHierarchy())
                    {
                        var malls = Object.FindObjectsOfType<DormMallUI>(true);
                        for (int i = 0; i < malls.Length; i++)
                        {
                            if (malls[i] != null && malls[i].gameObject.activeSelf)
                                malls[i].gameObject.SetActive(false);
                        }
                        ShowLauncher();
                        return true;
                    }
                    break;
                case PhoneAppId.Inventory:
                    if (InventoryUI.IsAnyInventoryPanelOpen())
                    {
                        InventoryUI.Instance.Close();
                        ShowLauncher();
                        return true;
                    }
                    break;
                case PhoneAppId.Multiplayer:
                {
                    var room = MultiplayerRoomCodePanelController.Instance
                               ?? Object.FindObjectOfType<MultiplayerRoomCodePanelController>(true);
                    if (room != null && room.IsPanelOpen)
                    {
                        room.ClosePanel();
                        ShowLauncher();
                        return true;
                    }
                    break;
                }
                case PhoneAppId.Account:
                    PhoneAccountPanelView.Hide();
                    ShowLauncher();
                    return true;
                case PhoneAppId.Custom1:
                    if (keyBindingsPage != null && keyBindingsPage.activeSelf)
                    {
                        CloseKeyBindingsPage();
                        return true;
                    }
                    break;
            }

            _overlayApp = PhoneAppId.None;
            ShowLauncher();
            return true;
        }

        void SyncOverlayClosedExternally()
        {
            if (_overlayApp == PhoneAppId.None)
                return;
            if (Time.unscaledTime < _overlaySyncNextTime)
                return;
            _overlaySyncNextTime = Time.unscaledTime + 0.2f;
            if (!IsOverlayStillVisible(_overlayApp))
                ShowLauncher();
        }

        bool IsOverlayStillVisible(PhoneAppId app)
        {
            switch (app)
            {
                case PhoneAppId.Settings:
                    return settingsPage != null && settingsPage.activeSelf;
                case PhoneAppId.Map:
                {
                    var map = Object.FindObjectOfType<MapSystem>(true);
                    return map != null && map.IsMapWindowOpen;
                }
                case PhoneAppId.Quest:
                {
                    var questUi = Object.FindObjectOfType<QuestSystemUI>(true);
                    return questUi != null && questUi.IsFullPanelOpen;
                }
                case PhoneAppId.Shop:
                    return DormMallUI.IsAnyMainShopPanelOpenInHierarchy();
                case PhoneAppId.Inventory:
                    return InventoryUI.IsAnyInventoryPanelOpen();
                case PhoneAppId.Multiplayer:
                {
                    var room = MultiplayerRoomCodePanelController.Instance
                               ?? Object.FindObjectOfType<MultiplayerRoomCodePanelController>(true);
                    return room != null && room.IsPanelOpen;
                }
                case PhoneAppId.Custom1:
                    return keyBindingsPage != null && keyBindingsPage.activeSelf;
                default:
                    return false;
            }
        }

        bool CanOpenPhoneFromHotkey()
        {
            if (GameServerSync.BlocksInputForServerSpawn)
                return false;
            if (GameplayUiHotkeyGuard.ShouldBlockGameplayHotkeys())
                return false;
            return true;
        }

        public void TogglePhone()
        {
            if (_isOpen)
                ClosePhone();
            else if (CanOpenPhoneFromHotkey())
                OpenPhone();
        }

        /// <summary>HUD 左上角设置按钮：只打开手机壳 + 九宫格，不进入旧版全屏 SettingPage。</summary>
        public void OpenPhoneLauncherFromHud()
        {
            _notifyOpenSettingsQuestOnLauncherReady = true;
            OpenPhone();
        }

        public void OpenPhoneToSettings()
        {
            if (TryOpenSettings())
                return;

            if (TryOpenLegacySettingsPanel())
                return;
        }

        /// <summary>打开设置页；无 settingsPage 时尝试旧版画质/Curve 面板。</summary>
        public bool TryOpenSettings()
        {
            if (settingsPage == null)
                return false;

            if (_isOpen)
            {
                OpenSettingsPage();
                return true;
            }

            _openSettingsWhenReady = true;
            _useMenuCameraForThisOpen = true;
            OpenPhone();
            return true;
        }

        static bool TryOpenLegacySettingsPanel()
        {
            var qc = Object.FindObjectOfType<QualityController>(true);
            if (qc != null && qc.qualityPanel != null)
            {
                qc.ToggleQualityPanel();
                NotifyOpenSettingsQuest();
                return true;
            }

            var curve = Object.FindObjectOfType<CurvePanelController>(true);
            if (curve != null && curve.curvePanel != null)
            {
                curve.ToggleCurvePanel();
                NotifyOpenSettingsQuest();
                return true;
            }

            return false;
        }

        public void OpenPhone()
        {
            if (_isOpen) return;
            GameplayUiHotkeyGuard.CloseOtherExclusivePanels(GameplayUiHotkeyGuard.ExclusivePanelKind.Phone);
            _isOpen = true;
            _overlayApp = PhoneAppId.None;

            EnsureHudPanelReference();
            EnsureCanvasRootsVisible();

            if (hudPanel != null) hudPanel.SetActive(false);
            PhoneUiGameplayHudHider.SetSuppressed(true);
            if (phonePanel != null) phonePanel.SetActive(true);
            if (settingsPage != null) settingsPage.SetActive(false);
            if (keyBindingsPage != null) keyBindingsPage.SetActive(false);
            if (!launcherOnly && phoneShell != null)
                phoneShell.SetActive(false);
            BumpPhoneCanvasSortOrder(true);

            PrepareMenuUiCameraBeforeOpen();
            CacheAndApplyInputBlock();

            if (!launcherOnly && presentation != null && !_useMenuCameraForThisOpen)
                presentation.BeginPresentation(OnPresentationReady);
            else
                OnPresentationReady();
        }

        void OnPresentationReady()
        {
            if (!_isOpen) return;
            PhoneUiTmpInputFix.FixUnder(phonePanel);
            if (phoneShell != null)
            {
                phoneShell.SetActive(true);
                if (settingsPage != null)
                    settingsPage.SetActive(false);
                PhoneUiLayout.ApplyLauncherHudLayout(this);
                PhoneUiChromeView.Ensure(this);
                PhoneUiLayout.ApplyLauncherHudLayout(this);
                PhoneUiVisualRefresh.ApplyToSession(this);
                _phoneLayoutFramesLeft = 2;
                if (_notifyOpenSettingsQuestOnLauncherReady)
                {
                    _notifyOpenSettingsQuestOnLauncherReady = false;
                    NotifyOpenSettingsQuest();
                }
            }

            if (_openSettingsWhenReady)
            {
                _openSettingsWhenReady = false;
                if (!OpenSettingsPageAfterReady())
                    RestoreInputBlock();
            }
        }

        bool OpenSettingsPageAfterReady()
        {
            if (settingsPage != null)
            {
                OpenSettingsPage();
                return true;
            }

            if (TryOpenLegacySettingsPanel())
            {
                if (_isOpen)
                    FinishCloseAfterPresentation();
                return true;
            }

            Debug.LogWarning("[PhoneSessionController] 无法打开设置：settingsPage 未配置。");
            return false;
        }

        public void ClosePhone()
        {
            if (!_isOpen) return;

            if (!launcherOnly && presentation != null && !_useMenuCameraForThisOpen && presentation.IsPresentationActive)
                presentation.EndPresentation(FinishCloseAfterPresentation);
            else
                FinishCloseAfterPresentation();
        }

        void FinishCloseAfterPresentation()
        {
            ForceCloseOverlaySilently();
            _isOpen = false;
            RestoreGameplayPanels();
            RestoreInputBlock();
        }

        void ForceCloseOverlaySilently()
        {
            if (_overlayApp == PhoneAppId.Map)
            {
                var map = Object.FindObjectOfType<MapSystem>(true);
                if (map != null && map.IsMapWindowOpen)
                    map.HideMap();
            }
            else if (_overlayApp == PhoneAppId.Quest)
            {
                var questUi = Object.FindObjectOfType<QuestSystemUI>(true);
                if (questUi != null && questUi.IsFullPanelOpen)
                    questUi.CloseFullPanelFromPhone();
            }
            else if (_overlayApp == PhoneAppId.Inventory && InventoryUI.Instance != null)
                InventoryUI.Instance.Close();
            else if (_overlayApp == PhoneAppId.Multiplayer)
            {
                var room = MultiplayerRoomCodePanelController.Instance
                           ?? Object.FindObjectOfType<MultiplayerRoomCodePanelController>(true);
                if (room != null && room.IsPanelOpen)
                    room.ClosePanel();
            }
            else if (_overlayApp == PhoneAppId.Account)
                PhoneAccountPanelView.Hide();
            else if (_overlayApp == PhoneAppId.Custom1)
                CloseKeyBindingsPage();
            else if (_overlayApp == PhoneAppId.Shop)
            {
                var malls = Object.FindObjectsOfType<DormMallUI>(true);
                for (int i = 0; i < malls.Length; i++)
                {
                    if (malls[i] != null && malls[i].gameObject.activeSelf)
                        malls[i].gameObject.SetActive(false);
                }
            }

            if (settingsPage != null)
                settingsPage.SetActive(false);
            if (keyBindingsPage != null)
                keyBindingsPage.SetActive(false);
            _overlayApp = PhoneAppId.None;
        }

        void ForceCloseUiAndInput()
        {
            if (!launcherOnly && presentation != null && !_useMenuCameraForThisOpen && presentation.IsPresentationActive)
                presentation.EndPresentation(FinishCloseAfterPresentation);
            else
                FinishCloseAfterPresentation();
        }

        void RestoreGameplayPanels()
        {
            if (phoneShell != null) phoneShell.SetActive(false);
            if (settingsPage != null) settingsPage.SetActive(false);
            if (keyBindingsPage != null) keyBindingsPage.SetActive(false);
            if (appGrid != null) appGrid.ResetAllSlots();
            if (phonePanel != null) phonePanel.SetActive(false);

            EnsureCanvasRootsVisible();
            if (hudPanel != null)
                hudPanel.SetActive(true);

            PhoneUiGameplayHudHider.SetSuppressed(false);
            BumpPhoneCanvasSortOrder(false);
        }

        void ApplyDefaultPanelState()
        {
            EnsureCanvasRootsVisible();
            if (hudPanel != null) hudPanel.SetActive(true);
            if (phonePanel != null) phonePanel.SetActive(false);
            if (phoneShell != null) phoneShell.SetActive(false);
            if (settingsPage != null) settingsPage.SetActive(false);
            if (keyBindingsPage != null) keyBindingsPage.SetActive(false);
        }

        public void EnsureHudPanelReference()
        {
            if (hudPanel != null)
                return;

            var mgr = GetComponent<PhoneUIManager>();
            if (mgr == null)
                mgr = FindObjectOfType<PhoneUIManager>(true);
            if (mgr != null && mgr.hudPanel != null)
                hudPanel = mgr.hudPanel;
        }

        void EnsureCanvasRootsVisible()
        {
            var cv = phonePanel != null
                ? phonePanel.GetComponentInParent<Canvas>(true)
                : hudPanel != null ? hudPanel.GetComponentInParent<Canvas>(true) : null;
            if (cv == null)
                return;

            if (!cv.gameObject.activeInHierarchy && cv.gameObject.activeSelf == false)
                cv.gameObject.SetActive(true);

            var rt = cv.GetComponent<RectTransform>();
            if (rt != null && rt.localScale.sqrMagnitude < 0.001f)
                rt.localScale = Vector3.one;
        }

        public void HandleAppSlotClick(PhoneAppSlot slot)
        {
            if (slot == null) return;

            switch (slot.appId)
            {
                case PhoneAppId.Settings:
                    OpenSettingsPage();
                    break;
                case PhoneAppId.Custom1:
                    OpenKeyBindingsPage();
                    break;
                default:
                    if (!PhoneAppRouter.TryOpenApp(slot.appId, this))
                        Debug.Log(Loc.F("[PhoneUI] App 未处理: {0}", "[PhoneUI] App not handled: {0}", slot.appId));
                    break;
            }
        }

        public void OpenSettingsPage()
        {
            if (!_useMenuCameraForThisOpen && !_endingPresentationForSettings
                && !launcherOnly && presentation != null && presentation.IsPresentationActive)
            {
                _endingPresentationForSettings = true;
                presentation.EndPresentation(() =>
                {
                    _endingPresentationForSettings = false;
                    if (!_isOpen)
                        return;
                    _useMenuCameraForThisOpen = true;
                    PrepareMenuUiCameraBeforeOpen();
                    EnsurePhoneUiCursor();
                    OpenSettingsPage();
                });
                return;
            }

            EnterOverlay(PhoneAppId.Settings);
            PhoneUiGameplayHudHider.EnsureHiddenForSettingsOverlay();
            if (settingsPage == null)
            {
                if (TryOpenLegacySettingsPanel())
                    return;
                Debug.LogWarning("[PhoneSessionController] settingsPage 未配置，无法打开设置页。");
                return;
            }

            settingsPage.SetActive(true);
            settingsPage.transform.SetAsLastSibling();
            PhoneSettingsPanelView.Ensure(this)?.RefreshBindings();

            NotifyOpenSettingsQuest();
        }

        static void NotifyOpenSettingsQuest()
        {
            QuestSystemManager.Instance?.ReportEvent("open_settings");
        }

        public void CloseSettingsPage()
        {
            if (settingsPage != null)
                settingsPage.SetActive(false);
            if (_isOpen)
                ShowLauncher();
        }

        public void EnsureKeyBindingsPage()
        {
            if (keyBindingsPage != null)
                return;
            if (phonePanel == null)
                return;

            keyBindingsPage = new GameObject("KeyBindingsPage", typeof(RectTransform), typeof(CanvasRenderer));
            var rt = keyBindingsPage.GetComponent<RectTransform>();
            rt.SetParent(phonePanel.transform, false);
            StretchFullRect(rt);
            keyBindingsPage.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            keyBindingsPage.SetActive(false);
        }

        public void OpenKeyBindingsPage()
        {
            EnterOverlay(PhoneAppId.Custom1);
            PhoneUiGameplayHudHider.EnsureHiddenForSettingsOverlay();
            EnsureKeyBindingsPage();
            if (keyBindingsPage != null)
            {
                keyBindingsPage.SetActive(true);
                keyBindingsPage.transform.SetAsLastSibling();
                PhoneKeyBindingPanelView.Ensure(this)?.Show();
            }
        }

        public void CloseKeyBindingsPage()
        {
            var view = keyBindingsPage != null ? keyBindingsPage.GetComponent<PhoneKeyBindingPanelView>() : null;
            view?.Hide();
            if (keyBindingsPage != null)
                keyBindingsPage.SetActive(false);
            GameInput.SetKeyBindingsPanelOpen(false);
            if (_isOpen)
                ShowLauncher();
        }

        static void StretchFullRect(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        void BumpPhoneCanvasSortOrder(bool phoneOpen)
        {
            var cv = phonePanel != null
                ? phonePanel.GetComponentInParent<Canvas>(true)
                : GetComponentInParent<Canvas>(true);
            if (cv == null)
                return;

            if (phoneOpen)
            {
                if (_phoneCanvasSortOrderSaved < 0)
                    _phoneCanvasSortOrderSaved = cv.sortingOrder;
                cv.sortingOrder = Mathf.Max(cv.sortingOrder, phoneCanvasSortOrder);
            }
            else if (_phoneCanvasSortOrderSaved >= 0)
            {
                cv.sortingOrder = _phoneCanvasSortOrderSaved;
                _phoneCanvasSortOrderSaved = -1;
            }
        }

        /// <summary>第三人称下打开设置前先切第一人称（与按 V 同步轨道角）；Presentation 自拍模式由镜头脚本自行处理。</summary>
        void PrepareMenuUiCameraBeforeOpen()
        {
            if (!launcherOnly && presentation != null && !_useMenuCameraForThisOpen)
                return;

            _menuCameraSwitcher = Object.FindObjectOfType<CameraSwitcher>(true);
            _wasThirdPersonBeforePhoneUi = _menuCameraSwitcher != null
                && _menuCameraSwitcher.IsThirdPersonGameplayActive;

            if (_wasThirdPersonBeforePhoneUi)
                _menuCameraSwitcher.SwitchToFirstPersonForMenuUi();
        }

        void RestoreMenuUiCameraAfterClose()
        {
            if (!_wasThirdPersonBeforePhoneUi || _menuCameraSwitcher == null)
                return;

            _menuCameraSwitcher.RestoreThirdPersonAfterMenuUi();
            _menuCameraSwitcher.RestoreThirdPersonMouseLookIfActive();
        }

        void CacheAndApplyInputBlock()
        {
            _cursorLockBefore = Cursor.lockState;
            _cursorVisibleBefore = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            _fpsCamera = FindFirstPersonCameraForMenuUi();
            if (_fpsCamera != null)
            {
                _fpsCursorLockedBefore = _fpsCamera.IsCursorLocked;
                _fpsSuppressMouseLookBefore = _fpsCamera.suppressMouseLook;
                _fpsCamera.SetCursorLocked(false);
                _fpsCamera.suppressMouseLook = true;
            }

            if (playerMovement != null)
            {
                _playerMovementEnabledBefore = playerMovement.enabled;
                _movementPausedBefore = playerMovement.isMovementPaused;
                playerMovement.enabled = true;
                playerMovement.isMovementPaused = true;
            }
        }

        void RestoreInputBlock()
        {
            bool returningToThirdPerson = _wasThirdPersonBeforePhoneUi || IsCurrentlyThirdPersonGameplay();

            if (_fpsCamera != null)
            {
                if (!returningToThirdPerson)
                {
                    _fpsCamera.SetCursorLocked(_fpsCursorLockedBefore);
                    _fpsCamera.suppressMouseLook = _fpsSuppressMouseLookBefore;
                    if (_fpsCamera.IsActive && _fpsCursorLockedBefore)
                        GameplayCursorLock.LockForGameplayView();
                }
                else
                {
                    _fpsCamera.suppressMouseLook = false;
                }

                _fpsCamera = null;
            }

            RestoreMenuUiCameraAfterClose();

            if (returningToThirdPerson)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                GameplayMenuCameraHelper.RestoreThirdPersonAfterPhoneUi();
            }
            else
            {
                Cursor.lockState = _cursorLockBefore;
                Cursor.visible = _cursorVisibleBefore;
            }

            if (playerMovement != null)
            {
                playerMovement.isMovementPaused = _movementPausedBefore;
                playerMovement.enabled = _playerMovementEnabledBefore;
                if (!returningToThirdPerson)
                    playerMovement.ApplyWalkingFirstPersonCameraBinding();
            }

            _wasThirdPersonBeforePhoneUi = false;
            _menuCameraSwitcher = null;
            _useMenuCameraForThisOpen = false;
            _openSettingsWhenReady = false;
            _endingPresentationForSettings = false;

            if (!_isOpen)
                GameplayCameraInputRestorer.RestoreNow();
        }

        static bool IsCurrentlyThirdPersonGameplay()
        {
            var switchers = Object.FindObjectsOfType<CameraSwitcher>(true);
            for (int i = 0; i < switchers.Length; i++)
            {
                if (switchers[i] != null && switchers[i].IsThirdPersonGameplayActive)
                    return true;
            }
            return false;
        }

        static FirstPersonCamera FindFirstPersonCameraForMenuUi()
        {
            var all = Object.FindObjectsOfType<FirstPersonCamera>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].IsActive)
                    return all[i];
            }

            return all.Length > 0 ? all[0] : null;
        }
    }
}
