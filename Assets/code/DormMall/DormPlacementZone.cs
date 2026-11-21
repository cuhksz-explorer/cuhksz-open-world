using UnityEngine;

namespace DormMall
{
    /// <summary>
    /// 寝室可放置区域。共享实例模式下所有登录玩家在同一张地图的同一区域内放置，各自布置互不影响（按 dorm::roomN 存档）。
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class DormPlacementZone : MonoBehaviour
    {
        [Tooltip("为 true：任意已分配寝室槽位的玩家都可在此区域放置（同地图不同实例）。")]
        public bool sharedInstanceZone = true;

        [Tooltip("非共享模式下仅该 roomIndex 的玩家可放置")]
        public int roomIndex;

        Collider _collider;

        void Awake()
        {
            _collider = GetComponent<Collider>();
            if (_collider != null)
                _collider.isTrigger = true;
        }

        public bool AppliesToRoom(int playerRoomIndex)
        {
            if (sharedInstanceZone)
                return playerRoomIndex >= 0;
            return playerRoomIndex >= 0 && roomIndex == playerRoomIndex;
        }

        public bool ContainsPoint(Vector3 worldPosition)
        {
            if (_collider == null)
                _collider = GetComponent<Collider>();
            if (_collider == null)
                return false;

            return _collider.bounds.Contains(worldPosition);
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            var col = GetComponent<Collider>();
            if (col == null)
                return;
            bool mine = Application.isPlaying
                && AppliesToRoom(UserDormRoomService.RoomIndex);
            Gizmos.color = sharedInstanceZone
                ? new Color(0.3f, 0.85f, 1f, 0.22f)
                : (mine ? new Color(0.2f, 1f, 0.35f, 0.25f) : new Color(1f, 0.75f, 0.2f, 0.18f));
            Gizmos.matrix = Matrix4x4.identity;
            Gizmos.DrawCube(col.bounds.center, col.bounds.size);
        }
#endif
    }
}
