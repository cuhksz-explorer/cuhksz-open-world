using System.Collections;
using GameQuestSystem;
using UnityEngine;
using Mirror;

/// <summary>
/// 局域网 Mirror 管理：需在首次运行前指定 <see cref="playerPrefab"/>（可用菜单 Tools/联机/Create Coop Player Prefab）。
/// Telepathy 默认端口见 <see cref="networkPort"/>。
/// </summary>
public class LanCoopNetworkManager : NetworkManager
{
    public static LanCoopNetworkManager Instance => singleton as LanCoopNetworkManager;

    [Tooltip("与 HTTP 信令里写入的 port 一致")]
    public ushort networkPort = 7777;
    public bool HasValidPlayerPrefab => playerPrefab != null;

    public override void Awake()
    {
        if (GetComponent<Transport>() == null)
            gameObject.AddComponent<TelepathyTransport>();
        if (playerPrefab == null)
        {
            var go = Resources.Load<GameObject>("CoopPlayer");
            if (go != null)
                playerPrefab = go;
            else
                Debug.LogWarning("[联机] 未找到 Resources/CoopPlayer，客户端可能无法看到彼此。请执行 Tools/联机/Create Coop Player Prefab。");
        }
        ApplyPort(networkPort);
        base.Awake();
    }

    public void ApplyPort(ushort port)
    {
        networkPort = port;
        if (GetComponent<TelepathyTransport>() is TelepathyTransport tp)
            tp.port = port;
    }

    public void StartHostWithPort(ushort port)
    {
        if (!HasValidPlayerPrefab)
        {
            Debug.LogError("[联机] 启动主机失败：缺少 playerPrefab（Resources/CoopPlayer）。");
            return;
        }
        ApplyPort(port);
        StartHost();
    }

    public void StartClientWithAddress(string address, ushort port)
    {
        if (!HasValidPlayerPrefab)
        {
            Debug.LogError("[联机] 启动客户端失败：缺少 playerPrefab（Resources/CoopPlayer）。");
            return;
        }
        ApplyPort(port);
        networkAddress = string.IsNullOrWhiteSpace(address) ? "127.0.0.1" : address.Trim();
        StartClient();
    }

    public void StopCoop()
    {
        if (NetworkServer.active && NetworkClient.active)
        {
            StopHost();
            return;
        }
        if (NetworkClient.active)
            StopClient();
        else if (NetworkServer.active)
            StopServer();
    }

    public void ResetSessionState()
    {
        StopCoop();
        networkAddress = "127.0.0.1";
        ApplyPort(networkPort);
    }

    public override void OnStopClient()
    {
        bool wasPureGuest = NetworkClient.active && !NetworkServer.active;
        base.OnStopClient();
        if (wasPureGuest && QuestSystemManager.Instance != null)
            QuestSystemManager.Instance.ReloadPersonalStateAfterCoop();
    }

    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        base.OnServerAddPlayer(conn);
        RefreshHostRoomStats();
    }

    public override void OnServerDisconnect(NetworkConnectionToClient conn)
    {
        base.OnServerDisconnect(conn);
        StartCoroutine(CoRefreshHostRoomStatsAfterDisconnect());
    }

    static void RefreshHostRoomStats()
    {
        if (!NetworkServer.active || NetworkServer.localConnection == null) return;
        var id = NetworkServer.localConnection.identity;
        if (id == null) return;
        var stats = id.GetComponent<CoopPlayerRoomStats>();
        stats?.ServerForceRefresh();
        CoopNetworkPlayer.ServerAssignRoomPaletteSlots();
    }

    IEnumerator CoRefreshHostRoomStatsAfterDisconnect()
    {
        yield return null;
        yield return null;
        RefreshHostRoomStats();
    }
}
