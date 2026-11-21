using UnityEngine;

namespace GameQuestSystem
{
    /// <summary>
    /// libraryfloor1 已确认的剧情落点（世界坐标 + 对应 NPC）。
    /// </summary>
    public static class LibraryFloor1StoryLocations
    {
        public const string SceneName = "libraryfloor1";

        public static readonly StorySpot M1_3_1 = new StorySpot(
            "libraryfloor1_m1_3_1",
            "图书馆一楼",
            new Vector3(-58.9099998f, 0.649999976f, -36.1300011f),
            "npc_沈青禾",
            "沈青禾");

        public static readonly StorySpot M2_0_1 = new StorySpot(
            "libraryfloor1_m2_0_1",
            "图书馆一楼·电脑区",
            new Vector3(-52.8400002f, 0.579999983f, 3.1400001f),
            "npc_沈青禾",
            "沈青禾");

        public static readonly StorySpot M2_2_1 = new StorySpot(
            "libraryfloor1_m2_2_1",
            "图书馆一楼",
            new Vector3(-87.8099976f, 0.579999983f, -12.7469997f),
            "npc_周砚",
            "周砚");

        public static bool TryGetSpot(string documentId, out StorySpot spot)
        {
            spot = default;
            if (string.IsNullOrWhiteSpace(documentId))
                return false;

            switch (documentId.Trim())
            {
                case "M1-3-1":
                    spot = M1_3_1;
                    return true;
                case "M2-0-1":
                    spot = M2_0_1;
                    return true;
                case "M2-2-1":
                    spot = M2_2_1;
                    return true;
                default:
                    return false;
            }
        }

        public static StoryLocationDefinition ToDefinition(StorySpot spot)
        {
            return new StoryLocationDefinition
            {
                locationId = spot.LocationId,
                displayName = spot.DisplayName,
                sceneName = SceneName,
                position = spot.Position,
                needsConfirmation = false
            };
        }
    }

    public readonly struct StorySpot
    {
        public string LocationId { get; }
        public string DisplayName { get; }
        public Vector3 Position { get; }
        public string NpcId { get; }
        public string NpcDisplayName { get; }

        public StorySpot(string locationId, string displayName, Vector3 position, string npcId, string npcDisplayName)
        {
            LocationId = locationId;
            DisplayName = displayName;
            Position = position;
            NpcId = npcId;
            NpcDisplayName = npcDisplayName;
        }
    }
}
