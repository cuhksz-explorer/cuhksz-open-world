using UnityEngine;

namespace GameQuestSystem
{
    /// <summary>
    /// undergarden（下园）已确认的剧情落点（世界坐标 + 对应 NPC）。
    /// </summary>
    public static class UndergardenStoryLocations
    {
        public const string SceneName = "undergarden";

        public static readonly Entry M1_2_3 = CreateEntry(
            "M1-2-3",
            "undergarden_m1_2_3",
            "下园花坛",
            new Vector3(-1357.52f, 16.19f, 1086.67f),
            "story_trigger",
            "剧情触发点");

        static readonly Entry[] AllEntries = { M1_2_3 };

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
