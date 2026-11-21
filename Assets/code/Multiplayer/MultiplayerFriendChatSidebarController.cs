using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Multiplayer;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Events;

/// <summary>
/// 按 <b>C</b> 或配置按钮：好友申请与「类微信」私聊侧栏（按好友分会话，本地持久化；不含联机邀请）。
/// </summary>
[DisallowMultipleComponent]
public partial class MultiplayerFriendChatSidebarController : MonoBehaviour
{
    const string TmpFontPath = "Fonts & Materials/AlibabaPuHuiTi-3-75-SemiBold SDF";
    const string ChatSaveDir = "FriendChatThreads";

    [Serializable] class FriendChatThreadSave
    {
        public ChatLineSaveData[] lines;
    }

    [Serializable] class ChatLineSaveData
    {
        public bool mine;
        public string body;
        public int sid;
        public float at;
    }

    class ChatLineLocal
    {
        public bool Mine;
        public string Body;
        public int ServerId;
        public float At;
    }

    [SerializeField] GameObject sidebarRoot;
    [SerializeField] bool ensureRuntimeCanvas = true;
    [Tooltip("关：不在运行时自动套用左侧全高侧栏布局，便于在场景里用 Rect Transform 手动摆位（配合菜单生成面板）。")]
    [SerializeField] bool applyRuntimeSidebarLayout = true;
    [Tooltip("侧栏参考分辨率下的逻辑宽度（双栏聊天建议 ≥ 780）。这是整个聊天面板总宽度的主调参入口。")]
    [SerializeField] float sidebarWidth = 820f;

    [Tooltip("任务面板 RectTransform；仅当勾选 useLegacyQuestAnchorLayout 时生效")]
    [SerializeField] RectTransform layoutBelowQuest;
    [SerializeField] float offsetBelowQuest = 8f;
    [SerializeField] bool useLegacyQuestAnchorLayout;

    [Header("打开侧栏时暂隐其它 Screen Space UI")]
    [SerializeField] bool hideOtherScreenSpaceCanvasesWhenOpen = true;
    [SerializeField] GameObject[] extraRootsToHideWhenSidebarOpen;
    [SerializeField] bool tryHideInteractPromptSharedByName = true;

    [SerializeField] Button openToggleButton;

    MultiplayerApiClient _api;
    Coroutine _chatPoll;
    Coroutine _friendRefresh;
    Coroutine _meFetch;
    Coroutine _repairUiAfterSceneLoad;

    int _selectedChatPeerId;
    int _lastChatMessageId;
    int _myUserId;
    bool _myUserIdReady;

    readonly List<FriendEntryDto> _friendCache = new List<FriendEntryDto>();
    readonly Dictionary<int, List<ChatLineLocal>> _messagesByPeer = new Dictionary<int, List<ChatLineLocal>>();
    readonly HashSet<int> _seenServerMessageIds = new HashSet<int>();
    readonly HashSet<int> _diskLoadedPeers = new HashSet<int>();

    TMP_FontAsset _runtimeFont;
    TMP_InputField _addFriendInput;
    TMP_InputField _friendRequestIdInput;
    TMP_InputField _chatInput;
    TextMeshProUGUI _pendingFriendsText;
    TextMeshProUGUI _statusText;
    TextMeshProUGUI _chatHeaderTitle;
    Transform _friendListContent;
    Transform _chatBubbleContent;
    ScrollRect _chatMessageScroll;

    bool _layoutApplied;

    readonly Dictionary<GameObject, bool> _savedRootActive = new Dictionary<GameObject, bool>();
    bool _hidOtherUi;

    public bool IsOpen => sidebarRoot != null && sidebarRoot.activeSelf;
    public int SelectedPeerId => _selectedChatPeerId;

    /// <summary>打开好友侧栏时暂停行走/视角（与房间号面板一致）。</summary>
    public static bool BlocksGameplayInput
    {
        get
        {
            var c = UnityEngine.Object.FindObjectOfType<MultiplayerFriendChatSidebarController>(true);
            return c != null && c.IsOpen;
        }
    }

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

    void Awake()
    {
        _api = GetComponent<MultiplayerApiClient>() ?? gameObject.AddComponent<MultiplayerApiClient>();
        TryBindExistingSidebarInSceneOrRuntime(true);
        if (sidebarRoot != null)
        {
            var binder = sidebarRoot.GetComponent<FriendChatSidebarUiBinder>();
            if (binder != null)
                BindSceneUi(binder);
            WireSidebarButtonsIfNeeded();
            sidebarRoot.SetActive(false);
        }
        else if (ensureRuntimeCanvas)
            BuildRuntimeUi();

        if (openToggleButton != null)
            openToggleButton.onClick.AddListener(ToggleSidebar);

        EnsureUiEventSystemExists();
        ApplyFriendChatCanvasPriority();
        WorldChannel_CompleteSetup();
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoadedRebindSidebar;
        WorldChannel_RegisterNetEvents();
        Loc.OnLanguageChanged += OnLanguageChanged;
    }

    void OnLanguageChanged()
    {
        if (_chatHeaderTitle != null && _selectedChatPeerId <= 0)
            _chatHeaderTitle.text = Loc.Choose("选择一个好友", "Select a friend");
    }

    void OnSceneLoadedRebindSidebar(Scene scene, LoadSceneMode mode)
    {
        // 冷加载/切场景后重新绑定：优先使用场景里的手动版，避免回退到 DontDestroyOnLoad 旧实例。
        TryBindExistingSidebarInSceneOrRuntime(true);
        WireSidebarButtonsIfNeeded();
        EnsureSidebarClosedByDefault();
        StripTmpRaycastsThatStealClicksUnderSidebar();
        // Single 加载会卸掉上一场景里的 EventSystem；若首包在菜单/Loading 里 Awake 时曾「已有 ES」而未创建 DDO ES，进关后会没有 EventSystem，UGUI 只剩空壳。
        EnsureUiEventSystemExists();
        ApplyFriendChatCanvasPriority();
        if (_repairUiAfterSceneLoad != null)
            StopCoroutine(_repairUiAfterSceneLoad);
        _repairUiAfterSceneLoad = StartCoroutine(CoRepairFriendChatInteractivityAfterSceneLoad());
    }

    void TryBindExistingSidebarInSceneOrRuntime(bool preferSceneInstance)
    {
        // 先找已存在的（场景里手动创建，或旧运行时残留）并复用，避免重复创建新画布抢占引用。
        var allBinders = FindObjectsOfType<FriendChatSidebarUiBinder>(true);
        FriendChatSidebarUiBinder best = null;
        FriendChatSidebarUiBinder fallback = null;
        for (int i = 0; i < allBinders.Length; i++)
        {
            var b = allBinders[i];
            if (b == null || b.gameObject == null) continue;
            if (b.gameObject.name != "FriendChatSidebar") continue;
            if (!IsDontDestroyObject(b.gameObject))
            {
                best = b;
                break;
            }
            if (fallback == null)
                fallback = b;
        }

        var target = (preferSceneInstance && best != null) ? best : (best ?? fallback);
        if (target != null)
        {
            var old = sidebarRoot;
            sidebarRoot = target.gameObject;
            BindSceneUi(target);
            if (preferSceneInstance && old != null && old != sidebarRoot && IsDontDestroyObject(old))
                TryDestroyRuntimeSidebarCanvas(old);
            EnsureSidebarClosedByDefault();
            return;
        }

        // 兜底：存在旧结构但缺少 binder 时，也尽量绑定。
        var root = GameObject.Find("FriendChatSidebar");
        if (root != null)
        {
            var old = sidebarRoot;
            sidebarRoot = root;
            var b = root.GetComponent<FriendChatSidebarUiBinder>();
            if (b != null)
                BindSceneUi(b);
            if (preferSceneInstance && old != null && old != sidebarRoot && IsDontDestroyObject(old))
                TryDestroyRuntimeSidebarCanvas(old);
            EnsureSidebarClosedByDefault();
        }
    }

    /// <summary>场景里 FriendChatSidebar 常为默认激活；切关重绑后须保持关闭，直到玩家按 C 打开。</summary>
    void EnsureSidebarClosedByDefault()
    {
        if (sidebarRoot == null || !sidebarRoot.activeSelf)
            return;
        ForceCloseSidebar();
    }

    static bool IsDontDestroyObject(GameObject go)
    {
        return go != null && go.scene.IsValid() && go.scene.name == "DontDestroyOnLoad";
    }

    const string PersistentEventSystemName = "EventSystem_FriendChat";
    /// <summary>须高于任务栏(210)、NPC 对话(950)等；须低于全屏加载层(约 32000)。</summary>
    const int FriendChatCanvasSortOrder = 2500;

    /// <summary>
    /// 保证至少有一个可用的 EventSystem。解决：首场景有 ES → 运行时未创建 DDO ES → Loadingscene/菜单被 Single 卸掉后关卡无 ES，侧栏按钮与输入框无法交互。
    /// </summary>
    static void EnsureUiEventSystemExists()
    {
        if (EventSystem.current != null && EventSystem.current.isActiveAndEnabled)
            return;

        var all = FindObjectsOfType<EventSystem>(true);
        for (int i = 0; i < all.Length; i++)
        {
            var es = all[i];
            if (es == null) continue;
            if (es.gameObject.name != PersistentEventSystemName) continue;
            es.gameObject.SetActive(true);
            es.enabled = true;
            EnsureStandaloneInputModule(es.gameObject);
            return;
        }

        for (int i = 0; i < all.Length; i++)
        {
            var es = all[i];
            if (es == null || !es.gameObject.activeInHierarchy) continue;
            es.enabled = true;
            EnsureStandaloneInputModule(es.gameObject);
            return;
        }

        var go = new GameObject(PersistentEventSystemName);
        go.AddComponent<EventSystem>();
        EnsureStandaloneInputModule(go);
        DontDestroyOnLoad(go);
    }

    void ApplyFriendChatCanvasPriority()
    {
        if (sidebarRoot == null) return;
        var cv = sidebarRoot.GetComponentInParent<Canvas>(true);
        if (cv == null) return;
        cv.sortingOrder = Mathf.Max(cv.sortingOrder, FriendChatCanvasSortOrder);
        var gr = cv.GetComponent<GraphicRaycaster>();
        if (gr != null)
            gr.enabled = true;
        var cg = cv.GetComponent<CanvasGroup>();
        if (cg != null)
        {
            cg.interactable = true;
            cg.blocksRaycasts = true;
        }
    }

    IEnumerator CoRepairFriendChatInteractivityAfterSceneLoad()
    {
        yield return null;
        yield return null;
        TryBindExistingSidebarInSceneOrRuntime(true);
        EnsureUiEventSystemExists();
        ApplyFriendChatCanvasPriority();
        WireSidebarButtonsIfNeeded();
        EnsureSidebarClosedByDefault();
        StripTmpRaycastsThatStealClicksUnderSidebar();
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
        _repairUiAfterSceneLoad = null;
    }

    /// <summary>
    /// TMP 默认 raycastTarget=true，叠在 Button 上会抢走点击，表现为「空壳按钮」。
    /// </summary>
    void StripTmpRaycastsThatStealClicksUnderSidebar()
    {
        if (sidebarRoot == null) return;
        var tmps = sidebarRoot.GetComponentsInChildren<TextMeshProUGUI>(true);
        for (int i = 0; i < tmps.Length; i++)
        {
            var tmp = tmps[i];
            if (tmp == null) continue;
            if (tmp.GetComponentInParent<TMP_InputField>(true) != null)
                tmp.raycastTarget = false;
            else if (tmp.GetComponentInParent<Button>(true) != null)
                tmp.raycastTarget = false;
        }
    }

    static void EnsureStandaloneInputModule(GameObject go)
    {
        if (go == null) return;
        if (go.GetComponent<BaseInputModule>() != null)
            return;
        go.AddComponent<StandaloneInputModule>();
    }

    void TryDestroyRuntimeSidebarCanvas(GameObject oldSidebar)
    {
        if (oldSidebar == null) return;
        var oldCanvas = oldSidebar.GetComponentInParent<Canvas>(true);
        if (oldCanvas == null || oldCanvas.gameObject == null) return;
        if (!IsDontDestroyObject(oldCanvas.gameObject)) return;
        if (oldCanvas.gameObject.name != "FriendChatSidebarCanvas") return;
        Destroy(oldCanvas.gameObject);
    }

    void Start()
    {
        StartCoroutine(CoApplyLayoutBelowQuestOnce());
    }

    IEnumerator CoApplyLayoutBelowQuestOnce()
    {
        yield return null;
        if (_layoutApplied || sidebarRoot == null)
            yield break;
        if (!applyRuntimeSidebarLayout && !useLegacyQuestAnchorLayout)
        {
            _layoutApplied = true;
            yield break;
        }
        var sr = sidebarRoot.GetComponent<RectTransform>();
        if (useLegacyQuestAnchorLayout && layoutBelowQuest != null)
        {
            var qr = layoutBelowQuest;
            sr.SetParent(qr.parent, false);
            sr.SetSiblingIndex(qr.GetSiblingIndex() + 1);
            sr.anchorMin = qr.anchorMin;
            sr.anchorMax = qr.anchorMax;
            sr.pivot = new Vector2(qr.pivot.x, 1f);
            float h = Mathf.Clamp(qr.rect.height, 80f, 800f);
            sr.sizeDelta = new Vector2(sidebarWidth, Mathf.Min(520f, Screen.height * 0.5f));
            sr.anchoredPosition = qr.anchoredPosition + new Vector2(0f, -h - offsetBelowQuest);
        }
        else
            ApplyLeftFullHeightSidebarLayout(sr);
        _layoutApplied = true;
    }

    void ApplyLeftFullHeightSidebarLayout(RectTransform sr)
    {
        if (sr == null) return;
        // ===== 外层面板总尺寸/位置（优先调这里） =====
        // sizeDelta.x: 总宽度（推荐配合 sidebarWidth 一起调）
        // anchoredPosition.x: 距离屏幕左侧偏移（越大越往右）
        sr.anchorMin = new Vector2(0f, 0f);
        sr.anchorMax = new Vector2(0f, 1f);
        sr.pivot = new Vector2(0f, 0.5f);
        sr.sizeDelta = new Vector2(Mathf.Max(200f, sidebarWidth), 0f);
        sr.anchoredPosition = new Vector2(36f, 0f);
    }

    void LateUpdate()
    {
        if (!IsOpen) return;
        // 有些系统会在 Update/LateUpdate 重新激活其 UI；侧栏打开期间每帧再压一次，避免输入时弹出其它面板抢焦点。
        try
        {
            HideOtherUi();
        }
        catch (MissingReferenceException)
        {
            // 同帧对象销毁导致的瞬时失效，忽略本帧避免刷错。
        }
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void ToggleSidebar()
    {
        if (sidebarRoot == null) return;
        bool show = !sidebarRoot.activeSelf;
        if (show)
        {
            EnsureUiEventSystemExists();
            ApplyFriendChatCanvasPriority();
            StripTmpRaycastsThatStealClicksUnderSidebar();
            MultiplayerRoomCodePanelController.Instance?.ClosePanel();
            HideOtherUi();
            sidebarRoot.SetActive(true);
            OnSidebarOpened();
        }
        else
        {
            ForceCloseSidebar();
        }
    }

    public void ForceCloseSidebar()
    {
        if (sidebarRoot == null || !sidebarRoot.activeSelf)
            return;
        sidebarRoot.SetActive(false);
        StopPollers();
        RestoreOtherUi();
    }

    /// <summary>外部可直接打开侧栏并跳转到指定 userId 会话（例如近距离交互 F 键触发）。</summary>
    public void OpenChatWithUserId(int userId, string usernameHint = null)
    {
        if (userId <= 0 || sidebarRoot == null)
            return;

        if (!sidebarRoot.activeSelf)
            ToggleSidebar();

        if (!string.IsNullOrWhiteSpace(usernameHint))
        {
            bool exists = false;
            for (int i = 0; i < _friendCache.Count; i++)
            {
                if (_friendCache[i].user_id == userId)
                {
                    exists = true;
                    break;
                }
            }
            if (!exists)
                _friendCache.Add(new FriendEntryDto { user_id = userId, username = usernameHint.Trim() });
            RebuildFriendListUi();
        }

        SelectChatPeer(userId);
    }

    void OnSidebarOpened()
    {
        if (!_api.IsReady)
        {
            SetStatus(Loc.Choose("请先登录游戏账号（GameServer）。", "Please log in to the game server first."));
            return;
        }
        SetStatus("");
        StopPollers();
        if (_meFetch != null) StopCoroutine(_meFetch);
        _meFetch = StartCoroutine(CoFetchMeOnce());
        _friendRefresh = StartCoroutine(CoRefreshFriendsUiLoop());
        _chatPoll = StartCoroutine(CoChatPollLoop());
    }

    IEnumerator CoFetchMeOnce()
    {
        yield return _api.CoMe(
            m =>
            {
                _myUserId = m != null ? m.user_id : 0;
                _myUserIdReady = _myUserId > 0;
            },
            _ => { _myUserIdReady = false; });
        _meFetch = null;
    }

    void OnDisable()
    {
        Loc.OnLanguageChanged -= OnLanguageChanged;
        if (_repairUiAfterSceneLoad != null)
        {
            StopCoroutine(_repairUiAfterSceneLoad);
            _repairUiAfterSceneLoad = null;
        }
        StopPollers();
        RestoreOtherUi();
        WorldChannel_UnregisterNetEvents();
        SceneManager.sceneLoaded -= OnSceneLoadedRebindSidebar;
    }

    void HideOtherUi()
    {
        if (_hidOtherUi || sidebarRoot == null) return;
        _savedRootActive.Clear();
        Canvas myRoot = null;
        try { myRoot = GetOutermostCanvas(sidebarRoot.transform); }
        catch (MissingReferenceException) { return; }
        if (extraRootsToHideWhenSidebarOpen != null)
        {
            foreach (var go in extraRootsToHideWhenSidebarOpen)
                RememberAndHide(go);
        }
        if (tryHideInteractPromptSharedByName)
        {
            var prompt = GameObject.Find(InteractPromptSharedUiLocator.PanelObjectName);
            if (prompt != null)
                RememberAndHide(prompt);
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
                    string.Equals(theirRoot.gameObject.name, "RoomCodeRuntimeCanvas", StringComparison.OrdinalIgnoreCase))
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
            // 目标在当前帧已销毁，忽略。
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
                // 目标已销毁，无需恢复。
            }
        }
        _savedRootActive.Clear();
    }

    void StopPollers()
    {
        if (_chatPoll != null) StopCoroutine(_chatPoll);
        if (_friendRefresh != null) StopCoroutine(_friendRefresh);
        if (_meFetch != null) StopCoroutine(_meFetch);
        _chatPoll = _friendRefresh = _meFetch = null;
    }

    void SetStatus(string msg)
    {
        if (_statusText != null)
            _statusText.text = msg;
        if (!string.IsNullOrEmpty(msg))
            Debug.Log("[好友/聊天] " + msg);
    }

    IEnumerator CoRefreshFriendsUiLoop()
    {
        var wait = new WaitForSeconds(3f);
        while (sidebarRoot != null && sidebarRoot.activeSelf)
        {
            yield return StartCoroutine(CoRefreshFriendsAndPendingOnce());
            yield return wait;
        }
    }

    IEnumerator CoRefreshFriendsAndPendingOnce()
    {
        yield return _api.CoFriendsList(
            r =>
            {
                _friendCache.Clear();
                if (r?.friends != null)
                    _friendCache.AddRange(r.friends);
                RebuildFriendListUi();
            },
            e => SetStatus(e));

        yield return _api.CoFriendsPending(
            r =>
            {
                if (_pendingFriendsText != null)
                {
                    _pendingFriendsText.text = Loc.Choose("收到的申请\n", "Received Requests\n");
                    if (r?.inbound != null)
                    {
                        foreach (var x in r.inbound)
                            _pendingFriendsText.text +=
                                Loc.F("编号 {0} · 来自「{1}」（同意/拒绝时只填编号）\n",
                                    "ID {0} · from \"{1}\" (enter ID only to accept/decline)\n", x.id, x.from_username);
                    }
                    _pendingFriendsText.text += Loc.Choose("\n已发出的申请\n", "\nSent Requests\n");
                    if (r?.outbound != null)
                    {
                        foreach (var x in r.outbound)
                            _pendingFriendsText.text += $"#{x.id} → {x.to_username}\n";
                    }
                }
            },
            e => SetStatus(e));
    }

    void RebuildFriendListUi()
    {
        if (_friendListContent == null) return;
        for (int i = _friendListContent.childCount - 1; i >= 0; i--)
            Destroy(_friendListContent.GetChild(i).gameObject);

        foreach (var f in _friendCache)
        {
            int uid = f.user_id;
            HubStyle.CreateFriendRow(_friendListContent, f, () => SelectChatPeer(uid), _runtimeFont);
        }

        if (_friendCache.Count == 0)
        {
            _selectedChatPeerId = 0;
            if (_chatHeaderTitle != null)
                _chatHeaderTitle.text = Loc.Choose("暂无好友", "No friends yet");
            ClearBubbleChildren();
            return;
        }

        bool selOk = false;
        for (int i = 0; i < _friendCache.Count; i++)
        {
            if (_friendCache[i].user_id == _selectedChatPeerId)
            {
                selOk = true;
                break;
            }
        }

        if (!selOk || _selectedChatPeerId <= 0)
            SelectChatPeer(_friendCache[0].user_id);
        else
        {
            RefreshChatHeader();
            RefreshChatBubbles();
        }

        StripTmpRaycastsThatStealClicksUnderSidebar();
    }

    IEnumerator CoChatPollLoop()
    {
        var wait = new WaitForSeconds(1.2f);
        while (sidebarRoot != null && sidebarRoot.activeSelf)
        {
            if (_api.IsReady)
            {
                yield return _api.CoChatInbox(_lastChatMessageId, ApplyInboxMessages, _ => { });
            }
            yield return wait;
        }
    }

    void ApplyInboxMessages(ChatInboxResponse r)
    {
        if (r?.messages == null || r.messages.Length == 0) return;
        if (!_myUserIdReady)
            return;
        bool touchedCurrent = false;
        foreach (var m in r.messages)
        {
            if (m == null) continue;

            if (_seenServerMessageIds.Contains(m.id))
            {
                if (m.id > _lastChatMessageId)
                    _lastChatMessageId = m.id;
                continue;
            }

            int peer = PeerUserIdForMessageLocal(m);
            if (peer <= 0)
                continue;

            bool mine = m.from_user_id == _myUserId;
            if (mine)
            {
                var list = GetOrCreateThread(peer);
                if (list.Count > 0)
                {
                    var last = list[list.Count - 1];
                    if (last.Mine && last.ServerId == 0 && last.Body == m.body)
                    {
                        last.ServerId = m.id;
                        last.At = m.created_at;
                        _seenServerMessageIds.Add(m.id);
                        SaveThreadToDisk(peer);
                        if (peer == _selectedChatPeerId)
                            touchedCurrent = true;
                        continue;
                    }
                }
            }

            _seenServerMessageIds.Add(m.id);
            AppendLine(peer, mine, m.body, m.id, m.created_at);
            if (m.id > _lastChatMessageId)
                _lastChatMessageId = m.id;
            SaveThreadToDisk(peer);
            if (peer == _selectedChatPeerId)
                touchedCurrent = true;
        }

        if (touchedCurrent)
            RefreshChatBubbles();
    }

    int PeerUserIdForMessageLocal(ChatMessageDto m)
    {
        if (!_myUserIdReady) return 0;
        if (m.from_user_id == _myUserId)
            return m.to_user_id;
        return m.from_user_id;
    }

    void SelectChatPeer(int userId)
    {
        _selectedChatPeerId = userId;
        RefreshChatHeader();
        if (!_diskLoadedPeers.Contains(userId))
        {
            _diskLoadedPeers.Add(userId);
            LoadThreadFromDisk(userId);
        }
        RefreshChatBubbles();
        ScrollChatToBottom();
    }

    List<ChatLineLocal> GetOrCreateThread(int peerId)
    {
        if (!_messagesByPeer.TryGetValue(peerId, out var list))
        {
            list = new List<ChatLineLocal>();
            _messagesByPeer[peerId] = list;
        }
        return list;
    }

    void AppendLine(int peerId, bool mine, string body, int serverId, float at)
    {
        GetOrCreateThread(peerId).Add(new ChatLineLocal
        {
            Mine = mine,
            Body = body ?? "",
            ServerId = serverId,
            At = at
        });
    }

    void LoadThreadFromDisk(int peerId)
    {
        var list = GetOrCreateThread(peerId);
        if (list.Count > 0) return;
        var path = ThreadFilePath(peerId);
        if (!File.Exists(path)) return;
        try
        {
            var json = File.ReadAllText(path);
            var save = JsonUtility.FromJson<FriendChatThreadSave>(json);
            if (save?.lines == null) return;
            foreach (var s in save.lines)
            {
                list.Add(new ChatLineLocal
                {
                    Mine = s.mine,
                    Body = s.body ?? "",
                    ServerId = s.sid,
                    At = s.at
                });
                if (s.sid > 0)
                {
                    _seenServerMessageIds.Add(s.sid);
                    if (s.sid > _lastChatMessageId)
                        _lastChatMessageId = s.sid;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[好友/聊天] 读取本地会话失败: " + e.Message);
        }
    }

    void SaveThreadToDisk(int peerId)
    {
        var thread = GetOrCreateThread(peerId);
        // 先拍快照再写数组，避免保存过程中会话列表增长导致 arr[i] 越界。
        var snapshot = thread.ToArray();
        var arr = new ChatLineSaveData[snapshot.Length];
        for (int i = 0; i < snapshot.Length; i++)
        {
            var L = snapshot[i];
            arr[i] = new ChatLineSaveData { mine = L.Mine, body = L.Body ?? "", sid = L.ServerId, at = L.At };
        }
        try
        {
            var dir = Path.Combine(Application.persistentDataPath, ChatSaveDir);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(ThreadFilePath(peerId), JsonUtility.ToJson(new FriendChatThreadSave { lines = arr }));
        }
        catch (Exception e)
        {
            Debug.LogWarning("[好友/聊天] 保存会话失败: " + e.Message);
        }
    }

    string ThreadFilePath(int peerId) =>
        Path.Combine(Application.persistentDataPath, ChatSaveDir, $"peer_{peerId}.json");

    void RefreshChatHeader()
    {
        if (_chatHeaderTitle == null) return;
        if (_selectedChatPeerId <= 0)
        {
            _chatHeaderTitle.text = Loc.Choose("选择一个好友", "Select a friend");
            return;
        }
        _chatHeaderTitle.text = FindFriendName(_selectedChatPeerId);
    }

    string FindFriendName(int userId)
    {
        for (int i = 0; i < _friendCache.Count; i++)
        {
            if (_friendCache[i].user_id == userId)
                return string.IsNullOrEmpty(_friendCache[i].username)
                    ? Loc.Choose("好友", "Friend")
                    : _friendCache[i].username;
        }
        return Loc.Choose("好友", "Friend");
    }

    void ClearBubbleChildren()
    {
        if (_chatBubbleContent == null) return;
        for (int i = _chatBubbleContent.childCount - 1; i >= 0; i--)
            Destroy(_chatBubbleContent.GetChild(i).gameObject);
    }

    void RefreshChatBubbles()
    {
        if (_chatBubbleContent == null) return;
        ClearBubbleChildren();
        if (_selectedChatPeerId <= 0 || _runtimeFont == null) return;
        if (!_messagesByPeer.TryGetValue(_selectedChatPeerId, out var list)) return;
        foreach (var line in list)
            HubStyle.AddChatBubble(_chatBubbleContent, line.Mine, line.Body, _runtimeFont, _myUserId, _selectedChatPeerId);
        Canvas.ForceUpdateCanvases();
        ScrollChatToBottom();
    }

    void ScrollChatToBottom()
    {
        if (_chatMessageScroll == null) return;
        _chatMessageScroll.verticalNormalizedPosition = 0f;
    }

    public void UiSendFriendRequest()
    {
        if (_addFriendInput == null) return;
        string name = _addFriendInput.text.Trim();
        if (string.IsNullOrEmpty(name))
        {
            SetStatus(Loc.Choose("填写对方用户名", "Enter the other player's username"));
            return;
        }
        StartCoroutine(_api.CoFriendsRequest(name,
            () => SetStatus(Loc.Choose("已发送好友申请", "Friend request sent")),
            SetStatus));
    }

    public void UiRespondFriendFromInput(bool accept)
    {
        if (_friendRequestIdInput == null || !int.TryParse(_friendRequestIdInput.text.Trim(), out var id) || id <= 0)
        {
            SetStatus(Loc.Choose("请填写申请编号数字。", "Enter the request ID number."));
            return;
        }
        SetStatus(Loc.F((accept ? "同意" : "拒绝") + "处理中… 编号 {0}",
            (accept ? "Accepting" : "Declining") + " request… ID {0}", id));
        StartCoroutine(CoRespondFriendAndRefresh(id, accept));
    }

    IEnumerator CoRespondFriendAndRefresh(int requestId, bool accept)
    {
        bool done = false;
        string err = null;
        yield return _api.CoFriendsRespond(
            requestId,
            accept,
            () => done = true,
            e => err = e);

        if (!string.IsNullOrEmpty(err))
        {
            SetStatus(Loc.F((accept ? "同意" : "拒绝") + "失败：{0}",
                (accept ? "Accept" : "Decline") + " failed: {0}", err));
            yield break;
        }
        if (!done)
        {
            SetStatus(Loc.Choose((accept ? "同意" : "拒绝") + "失败：服务器无响应",
                (accept ? "Accept" : "Decline") + " failed: no server response"));
            yield break;
        }

        yield return StartCoroutine(CoRefreshFriendsAndPendingOnce());
        SetStatus(Loc.F("已{0}申请编号 {1}", "Request {1} {0}",
            accept ? Loc.Choose("同意", "accepted") : Loc.Choose("拒绝", "declined"), requestId));
    }

    public void UiSendChat()
    {
        if (WorldChannel_IsWorldMode())
        {
            WorldChannel_UiSendWorld();
            return;
        }
        if (_chatInput == null || _selectedChatPeerId <= 0)
        {
            SetStatus(Loc.Choose("请先点击左侧好友", "Select a friend on the left first"));
            return;
        }
        string t = _chatInput.text.Trim();
        if (string.IsNullOrEmpty(t)) return;
        int peer = _selectedChatPeerId;
        AppendLine(peer, true, t, 0, Time.realtimeSinceStartup);
        SaveThreadToDisk(peer);
        _chatInput.text = "";
        RefreshChatBubbles();
        StartCoroutine(_api.CoChatSend(peer, t, () => { }, SetStatus));
    }

    void BindSceneUi(FriendChatSidebarUiBinder b)
    {
        if (b == null) return;
        _runtimeFont = b.font;
        _statusText = b.statusText;
        _pendingFriendsText = b.pendingFriendsText;
        _chatHeaderTitle = b.chatHeaderTitle;
        _addFriendInput = b.addFriendInput;
        _friendRequestIdInput = b.friendRequestIdInput;
        _chatInput = b.chatInput;
        _friendListContent = b.friendListContent;
        _chatBubbleContent = b.chatBubbleContent;
        _chatMessageScroll = b.chatMessageScroll;
        WorldChannel_ApplyBinder(b);
    }

    // 兜底：场景里已有（或老版本生成）的按钮若丢失 onClick，会在 Awake 自动重绑。
    void WireSidebarButtonsIfNeeded()
    {
        if (sidebarRoot == null) return;
        var buttons = sidebarRoot.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            var btn = buttons[i];
            if (btn == null || btn.gameObject == null) continue;
            string n = btn.gameObject.name;
            if (n == "Btn_发送好友申请")
                RebindButton(btn, UiSendFriendRequest);
            else if (n == "Btn_同意")
                RebindButton(btn, () => UiRespondFriendFromInput(true));
            else if (n == "Btn_拒绝")
                RebindButton(btn, () => UiRespondFriendFromInput(false));
            else if (n == "Btn_发送")
                RebindButton(btn, UiSendChat);
            else if (n == "Btn_X" || n == "Btn_关闭侧栏")
                RebindButton(btn, ToggleSidebar);
            else
            {
                // 场景里重命名过按钮也尽量自动识别，避免“看着有按钮但点了没反应”。
                if (n.Contains("发送好友") || n.Contains("添加好友"))
                    RebindButton(btn, UiSendFriendRequest);
                else if (n.Contains("同意"))
                    RebindButton(btn, () => UiRespondFriendFromInput(true));
                else if (n.Contains("拒绝"))
                    RebindButton(btn, () => UiRespondFriendFromInput(false));
                else if (n.Contains("发送喇叭") || (n.Contains("喇叭") && n.Contains("发送")))
                    RebindButton(btn, UiSendServerHorn);
                else if (n.Contains("发送"))
                    RebindButton(btn, UiSendChat);
                else if (n.Contains("好友私聊") || n == "Btn_好友私聊")
                    RebindButton(btn, () => SetChatChannel(ChatChannelKind.Friend));
                else if (n.Contains("世界频道") || n == "Btn_世界频道")
                    RebindButton(btn, () => SetChatChannel(ChatChannelKind.World));
            }
        }
    }

    static void RebindButton(Button button, UnityAction action)
    {
        if (button == null || action == null) return;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    void BuildRuntimeUi()
    {
        BuildUiInternal(true, FindObjectsOfType<EventSystem>(true).Length == 0);
    }

#if UNITY_EDITOR
    /// <summary>由菜单「Tools/联机/生成好友聊天面板…」调用：写入当前场景并关闭运行时自动改 Rect。</summary>
    public void BuildUiForSceneEditing()
    {
        if (UnityEditor.EditorApplication.isPlaying) return;
        bool needEs = FindObjectsOfType<EventSystem>(true).Length == 0;
        BuildUiInternal(false, needEs);
        ensureRuntimeCanvas = false;
        applyRuntimeSidebarLayout = false;
        UnityEditor.EditorUtility.SetDirty(this);
        if (gameObject.scene.IsValid())
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }
#endif

    void BuildUiInternal(bool dontDestroyCanvas, bool createEventSystemIfMissing)
    {
        var font = Resources.Load<TMP_FontAsset>(TmpFontPath);
        _runtimeFont = font;

        // ===== 全局通用尺寸 =====
        // scrollBarW: 各滚动区域滚动条宽度
        // pad: 聊天气泡区内边距
        const float scrollBarW = 14f;
        const float pad = 10f;

        var canvasGo = new GameObject("FriendChatSidebarCanvas");
#if UNITY_EDITOR
        if (!dontDestroyCanvas)
            UnityEditor.Undo.RegisterCreatedObjectUndo(canvasGo, "Friend Chat Canvas");
#endif
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = FriendChatCanvasSortOrder;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();
        if (dontDestroyCanvas)
            DontDestroyOnLoad(canvasGo);

        sidebarRoot = new GameObject("FriendChatSidebar");
        sidebarRoot.transform.SetParent(canvasGo.transform, false);
        var rt = sidebarRoot.AddComponent<RectTransform>();
        sidebarRoot.AddComponent<Image>().color = HubStyle.WxShellBg;
        var vSide = sidebarRoot.AddComponent<VerticalLayoutGroup>();
        vSide.childControlHeight = true;
        vSide.childForceExpandHeight = false;
        vSide.childControlWidth = true;
        vSide.childForceExpandWidth = true;
        vSide.spacing = 0;
        vSide.padding = new RectOffset(0, 0, 0, 0);

        Transform root = sidebarRoot.transform;

        var topBar = new GameObject("TopBar");
        topBar.transform.SetParent(root, false);
        topBar.AddComponent<Image>().color = HubStyle.WxBarBg;
        var topLe = topBar.AddComponent<LayoutElement>();
        // 顶部标题栏高度（含左上标题与右上角 X）
        topLe.minHeight = 54f;
        var topH = topBar.AddComponent<HorizontalLayoutGroup>();
        // 顶栏内边距（左,右,上,下）
        topH.padding = new RectOffset(8, 8, 2, 2);
        topH.childAlignment = TextAnchor.MiddleLeft;
        topH.spacing = 6;
        // 顶栏标题字号
        var titleTop = HubStyle.CreateTmpText(topBar.transform, "BarTitle", 15, FontStyles.Bold, font);
        titleTop.text = Loc.Choose("好友聊天", "Friend Chat");
        titleTop.color = MultiplayerUiPalette.TextGold;
        var titleLe = titleTop.GetComponent<LayoutElement>();
        if (titleLe != null)
        {
            // 按你截图：BarTitle Width=200 Height=50
            titleLe.preferredWidth = 200f;
            titleLe.minHeight = 50f;
            titleLe.flexibleWidth = 0f;
        }
        var spTop = new GameObject("Sp");
        spTop.transform.SetParent(topBar.transform, false);
        // Sp 改成小占位，不再吃满
        var spLe = spTop.AddComponent<LayoutElement>();
        spLe.preferredWidth = 10f;
        spLe.minWidth = 8f;
        spLe.flexibleWidth = 0f;
        HubStyle.CreateSmallBarButton(topBar.transform, "X", ToggleSidebar, font);

        var statusGo = new GameObject("StatusRow");
        statusGo.transform.SetParent(root, false);
        statusGo.AddComponent<LayoutElement>().minHeight = 18f;
        _statusText = HubStyle.CreateTmpText(statusGo.transform, "Status", 13, FontStyles.Normal, font);
        _statusText.color = MultiplayerUiPalette.TextRed;
        var stLe = _statusText.GetComponent<LayoutElement>();
        if (stLe != null) stLe.minHeight = 16f;

        var annGo = new GameObject("AnnouncementRow");
        annGo.transform.SetParent(root, false);
        annGo.AddComponent<LayoutElement>().minHeight = 92f;
        annGo.AddComponent<Image>().color = MultiplayerUiPalette.BgGoldTint;
        var annVlg = annGo.AddComponent<VerticalLayoutGroup>();
        annVlg.padding = new RectOffset(8, 8, 4, 6);
        annVlg.spacing = 2;
        CreateTmpTitle(annGo.transform, Loc.Choose("公告", "Notice"), 14, font);
        _announcementBody = HubStyle.CreateTmpText(annGo.transform, "AnnouncementBody", 13, FontStyles.Normal, font);
        _announcementBody.color = HubStyle.WxTextDark;
        _announcementBody.enableWordWrapping = true;
        HubStyle.MakeScrollableMultilineBlock(_announcementBody, 52f);

        var mgmtScrollGo = new GameObject("MgmtScroll");
        mgmtScrollGo.transform.SetParent(root, false);
        var mgmtScrollLe = mgmtScrollGo.AddComponent<LayoutElement>();
        // 「好友管理」区块高度（加好友/待处理申请这整块）
        mgmtScrollLe.preferredHeight = 320f;
        mgmtScrollLe.flexibleHeight = 0f;
        var mgScroll = mgmtScrollGo.AddComponent<ScrollRect>();
        mgScroll.horizontal = false;
        // 好友管理区改为尽量整块显示，不依赖滚动查看
        mgScroll.vertical = false;
        mgScroll.movementType = ScrollRect.MovementType.Clamped;
        mgScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

        var mgVp = new GameObject("Viewport");
        mgVp.transform.SetParent(mgmtScrollGo.transform, false);
        var mgVRt = mgVp.AddComponent<RectTransform>();
        mgVRt.anchorMin = Vector2.zero;
        mgVRt.anchorMax = Vector2.one;
        mgVRt.offsetMin = Vector2.zero;
        mgVRt.offsetMax = Vector2.zero;
        mgVp.AddComponent<RectMask2D>();
        mgScroll.viewport = mgVRt;

        var mgContent = new GameObject("Content");
        mgContent.transform.SetParent(mgVp.transform, false);
        var mgCRt = mgContent.AddComponent<RectTransform>();
        mgCRt.anchorMin = new Vector2(0f, 1f);
        mgCRt.anchorMax = new Vector2(1f, 1f);
        mgCRt.pivot = new Vector2(0f, 1f);
        var mgVlg = mgContent.AddComponent<VerticalLayoutGroup>();
        mgVlg.padding = new RectOffset(10, 10, 6, 8);
        mgVlg.spacing = 6;
        mgVlg.childAlignment = TextAnchor.UpperLeft;
        mgVlg.childControlWidth = true;
        mgVlg.childForceExpandWidth = true;
        mgVlg.childControlHeight = true;
        mgVlg.childForceExpandHeight = false;
        mgContent.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        mgScroll.content = mgCRt;

        // 不再为好友管理区预留滚动条

        Transform mgLay = mgContent.transform;
        CreateTmpTitle(mgLay, Loc.Choose("好友管理", "Friend Management"), 16, font);
        _addFriendInput = HubStyle.CreateTmpInput(mgLay, Loc.Choose("对方用户名或玩家数字ID", "Username or player ID"), font);
        HubStyle.CreateWeChatGreenButton(mgLay, Loc.Choose("发送好友申请", "Send Friend Request"), UiSendFriendRequest, font);
        _pendingFriendsText = HubStyle.CreateTmpText(mgLay, "Pending", 13, FontStyles.Normal, font);
        _pendingFriendsText.color = HubStyle.WxTextMuted;
        HubStyle.MakeScrollableMultilineBlock(_pendingFriendsText, 64f);
        _friendRequestIdInput = HubStyle.CreateTmpInput(mgLay, Loc.Choose("申请编号（数字）", "Request ID (number)"), font);
        var rowResp = new GameObject("RowResp");
        rowResp.transform.SetParent(mgLay, false);
        var rowH = rowResp.AddComponent<HorizontalLayoutGroup>();
        rowH.spacing = 8;
        rowH.childForceExpandWidth = true;
        rowResp.AddComponent<LayoutElement>().minHeight = 30f;
        HubStyle.CreateOutlineButton(rowResp.transform, Loc.Choose("同意", "Accept"), () => UiRespondFriendFromInput(true), font);
        HubStyle.CreateOutlineButton(rowResp.transform, Loc.Choose("拒绝", "Decline"), () => UiRespondFriendFromInput(false), font);

        var mainRow = new GameObject("MainSplit");
        mainRow.transform.SetParent(root, false);
        var mainLe = mainRow.AddComponent<LayoutElement>();
        mainLe.flexibleHeight = 1f;
        // 中部聊天区最小高度（太小会压扁左右列表和输入区）
        mainLe.minHeight = 260f;
        mainRow.AddComponent<Image>().color = HubStyle.WxShellBg;
        var mainH = mainRow.AddComponent<HorizontalLayoutGroup>();
        mainH.spacing = 0;
        mainH.childControlWidth = true;
        mainH.childForceExpandWidth = false;
        mainH.childControlHeight = true;
        mainH.childForceExpandHeight = true;

        var leftCol = new GameObject("FriendListColumn");
        leftCol.transform.SetParent(mainRow.transform, false);
        leftCol.AddComponent<Image>().color = HubStyle.WxListBg;
        var leftLe = leftCol.AddComponent<LayoutElement>();
        // 左侧好友列表列宽（调这里可改变左/右两列占比）
        leftLe.preferredWidth = 210f;
        leftLe.flexibleWidth = 0f;
        leftLe.flexibleHeight = 1f;

        var leftScrollGo = new GameObject("FriendScroll");
        leftScrollGo.transform.SetParent(leftCol.transform, false);
        var leftSrt = leftScrollGo.AddComponent<RectTransform>();
        leftSrt.anchorMin = Vector2.zero;
        leftSrt.anchorMax = Vector2.one;
        leftSrt.offsetMin = new Vector2(0f, 0f);
        leftSrt.offsetMax = new Vector2(0f, 0f);
        var leftScroll = leftScrollGo.AddComponent<ScrollRect>();
        leftScroll.horizontal = false;
        leftScroll.vertical = true;
        leftScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

        var lfVp = new GameObject("Viewport");
        lfVp.transform.SetParent(leftScrollGo.transform, false);
        var lfVRt = lfVp.AddComponent<RectTransform>();
        lfVRt.anchorMin = Vector2.zero;
        lfVRt.anchorMax = Vector2.one;
        lfVRt.offsetMin = Vector2.zero;
        lfVRt.offsetMax = new Vector2(-(scrollBarW + 2f), 0f);
        lfVp.AddComponent<RectMask2D>();
        leftScroll.viewport = lfVRt;

        var lfContent = new GameObject("Content");
        lfContent.transform.SetParent(lfVp.transform, false);
        var lfCRt = lfContent.AddComponent<RectTransform>();
        lfCRt.anchorMin = new Vector2(0f, 1f);
        lfCRt.anchorMax = new Vector2(1f, 1f);
        lfCRt.pivot = new Vector2(0f, 1f);
        var lfVlg = lfContent.AddComponent<VerticalLayoutGroup>();
        lfVlg.padding = new RectOffset(0, 0, 6, 6);
        lfVlg.spacing = 1;
        lfVlg.childControlWidth = true;
        lfVlg.childForceExpandWidth = true;
        lfVlg.childControlHeight = true;
        lfVlg.childForceExpandHeight = false;
        lfContent.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        leftScroll.content = lfCRt;
        _friendListContent = lfContent.transform;

        var lfSb = HubStyle.BuildVerticalScrollbar(leftScrollGo.transform, scrollBarW - 2f, 4f);
        leftScroll.verticalScrollbar = lfSb;

        var rightCol = new GameObject("ChatColumn");
        rightCol.transform.SetParent(mainRow.transform, false);
        var rightLe = rightCol.AddComponent<LayoutElement>();
        rightLe.flexibleWidth = 1f;
        rightLe.flexibleHeight = 1f;
        var rightV = rightCol.AddComponent<VerticalLayoutGroup>();
        rightV.spacing = 0;
        rightV.childControlHeight = true;
        rightV.childForceExpandHeight = false;
        rightV.childControlWidth = true;
        rightV.childForceExpandWidth = true;

        var channelRow = new GameObject("ChannelRow");
        channelRow.transform.SetParent(rightCol.transform, false);
        channelRow.AddComponent<Image>().color = HubStyle.WxBarBg;
        channelRow.AddComponent<LayoutElement>().minHeight = 38f;
        var chH = channelRow.AddComponent<HorizontalLayoutGroup>();
        chH.padding = new RectOffset(8, 8, 4, 4);
        chH.spacing = 8;
        chH.childAlignment = TextAnchor.MiddleLeft;
        var chHint = HubStyle.CreateTmpText(channelRow.transform, "ChHint", 13, FontStyles.Normal, font);
        chHint.text = Loc.Choose("聊天", "Chat");
        chHint.color = HubStyle.WxTextMuted;
        chHint.GetComponent<LayoutElement>().preferredWidth = 40f;
        HubStyle.CreateOutlineButton(channelRow.transform, Loc.Choose("好友私聊", "Friend DM"), () => SetChatChannel(ChatChannelKind.Friend), font);
        HubStyle.CreateOutlineButton(channelRow.transform, Loc.Choose("世界频道", "World Channel"), () => SetChatChannel(ChatChannelKind.World), font);

        var chatHead = new GameObject("ChatHeader");
        chatHead.transform.SetParent(rightCol.transform, false);
        chatHead.AddComponent<Image>().color = HubStyle.WxBarBg;
        // 右侧聊天对象标题栏高度（显示“昵称 · ID”）
        chatHead.AddComponent<LayoutElement>().minHeight = 26f;
        var headH = chatHead.AddComponent<HorizontalLayoutGroup>();
        headH.padding = new RectOffset(10, 10, 2, 2);
        headH.childAlignment = TextAnchor.MiddleLeft;
        headH.spacing = 0;
        headH.childControlWidth = true;
        headH.childForceExpandWidth = true;
        headH.childControlHeight = true;
        headH.childForceExpandHeight = false;
        _chatHeaderTitle = HubStyle.CreateTmpText(chatHead.transform, "HeaderTitle", 17, FontStyles.Bold, font);
        _chatHeaderTitle.text = Loc.Choose("选择一个好友", "Select a friend");
        _chatHeaderTitle.color = HubStyle.WxTextDark;
        _chatHeaderTitle.alignment = TextAlignmentOptions.MidlineLeft;
        var hTitleLe = _chatHeaderTitle.GetComponent<LayoutElement>();
        if (hTitleLe != null)
        {
            hTitleLe.minHeight = 18f;
            hTitleLe.flexibleWidth = 1f;
        }

        var chatScrollGo = new GameObject("ChatScroll");
        chatScrollGo.transform.SetParent(rightCol.transform, false);
        chatScrollGo.AddComponent<LayoutElement>().flexibleHeight = 1f;
        chatScrollGo.AddComponent<Image>().color = HubStyle.WxChatBg;
        _chatMessageScroll = chatScrollGo.AddComponent<ScrollRect>();
        _chatMessageScroll.horizontal = false;
        _chatMessageScroll.vertical = true;
        _chatMessageScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

        var cVp = new GameObject("Viewport");
        cVp.transform.SetParent(chatScrollGo.transform, false);
        var cVRt = cVp.AddComponent<RectTransform>();
        cVRt.anchorMin = Vector2.zero;
        cVRt.anchorMax = Vector2.one;
        cVRt.offsetMin = Vector2.zero;
        cVRt.offsetMax = new Vector2(-(scrollBarW + 4f), 0f);
        cVp.AddComponent<RectMask2D>();
        _chatMessageScroll.viewport = cVRt;

        var cContent = new GameObject("Content");
        cContent.transform.SetParent(cVp.transform, false);
        var cCRt = cContent.AddComponent<RectTransform>();
        cCRt.anchorMin = new Vector2(0f, 1f);
        cCRt.anchorMax = new Vector2(1f, 1f);
        cCRt.pivot = new Vector2(0f, 1f);
        var cVlg = cContent.AddComponent<VerticalLayoutGroup>();
        cVlg.padding = new RectOffset((int)pad, (int)pad, (int)pad, (int)pad);
        cVlg.spacing = 6;
        cVlg.childControlWidth = true;
        cVlg.childForceExpandWidth = true;
        cVlg.childControlHeight = true;
        cVlg.childForceExpandHeight = false;
        cContent.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        _chatMessageScroll.content = cCRt;
        _chatBubbleContent = cContent.transform;

        var cSb = HubStyle.BuildVerticalScrollbar(chatScrollGo.transform, scrollBarW, 4f);
        _chatMessageScroll.verticalScrollbar = cSb;

        var inputRow = new GameObject("InputRow");
        inputRow.transform.SetParent(rightCol.transform, false);
        inputRow.AddComponent<Image>().color = HubStyle.WxBarBg;
        // 底部输入区总高度（输入框+发送按钮）
        inputRow.AddComponent<LayoutElement>().minHeight = 30f;
        var inH = inputRow.AddComponent<HorizontalLayoutGroup>();
        // 输入区内边距（左,右,上,下）
        inH.padding = new RectOffset(6, 6, 2, 2);
        inH.spacing = 6;
        inH.childAlignment = TextAnchor.MiddleLeft;
        inH.childForceExpandWidth = true;
        _chatInput = HubStyle.CreateTmpInput(inputRow.transform, Loc.Choose("发消息…", "Message…"), font);
        var inLe = _chatInput.GetComponent<LayoutElement>();
        if (inLe != null)
        {
            // 按你截图：Input_发消息 Width=460 Height=40
            inLe.preferredWidth = 460f;
            inLe.flexibleWidth = 0f;
            inLe.minHeight = 40f;
        }
        // 发送按钮宽度（最后一个参数）
        // 按你截图：Btn_发送 Width=100 Height=40
        HubStyle.CreateWeChatGreenButtonFixed(inputRow.transform, Loc.Choose("发送", "Send"), UiSendChat, font, 100f);

        _hornRow = new GameObject("HornRow");
        _hornRow.transform.SetParent(rightCol.transform, false);
        _hornRow.AddComponent<Image>().color = HubStyle.WxBarBg;
        _hornRow.AddComponent<LayoutElement>().minHeight = 44f;
        var hornH = _hornRow.AddComponent<HorizontalLayoutGroup>();
        hornH.padding = new RectOffset(6, 6, 2, 2);
        hornH.spacing = 6;
        hornH.childAlignment = TextAnchor.MiddleLeft;
        hornH.childForceExpandWidth = true;
        _hornInput = HubStyle.CreateTmpInput(_hornRow.transform, Loc.Choose("全服喇叭（40字内，100金币）", "Server horn (40 chars, 100 coins)"), font);
        var hornInLe = _hornInput.GetComponent<LayoutElement>();
        if (hornInLe != null)
        {
            hornInLe.preferredWidth = 520f;
            hornInLe.flexibleWidth = 1f;
            hornInLe.minHeight = 36f;
        }
        _hornInput.characterLimit = CoopNetworkPlayer.ServerHornMaxChars;
        HubStyle.CreateWeChatGreenButtonFixed(_hornRow.transform, Loc.Choose("发送喇叭", "Send Horn"), UiSendServerHorn, font, 120f);
        _hornRow.SetActive(false);

        var binder = sidebarRoot.GetComponent<FriendChatSidebarUiBinder>() ?? sidebarRoot.AddComponent<FriendChatSidebarUiBinder>();
        binder.font = font;
        binder.statusText = _statusText;
        binder.pendingFriendsText = _pendingFriendsText;
        binder.chatHeaderTitle = _chatHeaderTitle;
        binder.addFriendInput = _addFriendInput;
        binder.friendRequestIdInput = _friendRequestIdInput;
        binder.chatInput = _chatInput;
        binder.friendListContent = _friendListContent;
        binder.chatBubbleContent = _chatBubbleContent;
        binder.chatMessageScroll = _chatMessageScroll;
        binder.announcementBody = _announcementBody;
        binder.hornInput = _hornInput;
        binder.hornRow = _hornRow;
        BindSceneUi(binder);
        WireSidebarButtonsIfNeeded();
        StripTmpRaycastsThatStealClicksUnderSidebar();

        ApplyLeftFullHeightSidebarLayout(rt);
        sidebarRoot.SetActive(false);

        if (dontDestroyCanvas)
            EnsureUiEventSystemExists();
        else if (createEventSystemIfMissing && FindObjectsOfType<EventSystem>(true).Length == 0)
        {
            var esGo = new GameObject(PersistentEventSystemName);
            esGo.AddComponent<EventSystem>();
            EnsureStandaloneInputModule(esGo);
        }
    }

    static void CreateTmpTitle(Transform parent, string text, float size, TMP_FontAsset font)
    {
        var t = HubStyle.CreateTmpText(parent, "Title", size, FontStyles.Bold, font);
        t.text = text;
        t.color = MultiplayerUiPalette.TextGold;
    }

    static class HubStyle
    {
        public static readonly Color WxShellBg = MultiplayerUiPalette.BgWhite;
        public static readonly Color WxChatBg = MultiplayerUiPalette.BgSubtle;
        public static readonly Color WxListBg = MultiplayerUiPalette.BgSubtle;
        public static readonly Color WxBarBg = MultiplayerUiPalette.BgWhite;
        public static readonly Color WxTextDark = MultiplayerUiPalette.TextBlack;
        public static readonly Color WxTextMuted = MultiplayerUiPalette.TextMuted;
        public static readonly Color WxGreen = MultiplayerUiPalette.TextGold;
        public static readonly Color WxSelfBubble = MultiplayerUiPalette.BgGoldTint;
        public static readonly Color WxOtherBubble = MultiplayerUiPalette.BgWhite;

        public static Scrollbar BuildVerticalScrollbar(Transform parent, float width, float marginFromEdge)
        {
            var root = new GameObject("ScrollbarVertical");
            root.transform.SetParent(parent, false);
            var rt = root.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.sizeDelta = new Vector2(width, -marginFromEdge * 2f);
            rt.anchoredPosition = new Vector2(-marginFromEdge, 0f);
            root.AddComponent<Image>().color = new Color(0.96f, 0.96f, 0.96f, 0.95f);
            var sb = root.AddComponent<Scrollbar>();
            sb.direction = Scrollbar.Direction.BottomToTop;

            var slide = new GameObject("SlidingArea");
            slide.transform.SetParent(root.transform, false);
            var srt = slide.AddComponent<RectTransform>();
            srt.anchorMin = Vector2.zero;
            srt.anchorMax = Vector2.one;
            srt.offsetMin = new Vector2(2f, 4f);
            srt.offsetMax = new Vector2(-2f, -4f);

            var handle = new GameObject("Handle");
            handle.transform.SetParent(slide.transform, false);
            var hi = handle.AddComponent<Image>();
            hi.color = new Color(0.55f, 0.55f, 0.58f, 0.9f);
            var hrt = handle.GetComponent<RectTransform>();
            hrt.anchorMin = Vector2.zero;
            hrt.anchorMax = Vector2.one;
            hrt.offsetMin = Vector2.zero;
            hrt.offsetMax = Vector2.zero;
            sb.targetGraphic = hi;
            sb.handleRect = hrt;
            return sb;
        }

        public static void MakeScrollableMultilineBlock(TextMeshProUGUI t, float minHeight = 40f)
        {
            if (t == null) return;
            t.enableWordWrapping = true;
            t.overflowMode = TextOverflowModes.Overflow;
            var le = t.GetComponent<LayoutElement>();
            if (le != null)
            {
                le.minHeight = minHeight;
                le.preferredHeight = -1f;
            }
            var fit = t.gameObject.GetComponent<ContentSizeFitter>();
            if (fit == null) fit = t.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        public static TextMeshProUGUI CreateTmpText(Transform parent, string name, float fontSize, FontStyles fs, TMP_FontAsset font)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = fontSize;
            t.fontStyle = fs;
            t.color = WxTextDark;
            t.text = "";
            t.alignment = TextAlignmentOptions.TopLeft;
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = fontSize + 8;
            le.flexibleWidth = 1;
            t.raycastTarget = false;
            return t;
        }

        public static TMP_InputField CreateTmpInput(Transform parent, string placeholder, TMP_FontAsset font)
        {
            var go = new GameObject("Input_" + placeholder);
            go.transform.SetParent(parent, false);
            go.AddComponent<Image>().color = MultiplayerUiPalette.InputBg;
            var input = go.AddComponent<TMP_InputField>();
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = 30;
            le.flexibleWidth = 1;
            var textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            var text = textGo.AddComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.fontSize = 15;
            text.color = WxTextDark;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            var tr = text.rectTransform;
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = new Vector2(10, 6);
            tr.offsetMax = new Vector2(-10, -6);
            input.textComponent = text;
            var phGo = new GameObject("Placeholder");
            phGo.transform.SetParent(go.transform, false);
            var ph = phGo.AddComponent<TextMeshProUGUI>();
            if (font != null) ph.font = font;
            ph.fontSize = 15;
            ph.color = new Color(WxTextMuted.r, WxTextMuted.g, WxTextMuted.b, 0.65f);
            ph.text = placeholder;
            ph.alignment = TextAlignmentOptions.MidlineLeft;
            var pr = ph.rectTransform;
            pr.anchorMin = Vector2.zero;
            pr.anchorMax = Vector2.one;
            pr.offsetMin = new Vector2(10, 6);
            pr.offsetMax = new Vector2(-10, -6);
            input.placeholder = ph;
            text.raycastTarget = false;
            ph.raycastTarget = false;
            return input;
        }

        public static void CreateFriendRow(Transform parent, FriendEntryDto f, UnityEngine.Events.UnityAction onClick, TMP_FontAsset font)
        {
            var go = new GameObject("Friend_" + f.user_id);
            go.transform.SetParent(parent, false);
            go.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.35f);
            var btn = go.AddComponent<Button>();
            var cb = btn.colors;
            cb.highlightedColor = new Color(0.92f, 0.92f, 0.92f);
            cb.pressedColor = new Color(0.86f, 0.86f, 0.86f);
            btn.colors = cb;
            btn.onClick.AddListener(onClick);
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = 58f;
            le.flexibleWidth = 1f;
            var v = go.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(12, 10, 10, 10);
            v.spacing = 2;
            v.childAlignment = TextAnchor.UpperLeft;

            var tName = CreateTmpText(go.transform, "N", 16, FontStyles.Bold, font);
            tName.text = string.IsNullOrEmpty(f.username) ? Loc.Choose("好友", "Friend") : f.username;
            tName.color = WxTextDark;
            var tId = CreateTmpText(go.transform, "IdLine", 13, FontStyles.Normal, font);
            tId.text = Loc.F("点击展开会话 · ID {0}", "Tap to open chat · ID {0}", f.user_id);
            tId.color = WxTextMuted;
            var leId = tId.GetComponent<LayoutElement>();
            if (leId != null) leId.minHeight = 18f;
        }

        // 统一左侧日志样式：ID + 内容，不做左右气泡分列。
        public static void AddWorldChannelLine(Transform parent, string speaker, string body, TMP_FontAsset font, float maxW = 360f)
        {
            var row = new GameObject("WorldLine");
            row.transform.SetParent(parent, false);
            row.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.45f);
            var rowLe = row.AddComponent<LayoutElement>();
            rowLe.flexibleWidth = 1f;
            var txt = CreateTmpText(row.transform, "Line", 15, FontStyles.Normal, font);
            txt.color = WxTextDark;
            txt.alignment = TextAlignmentOptions.MidlineLeft;
            txt.enableWordWrapping = true;
            txt.overflowMode = TextOverflowModes.Overflow;
            string line = Loc.F("[世界] {0}：{1}", "[World] {0}: {1}", speaker ?? "?", body ?? "");
            txt.text = line;
            var pv = txt.GetPreferredValues(line, maxW - 16f, 0f);
            rowLe.minHeight = Mathf.Max(28f, pv.y + 10f);
            var rt = txt.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(8f, 4f);
            rt.offsetMax = new Vector2(-8f, -4f);
        }

        public static void AddChatBubble(Transform parent, bool mine, string body, TMP_FontAsset font, int selfId, int peerId, float maxW = 320f)
        {
            var row = new GameObject("RowLog");
            row.transform.SetParent(parent, false);
            row.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.45f);
            var rowLe = row.AddComponent<LayoutElement>();
            rowLe.flexibleWidth = 1f;

            int showId = mine ? Mathf.Max(0, selfId) : Mathf.Max(0, peerId);
            var txt = CreateTmpText(row.transform, "Line", 15, FontStyles.Normal, font);
            txt.color = WxTextDark;
            txt.alignment = TextAlignmentOptions.MidlineLeft;
            txt.enableWordWrapping = true;
            txt.overflowMode = TextOverflowModes.Overflow;
            string line = $"ID {showId}: {body ?? ""}";
            txt.text = line;
            // 按文本真实高度撑开每一行，避免多行内容互相重叠。
            var pv = txt.GetPreferredValues(line, maxW - 16f, 0f);
            rowLe.minHeight = Mathf.Max(28f, pv.y + 10f);
            var rt = txt.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(8f, 4f);
            rt.offsetMax = new Vector2(-8f, -4f);
        }

        public static void CreateWeChatGreenButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick, TMP_FontAsset font)
        {
            var go = new GameObject("Btn_" + label);
            go.transform.SetParent(parent, false);
            go.AddComponent<Image>().color = WxGreen;
            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(onClick);
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = 30;
            le.flexibleWidth = 1;
            var tgo = new GameObject("Text");
            tgo.transform.SetParent(go.transform, false);
            var tmp = tgo.AddComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.text = label;
            tmp.fontSize = 15;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = WxTextDark;
            tmp.raycastTarget = false;
            var tr = tmp.rectTransform;
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = Vector2.zero;
            tr.offsetMax = Vector2.zero;
        }

        public static void CreateWeChatGreenButtonFixed(Transform parent, string label, UnityEngine.Events.UnityAction onClick, TMP_FontAsset font, float width)
        {
            var go = new GameObject("Btn_" + label);
            go.transform.SetParent(parent, false);
            go.AddComponent<Image>().color = WxGreen;
            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(onClick);
            var le = go.AddComponent<LayoutElement>();
            // 固定宽按钮：height 固定 40，width 由参数决定（用于底部发送按钮）
            le.minHeight = 40;
            le.preferredWidth = width;
            le.flexibleWidth = 0f;
            var tgo = new GameObject("Text");
            tgo.transform.SetParent(go.transform, false);
            var tmp = tgo.AddComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.text = label;
            tmp.fontSize = 15;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = WxTextDark;
            tmp.raycastTarget = false;
            var tr = tmp.rectTransform;
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = Vector2.zero;
            tr.offsetMax = Vector2.zero;
        }

        public static void CreateOutlineButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick, TMP_FontAsset font)
        {
            var go = new GameObject("Btn_" + label);
            go.transform.SetParent(parent, false);
            go.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.9f);
            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(onClick);
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = 28;
            le.flexibleWidth = 1;
            var tgo = new GameObject("Text");
            tgo.transform.SetParent(go.transform, false);
            var tmp = tgo.AddComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.text = label;
            tmp.fontSize = 14;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = WxTextDark;
            tmp.raycastTarget = false;
            var tr = tmp.rectTransform;
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = Vector2.zero;
            tr.offsetMax = Vector2.zero;
        }

        public static void CreateSmallBarButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick, TMP_FontAsset font)
        {
            var go = new GameObject("Btn_" + label);
            go.transform.SetParent(parent, false);
            go.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.08f);
            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(onClick);
            var le = go.AddComponent<LayoutElement>();
            // 按你截图：Btn_X Width=50 Height=50
            le.minHeight = 50f;
            le.preferredWidth = 50f;
            var tgo = new GameObject("Text");
            tgo.transform.SetParent(go.transform, false);
            var tmp = tgo.AddComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.text = label;
            tmp.fontSize = 14;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = MultiplayerUiPalette.TextBlack;
            tmp.raycastTarget = false;
            var tr = tmp.rectTransform;
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = Vector2.zero;
            tr.offsetMax = Vector2.zero;
        }
    }
}
