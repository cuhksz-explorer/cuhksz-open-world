using UnityEngine;

namespace GameQuestSystem
{
    /// <summary>
    /// computerroom 已确认的剧情落点（世界坐标 + 对应 NPC）。
    /// </summary>
    public static class ComputerRoomStoryLocations
    {
        public const string SceneName = "computerroom";
        public static readonly Vector3 DefaultPosition = new Vector3(-3.91f, 0.95f, -23.67f);

        public static readonly Entry M1_1_1 = CreateEntry("M1-1-1", "computerroom_m1_1_1", "机房/选课系统", DefaultPosition, "npc_沈青禾", "沈青禾");
        public static readonly Entry M1_1_3 = CreateEntry("M1-1-3", "computerroom_m1_1_3", "机房/选课系统", OffsetZ(DefaultPosition, 1.5f), "story_trigger", "剧情触发点");
        public static readonly Entry M1_2_1 = CreateEntry("M1-2-1", "computerroom_m1_2_1", "机房/选课系统", OffsetZ(DefaultPosition, 3f), "npc_赵梓璇", "赵梓璇");
        public static readonly Entry M1_4_1 = CreateEntry("M1-4-1", "computerroom_m1_4_1", "机房/选课系统", OffsetZ(DefaultPosition, 4.5f), "npc_均分幻影", "均分幻影");
        public static readonly Entry M1_4_3 = CreateEntry("M1-4-3", "computerroom_m1_4_3", "机房/选课系统", OffsetZ(DefaultPosition, 6f), "story_trigger", "剧情触发点");
        public static readonly Entry M1_5_1 = CreateEntry("M1-5-1", "computerroom_m1_5_1", "机房/选课系统", OffsetZ(DefaultPosition, 7.5f), "npc_周砚", "周砚");

        static readonly Entry[] AllEntries = { M1_1_1, M1_1_3, M1_2_1, M1_4_1, M1_4_3, M1_5_1 };

        public static bool TryGetEntry(string documentId, out Entry entry)
        {
            entry = default;
            if (string.IsNullOrWhiteSpace(documentId))
                return false;

            string id = documentId.Trim();
            for (int i = 0; i < AllEntries.Length; i++)
            {
                if (string.Equals(AllEntries[i].DocumentId, id, System.StringComparison.OrdinalIgnoreCase))
                {
                    entry = AllEntries[i];
                    return true;
                }
            }

            return false;
        }

        public static bool UsesConfirmedPosition(string documentId)
        {
            return TryGetEntry(documentId, out _);
        }

        public static StoryLocationDefinition ToDefinition(Entry entry)
        {
            return new StoryLocationDefinition
            {
                locationId = entry.LocationId,
                displayName = entry.DisplayName,
                sceneName = SceneName,
                position = entry.Position,
                needsConfirmation = false
            };
        }

        public static StorySpot ToStorySpot(Entry entry)
        {
            return new StorySpot(
                entry.LocationId,
                entry.DisplayName,
                entry.Position,
                entry.NpcId,
                entry.NpcDisplayName);
        }

        static Entry CreateEntry(
            string documentId,
            string locationId,
            string displayName,
            Vector3 position,
            string npcId,
            string npcDisplayName)
        {
            return new Entry(documentId, locationId, displayName, position, npcId, npcDisplayName);
        }

        static Vector3 OffsetZ(Vector3 basePosition, float deltaZ)
        {
            return new Vector3(basePosition.x, basePosition.y, basePosition.z + deltaZ);
        }

        public readonly struct Entry
        {
            public string DocumentId { get; }
            public string LocationId { get; }
            public string DisplayName { get; }
            public Vector3 Position { get; }
            public string NpcId { get; }
            public string NpcDisplayName { get; }

            public Entry(
                string documentId,
                string locationId,
                string displayName,
                Vector3 position,
                string npcId,
                string npcDisplayName)
            {
                DocumentId = documentId;
                LocationId = locationId;
                DisplayName = displayName;
                Position = position;
                NpcId = npcId;
                NpcDisplayName = npcDisplayName;
            }
        }
    }
}
