using Mirror;
using UnityEngine;

namespace Multiplayer
{
    /// <summary>
    /// 联机时把载具位移交给当前驾驶者：ClientToServer + AssignClientAuthority，
    /// 否则仅客户端在动 CharacterController，服务器与其他端（含房主）看不到车移动。
    /// </summary>
    [DefaultExecutionOrder(-10)]
    [RequireComponent(typeof(NetworkIdentity))]
    [RequireComponent(typeof(NetworkTransformReliable))]
    public class VehicleNetworkProxy : NetworkBehaviour
    {
        [SyncVar(hook = nameof(OnDriverChanged))] uint _driverNetId;

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

        /// <summary>
        /// 本端是否可开始/继续模拟车辆位移。
        /// - 纯本地（未联机）: true
        /// - 主机同进程：只要本地是驾驶者即可
        /// - 远端客户端：需要本地是驾驶者且已拿到对象 authority（NetworkTransform 才会上行）
        /// </summary>
        public bool CanLocalSimulateVehicle()
        {
            if (!NetworkClient.active)
                return true;

            if (!IsLocalDriver)
                return false;

            if (NetworkServer.active)
                return true; // host/server 端可直接驱动服务器对象

            return netIdentity != null && netIdentity.isOwned;
        }

        /// <summary>
        /// 兜底同步：驾驶者每帧把车体状态上报到服务器，再广播到其他客户端。
        /// 用于 authority 分配不稳定时，仍保证其他玩家看到车在移动。
        /// </summary>
        public void PushLocalDriveState(Vector3 pos, Quaternion rot, Vector3 horizontalVelocity, float speed)
        {
            if (!NetworkClient.active || !IsLocalDriver)
                return;
            CmdPushDriveState(pos, rot, horizontalVelocity, speed);
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
        void CmdPushDriveState(Vector3 pos, Quaternion rot, Vector3 horizontalVelocity, float speed, NetworkConnectionToClient sender = null)
        {
            if (sender == null || sender.identity == null)
                return;
            if (_driverNetId == 0 || sender.identity.netId != _driverNetId)
                return;

            // 服务器权威位置
            transform.SetPositionAndRotation(pos, rot);
            RpcApplyDriveState(pos, rot, horizontalVelocity, speed);
        }

        [ClientRpc(includeOwner = false)]
        void RpcApplyDriveState(Vector3 pos, Quaternion rot, Vector3 horizontalVelocity, float speed)
        {
            transform.SetPositionAndRotation(pos, rot);
            var vc = GetComponent<VehicleController>();
            if (vc != null)
                vc.ApplyRemoteDriveState(horizontalVelocity, speed);
        }

        void TryGrantDriveAuthority(NetworkConnectionToClient conn)
        {
            NetworkIdentity nid = netIdentity;
            if (nid == null || !NetworkServer.active || conn == null)
                return;
            if (nid.connectionToClient == conn)
                return;
            if (nid.connectionToClient != null)
                nid.RemoveClientAuthority();
            if (!nid.AssignClientAuthority(conn))
                Debug.LogWarning($"[VehicleNetworkProxy] AssignClientAuthority 失败（载具是否已由 Mirror 生成？）: {name}", this);
        }

        void ReleaseDriveAuthority(NetworkConnectionToClient conn)
        {
            NetworkIdentity nid = netIdentity;
            if (nid == null || !NetworkServer.active)
                return;
            if (conn != null && nid.connectionToClient == conn)
                nid.RemoveClientAuthority();
        }

        void OnDriverChanged(uint _, uint __) { }
    }
}
