using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using Mirror;
using Multiplayer;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 按 <b>K</b> 打开：公开房间号创建/分享/禁用 + 加入（HTTP + Mirror）。
/// </summary>
[DisallowMultipleComponent]
public class MultiplayerRoomCodePanelController : MonoBehaviour
{
    public static MultiplayerRoomCodePanelController Instance { get; private set; }

    const string TmpFontPath = "Fonts & Materials/AlibabaPuHuiTi-3-75-SemiBold SDF";

    [SerializeField] GameObject panelRoot;
    [SerializeField] bool ensureRuntimeCanvas = true;

    [Tooltip("优先使用；否则 Resources/CoopPlayer")]
    [SerializeField] CoopNetworkPlayer coopPlayerPrefab;

    [SerializeField] ushort mirrorPort = 7777;

    [Header("自动生成 UI 时的面板布局")]
    [Tooltip("开：面板按屏幕边距拉伸，适配分辨率；关：使用下方固定 Size Delta 居中")]
    [SerializeField] bool runtimePanelStretchToScreen = true;
    [Tooltip("相对屏幕宽/高的边距比例 (0~0.45)。X=左右各留空，Y=上下各留空")]
    [SerializeField] Vector2 runtimePanelScreenInset = new Vector2(0.08f, 0.1f);
    [Tooltip("仅当 runtimePanelStretchToScreen 为关时：面板像素尺寸（画布缩放后）")]
    [SerializeField] Vector2 runtimePanelSizeDelta = new Vector2(400, 420);
    [Tooltip("仅当拉伸关闭时：面板 anchoredPosition")]
    [SerializeField] Vector2 runtimePanelAnchoredPosition = Vector2.zero;
    [Tooltip("与下方排版基准一致。字号/间距按可用区域相对此参考的 min 比例缩放")]
    [SerializeField] Vector2 runtimePanelLayoutReferenceSize = new Vector2(400, 420);
    [SerializeField] [Range(26f, 56f)] float runtimeButtonMinHeight = 38f;
    [SerializeField] [Range(8, 28)] int runtimePanelPadding = 14;
    [SerializeField] [Range(4f, 18f)] float runtimePanelSpacing = 8f;

    [Header("自动生成 UI：字体与关闭键（基准字号，会再乘面板缩放 _layoutScale）")]
    [SerializeField] float runtimeFontHeaderTitle = 14f;
    [Tooltip("标题字下方额外留白（基准 pt，参与 Ls）")]
    [SerializeField] float runtimeFontHeaderTitlePadBelow = 2f;
    [SerializeField] float runtimeFontSubtitle = 14f;
    [SerializeField] float runtimeFontStatus = 14f;
    [SerializeField] float runtimeFontSectionHint = 15f;
    [SerializeField] float runtimeFontCode = 18f;
    [Tooltip("房间号一行：行高下限（基准 px，再 LsInt）")]
    [SerializeField] float runtimeCodeRowMinHeight = 46f;
    [Tooltip("房间号行相对主按钮高度 bh 的附加高度（基准 px）")]
    [SerializeField] float runtimeCodeRowHeightExtra = 10f;
    [SerializeField] float runtimeFontButton = 15f;
    [SerializeField] float runtimeFontButtonMain = 16f;
    [SerializeField] float runtimeFontInput = 16f;
    [SerializeField] float runtimeFontIpNote = 13f;
    [SerializeField] float runtimeFontCloseX = 15f;
    [Tooltip("关闭键正方形边长（基准 px，再 LsInt）；不参与横向 Layout 拉伸")]
    [SerializeField] [Range(18f, 36f)] float runtimeCloseButtonSize = 24f;
    [Tooltip("关闭键距标题栏右内边（基准 px）")]
    [SerializeField] float runtimeHeaderCloseMargin = 6f;
    [Tooltip("标题文字距左内边（基准 px）")]
    [SerializeField] float runtimeHeaderTitleMarginLeft = 6f;
    [Tooltip("房间号占位文字行内额外高度（基准 pt）")]
    [SerializeField] float runtimeFontCodePadBelow = 5f;

    [Header("打开时暂隐其它 Screen Space UI")]
    [SerializeField] bool hideOtherScreenSpaceCanvasesWhenOpen = true;
    [SerializeField] GameObject[] extraRootsToHideWhenPanelOpen;

    MultiplayerApiClient _api;
    Coroutine _hostRegistryHeartbeat;
    bool _roomActionInProgress;

    string _myPublicRoomCode = "";

    /// <summary>仅在 <see cref="BuildRuntimeUi"/> 构建期间使用，用于同比缩放字号与控件高度。</summary>
    float _layoutScale = 1f;

    float Ls(float v) => v * _layoutScale;

    int LsInt(float v) => Mathf.Max(1, Mathf.RoundToInt(v * _layoutScale));

    float LsFont(float basePoints) => Mathf.Max(9f, basePoints * _layoutScale);

    /// <summary>供侧栏在好友联机当主机时绑定公开房续期（若已生成）。</summary>
    public string ActivePublicRoomCode => _myPublicRoomCode;

    TextMeshProUGUI _codeText;
    TextMeshProUGUI _statusText;
    TMP_InputField _joinInput;
    TMP_InputField _advertisedHostIpInput;

    TextMeshProUGUI _generateDisableToggleLabel;
    Image _generateDisableToggleImage;

    TextMeshProUGUI _joinLeaveToggleLabel;
    Image _joinLeaveToggleImage;

    static readonly Color ToggleGenerateBg = MultiplayerUiPalette.ToggleGenerateBg;
    static readonly Color ToggleDisableBg = MultiplayerUiPalette.ToggleDisableBg;
    static readonly Color JoinLeaveJoinBg = MultiplayerUiPalette.JoinLeaveJoinBg;
    static readonly Color JoinLeaveLeaveBg = MultiplayerUiPalette.JoinLeaveLeaveBg;

    bool _statusSuccessBlink;

    readonly Dictionary<GameObject, bool> _savedRootActive = new Dictionary<GameObject, bool>();
    bool _hidOtherUi;
    Transform _panelDefaultParent;
    bool _panelAttachedToPhone;

    public bool IsPanelOpen => panelRoot != null && panelRoot.activeSelf;

    static Canvas GetOutermostCanvas(Transform t)
    {
        if (t == null) return null;
        Canvas best = null;
        for (Transform p = t; p != null;)
        {
            if (p == null) break;
            Canvas c = null;
            try { c = p.GetComponent<Canvas>(); }
            catch (MissingReferenceException) { break; }
            if (c != null) best = c;
            Transform next = null;
            try { next = p.parent; }
            catch (MissingReferenceException) { break; }
            p = next;
        }
        return best;
    }

    /// <summary>房间号面板打开时屏蔽行走与视角，避免误操作。</summary>
    public static bool BlocksGameplayInput
    {
        get
        {
            if (Instance != null && Instance.IsPanelOpen) return true;
            var p = UnityEngine.Object.FindObjectOfType<MultiplayerRoomCodePanelController>(true);
            return p != null && p.IsPanelOpen;
        }
    }

    void Awake()
    {
        _api = GetComponent<MultiplayerApiClient>() ?? gameObject.AddComponent<MultiplayerApiClient>();
        EnsureCoopPlayerPrefab();
        if (panelRoot != null && !IsLikelyRoomCodePanelRoot(panelRoot))
        {
            Debug.LogWarning(
                "[联机] Inspector 中 panelRoot 不是房间号面板，已忽略并改用运行时 UI: " + panelRoot.name);
            panelRoot = null;
        }

        if (panelRoot == null && ensureRuntimeCanvas)
            BuildRuntimeUi();
        else if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    void EnsureCoopPlayerPrefab()
    {
        if (coopPlayerPrefab != null)
            return;
        var go = Resources.Load<GameObject>("CoopPlayer");
        if (go == null)
        {
            Debug.LogWarning("[联机] 未找到 Resources/CoopPlayer。请执行 Tools/联机/Create Coop Player Prefab。");
            return;
        }
        coopPlayerPrefab = go.GetComponent<CoopNetworkPlayer>();
        if (coopPlayerPrefab == null)
            Debug.LogWarning("[联机] CoopPlayer 已加载但缺少 CoopNetworkPlayer 组件（脚本引用可能损坏）。请执行 Tools/联机/Create Coop Player Prefab 重新生成。");
    }

    static bool IsLikelyRoomCodePanelRoot(GameObject go)
    {
        if (go == null)
            return false;
        if (go.name.IndexOf("RoomCode", StringComparison.OrdinalIgnoreCase) >= 0)
            return true;
        return go.GetComponentInChildren<ScrollRect>(true) != null;
    }

    void OnEnable()
    {
        Instance = this;
        GameServerSync.OnAuthSuccess += OnAuthChanged;
        GameServerSync.OnLoggedOut += OnAuthLoggedOut;
        Loc.OnLanguageChanged += OnLanguageChanged;
    }

    void OnDisable()
    {
        Loc.OnLanguageChanged -= OnLanguageChanged;
        if (ReferenceEquals(Instance, this))
            Instance = null;
        GameServerSync.OnAuthSuccess -= OnAuthChanged;
        GameServerSync.OnLoggedOut -= OnAuthLoggedOut;
        RestoreOtherUi();
    }

    void OnLanguageChanged()
    {
        RefreshGenerateDisableToggleUi();
        RefreshJoinLeaveToggleUi();
    }

    void OnAuthChanged(string _, string __)
    {
        ResetLocalRoomSessionState();
    }

    void OnAuthLoggedOut()
    {
        ResetLocalRoomSessionState();
    }

    void ResetLocalRoomSessionState()
    {
        if (_hostRegistryHeartbeat != null)
        {
            StopCoroutine(_hostRegistryHeartbeat);
            _hostRegistryHeartbeat = null;
        }
        _roomActionInProgress = false;
        _myPublicRoomCode = "";
        RefreshCodeDisplayAfterExternalStop();
        if (LanCoopNetworkManager.Instance != null && CoopSessionState.InSession)
            LanCoopNetworkManager.Instance.StopCoop();
        RefreshJoinLeaveToggleUi();
    }

    void Update()
    {
        if (!IsPanelOpen)
            return;

        if (Input.GetKeyDown(KeyCode.Escape) && PhoneUI.PhoneSessionController.IsPhoneOpen)
        {
            var session = PhoneUI.PhoneSessionController.Instance;
            if (session != null && session.TryCloseOverlayAndShowLauncher())
                return;
        }
    }

    void LateUpdate()
    {
        if (!IsPanelOpen) return;
        try
        {
            HideOtherUi();
        }
        catch (MissingReferenceException)
        {
            // 场景切换/对象销毁同帧内，某些 Canvas/Transform 可能失效；忽略本帧，下一帧会重新收敛。
        }
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        RefreshStatusSuccessBlinkColor();
    }

    public void TogglePanel()
    {
        if (panelRoot == null) return;
        bool show = !panelRoot.activeSelf;
        if (show)
        {
            BumpPanelCanvasSortOrder();
            HideOtherUi();
        }
        panelRoot.SetActive(show);
        if (!show)
            RestoreOtherUi();
        else
        {
            AutoFillAdvertisedAddressIfEmpty();
            RefreshGenerateDisableToggleUi();
            RefreshJoinLeaveToggleUi();
        }
    }

    public void OpenPanel()
    {
        if (panelRoot == null) return;
        BumpPanelCanvasSortOrder();
        AttachPanelToPhoneOverlayIfNeeded();
        if (!PhoneUI.PhoneSessionController.IsPhoneOpen)
            HideOtherUi();
        panelRoot.SetActive(true);
        AutoFillAdvertisedAddressIfEmpty();
        RefreshGenerateDisableToggleUi();
        RefreshJoinLeaveToggleUi();
    }

    void BumpPanelCanvasSortOrder()
    {
        if (panelRoot == null) return;
        var cv = GetOutermostCanvas(panelRoot.transform);
        if (cv != null)
            cv.sortingOrder = Mathf.Max(cv.sortingOrder, 2700);
    }

    public void ClosePanel()
    {
        if (panelRoot == null) return;
        panelRoot.SetActive(false);
        DetachPanelFromPhoneOverlay();
        RestoreOtherUi();
    }

    void AttachPanelToPhoneOverlayIfNeeded()
    {
        if (!PhoneUI.PhoneSessionController.IsPhoneOpen)
            return;

        PhoneUI.PhoneUiOverlayLayout.ApplyMultiplayerPanelForPhone(panelRoot, ref _panelDefaultParent);
        _panelAttachedToPhone = _panelDefaultParent != null;
    }

    void DetachPanelFromPhoneOverlay()
    {
        if (!_panelAttachedToPhone)
            return;

        PhoneUI.PhoneUiOverlayLayout.RestoreMultiplayerPanelParent(panelRoot, _panelDefaultParent);
        _panelAttachedToPhone = false;
    }

    void UiClosePanel()
    {
        if (PhoneUI.PhoneSessionController.IsPhoneOpen)
        {
            var session = PhoneUI.PhoneSessionController.Instance;
            if (session != null && session.TryCloseOverlayAndShowLauncher())
                return;
        }

        ClosePanel();
    }

    public void RefreshCodeDisplayAfterExternalStop()
    {
        if (_codeText != null)
            _codeText.text = string.IsNullOrEmpty(_myPublicRoomCode) ? "——" : _myPublicRoomCode;
        RefreshGenerateDisableToggleUi();
    }

    public IEnumerator CoClosePublicRoomIfAny(System.Action onDone)
    {
        if (string.IsNullOrEmpty(_myPublicRoomCode))
        {
            onDone?.Invoke();
            yield break;
        }
        string code = _myPublicRoomCode;
        yield return _api.CoPublicRoomClose(code, () => { }, e => Debug.LogWarning(e));
        _myPublicRoomCode = "";
        RefreshCodeDisplayAfterExternalStop();
        onDone?.Invoke();
    }

    void HideOtherUi()
    {
        if (_hidOtherUi || panelRoot == null) return;
        _savedRootActive.Clear();
        Canvas myRoot = null;
        try { myRoot = panelRoot != null ? GetOutermostCanvas(panelRoot.transform) : null; }
        catch (MissingReferenceException) { return; }
        if (extraRootsToHideWhenPanelOpen != null)
        {
            foreach (var go in extraRootsToHideWhenPanelOpen)
                RememberAndHide(go);
        }
        if (hideOtherScreenSpaceCanvasesWhenOpen && myRoot != null)
        {
            foreach (var c in FindObjectsOfType<Canvas>(true))
            {
                if (c == null) continue;
                if (c.renderMode == RenderMode.WorldSpace) continue;
                Canvas theirRoot = null;
                try { theirRoot = GetOutermostCanvas(c.transform); }
                catch (MissingReferenceException) { continue; }
                if (theirRoot == myRoot) continue;
                if (theirRoot != null &&
                    string.Equals(theirRoot.gameObject.name, "FriendChatSidebarCanvas", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (PhoneUI.PhoneSessionController.IsPhoneOpen && theirRoot != null &&
                    theirRoot.GetComponentInChildren<PhoneUI.PhoneSessionController>(true) != null)
                    continue;
                RememberAndHide(c.gameObject);
            }
        }
        _hidOtherUi = _savedRootActive.Count > 0;
    }

    void RememberAndHide(GameObject go)
    {
        if (go == null || _savedRootActive.ContainsKey(go)) return;
        try
        {
            if (!go.scene.IsValid() || !go.scene.isLoaded)
                return;
            _savedRootActive[go] = go.activeSelf;
            go.SetActive(false);
        }
        catch (MissingReferenceException)
        {
            // 对象在记录/隐藏前被销毁，忽略即可。
        }
    }

    void RestoreOtherUi()
    {
        if (!_hidOtherUi) return;
        _hidOtherUi = false;
        foreach (var kv in new List<KeyValuePair<GameObject, bool>>(_savedRootActive))
        {
            try
            {
                if (kv.Key != null && kv.Key.scene.IsValid() && kv.Key.scene.isLoaded)
                    kv.Key.SetActive(kv.Value);
            }
            catch (MissingReferenceException)
            {
                // 销毁对象无需恢复。
            }
        }
        _savedRootActive.Clear();
    }

    void SetStatus(string msg)
    {
        if (_statusText != null)
        {
            _statusText.text = msg;
            _statusSuccessBlink = MultiplayerUiPalette.IsCoopSuccessStatus(msg);
            if (!_statusSuccessBlink)
                _statusText.color = MultiplayerUiPalette.TextBlack;
        }
        if (!string.IsNullOrEmpty(msg))
            Debug.Log("[房间号] " + msg);
    }

    void RefreshStatusSuccessBlinkColor()
    {
        if (_statusText == null) return;
        if (!_statusSuccessBlink && MultiplayerUiPalette.IsCoopSuccessStatus(_statusText.text))
            _statusSuccessBlink = true;
        if (!_statusSuccessBlink) return;
        _statusText.color = MultiplayerUiPalette.GetSuccessBlinkColor(Time.unscaledTime);
    }

    public void UiGenerateRoomCode()
    {
        if (_roomActionInProgress)
        {
            SetStatus(Loc.Choose("房间操作进行中，请稍候…", "Room operation in progress, please wait…"));
            return;
        }
        if (!_api.IsReady)
        {
            SetStatus(Loc.Choose("请先登录终端服务器", "Please log in to the game server first"));
            return;
        }
        if (NetworkServer.active || NetworkClient.active)
        {
            SetStatus(Loc.Choose("当前已存在联机会话，请先禁用代码后再生成新房间", "A co-op session already exists — disable the code first"));
            RefreshGenerateDisableToggleUi();
            RefreshJoinLeaveToggleUi();
            return;
        }
        if (CoopSessionState.InSession)
        {
            SetStatus(Loc.Choose("已在联机中，请点击同一按钮「禁用代码」结束当前会话", "Already in co-op — click Disable Code to end the session"));
            return;
        }
        EnsureNetworkManager();
        if (LanCoopNetworkManager.Instance == null)
        {
            SetStatus("LanCoopNetworkManager 未就绪");
            return;
        }
        EnsureCoopPlayerPrefab();
        if (coopPlayerPrefab == null)
        {
            SetStatus(Loc.Choose("缺少 Coop 玩家预制体：Tools/联机/Create Coop Player Prefab", "Missing Coop player prefab — run Tools/Co-op/Create Coop Player Prefab"));
            return;
        }
        if (!IsPortAvailable(mirrorPort))
        {
            SetStatus($"端口 {mirrorPort} 正在被占用，请先关闭占用进程或更换端口");
            _roomActionInProgress = false;
            return;
        }
        _roomActionInProgress = true;
        StartCoroutine(CoGenerateRoomCodeAndStartHost());
    }

    IEnumerator CoGenerateRoomCodeAndStartHost()
    {
        CoopPublicRoomCreateResponse r = null;
        string err = null;
        yield return _api.CoPublicRoomCreate(resp => r = resp, e => err = e);
        if (!string.IsNullOrEmpty(err) || r == null || string.IsNullOrEmpty(r.room_code))
        {
            SetStatus(string.IsNullOrEmpty(err) ? "创建房间失败" : err);
            RefreshGenerateDisableToggleUi();
            RefreshJoinLeaveToggleUi();
            _roomActionInProgress = false;
            yield break;
        }
        _myPublicRoomCode = r.room_code;
        if (_codeText != null)
            _codeText.text = _myPublicRoomCode;
        RefreshGenerateDisableToggleUi();
        SetStatus(Loc.Choose("房间代码已生成，正在启动主机并绑定…", "Room code created — starting host…"));
        yield return CoStartHostWithPublicRoom();
    }

    public void UiShareCopy()
    {
        if (string.IsNullOrEmpty(_myPublicRoomCode))
        {
            SetStatus(Loc.Choose("请先生成房间代码", "Generate a room code first"));
            return;
        }
        GUIUtility.systemCopyBuffer = _myPublicRoomCode;
        SetStatus(Loc.F("已复制到剪贴板：{0}", "Copied to clipboard: {0}", _myPublicRoomCode));
    }

    public void UiPasteJoinCode()
    {
        if (_joinInput == null) return;
        string clip = GUIUtility.systemCopyBuffer;
        if (string.IsNullOrEmpty(clip))
        {
            SetStatus(Loc.Choose("剪贴板为空", "Clipboard is empty"));
            return;
        }
        _joinInput.text = clip.Trim().ToUpperInvariant();
    }

    public void UiDisableCode()
    {
        if (_roomActionInProgress)
        {
            SetStatus(Loc.Choose("房间操作进行中，请稍候…", "Room operation in progress, please wait…"));
            return;
        }
        StartCoroutine(CoShutdownCoopAndPublicRoom("已停止联机并禁用房间代码"));
    }

    public void UiStartHostAndBind()
    {
        if (_roomActionInProgress)
        {
            SetStatus(Loc.Choose("房间操作进行中，请稍候…", "Room operation in progress, please wait…"));
            return;
        }
        EnsureNetworkManager();
        if (LanCoopNetworkManager.Instance == null)
        {
            SetStatus("LanCoopNetworkManager 未就绪");
            return;
        }
        if (string.IsNullOrEmpty(_myPublicRoomCode))
        {
            SetStatus(Loc.Choose("请先生成房间代码", "Generate a room code first"));
            return;
        }
        if (coopPlayerPrefab == null)
        {
            SetStatus(Loc.Choose("缺少 Coop 玩家预制体：Tools/联机/Create Coop Player Prefab", "Missing Coop player prefab — run Tools/Co-op/Create Coop Player Prefab"));
            return;
        }
        if (!IsPortAvailable(mirrorPort))
        {
            SetStatus($"端口 {mirrorPort} 正在被占用，请先关闭占用进程或更换端口");
            _roomActionInProgress = false;
            return;
        }
        _roomActionInProgress = true;
        StartCoroutine(CoStartHostWithPublicRoom());
    }

    IEnumerator CoStartHostWithPublicRoom()
    {
        if (!EnsureAdvertisedAddressConfigured())
        {
            _roomActionInProgress = false;
            yield break;
        }
        if (LanCoopNetworkManager.Instance == null)
        {
            SetStatus("LanCoopNetworkManager 未就绪");
            _roomActionInProgress = false;
            yield break;
        }
        if (NetworkServer.active || NetworkClient.active)
        {
            SetStatus(Loc.Choose("检测到已有联机连接，请先退出当前会话", "Co-op connection detected — leave the session first"));
            RefreshGenerateDisableToggleUi();
            RefreshJoinLeaveToggleUi();
            _roomActionInProgress = false;
            yield break;
        }
        LanCoopNetworkManager.Instance.playerPrefab = coopPlayerPrefab.gameObject;
        LanCoopNetworkManager.Instance.StartHostWithPort(mirrorPort);

        float waitHost = Time.realtimeSinceStartup;
        while (!CoopSessionState.IsSessionHost && Time.realtimeSinceStartup - waitHost < 5f)
            yield return null;
        if (!CoopSessionState.IsSessionHost)
        {
            SetStatus(Loc.Choose("主机启动失败", "Failed to start host"));
            RefreshGenerateDisableToggleUi();
            RefreshJoinLeaveToggleUi();
            _roomActionInProgress = false;
            yield break;
        }

        bool publishOk = false;
        yield return _api.CoHostPublish(mirrorPort, GetAdvertisedAddress(),
            _ => publishOk = true, e => SetStatus("主机地址上报失败：" + e));
        if (!publishOk)
        {
            if (LanCoopNetworkManager.Instance != null)
                LanCoopNetworkManager.Instance.StopCoop();
            SetStatus(Loc.Choose("主机地址上报失败，已停止联机", "Host address publish failed — co-op stopped"));
            RefreshGenerateDisableToggleUi();
            RefreshJoinLeaveToggleUi();
            _roomActionInProgress = false;
            yield break;
        }

        bool bindOk = false;
        yield return _api.CoPublicRoomBind(_myPublicRoomCode, mirrorPort, GetAdvertisedAddress(),
            _ => bindOk = true, e => SetStatus("公开房间绑定失败：" + e));
        if (!bindOk)
        {
            yield return _api.CoHostUnpublish(() => { }, e => Debug.LogWarning(e));
            if (!string.IsNullOrEmpty(_myPublicRoomCode))
                yield return _api.CoPublicRoomClose(_myPublicRoomCode, () => { }, e => Debug.LogWarning(e));
            if (LanCoopNetworkManager.Instance != null)
                LanCoopNetworkManager.Instance.StopCoop();
            _myPublicRoomCode = "";
            RefreshCodeDisplayAfterExternalStop();
            SetStatus(Loc.Choose("公开房间绑定失败，已回滚本次开房", "Public room bind failed — rolled back"));
            RefreshGenerateDisableToggleUi();
            RefreshJoinLeaveToggleUi();
            _roomActionInProgress = false;
            yield break;
        }

        if (_hostRegistryHeartbeat != null)
            StopCoroutine(_hostRegistryHeartbeat);
        _hostRegistryHeartbeat = StartCoroutine(CoHostRegistryHeartbeat());
        SetStatus($"联机已就绪，房间号 {_myPublicRoomCode}，端口 {mirrorPort}");
        RefreshGenerateDisableToggleUi();
        RefreshJoinLeaveToggleUi();
        _roomActionInProgress = false;
    }

    IEnumerator CoHostRegistryHeartbeat()
    {
        var wait = new WaitForSeconds(45f);
        while (CoopSessionState.IsSessionHost)
        {
            yield return wait;
            if (!CoopSessionState.IsSessionHost) break;
            if (!EnsureAdvertisedAddressConfigured())
            {
                Debug.LogWarning("[房间号] 主机续期跳过：advertised_address 为空（必填）");
                continue;
            }
            yield return _api.CoHostPublish(mirrorPort, GetAdvertisedAddress(), _ => { },
                e => Debug.LogWarning("[房间号] 主机续期失败: " + e));
            if (!string.IsNullOrEmpty(_myPublicRoomCode))
                yield return _api.CoPublicRoomBind(_myPublicRoomCode, mirrorPort, GetAdvertisedAddress(),
                    _ => { }, e => Debug.LogWarning("[房间号] 公开房间续期失败: " + e));
        }
        _hostRegistryHeartbeat = null;
    }

    public void UiJoinRoom()
    {
        if (_roomActionInProgress)
        {
            SetStatus(Loc.Choose("房间操作进行中，请稍候…", "Room operation in progress, please wait…"));
            return;
        }
        if (_joinInput == null) return;
        string code = _joinInput.text.Trim().ToUpperInvariant();
        if (code.Length < 4)
        {
            SetStatus(Loc.Choose("请输入完整房间号", "Enter the full room code"));
            return;
        }
        EnsureNetworkManager();
        if (LanCoopNetworkManager.Instance == null)
        {
            SetStatus("LanCoopNetworkManager 未就绪");
            return;
        }
        if (coopPlayerPrefab == null)
        {
            SetStatus(Loc.Choose("缺少 Coop 玩家预制体", "Missing Coop player prefab"));
            return;
        }
        StartCoroutine(CoJoinPublicRoom(code));
    }

    IEnumerator CoJoinPublicRoom(string roomCode)
    {
        _roomActionInProgress = true;
        float deadline = Time.realtimeSinceStartup + 90f;
        while (Time.realtimeSinceStartup < deadline)
        {
            CoopPublicRoomResolveResponse r = null;
            string err = null;
            yield return _api.CoPublicRoomResolve(roomCode, x => r = x, e => err = e);
            if (!string.IsNullOrEmpty(err))
            {
                SetStatus(err);
                _roomActionInProgress = false;
                yield break;
            }
            if (r != null && r.has_room && r.has_host && !string.IsNullOrEmpty(r.effective_ip) && r.advertised_port > 0)
            {
                LanCoopNetworkManager.Instance.playerPrefab = coopPlayerPrefab.gameObject;
                LanCoopNetworkManager.Instance.StartClientWithAddress(r.effective_ip,
                    (ushort)Mathf.Clamp(r.advertised_port, 1, 65535));
                SetStatus(Loc.Choose("正在加入房间…", "Joining room…"));
                float connectDeadline = Time.realtimeSinceStartup + 12f;
                while (!NetworkClient.isConnected && Time.realtimeSinceStartup < connectDeadline)
                    yield return null;
                if (NetworkClient.isConnected)
                {
                    SetStatus($"已加入房间 {roomCode}");
                    RefreshGenerateDisableToggleUi();
                    RefreshJoinLeaveToggleUi();
                    _roomActionInProgress = false;
                    yield break;
                }

                if (LanCoopNetworkManager.Instance != null && NetworkClient.active)
                    LanCoopNetworkManager.Instance.StopClient();
                SetStatus(Loc.Choose("加入房间失败：未连接到主机", "Join failed — could not connect to host"));
                _roomActionInProgress = false;
                yield break;
            }
            if (r != null && !r.has_room)
            {
                SetStatus(Loc.Choose("房间不存在或已过期", "Room not found or expired"));
                _roomActionInProgress = false;
                yield break;
            }
            SetStatus(Loc.Choose("等待房主上线…", "Waiting for host…"));
            yield return new WaitForSeconds(1.5f);
        }
        SetStatus(Loc.Choose("等待主机超时", "Timed out waiting for host"));
        _roomActionInProgress = false;
    }

    public void UiStopCoop()
    {
        if (_roomActionInProgress)
        {
            SetStatus(Loc.Choose("房间操作进行中，请稍候…", "Room operation in progress, please wait…"));
            return;
        }
        StartCoroutine(CoShutdownCoopAndPublicRoom("已停止联机"));
    }

    public void UiLeaveRoom()
    {
        UiStopCoop();
    }

    /// <summary>停止 Mirror、主机注销、关闭公开房间（若有代码）、清空本地代码显示。</summary>
    IEnumerator CoShutdownCoopAndPublicRoom(string statusMessage)
    {
        _roomActionInProgress = true;
        bool wasHost = CoopSessionState.IsSessionHost;
        if (_hostRegistryHeartbeat != null)
        {
            StopCoroutine(_hostRegistryHeartbeat);
            _hostRegistryHeartbeat = null;
        }
        if (wasHost && _api != null && _api.IsReady)
            yield return _api.CoHostUnpublish(() => { }, e => Debug.LogWarning(e));

        if (!string.IsNullOrEmpty(_myPublicRoomCode) && _api != null && _api.IsReady)
        {
            string code = _myPublicRoomCode;
            yield return _api.CoPublicRoomClose(code, () => { }, e => Debug.LogWarning(e));
        }
        _myPublicRoomCode = "";
        RefreshCodeDisplayAfterExternalStop();

        if (LanCoopNetworkManager.Instance != null)
            LanCoopNetworkManager.Instance.StopCoop();
        SetStatus(statusMessage);
        RefreshGenerateDisableToggleUi();
        RefreshJoinLeaveToggleUi();
        _roomActionInProgress = false;
    }

    /// <summary>有房间代码或已在联机中时，同一按钮显示为「禁用代码」。</summary>
    bool ShouldOfferDisableInsteadOfGenerate()
    {
        return CoopSessionState.InSession || !string.IsNullOrEmpty(_myPublicRoomCode);
    }

    void RefreshGenerateDisableToggleUi()
    {
        if (_generateDisableToggleLabel != null)
        {
            bool disableMode = ShouldOfferDisableInsteadOfGenerate();
            _generateDisableToggleLabel.text = disableMode ? Loc.Choose("禁用代码", "Disable Code") : Loc.Choose("生成房间代码", "Generate Room Code");
            if (_generateDisableToggleImage != null)
                _generateDisableToggleImage.color = disableMode ? ToggleDisableBg : ToggleGenerateBg;
        }
    }

    void RefreshJoinLeaveToggleUi()
    {
        if (_joinLeaveToggleLabel == null)
            return;
        if (CoopSessionState.InSession)
        {
            _joinLeaveToggleLabel.text = Loc.Choose("退出房间", "Leave Room");
            if (_joinLeaveToggleImage != null)
                _joinLeaveToggleImage.color = JoinLeaveLeaveBg;
        }
        else
        {
            _joinLeaveToggleLabel.text = Loc.Choose("加入房间", "Join Room");
            if (_joinLeaveToggleImage != null)
                _joinLeaveToggleImage.color = JoinLeaveJoinBg;
        }
    }

    /// <summary>加入/退出合一：未在联机为加入；已在联机为退出，循环。</summary>
    public void UiToggleJoinOrLeaveRoom()
    {
        if (CoopSessionState.InSession)
            UiLeaveRoom();
        else
            UiJoinRoom();
    }

    /// <summary>生成/禁用合一：首次为生成并开始主机；之后为禁用并停止联机，循环。</summary>
    public void UiToggleGenerateOrDisable()
    {
        if (ShouldOfferDisableInsteadOfGenerate())
            UiDisableCode();
        else
            UiGenerateRoomCode();
    }

    string GetAdvertisedAddress()
    {
        if (_advertisedHostIpInput == null) return "";
        return _advertisedHostIpInput.text != null ? _advertisedHostIpInput.text.Trim() : "";
    }

    bool EnsureAdvertisedAddressConfigured()
    {
        string adv = GetAdvertisedAddress();
        if (string.IsNullOrEmpty(adv))
        {
            SetStatus("请先填写「对外 IP/域名」（必填）");
            return false;
        }
        return true;
    }

    public void UiAutoFillAdvertisedAddress()
    {
        if (_advertisedHostIpInput == null)
        {
            SetStatus("未找到对外地址输入框");
            return;
        }
        string lanIp = TryGetPreferredLanIpv4();
        if (string.IsNullOrEmpty(lanIp))
        {
            SetStatus("自动填充失败：未检测到可用局域网 IPv4，请手动填写");
            return;
        }
        _advertisedHostIpInput.text = lanIp;
        SetStatus("已自动填充对外地址：" + lanIp);
    }

    void AutoFillAdvertisedAddressIfEmpty()
    {
        if (_advertisedHostIpInput == null)
            return;
        string current = _advertisedHostIpInput.text != null ? _advertisedHostIpInput.text.Trim() : "";
        if (!string.IsNullOrEmpty(current))
            return;
        string lanIp = TryGetPreferredLanIpv4();
        if (!string.IsNullOrEmpty(lanIp))
            _advertisedHostIpInput.text = lanIp;
    }

    string TryGetPreferredLanIpv4()
    {
        try
        {
            string best = "";
            var host = Dns.GetHostEntry(Dns.GetHostName());
            if (host != null && host.AddressList != null)
            {
                for (int i = 0; i < host.AddressList.Length; i++)
                {
                    var ip = host.AddressList[i];
                    if (ip == null || ip.AddressFamily != AddressFamily.InterNetwork)
                        continue;
                    string s = ip.ToString();
                    if (string.IsNullOrEmpty(s) || s.StartsWith("127."))
                        continue;
                    if (s.StartsWith("10.") || s.StartsWith("192.168.") || s.StartsWith("172.16.") || s.StartsWith("172.17.")
                        || s.StartsWith("172.18.") || s.StartsWith("172.19.") || s.StartsWith("172.2") || s.StartsWith("172.30.")
                        || s.StartsWith("172.31."))
                        return s;
                    if (string.IsNullOrEmpty(best))
                        best = s;
                }
            }
            return best;
        }
        catch
        {
            return "";
        }
    }

    void EnsureNetworkManager()
    {
        if (NetworkManager.singleton != null && LanCoopNetworkManager.Instance == null)
        {
            SetStatus("请使用 LanCoopNetworkManager");
            return;
        }
        if (LanCoopNetworkManager.Instance != null)
        {
            if (coopPlayerPrefab != null && LanCoopNetworkManager.Instance.playerPrefab == null)
                LanCoopNetworkManager.Instance.playerPrefab = coopPlayerPrefab.gameObject;
            return;
        }
        var go = new GameObject("LanCoopNetworkManager");
        DontDestroyOnLoad(go);
        var nm = go.AddComponent<LanCoopNetworkManager>();
        nm.ApplyPort(mirrorPort);
        if (coopPlayerPrefab != null)
            nm.playerPrefab = coopPlayerPrefab.gameObject;
    }

    static bool IsPortAvailable(ushort port)
    {
        TcpListener listener = null;
        try
        {
            listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
        finally
        {
            try { listener?.Stop(); } catch { }
        }
    }

    void BuildRuntimeUi()
    {
        var font = Resources.Load<TMP_FontAsset>(TmpFontPath);

        var canvasGo = new GameObject("RoomCodeRuntimeCanvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 92;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();
        DontDestroyOnLoad(canvasGo);

        if (UnityEngine.EventSystems.EventSystem.current == null)
        {
            var es = new GameObject("EventSystem_RoomCode");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            DontDestroyOnLoad(es);
        }

        panelRoot = new GameObject("RoomCodePanel");
        panelRoot.transform.SetParent(canvasGo.transform, false);
        var rootRt = panelRoot.AddComponent<RectTransform>();
        float ix = Mathf.Clamp(runtimePanelScreenInset.x, 0f, 0.45f);
        float iy = Mathf.Clamp(runtimePanelScreenInset.y, 0f, 0.45f);
        if (runtimePanelStretchToScreen)
        {
            rootRt.anchorMin = new Vector2(ix, iy);
            rootRt.anchorMax = new Vector2(1f - ix, 1f - iy);
            rootRt.pivot = new Vector2(0.5f, 0.5f);
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;
            rootRt.sizeDelta = Vector2.zero;
            rootRt.anchoredPosition = Vector2.zero;
        }
        else
        {
            rootRt.anchorMin = rootRt.anchorMax = new Vector2(0.5f, 0.5f);
            rootRt.pivot = new Vector2(0.5f, 0.5f);
            rootRt.sizeDelta = runtimePanelSizeDelta;
            rootRt.anchoredPosition = runtimePanelAnchoredPosition;
        }

        var rootImg = panelRoot.AddComponent<Image>();
        rootImg.color = MultiplayerUiPalette.BgWhite;

        float refW = Mathf.Max(1f, runtimePanelLayoutReferenceSize.x);
        float refH = Mathf.Max(1f, runtimePanelLayoutReferenceSize.y);
        float usableW = Screen.width * (1f - 2f * ix);
        float usableH = Screen.height * (1f - 2f * iy);
        if (!runtimePanelStretchToScreen)
        {
            usableW = Mathf.Max(1f, runtimePanelSizeDelta.x);
            usableH = Mathf.Max(1f, runtimePanelSizeDelta.y);
        }

        _layoutScale = Mathf.Min(usableW / refW, usableH / refH);
        _layoutScale = Mathf.Clamp(_layoutScale, 0.34f, 1.45f);

        int pad = LsInt(runtimePanelPadding);
        int bh = LsInt(runtimeButtonMinHeight);
        int mainBtnH = bh + LsInt(10);

        var rootVlg = panelRoot.AddComponent<VerticalLayoutGroup>();
        rootVlg.padding = new RectOffset(LsInt(8f), LsInt(8f), LsInt(4f), LsInt(8f));
        rootVlg.spacing = Ls(4f);
        rootVlg.childAlignment = TextAnchor.UpperCenter;
        rootVlg.childControlHeight = true;
        rootVlg.childForceExpandHeight = false;
        rootVlg.childControlWidth = true;
        rootVlg.childForceExpandWidth = true;

        int closeSide = LsInt(runtimeCloseButtonSize);
        float hdrPadL = Ls(runtimeHeaderTitleMarginLeft);
        float hdrPadR = closeSide + Ls(runtimeHeaderCloseMargin) + Ls(4f);

        var header = new GameObject("HeaderRow", typeof(RectTransform));
        header.transform.SetParent(panelRoot.transform, false);
        var headerLe = header.AddComponent<LayoutElement>();
        headerLe.minHeight = Mathf.Max(LsInt(20f), closeSide + LsInt(6f));
        headerLe.preferredHeight = Mathf.Max(LsInt(26f), closeSide + LsInt(8f));
        headerLe.flexibleHeight = 0f;

        var title = CreateLabel(header.transform, Loc.Choose("通过房间代码发送邀请", "Invite via Room Code"), runtimeFontHeaderTitle, FontStyles.Bold, font,
            TextAlignmentOptions.Left, runtimeFontHeaderTitlePadBelow, textColor: MultiplayerUiPalette.TextGold);
        title.enableAutoSizing = false;
        title.enableWordWrapping = false;
        title.overflowMode = TextOverflowModes.Ellipsis;
        var titleLe = title.GetComponent<LayoutElement>();
        titleLe.flexibleWidth = 0f;
        titleLe.flexibleHeight = 0f;
        var trTitle = title.rectTransform;
        trTitle.anchorMin = new Vector2(0f, 0f);
        trTitle.anchorMax = new Vector2(1f, 1f);
        trTitle.pivot = new Vector2(0f, 0.5f);
        trTitle.offsetMin = new Vector2(hdrPadL, 0f);
        trTitle.offsetMax = new Vector2(-hdrPadR, 0f);

        CreateAnchoredSquareCloseButton(header.transform, font, closeSide, runtimeHeaderCloseMargin);

        var scrollGo = new GameObject("Scroll");
        scrollGo.transform.SetParent(panelRoot.transform, false);
        var scrollLayout = scrollGo.AddComponent<LayoutElement>();
        scrollLayout.flexibleHeight = 1f;
        scrollLayout.minHeight = LsInt(140f);
        scrollLayout.flexibleWidth = 1f;

        var scrollHit = scrollGo.AddComponent<Image>();
        scrollHit.color = new Color(0f, 0f, 0f, 0.004f);
        scrollHit.raycastTarget = true;

        var scroll = scrollGo.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 32f;
        scroll.inertia = true;
        scroll.decelerationRate = 0.2f;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

        var viewport = new GameObject("Viewport");
        viewport.transform.SetParent(scrollGo.transform, false);
        var vpRt = viewport.AddComponent<RectTransform>();
        vpRt.anchorMin = Vector2.zero;
        vpRt.anchorMax = Vector2.one;
        vpRt.offsetMin = Vector2.zero;
        vpRt.offsetMax = Vector2.zero;
        viewport.AddComponent<RectMask2D>();

        var content = new GameObject("Content");
        content.transform.SetParent(viewport.transform, false);
        var contentRt = content.AddComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0f, 1f);
        contentRt.anchorMax = new Vector2(1f, 1f);
        contentRt.pivot = new Vector2(0.5f, 1f);
        contentRt.anchoredPosition = Vector2.zero;
        contentRt.sizeDelta = new Vector2(0f, 0f);

        scroll.viewport = vpRt;
        scroll.content = contentRt;

        var vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(pad, pad, pad, pad + LsInt(2f));
        vlg.spacing = Ls(runtimePanelSpacing);
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlHeight = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childForceExpandWidth = true;

        var csf = content.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        Transform body = content.transform;

        CreateLabel(body, Loc.Choose("通过房间代码发送和接受组队邀请。", "Send and accept party invites with a room code."), runtimeFontSubtitle, FontStyles.Normal, font, TextAlignmentOptions.Center,
                textColor: MultiplayerUiPalette.TextBlack)
            .enableWordWrapping = true;

        _statusText = CreateLabel(body, "", runtimeFontStatus, FontStyles.Normal, font, TextAlignmentOptions.Center,
            textColor: MultiplayerUiPalette.TextBlack);
        _statusSuccessBlink = false;

        CreateLabel(body, Loc.Choose("复制房间代码，即可发送邀请：", "Copy your room code to send an invite:"), runtimeFontSectionHint, FontStyles.Normal, font, TextAlignmentOptions.Left,
            textColor: MultiplayerUiPalette.TextGold);

        var codeRow = new GameObject("CodeRow", typeof(RectTransform));
        codeRow.transform.SetParent(body, false);
        int codeRowH = Mathf.Max(LsInt(runtimeCodeRowMinHeight), bh + LsInt(runtimeCodeRowHeightExtra));
        codeRow.AddComponent<LayoutElement>().minHeight = codeRowH;
        var crH = codeRow.AddComponent<HorizontalLayoutGroup>();
        crH.spacing = Ls(10f);
        crH.childForceExpandWidth = true;
        crH.childControlWidth = true;
        crH.padding = new RectOffset(LsInt(6f), LsInt(6f), LsInt(4f), LsInt(4f));
        var teal = codeRow.AddComponent<Image>();
        teal.color = MultiplayerUiPalette.BgGoldTint;

        _codeText = CreateLabel(codeRow.transform, "——", runtimeFontCode, FontStyles.Bold, font, TextAlignmentOptions.Left, runtimeFontCodePadBelow,
            textColor: MultiplayerUiPalette.TextGold);
        _codeText.GetComponent<LayoutElement>().flexibleWidth = 1;
        int shareH = Mathf.Max(LsInt(28f), codeRowH - LsInt(8f));
        CreateStyledButton(codeRow.transform, "分享", UiShareCopy, font, MultiplayerUiPalette.BtnSecondaryBg, LsInt(80f), shareH,
            runtimeFontButton, MultiplayerUiPalette.TextBlack);

        var toggleGo = CreateStyledButton(body, "生成房间代码", UiToggleGenerateOrDisable, font, ToggleGenerateBg, 0, mainBtnH,
            runtimeFontButtonMain, MultiplayerUiPalette.TextBlack);
        toggleGo.name = "Btn_GenerateOrDisable";
        _generateDisableToggleImage = toggleGo.GetComponent<Image>();
        _generateDisableToggleLabel = toggleGo.GetComponentInChildren<TextMeshProUGUI>();
        RefreshGenerateDisableToggleUi();

        var sep = new GameObject("Sep", typeof(RectTransform));
        sep.transform.SetParent(body, false);
        sep.AddComponent<LayoutElement>().minHeight = Mathf.Max(1, LsInt(2f));
        var sepImg = sep.AddComponent<Image>();
        sepImg.color = MultiplayerUiPalette.SepLine;

        CreateLabel(body, Loc.Choose("粘贴另一个房间代码，即可加入：", "Paste another room code to join:"), runtimeFontSectionHint, FontStyles.Normal, font, TextAlignmentOptions.Left,
            textColor: MultiplayerUiPalette.TextGold);

        var joinRow = new GameObject("JoinRow", typeof(RectTransform));
        joinRow.transform.SetParent(body, false);
        joinRow.AddComponent<LayoutElement>().minHeight = Mathf.Max(LsInt(30f), bh + LsInt(2f));
        var jrH = joinRow.AddComponent<HorizontalLayoutGroup>();
        jrH.spacing = Ls(10f);
        jrH.childForceExpandWidth = true;
        _joinInput = CreateInput(joinRow.transform, "输入房间代码", font, runtimeFontInput);
        _joinInput.GetComponent<LayoutElement>().flexibleWidth = 1;
        int pasteH = Mathf.Max(LsInt(26f), bh - LsInt(4f));
        CreateStyledButton(joinRow.transform, "粘贴", UiPasteJoinCode, font, MultiplayerUiPalette.BtnSecondaryBg, LsInt(72f), pasteH,
            runtimeFontButton, MultiplayerUiPalette.TextBlack);

        var joinLeaveGo = CreateStyledButton(body, "加入房间", UiToggleJoinOrLeaveRoom, font, JoinLeaveJoinBg, 0, mainBtnH,
            runtimeFontButtonMain, MultiplayerUiPalette.TextGold);
        joinLeaveGo.name = "Btn_JoinOrLeaveRoom";
        _joinLeaveToggleImage = joinLeaveGo.GetComponent<Image>();
        _joinLeaveToggleLabel = joinLeaveGo.GetComponentInChildren<TextMeshProUGUI>();
        RefreshJoinLeaveToggleUi();

        CreateLabel(body, Loc.Choose("当主机时对外 IP/域名（必填）", "Public IP/domain when hosting (required)"), runtimeFontIpNote, FontStyles.Normal, font, TextAlignmentOptions.Left,
            textColor: MultiplayerUiPalette.TextBlack);
        var advRow = new GameObject("AdvertisedAddressRow", typeof(RectTransform));
        advRow.transform.SetParent(body, false);
        advRow.AddComponent<LayoutElement>().minHeight = Mathf.Max(LsInt(30f), bh + LsInt(2f));
        var advRowH = advRow.AddComponent<HorizontalLayoutGroup>();
        advRowH.spacing = Ls(10f);
        advRowH.childForceExpandWidth = true;
        advRowH.childControlWidth = true;
        _advertisedHostIpInput = CreateInput(advRow.transform, "对外 IP/域名（必填）", font, runtimeFontInput);
        _advertisedHostIpInput.GetComponent<LayoutElement>().flexibleWidth = 1;
        int autoFillH = Mathf.Max(LsInt(26f), bh - LsInt(4f));
        CreateStyledButton(advRow.transform, "自动填充", UiAutoFillAdvertisedAddress, font, MultiplayerUiPalette.BtnSecondaryBg, LsInt(108f),
            autoFillH, runtimeFontButton, MultiplayerUiPalette.TextBlack);

        _layoutScale = 1f;
        panelRoot.SetActive(false);
    }

    /// <summary>锚定在父 Rect 右上角，固定正方形，不参与 HorizontalLayoutGroup 分配宽度。</summary>
    void CreateAnchoredSquareCloseButton(Transform parent, TMP_FontAsset font, int sidePixels, float marginFromRightBase)
    {
        float m = Ls(marginFromRightBase);
        var slot = new GameObject("CloseSlot", typeof(RectTransform));
        slot.transform.SetParent(parent, false);
        var slotRt = slot.GetComponent<RectTransform>();
        slotRt.anchorMin = slotRt.anchorMax = new Vector2(1f, 0.5f);
        slotRt.pivot = new Vector2(1f, 0.5f);
        slotRt.sizeDelta = new Vector2(sidePixels, sidePixels);
        slotRt.anchoredPosition = new Vector2(-m, 0f);

        var go = new GameObject("BtnClose", typeof(RectTransform));
        go.transform.SetParent(slot.transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.sizeDelta = Vector2.zero;

        var img = go.AddComponent<Image>();
        img.color = MultiplayerUiPalette.CloseBtnBg;
        var btn = go.AddComponent<Button>();
        btn.onClick.AddListener(UiClosePanel);
        var tgo = new GameObject("X", typeof(RectTransform));
        tgo.transform.SetParent(go.transform, false);
        var tmp = tgo.AddComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.text = "×";
        tmp.fontSize = LsFont(runtimeFontCloseX);
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = MultiplayerUiPalette.TextBlack;
        var tr = tmp.rectTransform;
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = Vector2.zero;
        tr.offsetMax = Vector2.zero;
    }

    TextMeshProUGUI CreateLabel(Transform parent, string text, float baseFontSize, FontStyles fs, TMP_FontAsset font,
        TextAlignmentOptions align, float minHeightExtraPoints = 6f, Color? textColor = null)
    {
        float sz = LsFont(baseFontSize);
        var go = new GameObject("Txt", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = sz;
        t.fontStyle = fs;
        t.color = textColor ?? MultiplayerUiPalette.TextBlack;
        t.alignment = align;
        go.AddComponent<LayoutElement>().minHeight = sz + Ls(minHeightExtraPoints);
        return t;
    }

    GameObject CreateStyledButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick, TMP_FontAsset font,
        Color bg, int preferredWidth, int height, float labelFontBase = 15f, Color? labelColor = null)
    {
        var go = new GameObject("Btn_" + label, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var le = go.AddComponent<LayoutElement>();
        le.minHeight = height;
        if (preferredWidth > 0)
            le.preferredWidth = preferredWidth;
        var img = go.AddComponent<Image>();
        img.color = bg;
        var btn = go.AddComponent<Button>();
        btn.onClick.AddListener(onClick);
        var tgo = new GameObject("Text", typeof(RectTransform));
        tgo.transform.SetParent(go.transform, false);
        var tmp = tgo.AddComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.text = label;
        tmp.fontSize = LsFont(labelFontBase);
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = labelColor ?? MultiplayerUiPalette.TextGold;
        var tr = tmp.rectTransform;
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        float ox = Ls(8f), oy = Ls(4f);
        tr.offsetMin = new Vector2(ox, oy);
        tr.offsetMax = new Vector2(-ox, -oy);
        return go;
    }

    TMP_InputField CreateInput(Transform parent, string placeholder, TMP_FontAsset font, float inputFontBase)
    {
        var go = new GameObject("Input", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.AddComponent<Image>().color = MultiplayerUiPalette.InputBg;
        var input = go.AddComponent<TMP_InputField>();
        var le = go.AddComponent<LayoutElement>();
        le.minHeight = LsInt(36f);
        le.flexibleWidth = 1;

        var textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(go.transform, false);
        var text = textGo.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.fontSize = LsFont(inputFontBase);
        text.color = MultiplayerUiPalette.TextBlack;
        var tr = text.rectTransform;
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        float ix = Ls(10f), iy = Ls(6f);
        tr.offsetMin = new Vector2(ix, iy);
        tr.offsetMax = new Vector2(-ix, -iy);
        input.textComponent = text;

        var phGo = new GameObject("Placeholder", typeof(RectTransform));
        phGo.transform.SetParent(go.transform, false);
        var ph = phGo.AddComponent<TextMeshProUGUI>();
        if (font != null) ph.font = font;
        ph.fontSize = LsFont(inputFontBase);
        ph.color = new Color(MultiplayerUiPalette.TextMuted.r, MultiplayerUiPalette.TextMuted.g, MultiplayerUiPalette.TextMuted.b, 0.65f);
        ph.text = placeholder;
        var pr = ph.rectTransform;
        pr.anchorMin = Vector2.zero;
        pr.anchorMax = Vector2.one;
        pr.offsetMin = new Vector2(ix, iy);
        pr.offsetMax = new Vector2(-ix, -iy);
        input.placeholder = ph;
        return input;
    }
}
