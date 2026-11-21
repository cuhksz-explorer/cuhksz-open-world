using GameQuestSystem;
using DormMall;
using Mirror;
using UnityEngine;

/// <summary>
/// 挂在 CoopPlayer 上：仅「主机本地玩家」在服务器侧把 <see cref="QuestSystemManager"/> 快照写入 SyncVar，
/// 客端通过 Hook 应用房主任务与金币显示。
/// </summary>
[DisallowMultipleComponent]
public class CoopQuestStateHub : NetworkBehaviour
{
    public static CoopQuestStateHub HostHub { get; private set; }

    [SyncVar(hook = nameof(OnSnapshotHook))]
    string _snapshotJson = "";
    [SyncVar(hook = nameof(OnInventorySnapshotHook))]
    string _inventorySnapshotJson = "";
    [SyncVar(hook = nameof(OnHostTimeOfDayHook))]
    float _hostTimeOfDay;
    [SyncVar(hook = nameof(OnHostTimeRunningHook))]
    bool _hostTimeRunning = true;
    [SyncVar(hook = nameof(OnHostDayLengthHook))]
    float _hostDayLengthSeconds = 60f;

    QuestSystemManager _subscribed;
    float _nextHostTimeSyncAt;
    Coroutine _applySnapshotRetry;

    bool IsHostPlayerOnServer =>
        isServer && connectionToClient != null && connectionToClient == NetworkServer.localConnection;

    public override void OnStartServer()
    {
        base.OnStartServer();
        if (!IsHostPlayerOnServer)
            return;
        HostHub = this;
        TrySubscribeQuestEvents();
    }

    void TrySubscribeQuestEvents()
    {
        var q = QuestSystemManager.Instance;
        if (q == null)
            return;
        if (_subscribed == q)
            return;
        if (_subscribed != null)
        {
            _subscribed.OnQuestChanged -= ServerPushSnapshot;
            _subscribed.OnGoldChanged -= ServerOnGoldChanged;
        }
        _subscribed = q;
        q.OnQuestChanged += ServerPushSnapshot;
        q.OnGoldChanged += ServerOnGoldChanged;
        ServerPushSnapshot();
    }

    void Update()
    {
        if (!IsHostPlayerOnServer)
            return;
        if (_subscribed == null || QuestSystemManager.Instance != _subscribed)
            TrySubscribeQuestEvents();
        ServerPushInventorySnapshot();
        if (Time.unscaledTime >= _nextHostTimeSyncAt)
        {
            _nextHostTimeSyncAt = Time.unscaledTime + 0.25f;
            ServerPushHostTimeSnapshot();
        }
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        if (netIdentity != null && netIdentity.isOwned)
            return;

        if (!string.IsNullOrEmpty(_snapshotJson))
            TryApplyQuestSnapshotOrRetry(_snapshotJson);
        if (!string.IsNullOrEmpty(_inventorySnapshotJson))
            InventoryData.ApplySerializedItems(_inventorySnapshotJson);
    }

    void ServerOnGoldChanged(int _) => ServerPushSnapshot();

    void ServerPushSnapshot()
    {
        var q = QuestSystemManager.Instance;
        if (q == null)
            return;
        string json = q.SerializeCoopSnapshotForNet();
        if (_snapshotJson == json)
            return;
        _snapshotJson = json;
    }

    void ServerPushInventorySnapshot()
    {
        string json = InventoryData.SerializeItemsJson();
        if (_inventorySnapshotJson == json)
            return;
        _inventorySnapshotJson = json;
    }

    void ServerPushHostTimeSnapshot()
    {
        var sim = Object.FindObjectOfType<SunSimulation>(true);
        if (sim == null)
            return;
        _hostTimeOfDay = sim.currentTimeOfDay;
        _hostTimeRunning = sim.IsRunning;
        _hostDayLengthSeconds = sim.dayLengthInSeconds;
    }

    public override void OnStopServer()
    {
        if (_subscribed != null)
        {
            _subscribed.OnQuestChanged -= ServerPushSnapshot;
            _subscribed.OnGoldChanged -= ServerOnGoldChanged;
            _subscribed = null;
        }
        if (HostHub == this)
            HostHub = null;
        base.OnStopServer();
    }

    public override void OnStopClient()
    {
        if (_applySnapshotRetry != null)
        {
            StopCoroutine(_applySnapshotRetry);
            _applySnapshotRetry = null;
        }
        base.OnStopClient();
    }

    void OnSnapshotHook(string oldValue, string newValue)
    {
        if (netIdentity.isOwned)
            return;
        if (string.IsNullOrEmpty(newValue))
            return;
        TryApplyQuestSnapshotOrRetry(newValue);
    }

    void OnInventorySnapshotHook(string oldValue, string newValue)
    {
        if (netIdentity.isOwned)
            return;
        if (string.IsNullOrEmpty(newValue))
            return;
        InventoryData.ApplySerializedItems(newValue);
        if (InventoryUI.Instance != null)
            InventoryUI.Instance.RequestRefreshAfterHostSync();
    }

    void OnHostTimeOfDayHook(float oldValue, float newValue)
    {
        if (netIdentity.isOwned)
            return;
        ApplyHostTimeToGuest();
    }

    void OnHostTimeRunningHook(bool oldValue, bool newValue)
    {
        if (netIdentity.isOwned)
            return;
        ApplyHostTimeToGuest();
    }

    void OnHostDayLengthHook(float oldValue, float newValue)
    {
        if (netIdentity.isOwned)
            return;
        ApplyHostTimeToGuest();
    }

    void ApplyHostTimeToGuest()
    {
        if (!CoopSessionState.IsSessionGuest)
            return;
        var sim = Object.FindObjectOfType<SunSimulation>(true);
        if (sim == null)
            return;
        sim.ApplyHostTimeSnapshot(_hostTimeOfDay, _hostTimeRunning, _hostDayLengthSeconds);
    }

    void TryApplyQuestSnapshotOrRetry(string snapshot)
    {
        var q = QuestSystemManager.Instance;
        if (q != null)
        {
            q.TryApplyCoopHostSnapshotFromJson(snapshot);
            return;
        }

        if (_applySnapshotRetry != null)
            StopCoroutine(_applySnapshotRetry);
        _applySnapshotRetry = StartCoroutine(CoRetryApplySnapshot(snapshot));
    }

    System.Collections.IEnumerator CoRetryApplySnapshot(string snapshot)
    {
        float deadline = Time.unscaledTime + 5f;
        while (Time.unscaledTime < deadline)
        {
            var q = QuestSystemManager.Instance;
            if (q != null)
            {
                q.TryApplyCoopHostSnapshotFromJson(snapshot);
                _applySnapshotRetry = null;
                yield break;
            }
            yield return null;
        }
        _applySnapshotRetry = null;
    }
}
