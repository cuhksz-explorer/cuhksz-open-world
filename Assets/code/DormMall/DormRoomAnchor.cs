using UnityEngine;

namespace DormMall
{
    /// <summary>
    /// 寝室出生点。共享实例模式下所有人进入同一张 dorm 地图的同一位置，仅布置数据按账号/槽位隔离。
    /// </summary>
    public class DormRoomAnchor : MonoBehaviour
    {
        [Tooltip("为 true：所有玩家共用此出生点（推荐：同地图不同实例）。为 false：仅 roomIndex 匹配的玩家使用。")]
        public bool sharedInstanceSpawn = true;

        [Tooltip("非共享模式下使用的寝室槽位编号")]
        public int roomIndex;

        [Tooltip("玩家进入该寝室时的朝向；为 0 时使用本物体 Transform 旋转")]
        public bool useTransformRotation = true;

        public Vector3 SpawnPosition => transform.position;

        public Quaternion SpawnRotation =>
            useTransformRotation ? transform.rotation : Quaternion.identity;

        public bool AppliesToRoom(int playerRoomIndex)
        {
            if (sharedInstanceSpawn)
                return true;
            return playerRoomIndex >= 0 && roomIndex == playerRoomIndex;
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 0.85f, 1f, 0.85f);
            Gizmos.DrawWireSphere(transform.position, 0.35f);
            string label = sharedInstanceSpawn
                ? "寝室出生点（共享）"
                : "寝室 #" + roomIndex;
            UnityEditor.Handles.Label(transform.position + Vector3.up * 0.6f, label);
        }
#endif
    }
}
