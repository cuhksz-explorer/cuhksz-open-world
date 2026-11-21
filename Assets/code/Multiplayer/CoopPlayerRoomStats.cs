using Mirror;
using UnityEngine;

/// <summary>
/// 挂在 Coop 玩家预制体上：仅「主机上的本地玩家」在服务器侧更新 <see cref="_onlineCount"/>，
/// 通过 SyncVar 同步到所有客户端，驱动 <see cref="CoopRoomOnlineHud"/>。
/// </summary>
[DisallowMultipleComponent]
public class CoopPlayerRoomStats : NetworkBehaviour
{
    [SyncVar(hook = nameof(OnOnlineCountHook))]
    int _onlineCount;

    bool IsHostPlayerOnServer =>
        isServer && connectionToClient != null && connectionToClient == NetworkServer.localConnection;

    public override void OnStartServer()
    {
        base.OnStartServer();
        if (IsHostPlayerOnServer)
            ServerForceRefresh();
    }

    void Update()
    {
        if (!IsHostPlayerOnServer) return;
        int n = CountConnectionsWithPlayer();
        if (_onlineCount != n)
        {
            _onlineCount = n;
            CoopNetworkPlayer.ServerAssignRoomPaletteSlots();
        }
    }

    static int CountConnectionsWithPlayer()
    {
        if (NetworkServer.connections == null) return 0;
        int n = 0;
        foreach (var kv in NetworkServer.connections)
        {
            var conn = kv.Value;
            if (conn != null && conn.identity != null)
                n++;
        }
        return n;
    }
     
    /// <summary>服务器侧由 <see cref="LanCoopNetworkManager"/> 在加人/断线后调用。</summary>
    public void ServerForceRefresh()
    {
        if (!IsHostPlayerOnServer) return;
        int n = CountConnectionsWithPlayer();
        if (_onlineCount != n)
            _onlineCount = n;
        else
            CoopRoomOnlineHud.ApplySyncedCount(n);
        CoopNetworkPlayer.ServerAssignRoomPaletteSlots();
    }

    void OnOnlineCountHook(int oldValue, int newValue)
    {
        CoopRoomOnlineHud.ApplySyncedCount(newValue);
    }
}
