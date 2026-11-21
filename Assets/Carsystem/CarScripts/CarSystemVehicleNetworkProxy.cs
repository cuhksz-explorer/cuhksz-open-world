using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// Carsystem 载具的 Mirror 代理：负责驾驶者占用、Authority 和兜底状态同步。
/// 运动核心仍由 <see cref="Carbilili"/> 驱动，不复用旧系统的位移控制。
/// </summary>
[DefaultExecutionOrder(-10)]
[RequireComponent(typeof(NetworkIdentity))]
[RequireComponent(typeof(NetworkTransformReliable))]
public class CarSystemVehicleNetworkProxy : NetworkBehaviour
{
    [SyncVar(hook = nameof(OnDriverChanged))]
    uint _driverNetId;

    static readonly HashSet<int> s_registeredPrefabIds = new HashSet<int>();

    public bool IsOccupied => _driverNetId != 0;

    public bool IsLocalDriver
    {
        get
        {
            uint local = NetworkClient.localPlayer != null ? NetworkClient.localPlayer.netId : 0u;
            return local != 0 && _driverNetId == local;
        }
    }

    public bool IsOccupiedByOther()
    {
        uint local = NetworkClient.localPlayer != null ? NetworkClient.localPlayer.netId : 0u;
        return IsOccupied && _driverNetId != local;
    }

    public bool CanLocalSimulateVehicle()
    {
        if (!NetworkClient.active)
            return true;
        if (!IsLocalDriver)
            return false;
        if (NetworkServer.active)
            return true;
        return netIdentity != null && netIdentity.isOwned;
    }

    public static void TryRegisterNetworkPrefab(GameObject prefab)
    {
        if (prefab == null)
            return;

        int id = prefab.GetInstanceID();
        if (!s_registeredPrefabIds.Add(id))
            return;

        if (NetworkManager.singleton != null && !NetworkManager.singleton.spawnPrefabs.Contains(prefab))
            NetworkManager.singleton.spawnPrefabs.Add(prefab);

        if (NetworkClient.active)
            NetworkClient.RegisterPrefab(prefab);
    }

    void Awake()
    {
        var nt = GetComponent<NetworkTransformReliable>();
        if (nt != null)
            nt.syncDirection = SyncDirection.ClientToServer;
    }

    public void TryBeginDrive()
    {
        if (!NetworkClient.active)
            return;
        CmdTryBeginDrive();
    }

    public void TryEndDrive()
    {
        if (!NetworkClient.active)
            return;
        CmdTryEndDrive();
    }

    public void PushLocalDriveState(Vector3 pos, Quaternion rot, Vector3 linearVelocity, Vector3 angularVelocity)
    {
        if (!NetworkClient.active || !IsLocalDriver)
            return;
        CmdPushDriveState(pos, rot, linearVelocity, angularVelocity);
    }

    [Command(requiresAuthority = false)]
    void CmdTryBeginDrive(NetworkConnectionToClient sender = null)
    {
        if (sender == null || sender.identity == null)
            return;

        uint requester = sender.identity.netId;
        if (_driverNetId != 0 && _driverNetId != requester)
            return;

        _driverNetId = requester;
        TryGrantDriveAuthority(sender);
    }

    [Command(requiresAuthority = false)]
    void CmdTryEndDrive(NetworkConnectionToClient sender = null)
    {
        if (sender == null || sender.identity == null)
            return;

        uint requester = sender.identity.netId;
        if (_driverNetId != requester)
            return;

        _driverNetId = 0;
        ReleaseDriveAuthority(sender);
    }

    [Command(requiresAuthority = false)]
    void CmdPushDriveState(
        Vector3 pos,
        Quaternion rot,
        Vector3 linearVelocity,
        Vector3 angularVelocity,
        NetworkConnectionToClient sender = null)
    {
        if (sender == null || sender.identity == null)
            return;
        if (_driverNetId == 0 || sender.identity.netId != _driverNetId)
            return;

        transform.SetPositionAndRotation(pos, rot);

        var iv = GetComponent<InteractableVehicle>();
        if (iv != null)
            iv.ApplyRemoteDriveState(linearVelocity, angularVelocity);

        RpcApplyDriveState(pos, rot, linearVelocity, angularVelocity);
    }

    [ClientRpc(includeOwner = false)]
    void RpcApplyDriveState(Vector3 pos, Quaternion rot, Vector3 linearVelocity, Vector3 angularVelocity)
    {
        transform.SetPositionAndRotation(pos, rot);
        var iv = GetComponent<InteractableVehicle>();
        if (iv != null)
            iv.ApplyRemoteDriveState(linearVelocity, angularVelocity);
    }

    void TryGrantDriveAuthority(NetworkConnectionToClient conn)
    {
        if (!NetworkServer.active || conn == null || netIdentity == null)
            return;
        if (netIdentity.connectionToClient == conn)
            return;
        if (netIdentity.connectionToClient != null)
            netIdentity.RemoveClientAuthority();
        if (!netIdentity.AssignClientAuthority(conn))
            Debug.LogWarning($"[CarSystemVehicleNetworkProxy] AssignClientAuthority 失败：{name}", this);
    }

    void ReleaseDriveAuthority(NetworkConnectionToClient conn)
    {
        if (!NetworkServer.active || netIdentity == null)
            return;
        if (conn != null && netIdentity.connectionToClient == conn)
            netIdentity.RemoveClientAuthority();
    }

    void OnDriverChanged(uint _, uint __) { }
}
