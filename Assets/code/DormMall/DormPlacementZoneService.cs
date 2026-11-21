using UnityEngine;

namespace DormMall
{
    /// <summary>
    /// 判断当前是否允许放置（dorm 场景 + 共享或专属放置区）。
    /// </summary>
    public static class DormPlacementZoneService
    {
        static DormPlacementZone[] _cachedZones;
        static int _cachedZonesFrame = -1;

        public static bool CanEnterPlacementMode(out string blockReason)
        {
            blockReason = null;
            if (!DormSceneConfig.IsActiveDormScene())
            {
                blockReason = "物品只能在自己的寝室（dorm 场景）内放置。";
                return false;
            }

            if (UserDormRoomService.RoomIndex < 0)
            {
                blockReason = "尚未分配寝室槽位，请登录后再试。";
                return false;
            }

            if (GetZoneForCurrentPlayer() == null)
            {
                blockReason = "场景中未找到寝室放置区域（DormPlacementZone）。";
                return false;
            }

            return true;
        }

        public static bool IsPlacementAllowedAt(Vector3 worldPosition)
        {
            if (!DormSceneConfig.IsActiveDormScene())
                return false;
            if (UserDormRoomService.RoomIndex < 0)
                return false;

            var zone = GetZoneForCurrentPlayer();
            return zone != null && zone.ContainsPoint(worldPosition);
        }

        public static DormPlacementZone GetZoneForCurrentPlayer()
        {
            int room = UserDormRoomService.RoomIndex;
            if (room < 0)
                return null;

            var zones = GetAllZones();
            DormPlacementZone shared = null;
            for (int i = 0; i < zones.Length; i++)
            {
                var z = zones[i];
                if (z == null)
                    continue;
                if (z.sharedInstanceZone)
                {
                    if (shared == null)
                        shared = z;
                    continue;
                }
                if (z.roomIndex == room)
                    return z;
            }
            return shared;
        }

        static DormPlacementZone[] GetAllZones()
        {
            int frame = Time.frameCount;
            if (_cachedZones != null && _cachedZonesFrame == frame)
                return _cachedZones;
            _cachedZones = Object.FindObjectsOfType<DormPlacementZone>(true);
            _cachedZonesFrame = frame;
            return _cachedZones;
        }

        public static void InvalidateCache()
        {
            _cachedZones = null;
            _cachedZonesFrame = -1;
        }
    }
}
