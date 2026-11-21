using System;
using System.Collections;
using System.Text;
using Mirror;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using Multiplayer;

/// <summary>
/// 连接 GameServer（默认地址见 <see cref="GameServerEndpoint.DefaultBaseUrl"/>）：注册/登录、每秒上报场景与坐标、启动时按服务器记录恢复位置。
/// 需在场景中存在带 PlayerMovement 且 Tag 为 Player 的角色（或由你指定 playerMovement）。
/// </summary>
public class GameServerSync : MonoBehaviour
{
    const string PrefsTokenKey = "GameServer_Token";
    const string PrefsUsernameKey = "GameServer_Username";
    const string PrefsNicknameKey = "GameServer_Nickname";
    const string PrefsCharacterIdKey = "GameServer_CharacterId_v1";
    const string PrefsCloneAuthClearedOnceKey = "GameServer_CloneAuthClearedOnce_v1";

    /// <summary>与 NewUserIntroCinematic 共用：注册成功待播开场（避免切场景前尚无订阅者时丢失）。</summary>
    public const string PrefsIntroCinematicPendingUserKey = "IntroCinematicPendingUser_v1";

    [Header("服务器")]
    [Tooltip("终端 GameServer 根地址。留空则使用 GameServerEndpoint.DefaultBaseUrl；亦可用 PlayerPrefs 键 GameServer_BaseUrl_Override 运行时覆盖，见 GameServerEndpoint。")]
    [SerializeField] string baseUrl = GameServerEndpoint.DefaultBaseUrl;
    [Tooltip("检测到“生成客户端/克隆实例”时，启动即清除本地登录缓存（token/用户名），避免沿用主实例账号导致任务 UI 不刷新。")]
    [SerializeField] bool clearSavedAuthOnGeneratedClientStartup = true;

    [Header("引用")]
    [SerializeField] PlayerMovement playerMovement;
    [Tooltip("为空时在场景中查找 CameraSwitcher（C1C3transfer），用于上报/恢复第一、三人称")]
    [SerializeField] CameraSwitcher cameraSwitcher;

    [Header("行为")]
    [Tooltip("进入 Play 后若本地已有 token，是否向服务器拉取「最近一次」所在场景与位置（不限定场景名）")]
    [SerializeField] bool restorePositionOnStart = true;

    [Tooltip("为 true 时：仅当当前场景内已有 PlayerMovement 才执行「启动时恢复」。菜单等无角色场景请保持勾选，避免一进来就被拉回关卡。")]
    [SerializeField] bool restorePositionOnStartOnlyIfPlayerInScene = true;

    [Tooltip("每次场景加载完成后，按「当前场景名」向服务器拉取该地图独立存档并传送（传送换场景时请保持开启）")]
    [SerializeField] bool restorePositionForCurrentSceneOnLoad = true;

    [Tooltip("场景加载并处理存档后，立即向服务器上报一次当前场景与坐标（避免未到同步间隔就退出导致下次读到空档/原点）")]
    [SerializeField] bool pushPositionToServerOnSceneLoaded = true;

    [Tooltip("登录成功后持续每隔此秒数上报位置（建议 1）")]
    [SerializeField] float positionSyncIntervalSeconds = 1f;

    [Tooltip("仅在编辑器或开发构建中打印请求日志")]
    [SerializeField] bool verboseLogging = true;

    [Tooltip("勾选后本物体跨场景保留，避免切场景时停止位置上报（同一物体请勿重复放置多个）")]
    [SerializeField] bool dontDestroyOnLoad = true;

    [Tooltip("在从服务器拉取并应用出生点/场景存档完成前，禁止移动、地图、第一/三人称视角操作（避免未同步到位时乱跑）")]
    [SerializeField] bool lockInputUntilServerSpawnApplied = true;
    [Header("定向修复：重建 Directional Light")]
    [Tooltip("切场景后，若目标场景名匹配则运行时删除并重建该场景中的 Directional Light。")]
    [SerializeField] bool recreateDirectionalLightOnSceneLoad = true;
    [SerializeField] string recreateDirectionalLightSceneName = "undergarden";
    [SerializeField] Vector3 recreatedDirectionalLightEuler = new Vector3(50f, -30f, 0f);
    [SerializeField] float recreatedDirectionalLightIntensity = 1f;
    [SerializeField] Color recreatedDirectionalLightColor = Color.white;

    [Tooltip("为 true：登录/注册成功后不自动拉取存档与传送，需由菜单调用 EnterWorldFromServerSave()（例如先展示传送门再点进入）")]
    [SerializeField] bool deferSpawnFetchUntilEnterWorld = false;

    [Tooltip("在 PortalEnter（点击进入世界）场景下，若服务器无存档或拉取失败，则自动进入兜底场景。")]
    [SerializeField] bool loadFallbackSceneWhenPortalEnterHasNoSaved = true;
    [Tooltip("兜底进入场景名（需在 Build Settings 中）。例如：MainScene")]
    [SerializeField] string fallbackWorldSceneName = "MainScene";

    [Header("登录后全局存档")]
    [Tooltip("仅在这些场景名（与 Build 中场景名完全一致）下登录/注册时，才允许根据服务器「最近一条」记录 LoadScene 到其它场景。在关卡内登录时不要加入，否则会因服务器尚未更新而把玩家拉回旧场景。")]
    [SerializeField] string[] scenesAllowingGlobalSpawnRedirectAfterAuth =
    {
        "Menu",
        "MainMenu",
    };

    [Header("本机离线 / 无 GameServer")]
    [Tooltip("为 true：不做「冷启动 → 强制进 Loading 做 connection_check」；不因健康检查踢回登录；进场景时也不拉取/上报云存档（任意场景可直接 Play 调试）。为 false：恢复原先依赖终端服务器的冷加载与云同步。")]
    [SerializeField] bool allowLocalPlayWithoutGameServer = false;

    public bool AllowLocalPlayWithoutGameServer => allowLocalPlayWithoutGameServer;

    public const string OfflineGuestTokenPrefix = "__offline_guest__";
    public const string OfflineGuestUsername = "离线访客";
    public const string OfflineDefaultWorldScene = "undergarden";

    public bool IsOfflineGuestSession =>
        IsLoggedIn && (AuthToken ?? "").StartsWith(OfflineGuestTokenPrefix, StringComparison.Ordinal);

    /// <summary>
    /// 登录界面「离线游玩」：启用本机无服模式、写入本地访客 token，并设置 PortalEnter 兜底场景。
    /// 进厂仍由 MenuLoginPortalFlow.EnterWorld → EnterWorldFromServerSave 处理。
    /// </summary>
    public void BeginOfflineGuestSession(string fallbackSceneName = OfflineDefaultWorldScene)
    {
        allowLocalPlayWithoutGameServer = true;
        loadFallbackSceneWhenPortalEnterHasNoSaved = true;
        if (!string.IsNullOrWhiteSpace(fallbackSceneName))
            fallbackWorldSceneName = fallbackSceneName.Trim();

        var token = OfflineGuestTokenPrefix + Guid.NewGuid().ToString("N");
        SaveAuth(token, OfflineGuestUsername);

        if (verboseLogging)
            Debug.Log($"[GameServerSync] 离线游玩已就绪，兜底场景: {fallbackWorldSceneName}");
    }

    [Header("冷启动与 Loading 校验")]
    [Tooltip("关闭后不再请求 /api/connection_check、不因 cold_session 强制跳转 Loading，也不在启动时按服务器最近存档跨场景加载；登录/登出/进入世界流程不受影响。")]
    [SerializeField] bool enableColdStartLoading = false;
    [Tooltip("服务器判定 cold_session 时，若当前不在此场景则 Single 加载，用于账号校验（须与 Build 一致）。服务端空闲阈值默认 60s，见 server.py COLD_SESSION_IDLE_SEC。需先开启 enableColdStartLoading；若勾选「允许无 GameServer 本机游玩」则不会执行此跳转。")]
    [SerializeField] string loadingSceneName = "Loadingscene";
    [Header("在线健康检查")]
    [Tooltip("登录后每隔固定秒数检测服务器可用性；失败时自动退回 Loading 登录页。若勾选「允许无 GameServer 本机游玩」则不会启动此检查。")]
    [SerializeField] bool enableServerHealthWatch = true;
    [SerializeField] float serverHealthCheckIntervalSeconds = 10f;
    [SerializeField] int serverHealthFailThreshold = 1;

    [Header("联机面板热键")]
    [Tooltip("GameServer 物体通常 DontDestroyOnLoad：在此轮询可避免「联机脚本只在菜单场景」时进关后按 K/C 无响应。按键绑定见 GameControls（OpenMultiplayer / OpenFriends）。")]
    [SerializeField] bool pollMultiplayerPanelHotkeyHere = true;
    [SerializeField] bool pollFlagAuctionPanelHotkeyHere = true;
    [Tooltip("关闭联机面板后是否锁鼠标（与 MultiplayerEntryToggle 一致）")]
    [SerializeField] bool multiplayerHotkeyLockCursorWhenClosed = true;

    /// <summary>每次场景加载完成递增；用于判断「拉全局存档」期间玩家是否已本地换场景。</summary>
    int _sceneLoadGeneration;

    string AuthToken => PlayerPrefs.GetString(PrefsTokenKey, "");
    public string AuthTokenForExtensions => AuthToken;
    public string BaseUrlTrimmed => (baseUrl ?? "").TrimEnd('/');

    public bool IsLoggedIn => !string.IsNullOrEmpty(AuthToken);
    public string SavedUsername => PlayerPrefs.GetString(PrefsUsernameKey, "");
    public string SavedNickname => PlayerPrefs.GetString(PrefsNicknameKey, "");
    public string SavedCharacterId => PlayerPrefs.GetString(PrefsCharacterIdKey, "");

    public static event Action<string, string> OnAuthSuccess;
    public static event Action<string> OnAuthFailure;

    /// <summary>已清除本地 token 后触发；用于清空 DontDestroy 下的任务/金币等，避免新号仍显示上一账号进度。</summary>
    public static event Action OnLoggedOut;

    /// <summary>仅注册成功时触发（登录不会触发）。用于新账号首次开场动画等。</summary>
    public static event Action<string> OnRegisterSuccess;

    public static GameServerSync Instance { get; private set; }

    /// <summary>用于 PlayerMovement / 相机：是否因服务器存档同步而禁止玩家输入。</summary>
    public static bool BlocksInputForServerSpawn =>
        Instance != null && Instance._spawnInputLockCount > 0;

    /// <summary>服务器出生点/场景存档同步开始（引用计数从 0→1）。用于全屏加载界面等。</summary>
    public static event Action OnServerSpawnSyncLockStarted;

    /// <summary>服务器出生点同步全部结束（引用计数回到 0）。</summary>
    public static event Action OnServerSpawnSyncLockEnded;

    int _spawnInputLockCount;
    bool _hasSeenFirstSceneLoad;

    Coroutine _syncLoop;
    Coroutine _healthLoop;
    int _serverHealthFailCount;
    bool _coldBootstrapRan;
    bool _startSequenceRan;

    void Awake()
    {
        baseUrl = GameServerEndpoint.ResolveBaseUrl(baseUrl);

        TryClearSavedAuthForGeneratedClient();

        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (dontDestroyOnLoad)
            DontDestroyOnLoad(gameObject);
        if (GetComponent<ProximityChatPanelController>() == null)
            gameObject.AddComponent<ProximityChatPanelController>();
        if (GetComponent<MultiplayerRoomCodePanelController>() == null)
            gameObject.AddComponent<MultiplayerRoomCodePanelController>();
        if (GetComponent<MultiplayerFriendChatSidebarController>() == null)
            gameObject.AddComponent<MultiplayerFriendChatSidebarController>();
        if (GetComponent<PlayerCharacterApplier>() == null)
            gameObject.AddComponent<PlayerCharacterApplier>();
        RefreshPlayerReference();
    }

    void TryClearSavedAuthForGeneratedClient()
    {
        if (!clearSavedAuthOnGeneratedClientStartup)
            return;
        if (!LooksLikeGeneratedClientProcess())
            return;
        if (PlayerPrefs.GetInt(PrefsCloneAuthClearedOnceKey, 0) == 1)
            return;

        bool hadToken = PlayerPrefs.HasKey(PrefsTokenKey);
        bool hadUser = PlayerPrefs.HasKey(PrefsUsernameKey);
        if (hadToken) PlayerPrefs.DeleteKey(PrefsTokenKey);
        if (hadUser) PlayerPrefs.DeleteKey(PrefsUsernameKey);
        // 克隆端仅首次清空一次；后续启动保留该端自己的登录缓存。
        PlayerPrefs.SetInt(PrefsCloneAuthClearedOnceKey, 1);
        PlayerPrefs.Save();
        Debug.Log("[GameServerSync] 检测到生成客户端实例：首次启动已清空登录缓存，后续将保留。");
    }

    static bool LooksLikeGeneratedClientProcess()
    {
        string dataPath = (Application.dataPath ?? "").Replace('\\', '/').ToLowerInvariant();
        if (dataPath.Contains("/clone") || dataPath.Contains("_clone") || dataPath.Contains("clone_"))
            return true;

        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            string a = (args[i] ?? "").ToLowerInvariant();
            if (a.Contains("clone") || a.Contains("parrelsync") || a.Contains("clientinstance"))
                return true;
        }
        return false;
    }

    void Update()
    {
        if (GameplayUiHotkeyGuard.ShouldBlockDontDestroyOnLoadHotkeys())
            MenuLoginPortalFlow.TryPollSwitchAccountHotkey();
        else
        {
            if (pollMultiplayerPanelHotkeyHere)
            {
                MultiplayerPanelHotkeyUtility.PollRoomCodePanelHotkey(multiplayerHotkeyLockCursorWhenClosed);
                MultiplayerPanelHotkeyUtility.PollFriendChatSidebarHotkey(multiplayerHotkeyLockCursorWhenClosed);
            }

            if (pollFlagAuctionPanelHotkeyHere)
                FlagAuctionPanelHotkeyUtility.PollFromBindings();
        }
    }

    void OnEnable()
    {
        if (Instance != null && Instance != this)
            return;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _sceneLoadGeneration++;
        RefreshPlayerReference();
        if (string.IsNullOrEmpty(scene.name)) return;

        if (string.Equals(scene.name, loadingSceneName, System.StringComparison.OrdinalIgnoreCase))
            GameplayUiHotkeyGuard.DismissDontDestroyOnLoadPanelsForLoginFlowScene();

        if (_hasSeenFirstSceneLoad &&
            recreateDirectionalLightOnSceneLoad &&
            string.Equals(scene.name, recreateDirectionalLightSceneName, StringComparison.OrdinalIgnoreCase))
        {
            StartCoroutine(CoRecreateDirectionalLight(scene.name));
        }
        _hasSeenFirstSceneLoad = true;

        StartCoroutine(CoOnSceneLoadedFlow(scene.name));
    }

    IEnumerator CoRecreateDirectionalLight(string targetSceneName)
    {
        // 等 1 帧，确保场景对象全部实例化完成后再操作灯光。
        yield return null;
        if (!string.Equals(SceneManager.GetActiveScene().name, targetSceneName, StringComparison.Ordinal))
            yield break;

        Light template = null;
        var allLights = FindObjectsOfType<Light>(true);
        for (int i = 0; i < allLights.Length; i++)
        {
            var l = allLights[i];
            if (l == null || l.type != LightType.Directional) continue;
            if (l.gameObject.scene.name != targetSceneName) continue;
            template = l;
            break;
        }

        if (allLights != null)
        {
            for (int i = 0; i < allLights.Length; i++)
            {
                var l = allLights[i];
                if (l == null || l.type != LightType.Directional) continue;
                if (l.gameObject.scene.name != targetSceneName) continue;
                Destroy(l.gameObject);
            }
        }

        // 再等 1 帧，避免与 Destroy 的时序冲突。
        yield return null;
        if (!string.Equals(SceneManager.GetActiveScene().name, targetSceneName, StringComparison.Ordinal))
            yield break;

        var go = new GameObject("[AutoRecreated] Directional Light");
        var newLight = go.AddComponent<Light>();
        newLight.type = LightType.Directional;
        newLight.shadows = template != null ? template.shadows : LightShadows.Soft;
        newLight.shadowStrength = template != null ? template.shadowStrength : 1f;
        newLight.intensity = template != null ? template.intensity : recreatedDirectionalLightIntensity;
        newLight.color = template != null ? template.color : recreatedDirectionalLightColor;
        go.transform.rotation = template != null
            ? template.transform.rotation
            : Quaternion.Euler(recreatedDirectionalLightEuler);
        RenderSettings.sun = newLight;
    }

    IEnumerator CoOnSceneLoadedFlow(string sceneName)
    {
        TryHideDdolGameServerLoginPanelWhenLeavingLoading(sceneName);

        if (allowLocalPlayWithoutGameServer)
        {
            yield return null;
            RefreshPlayerReference();
            TryApplySelectedPlayerCharacter();
            yield break;
        }

        bool useLock = lockInputUntilServerSpawnApplied && IsLoggedIn &&
                       (restorePositionForCurrentSceneOnLoad || pushPositionToServerOnSceneLoaded);
        if (useLock)
            PushSpawnInputLock();

        yield return null;
        RefreshPlayerReference();
        TryApplySelectedPlayerCharacter();

        if (!IsLoggedIn || playerMovement == null)
        {
            if (useLock)
                PopSpawnInputLock();
            yield break;
        }

        if (restorePositionForCurrentSceneOnLoad)
        {
            var teleportManager = TeleportDataManager.Instance;
            if (teleportManager != null && teleportManager.ShouldSuppressServerSpawnRestore(sceneName))
            {
                if (verboseLogging)
                    Debug.Log($"[GameServerSync] 检测到跨场景传送落点，跳过服务器坐标恢复: {sceneName}");
                teleportManager.ClearServerSpawnRestoreBlock();
            }
            else
            {
                yield return StartCoroutine(CoFetchSpawnForLoadedScene(sceneName));
            }
        }

        if (pushPositionToServerOnSceneLoaded)
            yield return StartCoroutine(CoSendPositionOnce());

        if (useLock)
            PopSpawnInputLock();
    }

    /// <summary>
    /// 若登录面板被挂在 DontDestroyOnLoad 下（例如作为 GameServerSync 子物体），离开 Loading 后仍会全屏挡射线；
    /// 进入任意非 Loading 场景时关闭之（需要时仍可用 P / GameServerLoginPanelToggle 再打开）。
    /// </summary>
    void TryHideDdolGameServerLoginPanelWhenLeavingLoading(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return;
        if (string.Equals(sceneName, loadingSceneName, StringComparison.OrdinalIgnoreCase)) return;

        var auths = FindObjectsOfType<GameServerAuthUI>(true);
        for (int i = 0; i < auths.Length; i++)
        {
            var a = auths[i];
            if (a == null) continue;
            var go = a.gameObject;
            if (!go.scene.IsValid()) continue;
            if (!string.Equals(go.scene.name, "DontDestroyOnLoad", StringComparison.Ordinal)) continue;
            if (!go.activeInHierarchy) continue;
            go.SetActive(false);
            Debug.Log("[GameServerSync] 已进入非 Loading 场景：已隐藏 DontDestroy 下的 GameServer_LoginPanel，避免阻挡 UI。");
            break;
        }
    }

    void RefreshPlayerReference()
    {
        if (playerMovement == null)
            playerMovement = FindObjectOfType<PlayerMovement>();
        if (cameraSwitcher == null)
            cameraSwitcher = FindObjectOfType<CameraSwitcher>();
    }

    void TryApplySelectedPlayerCharacter()
    {
        var applier = GetComponent<PlayerCharacterApplier>();
        if (applier != null)
            applier.ApplyToPlayerInScene();
    }

    void Start()
    {
        if (_startSequenceRan)
            return;
        _startSequenceRan = true;
        StartCoroutine(CoStartSequence());
    }

    IEnumerator CoStartSequence()
    {
        RefreshPlayerReference();
        yield return StartCoroutine(CoBootstrapColdSession());
        RefreshPlayerReference();

        bool allowColdRestore = enableColdStartLoading &&
                                !allowLocalPlayWithoutGameServer &&
                                restorePositionOnStart && IsLoggedIn &&
                                (!restorePositionOnStartOnlyIfPlayerInScene || playerMovement != null);
        if (allowColdRestore)
            yield return StartCoroutine(CoFetchAndApplySpawn(GlobalSpawnContext.ColdPlayStart));
        if (IsLoggedIn)
        {
            StartServerHealthWatchIfNeeded();
            StartPositionLoop();
        }
    }

    /// <summary>
    /// 首帧调用：根据服务器 /api/connection_check 决定是否进入 Loading 场景做账号校验。
    /// </summary>
    IEnumerator CoBootstrapColdSession()
    {
        if (_coldBootstrapRan)
            yield break;
        _coldBootstrapRan = true;

        if (!enableColdStartLoading)
        {
            if (verboseLogging)
                Debug.Log("[GameServerSync] 已关闭冷启动加载：跳过 connection_check、强制 Loading 与启动跨场景恢复。");
            yield break;
        }

        if (allowLocalPlayWithoutGameServer)
        {
            if (verboseLogging)
                Debug.Log("[GameServerSync] 已启用本机无服模式：跳过冷启动 connection_check 与强制 Loading。");
            yield break;
        }

        string url = $"{BaseUrlTrimmed}/api/connection_check";
        bool coldSession = true;
        using (var req = UnityWebRequest.Get(url))
        {
            bool sendOk = false;
            yield return StartCoroutine(CoSendWebRequestSafe(req, "connection_check", ok => sendOk = ok));
            if (!sendOk)
            {
                if (verboseLogging)
                    Debug.LogWarning("[GameServerSync] connection_check 请求未发出，按冷启动进入 Loading 校验。");
                coldSession = true;
                goto END_CONNECTION_CHECK;
            }
            if (req.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    var w = JsonUtility.FromJson<ConnectionCheckWire>(req.downloadHandler.text);
                    if (w != null)
                        coldSession = w.cold_session;
                }
                catch
                {
                    coldSession = true;
                }
            }
            else
            {
                if (verboseLogging)
                    Debug.LogWarning($"[GameServerSync] connection_check 失败，按冷启动进入 Loading 校验: {req.error}");
                coldSession = true;
            }
        }
END_CONNECTION_CHECK:

        if (!coldSession)
            yield break;

        string active = SceneManager.GetActiveScene().name;
        if (string.IsNullOrEmpty(loadingSceneName) ||
            string.Equals(active, loadingSceneName, StringComparison.OrdinalIgnoreCase))
            yield break;

        SceneManager.LoadScene(loadingSceneName, LoadSceneMode.Single);
        yield return null;
    }

    /// <summary>
    /// 用已保存 token 请求 /api/player_state；失败则 Logout。供 Loading 场景自动登录流程使用。
    /// </summary>
    public IEnumerator CoValidateStoredToken(Action<bool> onComplete)
    {
        if (!IsLoggedIn)
        {
            onComplete?.Invoke(false);
            yield break;
        }

        string url = $"{BaseUrlTrimmed}/api/player_state?token={UnityWebRequest.EscapeURL(AuthToken)}";
        using (var req = UnityWebRequest.Get(url))
        {
            bool sendOk = false;
            yield return StartCoroutine(CoSendWebRequestSafe(req, "player_state(validate token)", ok => sendOk = ok));
            if (!sendOk)
            {
                LogFail("校验 token (player_state)", "Insecure connection not allowed", null);
                Logout();
                onComplete?.Invoke(false);
                yield break;
            }
            if (req.result != UnityWebRequest.Result.Success)
            {
                LogFail("校验 token (player_state)", req.error, req.downloadHandler?.text);
                Logout();
                onComplete?.Invoke(false);
                yield break;
            }

            try
            {
                var w = JsonUtility.FromJson<PlayerStateCheckWire>(req.downloadHandler.text);
                if (w != null && w.ok)
                {
                    string uname = SavedUsername ?? "";
                    OnAuthSuccess?.Invoke(uname, AuthToken);
                    onComplete?.Invoke(true);
                    yield break;
                }
            }
            catch
            {
                // fall through
            }
        }

        Logout();
        onComplete?.Invoke(false);
    }

    /// <summary>
    /// 在菜单登录并展示传送门后调用：按服务器记录 LoadScene 并传送（无 PlayerMovement 的菜单场景也可使用）。
    /// 需在 Inspector 勾选 deferSpawnFetchUntilEnterWorld，且登录流程中勿提前执行 ColdPlay 跨场景恢复。
    /// </summary>
    public void EnterWorldFromServerSave()
    {
        if (!IsLoggedIn) return;
        StartCoroutine(CoEnterWorldFromServerSave());
    }

    IEnumerator CoEnterWorldFromServerSave()
    {
        yield return StartCoroutine(CoFetchAndApplySpawn(GlobalSpawnContext.PortalEnter));
        StartPositionLoop();
    }

    void OnApplicationQuit()
    {
        if (LanCoopNetworkManager.Instance != null && (NetworkClient.active || NetworkServer.active))
            LanCoopNetworkManager.Instance.StopCoop();
    }

    void OnDestroy()
    {
        if (_syncLoop != null)
            StopCoroutine(_syncLoop);
        if (_healthLoop != null)
            StopCoroutine(_healthLoop);
        if (Instance == this)
            Instance = null;
    }

    public void StartPositionLoop()
    {
        if (_syncLoop != null)
            StopCoroutine(_syncLoop);
        if (!IsLoggedIn || playerMovement == null) return;
        _syncLoop = StartCoroutine(CoPositionSyncLoop());
        StartServerHealthWatchIfNeeded();
    }

    public void StopPositionLoop()
    {
        if (_syncLoop != null)
        {
            StopCoroutine(_syncLoop);
            _syncLoop = null;
        }
        StopServerHealthWatch();
    }

    /// <summary>注册并自动保存 token，开始位置同步。</summary>
    public void Register(string username, string password)
    {
        StartCoroutine(CoRegister(username, password));
    }

    /// <summary>登录并保存 token，开始位置同步。</summary>
    public void Login(string username, string password)
    {
        StartCoroutine(CoLogin(username, password));
    }

    public void Logout()
    {
        StopPositionLoop();
        StopServerHealthWatch();
        OnLoggedOut?.Invoke();
        PlayerPrefs.DeleteKey(PrefsTokenKey);
        PlayerPrefs.DeleteKey(PrefsUsernameKey);
        PlayerPrefs.DeleteKey(PrefsNicknameKey);
        PlayerPrefs.DeleteKey(PrefsCharacterIdKey);
        PlayerPrefs.Save();
    }

    IEnumerator CoRegister(string username, string password)
    {
        var body = $"{{\"username\":\"{JsonEscape(username)}\",\"password\":\"{JsonEscape(password)}\"}}";
        using (var req = new UnityWebRequest($"{baseUrl.TrimEnd('/')}/api/register", "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            bool sendOk = false;
            yield return StartCoroutine(CoSendWebRequestSafe(req, "register", ok => sendOk = ok));
            if (!sendOk)
            {
                OnAuthFailure?.Invoke(Loc.Choose(
                    "HTTP 被平台策略拦截（Insecure connection not allowed）",
                    "HTTP blocked by platform policy (Insecure connection not allowed)"));
                yield break;
            }

            if (req.result != UnityWebRequest.Result.Success)
            {
                LogFail("注册失败", req.error, req.downloadHandler?.text);
                OnAuthFailure?.Invoke(req.error);
                yield break;
            }

            var json = req.downloadHandler.text;
            if (!TryParseToken(json, out var token, out var uname))
            {
                OnAuthFailure?.Invoke(Loc.Choose("解析注册响应失败", "Failed to parse registration response"));
                yield break;
            }

            SaveAuth(token, uname);
            PlayerPrefs.SetString(PrefsIntroCinematicPendingUserKey, uname);
            PlayerPrefs.Save();
            OnRegisterSuccess?.Invoke(uname);
            OnAuthSuccess?.Invoke(uname, token);
            if (!deferSpawnFetchUntilEnterWorld)
            {
                StartCoroutine(CoFetchAndApplySpawn(GlobalSpawnContext.AfterAuth));
                StartServerHealthWatchIfNeeded();
                StartPositionLoop();
            }
        }
    }

    IEnumerator CoLogin(string username, string password)
    {
        var body = $"{{\"username\":\"{JsonEscape(username)}\",\"password\":\"{JsonEscape(password)}\"}}";
        using (var req = new UnityWebRequest($"{baseUrl.TrimEnd('/')}/api/login", "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            bool sendOk = false;
            yield return StartCoroutine(CoSendWebRequestSafe(req, "login", ok => sendOk = ok));
            if (!sendOk)
            {
                OnAuthFailure?.Invoke(Loc.Choose(
                    "HTTP 被平台策略拦截（Insecure connection not allowed）",
                    "HTTP blocked by platform policy (Insecure connection not allowed)"));
                yield break;
            }

            if (req.result != UnityWebRequest.Result.Success)
            {
                LogFail("登录失败", req.error, req.downloadHandler?.text);
                OnAuthFailure?.Invoke(req.error);
                yield break;
            }

            var json = req.downloadHandler.text;
            if (!TryParseToken(json, out var token, out var uname))
            {
                OnAuthFailure?.Invoke(Loc.Choose("解析登录响应失败", "Failed to parse login response"));
                yield break;
            }

            SaveAuth(token, uname);
            OnAuthSuccess?.Invoke(uname, token);
            if (!deferSpawnFetchUntilEnterWorld)
            {
                StartCoroutine(CoFetchAndApplySpawn(GlobalSpawnContext.AfterAuth));
                StartServerHealthWatchIfNeeded();
                StartPositionLoop();
            }
        }
    }

    enum GlobalSpawnContext
    {
        ColdPlayStart,
        AfterAuth,
        /// <summary>用户确认进入世界（如点击传送门），允许从菜单跨场景加载存档场景。</summary>
        PortalEnter,
    }

    bool SceneAllowsGlobalSpawnRedirectAfterAuth(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName) || scenesAllowingGlobalSpawnRedirectAfterAuth == null)
            return false;
        for (int i = 0; i < scenesAllowingGlobalSpawnRedirectAfterAuth.Length; i++)
        {
            var n = scenesAllowingGlobalSpawnRedirectAfterAuth[i];
            if (!string.IsNullOrEmpty(n) &&
                string.Equals(n.Trim(), sceneName, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    IEnumerator CoFetchAndApplySpawn(GlobalSpawnContext context)
    {
        bool useLock = lockInputUntilServerSpawnApplied && IsLoggedIn;
        if (useLock)
            PushSpawnInputLock();

        yield return StartCoroutine(CoFetchAndApplySpawnCore(context));

        if (useLock)
            PopSpawnInputLock();
    }

    IEnumerator CoFetchAndApplySpawnCore(GlobalSpawnContext context)
    {
        if (!IsLoggedIn) yield break;

        var sceneWhenRequested = SceneManager.GetActiveScene().name;
        int genAtRequest = _sceneLoadGeneration;

        var url = $"{baseUrl.TrimEnd('/')}/api/spawn?token={UnityWebRequest.EscapeURL(AuthToken)}";
        using (var req = UnityWebRequest.Get(url))
        {
            bool sendOk = false;
            yield return StartCoroutine(CoSendWebRequestSafe(req, "spawn(global)", ok => sendOk = ok));
            if (!sendOk)
            {
                if (verboseLogging)
                    Debug.LogWarning("[GameServerSync] 拉取出生点请求未发出（HTTP 可能被平台策略拦截）。");
                if (context == GlobalSpawnContext.PortalEnter)
                    TryLoadFallbackWorldScene("拉取出生点请求未发出");
                yield break;
            }
            if (req.result != UnityWebRequest.Result.Success)
            {
                if (verboseLogging)
                    Debug.LogWarning($"[GameServerSync] 拉取出生点失败: {req.error}");
                if (context == GlobalSpawnContext.PortalEnter)
                    TryLoadFallbackWorldScene("拉取出生点失败");
                yield break;
            }

            if (!TryParseSpawn(req.downloadHandler.text, out var data) || !data.has_saved)
            {
                if (context == GlobalSpawnContext.PortalEnter)
                    TryLoadFallbackWorldScene("账号暂无服务器存档");
                yield break;
            }

            var nowScene = SceneManager.GetActiveScene().name;
            bool generationStillSame = genAtRequest == _sceneLoadGeneration;
            bool allowServerLoadOtherScene = context == GlobalSpawnContext.ColdPlayStart
                || context == GlobalSpawnContext.PortalEnter
                || SceneAllowsGlobalSpawnRedirectAfterAuth(sceneWhenRequested);

            // 已与服务器记录处于同一场景：只同步坐标
            if (string.Equals(nowScene, data.scene_name, StringComparison.Ordinal))
            {
                RefreshPlayerReference();
                if (playerMovement == null) yield break;
                ApplySpawnTransform(data);
                yield break;
            }

            // 需要 LoadScene 到 data.scene_name：仅在「允许跨场景」且「请求期间未发生过场景加载」且「仍停在发起请求时的场景」时执行，避免登录/切场景竞态拉回旧图
            bool mayApplyCrossSceneSpawn = allowServerLoadOtherScene
                && generationStillSame
                && string.Equals(nowScene, sceneWhenRequested, StringComparison.Ordinal);

            if (mayApplyCrossSceneSpawn)
            {
                yield return ApplySpawn(data);
                yield break;
            }

            if (verboseLogging)
            {
                if (!allowServerLoadOtherScene && context == GlobalSpawnContext.AfterAuth)
                    Debug.Log(
                        "[GameServerSync] 登录/注册后不在「允许全局跳转」场景白名单，或请求期间已换场景：不根据全局记录 LoadScene，仅拉取当前场景存档。");
                else if (!generationStillSame)
                    Debug.Log(
                        "[GameServerSync] 拉全局存档期间场景已变更：忽略服务器中的其它场景名，仅拉取当前场景存档。");
            }

            yield return StartCoroutine(CoFetchSpawnForLoadedScene(nowScene));
        }
    }

    void TryLoadFallbackWorldScene(string reason)
    {
        if (!loadFallbackSceneWhenPortalEnterHasNoSaved)
            return;
        if (string.IsNullOrWhiteSpace(fallbackWorldSceneName))
        {
            Debug.LogWarning($"[GameServerSync] {reason}，但 fallbackWorldSceneName 为空，无法兜底切场景。");
            return;
        }

        try
        {
            SceneManager.LoadScene(fallbackWorldSceneName.Trim());
            if (verboseLogging)
                Debug.Log($"[GameServerSync] {reason}，已切到兜底场景: {fallbackWorldSceneName}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[GameServerSync] 兜底切场景失败: {fallbackWorldSceneName}\n{ex.Message}");
        }
    }

    /// <summary>当前场景已加载后，仅拉取「该场景名」在服务器上的记录并应用坐标（不会再次 LoadScene）。</summary>
    IEnumerator CoFetchSpawnForLoadedScene(string loadedSceneName)
    {
        yield return null;
        RefreshPlayerReference();
        if (playerMovement == null || !IsLoggedIn || string.IsNullOrEmpty(loadedSceneName))
            yield break;
        if (!string.Equals(SceneManager.GetActiveScene().name, loadedSceneName, StringComparison.Ordinal))
            yield break;

        var url =
            $"{baseUrl.TrimEnd('/')}/api/spawn?token={UnityWebRequest.EscapeURL(AuthToken)}" +
            $"&scene_name={UnityWebRequest.EscapeURL(loadedSceneName)}";
        using (var req = UnityWebRequest.Get(url))
        {
            bool sendOk = false;
            yield return StartCoroutine(CoSendWebRequestSafe(req, "spawn(scene)", ok => sendOk = ok));
            if (!sendOk)
            {
                if (verboseLogging)
                    Debug.LogWarning($"[GameServerSync] 拉取场景存档请求未发出: {loadedSceneName}");
                yield break;
            }
            if (req.result != UnityWebRequest.Result.Success)
            {
                if (verboseLogging)
                    Debug.LogWarning($"[GameServerSync] 拉取场景存档失败: {loadedSceneName} {req.error}");
                yield break;
            }

            if (!string.Equals(SceneManager.GetActiveScene().name, loadedSceneName, StringComparison.Ordinal))
                yield break;

            if (!TryParseSpawn(req.downloadHandler.text, out var data) || !data.has_saved)
                yield break;
            if (!string.Equals(data.scene_name, loadedSceneName, StringComparison.Ordinal))
                yield break;

            ApplySpawnTransform(data);
        }
    }

    IEnumerator ApplySpawn(SpawnData data)
    {
        var active = SceneManager.GetActiveScene().name;
        if (!string.Equals(active, data.scene_name, StringComparison.Ordinal))
        {
            AsyncOperation op = null;
            try
            {
                op = SceneManager.LoadSceneAsync(data.scene_name);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameServerSync] 加载场景失败: {data.scene_name}\n{ex.Message}");
                yield break;
            }

            if (op == null) yield break;
            while (!op.isDone) yield return null;

            playerMovement = FindObjectOfType<PlayerMovement>();
            if (playerMovement == null)
            {
                Debug.LogError("[GameServerSync] 新场景中未找到 PlayerMovement。");
                yield break;
            }
        }

        ApplySpawnTransform(data);
    }

    void ApplySpawnTransform(SpawnData data)
    {
        if (playerMovement == null) return;

        var pos = new Vector3(data.x, data.y, data.z);
        playerMovement.Teleport(pos);
        var euler = playerMovement.transform.eulerAngles;
        euler.y = data.rot_y;
        playerMovement.transform.rotation = Quaternion.Euler(euler);

        if (cameraSwitcher == null)
            cameraSwitcher = FindObjectOfType<CameraSwitcher>();
        if (cameraSwitcher != null && !string.IsNullOrEmpty(data.view_mode))
            cameraSwitcher.ApplySavedViewMode(data.view_mode);
    }

    IEnumerator CoPositionSyncLoop()
    {
        var wait = new WaitForSeconds(Mathf.Max(0.25f, positionSyncIntervalSeconds));
        while (enabled && IsLoggedIn && playerMovement != null)
        {
            yield return StartCoroutine(CoSendPositionOnce());
            yield return wait;
        }
    }

    void StartServerHealthWatchIfNeeded()
    {
        if (allowLocalPlayWithoutGameServer || !enableServerHealthWatch || !IsLoggedIn)
            return;
        if (_healthLoop != null)
            StopCoroutine(_healthLoop);
        _serverHealthFailCount = 0;
        _healthLoop = StartCoroutine(CoServerHealthWatchLoop());
    }

    void StopServerHealthWatch()
    {
        if (_healthLoop != null)
        {
            StopCoroutine(_healthLoop);
            _healthLoop = null;
        }
        _serverHealthFailCount = 0;
    }

    IEnumerator CoServerHealthWatchLoop()
    {
        var wait = new WaitForSeconds(Mathf.Max(3f, serverHealthCheckIntervalSeconds));
        while (enabled && IsLoggedIn)
        {
            yield return wait;
            if (!IsLoggedIn)
                yield break;

            string url = $"{BaseUrlTrimmed}/api/connection_check";
            bool sendOk = false;
            using (var req = UnityWebRequest.Get(url))
            {
                yield return StartCoroutine(CoSendWebRequestSafe(req, "connection_check(health watch)", ok => sendOk = ok));
                bool ok = sendOk && req.result == UnityWebRequest.Result.Success;
                if (!ok)
                {
                    _serverHealthFailCount++;
                    if (verboseLogging)
                        Debug.LogWarning($"[GameServerSync] 服务器健康检查失败({_serverHealthFailCount}): {req.error}");
                }
                else
                {
                    _serverHealthFailCount = 0;
                }
            }

            if (_serverHealthFailCount >= Mathf.Max(1, serverHealthFailThreshold))
            {
                ForceBackToLoadingOnServerLost();
                yield break;
            }
        }
        _healthLoop = null;
    }

    void ForceBackToLoadingOnServerLost()
    {
        Debug.LogWarning("[GameServerSync] 无法连接服务器，自动返回登录页。");
        Logout();
        string active = SceneManager.GetActiveScene().name;
        if (!string.IsNullOrWhiteSpace(loadingSceneName) &&
            !string.Equals(active, loadingSceneName, StringComparison.OrdinalIgnoreCase))
            SceneManager.LoadScene(loadingSceneName, LoadSceneMode.Single);
    }

    IEnumerator CoSendPositionOnce()
    {
        if (playerMovement == null) yield break;
        if (CoopSessionState.BlockGuestCloudWrites)
            yield break;

        var scn = SceneManager.GetActiveScene().name;
        var p = playerMovement.transform.position;
        var ry = playerMovement.transform.eulerAngles.y;
        if (cameraSwitcher == null)
            cameraSwitcher = FindObjectOfType<CameraSwitcher>();
        var viewMode = cameraSwitcher != null
            ? cameraSwitcher.ViewModeForServer
            : (CameraViewScenePolicy.IsThirdPersonAllowedInActiveScene() ? "third" : "first");
        var body =
            $"{{\"token\":\"{JsonEscape(AuthToken)}\",\"scene_name\":\"{JsonEscape(scn)}\"," +
            $"\"x\":{p.x.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
            $"\"y\":{p.y.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
            $"\"z\":{p.z.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
            $"\"rot_y\":{ry.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
            $"\"view_mode\":\"{JsonEscape(viewMode)}\"}}";

        using (var req = new UnityWebRequest($"{baseUrl.TrimEnd('/')}/api/position", "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            bool sendOk = false;
            yield return StartCoroutine(CoSendWebRequestSafe(req, "position(sync)", ok => sendOk = ok));
            if (!sendOk)
            {
                if (verboseLogging)
                    Debug.LogWarning("[GameServerSync] 上报位置请求未发出（HTTP 可能被平台策略拦截）。");
                yield break;
            }

            if (req.result != UnityWebRequest.Result.Success && verboseLogging)
                Debug.LogWarning($"[GameServerSync] 上报位置失败: {req.error} {req.downloadHandler?.text}");
        }
    }

    void SaveAuth(string token, string username)
    {
        PlayerPrefs.SetString(PrefsTokenKey, token);
        PlayerPrefs.SetString(PrefsUsernameKey, username);
        PlayerPrefs.Save();
    }

    public IEnumerator CoFetchMeProfile(Action<string, string, string> onOk, Action<string> onErr)
    {
        if (!IsLoggedIn)
        {
            onErr?.Invoke(Loc.Choose("未登录", "Not logged in"));
            yield break;
        }

        string url = $"{BaseUrlTrimmed}/api/me?token={UnityWebRequest.EscapeURL(AuthToken)}";
        using (var req = UnityWebRequest.Get(url))
        {
            bool sendOk = false;
            yield return StartCoroutine(CoSendWebRequestSafe(req, "me", ok => sendOk = ok));
            if (!sendOk)
            {
                onErr?.Invoke(Loc.Choose("请求失败", "Request failed"));
                yield break;
            }
            if (req.result != UnityWebRequest.Result.Success)
            {
                onErr?.Invoke(req.error + " " + req.downloadHandler?.text);
                yield break;
            }

            MeProfileWire w = null;
            try { w = JsonUtility.FromJson<MeProfileWire>(req.downloadHandler.text); }
            catch { }
            if (w == null)
            {
                onErr?.Invoke(Loc.Choose("解析 /api/me 失败", "Failed to parse /api/me response"));
                yield break;
            }

            string uname = (w.username ?? "").Trim();
            string nick = (w.nickname ?? "").Trim();
            string characterId = (w.character_id ?? "").Trim();
            if (!string.IsNullOrEmpty(uname))
                PlayerPrefs.SetString(PrefsUsernameKey, uname);
            PlayerPrefs.SetString(PrefsNicknameKey, nick);
            PlayerPrefs.SetString(PrefsCharacterIdKey, characterId);
            PlayerPrefs.Save();
            onOk?.Invoke(uname, nick, characterId);
        }
    }

    public IEnumerator CoFetchMeProfile(Action<string, string> onOk, Action<string> onErr)
    {
        yield return StartCoroutine(CoFetchMeProfile(
            (u, n, _) => onOk?.Invoke(u, n),
            onErr));
    }

    public IEnumerator CoSetMyNickname(string nickname, Action<string> onOk, Action<string> onErr)
    {
        if (!IsLoggedIn)
        {
            onErr?.Invoke(Loc.Choose("未登录", "Not logged in"));
            yield break;
        }

        string nick = (nickname ?? "").Trim();
        if (string.IsNullOrEmpty(nick))
        {
            onErr?.Invoke(Loc.Choose("昵称不能为空", "Nickname cannot be empty"));
            yield break;
        }

        string body = "{\"token\":\"" + JsonEscape(AuthToken) + "\",\"nickname\":\"" + JsonEscape(nick) + "\"}";
        using (var req = new UnityWebRequest($"{BaseUrlTrimmed}/api/me/nickname", "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            bool sendOk = false;
            yield return StartCoroutine(CoSendWebRequestSafe(req, "me/nickname", ok => sendOk = ok));
            if (!sendOk)
            {
                onErr?.Invoke(Loc.Choose("请求失败", "Request failed"));
                yield break;
            }
            if (req.result != UnityWebRequest.Result.Success)
            {
                onErr?.Invoke(req.error + " " + req.downloadHandler?.text);
                yield break;
            }
        }

        PlayerPrefs.SetString(PrefsNicknameKey, nick);
        PlayerPrefs.Save();
        onOk?.Invoke(nick);
    }

    public IEnumerator CoSetMyProfile(string nickname, string characterId, Action<string> onOk, Action<string> onErr)
    {
        if (!IsLoggedIn)
        {
            onErr?.Invoke(Loc.Choose("未登录", "Not logged in"));
            yield break;
        }

        string nick = (nickname ?? "").Trim();
        string charId = (characterId ?? "").Trim();
        if (string.IsNullOrEmpty(nick))
        {
            onErr?.Invoke(Loc.Choose("昵称不能为空", "Nickname cannot be empty"));
            yield break;
        }
        if (string.IsNullOrEmpty(charId))
        {
            onErr?.Invoke(Loc.Choose("请选择角色", "Please select a character"));
            yield break;
        }

        string body = "{\"token\":\"" + JsonEscape(AuthToken)
                      + "\",\"nickname\":\"" + JsonEscape(nick)
                      + "\",\"character_id\":\"" + JsonEscape(charId) + "\"}";
        using (var req = new UnityWebRequest($"{BaseUrlTrimmed}/api/me/profile_setup", "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            bool sendOk = false;
            yield return StartCoroutine(CoSendWebRequestSafe(req, "me/profile_setup", ok => sendOk = ok));
            if (!sendOk)
            {
                onErr?.Invoke(Loc.Choose("请求失败", "Request failed"));
                yield break;
            }
            if (req.result != UnityWebRequest.Result.Success)
            {
                onErr?.Invoke(req.error + " " + req.downloadHandler?.text);
                yield break;
            }
        }

        PlayerPrefs.SetString(PrefsNicknameKey, nick);
        PlayerPrefs.SetString(PrefsCharacterIdKey, charId);
        PlayerPrefs.Save();
        onOk?.Invoke(nick);
    }

    static string JsonEscape(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    void LogFail(string title, string error, string body)
    {
        if (verboseLogging)
            Debug.LogWarning($"[GameServerSync] {title}: {error} {body}");
    }

    IEnumerator CoSendWebRequestSafe(UnityWebRequest req, string apiName, Action<bool> onDone)
    {
        UnityWebRequestAsyncOperation op = null;
        try
        {
            op = req.SendWebRequest();
        }
        catch (InvalidOperationException ex)
        {
            string msg = ex != null ? ex.Message : "InvalidOperationException";
            if (msg.IndexOf("Insecure connection not allowed", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Debug.LogError(
                    $"[GameServerSync] 请求被平台明文策略拦截: {apiName} url={req.url} platform={Application.platform}. " +
                    "当前是 HTTP 地址，若使用 Android/WebGL 请改 HTTPS 或放开对应平台的 HTTP 设置。");
            }
            else
            {
                Debug.LogError($"[GameServerSync] 请求发送异常({apiName}): {msg}");
            }

            onDone?.Invoke(false);
            yield break;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[GameServerSync] 请求发送异常({apiName}): {ex.Message}");
            onDone?.Invoke(false);
            yield break;
        }

        if (op != null)
            yield return op;
        onDone?.Invoke(true);
    }

    struct SpawnData
    {
        public string scene_name;
        public float x, y, z, rot_y;
        public bool has_saved;
        public string view_mode;
    }

    static bool TryParseSpawn(string json, out SpawnData data)
    {
        data = default;
        try
        {
            var wr = JsonUtility.FromJson<SpawnWire>(json);
            if (wr == null) return false;
            data.scene_name = wr.scene_name ?? "";
            data.x = wr.x;
            data.y = wr.y;
            data.z = wr.z;
            data.rot_y = wr.rot_y;
            data.has_saved = wr.has_saved;
            data.view_mode = wr.view_mode ?? "third";
            return true;
        }
        catch
        {
            return false;
        }
    }

    [Serializable]
    class SpawnWire
    {
        public string scene_name;
        public float x, y, z, rot_y;
        public bool has_saved;
        public string view_mode;
    }

    void PushSpawnInputLock()
    {
        RefreshPlayerReference();
        if (playerMovement != null)
        {
            playerMovement.horizontalInput = 0f;
            playerMovement.verticalInput = 0f;
        }

        if (_spawnInputLockCount == 0)
            OnServerSpawnSyncLockStarted?.Invoke();
        _spawnInputLockCount++;
    }

    void PopSpawnInputLock()
    {
        _spawnInputLockCount = Mathf.Max(0, _spawnInputLockCount - 1);
        if (_spawnInputLockCount == 0)
            OnServerSpawnSyncLockEnded?.Invoke();
    }


    static bool TryParseToken(string json, out string token, out string username)
    {
        token = null;
        username = null;
        try
        {
            var w = JsonUtility.FromJson<TokenWire>(json);
            if (w == null || string.IsNullOrEmpty(w.token)) return false;
            token = w.token;
            username = w.username ?? "";
            return true;
        }
        catch
        {
            return false;
        }
    }

    [Serializable]
    class TokenWire
    {
        public string token;
        public string username;
    }

    [Serializable]
    class ConnectionCheckWire
    {
        public bool cold_session;
    }

    [Serializable]
    class PlayerStateCheckWire
    {
        public bool ok;
    }

    [Serializable]
    class MeProfileWire
    {
        public string username;
        public string nickname;
        public string character_id;
    }
}
