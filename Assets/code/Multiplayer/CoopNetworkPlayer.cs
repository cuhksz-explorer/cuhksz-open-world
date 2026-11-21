using System.Collections;
using System.Collections.Generic;
using GameQuestSystem;
using UnityEngine;
using Mirror;
using Multiplayer;
using UnityEngine.SceneManagement;

/// <summary>
/// 联机玩家占位体：本地玩家每帧跟随场景中的 <see cref="PlayerMovement"/>，
/// 由 <see cref="NetworkTransformBase"/> 同步到对端；本地隐藏渲染避免与主角色重叠。
/// </summary>
[RequireComponent(typeof(NetworkIdentity))]
[RequireComponent(typeof(NetworkTransformReliable))]
public class CoopNetworkPlayer : NetworkBehaviour
{
    public const int WorldChatMaxChars = 50;
    public const int WorldChatGoldCost = 5;
    public const float WorldChatCooldownSeconds = 10f;
    public const int ServerHornMaxChars = 40;
    public const int ServerHornGoldCost = 100;

    /// <summary>世界频道：所有客户端追加一行（uid, 展示名, 正文）。</summary>
    public static event System.Action<int, string, string> OnWorldChatMessage;
    /// <summary>全服喇叭：所有客户端飘字（展示名, 正文）。</summary>
    public static event System.Action<string, string> OnServerHorn;

    static readonly Dictionary<int, float> s_serverWorldChatLastAt = new Dictionary<int, float>();

    [Header("远端玩家显示模型（可固定）")]
    [Tooltip("指定后，远端玩家将始终使用这个模型；不指定时退回到复制场景中的 PlayerMovement 外观。")]
    [SerializeField] GameObject lockedRemoteVisualPrefab;
    [Tooltip("为 true 时，优先复制当前场景 PlayerMovement 外观（推荐：联机外观与本地替换保持一致）。")]
    [SerializeField] bool preferScenePlayerVisualOverLockedPrefab = true;

    PlayerMovement _tracked;
    MultiplayerApiClient _api;
    GameObject _remoteVisualInstance;
    Animator _remoteAnimator;

    [Header("Animator 参数（匹配 wasd(2).cs）")]
    [SerializeField] string wParam = "W";
    [SerializeField] string aParam = "A";
    [SerializeField] string sParam = "S";
    [SerializeField] string dParam = "D";
    [SerializeField] string runParam = "Run";
       
    [SyncVar] public int userId; 
    [SyncVar] public string username;
    [SyncVar] public string nickname;
    [SyncVar(hook = nameof(OnRoomPaletteSlotChanged))]
    int _roomPaletteSlot;
    [SyncVar(hook = nameof(OnRoomPaletteCountChanged))]
    int _roomPaletteCount;
    static readonly int ShaderIdColor = Shader.PropertyToID("_Color");
    static readonly int ShaderIdBaseColor = Shader.PropertyToID("_BaseColor");
    [SyncVar(hook = nameof(OnAnimSyncChanged))] bool _syncW;
    [SyncVar(hook = nameof(OnAnimSyncChanged))] bool _syncA;
    [SyncVar(hook = nameof(OnAnimSyncChanged))] bool _syncS;
    [SyncVar(hook = nameof(OnAnimSyncChanged))] bool _syncD;
    [SyncVar(hook = nameof(OnAnimSyncChanged))] bool _syncRun;
    [SyncVar(hook = nameof(OnRemoteSceneChanged))] string _sceneName = "";
    string _lastSentSceneName = "";

    void Awake()
    {
        var nt = GetComponent<NetworkTransformReliable>();
        if (nt != null)
            nt.syncDirection = SyncDirection.ClientToServer;
    }

    /// <summary>
    /// n 个玩家将色相环切成 n+1 段，取第 1..n 段的中点：H = (slot+1)/(n+1)。
    /// </summary>
    public static Color ComputeRoomIdentityColor(int slotIndex, int totalPlayers, float saturation = 0.82f, float value = 0.95f)
    {
        if (totalPlayers <= 0)
            return Color.white;
        int n = Mathf.Max(1, totalPlayers);
        int s = Mathf.Clamp(slotIndex, 0, n - 1);
        float h = (s + 1f) / (n + 1f);
        return Color.HSVToRGB(h, saturation, value);
    }

    public Color GetRoomIdentityColor(float saturation = 0.82f, float value = 0.95f) =>
        ComputeRoomIdentityColor(_roomPaletteSlot, _roomPaletteCount, saturation, value);

    /// <summary>仅在服务器调用：按 netId 排序后为房间内每位玩家分配 0..n-1 槽位。</summary>
    public static void ServerAssignRoomPaletteSlots()
    {
        if (!NetworkServer.active)
            return;
        var list = new List<CoopNetworkPlayer>();
        foreach (var kv in NetworkServer.spawned)
        {
            if (kv.Value == null)
                continue;
            var c = kv.Value.GetComponent<CoopNetworkPlayer>();
            if (c != null)
                list.Add(c);
        }
        list.Sort((a, b) => a.netId.CompareTo(b.netId));
        int n = list.Count;
        for (int i = 0; i < n; i++)
        {
            list[i]._roomPaletteSlot = i;
            list[i]._roomPaletteCount = n;
        }
    }

    void OnRoomPaletteSlotChanged(int oldValue, int newValue) => ClientApplyRoomPaletteTint();

    void OnRoomPaletteCountChanged(int oldValue, int newValue) => ClientApplyRoomPaletteTint();

    void ClientApplyRoomPaletteTint()
    {
        if (isLocalPlayer)
            return;
        ApplyPaletteTintToRemoteVisual();
    }

    void ApplyPaletteTintToRemoteVisual()
    {
        if (_remoteVisualInstance == null)
            return;
        Color c = GetRoomIdentityColor(0.72f, 0.93f);
        var mpb = new MaterialPropertyBlock();
        var renderers = _remoteVisualInstance.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            r.GetPropertyBlock(mpb);
            mpb.SetColor(ShaderIdColor, c);
            mpb.SetColor(ShaderIdBaseColor, c);
            r.SetPropertyBlock(mpb);
        }
    }

    public override void OnStartLocalPlayer()
    {
        foreach (var r in GetComponentsInChildren<Renderer>(true))
            r.enabled = false;
        _api = FindObjectOfType<MultiplayerApiClient>();
        if (_api != null)
            StartCoroutine(_api.CoMe(OnMeOk, _ => { }));

        string sn = SceneManager.GetActiveScene().name ?? "";
        if (!string.IsNullOrEmpty(sn))
        {
            _lastSentSceneName = sn;
            CmdSyncScene(sn);
        }
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        if (GetComponent<CoopPlayerNameplate>() == null)
            gameObject.AddComponent<CoopPlayerNameplate>();
        if (!isLocalPlayer)
            TryBuildRemoteVisual();
    }

    void OnMeOk(MeResponseDto me)
    {
        if (me == null || me.user_id <= 0) return;
        CmdSyncProfile(me.user_id, me.username ?? "", me.nickname ?? "");
    }

    [Command]
    void CmdSyncProfile(int uid, string uname, string nick)
    {
        userId = Mathf.Max(0, uid);
        username = (uname ?? "").Trim();
        nickname = (nick ?? "").Trim();
    }

    void TryBuildRemoteVisual()
    {
        if (_remoteVisualInstance != null)
            return;

        PlayerMovement templatePlayer = FindObjectOfType<PlayerMovement>(true);

        bool useScenePlayerVisual = preferScenePlayerVisualOverLockedPrefab && templatePlayer != null;
        if (useScenePlayerVisual)
        {
            _remoteVisualInstance = Instantiate(templatePlayer.gameObject, transform);
        }
        else if (lockedRemoteVisualPrefab != null)
        {
            _remoteVisualInstance = Instantiate(lockedRemoteVisualPrefab, transform);
        }
        else
        {
            if (templatePlayer == null)
                return;
            _remoteVisualInstance = Instantiate(templatePlayer.gameObject, transform);
        }

        _remoteVisualInstance.name = "RemoteVisual";
        _remoteVisualInstance.transform.localPosition = Vector3.zero;
        _remoteVisualInstance.transform.localRotation = Quaternion.identity;

        // 去掉行为脚本/碰撞体/相机等，仅保留可见模型（与本地主角外观一致）。
        var all = _remoteVisualInstance.GetComponentsInChildren<Component>(true);
        for (int i = 0; i < all.Length; i++)
        {
            var c = all[i];
            if (c == null || c is Transform || c is Renderer || c is MeshFilter || c is Animator)
                continue;
            Destroy(c);
        }

        // 旧 CoopPlayer 预制体若有胶囊等占位渲染，统一关闭。
        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            if (_remoteVisualInstance != null && r.transform.IsChildOf(_remoteVisualInstance.transform))
                continue;
            r.enabled = false;
        }

        // 与场景中本机角色根节点的世界缩放一致（联机根物体 scale 可能与玩家父级不同）
        if (templatePlayer != null)
            AlignRemoteVisualWorldScaleTo(templatePlayer.transform);

        _remoteAnimator = _remoteVisualInstance.GetComponentInChildren<Animator>(true);
        if (templatePlayer != null)
            CopyAnimatorAssetsFromTemplateIfMissing(templatePlayer);

        if (_remoteAnimator != null)
        {
            _remoteAnimator.applyRootMotion = false;
            _remoteAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            _remoteAnimator.enabled = true;
        }

        ApplyPaletteTintToRemoteVisual();

        // SyncVar 首次下发时可能尚未进 hook；Destroy 也可能延后一帧，再刷一次参数更稳
        ApplyAnimStateToRemote();
        StartCoroutine(CoReapplyRemoteAnimNextFrame());
    }

    IEnumerator CoReapplyRemoteAnimNextFrame()
    {
        yield return null;
        if (_remoteAnimator == null)
            _remoteAnimator = _remoteVisualInstance != null
                ? _remoteVisualInstance.GetComponentInChildren<Animator>(true)
                : null;
        ApplyAnimStateToRemote();
    }

    /// <summary>使远端可视根在世界空间与参考 Transform 的 lossyScale 一致。</summary>
    void AlignRemoteVisualWorldScaleTo(Transform referenceRoot)
    {
        if (_remoteVisualInstance == null || referenceRoot == null) return;
        Vector3 refLossy = referenceRoot.lossyScale;
        Vector3 hostLossy = transform.lossyScale;
        _remoteVisualInstance.transform.localScale = new Vector3(
            refLossy.x / Mathf.Max(1e-5f, hostLossy.x),
            refLossy.y / Mathf.Max(1e-5f, hostLossy.y),
            refLossy.z / Mathf.Max(1e-5f, hostLossy.z));
    }

    void CopyAnimatorAssetsFromTemplateIfMissing(PlayerMovement templatePlayer)
    {
        if (_remoteAnimator == null || templatePlayer == null) return;
        var src = templatePlayer.GetComponentInChildren<Animator>(true);
        if (src == null) return;
        if (_remoteAnimator.runtimeAnimatorController == null && src.runtimeAnimatorController != null)
            _remoteAnimator.runtimeAnimatorController = src.runtimeAnimatorController;
        if (_remoteAnimator.avatar == null && src.avatar != null)
            _remoteAnimator.avatar = src.avatar;
    }

    void Update()
    {
        if (isLocalPlayer)
        {
            if (_tracked == null)
                _tracked = FindObjectOfType<PlayerMovement>();
            if (_tracked == null)
                return;
            var t = _tracked.transform;
            transform.SetPositionAndRotation(t.position, t.rotation);
            SyncSceneName();
            if (!GameplayUiHotkeyGuard.BlocksGameplayBecauseMultiplayerUi())
                SyncAnimByWasdRule();
        }
        else
        {
            RefreshRemoteVisualVisibilityByScene();
        }
    }

    void SyncSceneName()
    {
        string active = (SceneManager.GetActiveScene().name ?? "").Trim();
        if (string.Equals(active, _lastSentSceneName, System.StringComparison.Ordinal))
            return;
        _lastSentSceneName = active;
        CmdSyncScene(active);
    }

    void SyncAnimByWasdRule()
    {
        bool wCurrent = Input.GetKey(KeyCode.W);
        bool aCurrent = Input.GetKey(KeyCode.A);
        bool sCurrent = Input.GetKey(KeyCode.S);
        bool dCurrent = Input.GetKey(KeyCode.D);
        bool runCurrent = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        bool wOut = false, aOut = false, sOut = false, dOut = false;
        bool oppositePressed = (wCurrent && sCurrent) || (aCurrent && dCurrent);
        if (!oppositePressed)
        {
            int keyCount = (wCurrent ? 1 : 0) + (aCurrent ? 1 : 0) + (sCurrent ? 1 : 0) + (dCurrent ? 1 : 0);
            if (keyCount == 1)
            {
                wOut = wCurrent;
                aOut = aCurrent;
                sOut = sCurrent;
                dOut = dCurrent;
            }
            else if (keyCount > 1)
            {
                if (wCurrent) wOut = true;
                else if (sCurrent) sOut = true;
                else if (aCurrent) aOut = true;
                else if (dCurrent) dOut = true;
            }
        }

        if (wOut == _syncW && aOut == _syncA && sOut == _syncS && dOut == _syncD && runCurrent == _syncRun)
            return;
        CmdSetAnimState(wOut, aOut, sOut, dOut, runCurrent);
    }

    [Command]
    void CmdSetAnimState(bool w, bool a, bool s, bool d, bool run)
    {
        _syncW = w;
        _syncA = a;
        _syncS = s;
        _syncD = d;
        _syncRun = run;
    }

    [Command]
    void CmdSyncScene(string sceneName)
    {
        _sceneName = (sceneName ?? "").Trim();
    }

    void OnAnimSyncChanged(bool _, bool __)
    {
        ApplyAnimStateToRemote();
    }

    void OnRemoteSceneChanged(string _, string __)
    {
        RefreshRemoteVisualVisibilityByScene();
    }

    void RefreshRemoteVisualVisibilityByScene()
    {
        if (_remoteVisualInstance == null)
            return;
        string localScene = (SceneManager.GetActiveScene().name ?? "").Trim();
        string remote = (_sceneName ?? "").Trim();
        bool sameScene = string.Equals(localScene, remote, System.StringComparison.OrdinalIgnoreCase);
        if (_remoteVisualInstance.activeSelf != sameScene)
            _remoteVisualInstance.SetActive(sameScene);
    }

    /// <summary>主机放置寝室物品后同步到客机（仅视觉副本，不含网络物理权威）。</summary>
    [Command]
    public void CmdSyncDormPlacement(string resourcePath, Vector3 worldPos, Quaternion worldRot, Vector3 visualLocalScale)
    {
        if (string.IsNullOrEmpty(resourcePath)) return;
        RpcApplyDormPlacement(resourcePath, worldPos, worldRot, visualLocalScale);
    }

    /// <summary>世界频道：10 秒冷却、扣 5 金币（联机时在主机权威钱包扣款），广播给所有人。</summary>
    [Command]
    public void CmdSendWorldChat(string raw)
    {
        if (!isServer || connectionToClient == null) return;
        string msg = SanitizeChatOneLine(raw);
        if (string.IsNullOrEmpty(msg)) return;
        if (msg.Length > WorldChatMaxChars)
            msg = msg.Substring(0, WorldChatMaxChars);

        int cid = connectionToClient.connectionId;
        float now = Time.unscaledTime;
        if (s_serverWorldChatLastAt.TryGetValue(cid, out float lastAt) && now - lastAt < WorldChatCooldownSeconds)
            return;

        var q = QuestSystemManager.Instance;
        if (q == null || !q.TryConsumeGold(WorldChatGoldCost, "world_chat"))
            return;

        s_serverWorldChatLastAt[cid] = now;
        RpcWorldChatLine(userId, BuildSpeakerDisplayName(), msg);
    }

    /// <summary>全服喇叭：扣 100 金币，所有客户端屏幕飘字。</summary>
    [Command]
    public void CmdSendServerHorn(string raw)
    {
        if (!isServer || connectionToClient == null) return;
        string msg = SanitizeChatOneLine(raw);
        if (string.IsNullOrEmpty(msg)) return;
        if (msg.Length > ServerHornMaxChars)
            msg = msg.Substring(0, ServerHornMaxChars);

        var q = QuestSystemManager.Instance;
        if (q == null || !q.TryConsumeGold(ServerHornGoldCost, "server_horn"))
            return;

        RpcServerHorn(BuildSpeakerDisplayName(), msg);
    }

    [ClientRpc]
    void RpcWorldChatLine(int fromUserId, string speaker, string body)
    {
        OnWorldChatMessage?.Invoke(fromUserId, speaker ?? "", body ?? "");
    }

    [ClientRpc]
    void RpcServerHorn(string speaker, string body)
    {
        OnServerHorn?.Invoke(speaker ?? "", body ?? "");
    }

    string BuildSpeakerDisplayName()
    {
        string nick = (nickname ?? "").Trim();
        if (!string.IsNullOrEmpty(nick)) return nick;
        string un = (username ?? "").Trim();
        if (!string.IsNullOrEmpty(un)) return un;
        int uid = Mathf.Max(0, userId);
        return uid > 0 ? $"玩家{uid}" : "玩家";
    }

    static string SanitizeChatOneLine(string raw)
    {
        if (raw == null) return "";
        string t = raw.Trim();
        if (t.Length == 0) return "";
        t = t.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ");
        return t.Trim();
    }

    /// <summary>由 UI 调用：发世界频道（联机走 Command，单机走本地扣款）。</summary>
    public static void SendWorldChatFromUi(string text)
    {
        string msg = SanitizeChatOneLine(text);
        if (string.IsNullOrEmpty(msg)) return;
        if (msg.Length > WorldChatMaxChars)
            msg = msg.Substring(0, WorldChatMaxChars);

        if (NetworkClient.active)
        {
            var local = FindLocal();
            if (local != null)
                local.CmdSendWorldChat(msg);
            return;
        }

        var q = QuestSystemManager.Instance;
        if (q == null) return;
        float now = Time.unscaledTime;
        if (now < s_offlineNextWorldOkAtUnscaled) return;
        if (!q.TryConsumeGold(WorldChatGoldCost, "world_chat_offline")) return;
        s_offlineNextWorldOkAtUnscaled = now + WorldChatCooldownSeconds;
        string me = "本地";
        OnWorldChatMessage?.Invoke(0, me, msg);
    }

    /// <summary>由 UI 调用：全服喇叭（联机 Rpc，单机仅本地飘字）。</summary>
    public static void SendServerHornFromUi(string text)
    {
        string msg = SanitizeChatOneLine(text);
        if (string.IsNullOrEmpty(msg)) return;
        if (msg.Length > ServerHornMaxChars)
            msg = msg.Substring(0, ServerHornMaxChars);

        if (NetworkClient.active)
        {
            var local = FindLocal();
            if (local != null)
                local.CmdSendServerHorn(msg);
            return;
        }

        var q = QuestSystemManager.Instance;
        if (q == null) return;
        if (!q.TryConsumeGold(ServerHornGoldCost, "server_horn_offline")) return;
        OnServerHorn?.Invoke("本地", msg);
    }

    static float s_offlineNextWorldOkAtUnscaled;

    public static bool RequestCarSystemVehicleSpawn(InteractableNPC npc, int vehicleIndex)
    {
        if (npc == null)
            return false;

        var local = FindLocal();
        if (local == null)
            return false;

        local.CmdRequestCarSystemVehicleSpawn(npc.GetStablePath(), vehicleIndex);
        return true;
    }

    static CoopNetworkPlayer FindLocal()
    {
        if (NetworkClient.localPlayer != null)
        {
            var c = NetworkClient.localPlayer.GetComponent<CoopNetworkPlayer>();
            if (c != null)
                return c;
        }
        var all = Object.FindObjectsOfType<CoopNetworkPlayer>();
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].isLocalPlayer)
                return all[i];
        }
        return null;
    }

    [Command]
    void CmdRequestCarSystemVehicleSpawn(string npcPath, int vehicleIndex)
    {
        if (string.IsNullOrWhiteSpace(npcPath))
            return;

        var npc = InteractableNPC.FindByStablePath(npcPath);
        if (npc == null)
        {
            Debug.LogWarning("[CoopNetworkPlayer] 未找到请求生成车辆的 InteractableNPC: " + npcPath);
            return;
        }

        npc.SpawnSelectedVehicleByIndex(vehicleIndex);
    }

    [ClientRpc(includeOwner = false)]
    void RpcApplyDormPlacement(string resourcePath, Vector3 worldPos, Quaternion worldRot, Vector3 visualLocalScale)
    {
        if (string.IsNullOrEmpty(resourcePath)) return;
        var prefab = Resources.Load<GameObject>(resourcePath);
        if (prefab == null) return;
        var root = new GameObject("Placed_" + (prefab.name ?? "Item") + "_Net");
        var vis = Instantiate(prefab);
        vis.name = (prefab.name ?? "Item") + "_Visual";
        vis.transform.SetParent(root.transform, false);
        vis.transform.localScale = visualLocalScale;
        root.transform.SetPositionAndRotation(worldPos, worldRot);
    }

    void ApplyAnimStateToRemote()
    {
        if (_remoteAnimator == null)
            return;
        TrySetAnimatorBool(wParam, _syncW);
        TrySetAnimatorBool(aParam, _syncA);
        TrySetAnimatorBool(sParam, _syncS);
        TrySetAnimatorBool(dParam, _syncD);
        TrySetAnimatorBool(runParam, _syncRun);
    }

    void TrySetAnimatorBool(string paramName, bool value)
    {
        if (_remoteAnimator == null || string.IsNullOrEmpty(paramName)) return;
        for (int i = 0; i < _remoteAnimator.parameterCount; i++)
        {
            var p = _remoteAnimator.GetParameter(i);
            if (p.name == paramName && p.type == AnimatorControllerParameterType.Bool)
            {
                _remoteAnimator.SetBool(paramName, value);
                return;
            }
        }
    }
}
