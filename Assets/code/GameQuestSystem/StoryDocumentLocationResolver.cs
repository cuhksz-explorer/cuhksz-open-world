using System;
using System.Collections.Generic;

namespace GameQuestSystem
{
    /// <summary>
    /// 解析剧情文档（含分支）的目标场景与坐标；分支缺省时继承根任务位置。
    /// </summary>
    public static class StoryDocumentLocationResolver
    {
        public static Dictionary<string, string> BuildChoiceParentMap(StoryDialogueDatabase database)
        {
            var parent = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (database?.documents == null)
                return parent;

            for (int i = 0; i < database.documents.Count; i++)
            {
                var doc = database.documents[i];
                if (doc?.choices == null)
                    continue;

                for (int c = 0; c < doc.choices.Count; c++)
                {
                    var choice = doc.choices[c];
                    if (choice == null || string.IsNullOrWhiteSpace(choice.targetDocumentId))
                        continue;
                    parent[choice.targetDocumentId.Trim()] = doc.documentId;
                }
            }

            return parent;
        }

        public static string FindRootDocumentId(
            string documentId,
            StoryDialogueDatabase database,
            Dictionary<string, string> parentMap = null)
        {
            if (string.IsNullOrWhiteSpace(documentId) || database == null)
                return documentId ?? "";

            parentMap ??= BuildChoiceParentMap(database);
            string current = documentId.Trim();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            while (!string.IsNullOrEmpty(current) && seen.Add(current))
            {
                var doc = database.FindDocument(current);
                if (doc != null && doc.isRootTask)
                    return current;

                if (!parentMap.TryGetValue(current, out string parentId))
                    break;

                current = parentId;
            }

            return documentId;
        }

        public static string ResolveTargetSceneName(StoryDialogueDatabase database, StoryDialogueDocument doc)
        {
            if (doc == null)
                return "";

            if (!string.IsNullOrWhiteSpace(doc.targetSceneName))
                return doc.targetSceneName.Trim();

            string rootId = FindRootDocumentId(doc.documentId, database);
            var root = database.FindDocument(rootId);
            return root != null && !string.IsNullOrWhiteSpace(root.targetSceneName)
                ? root.targetSceneName.Trim()
                : "";
        }

        public static UnityEngine.Vector3 ResolveFallbackPosition(StoryDialogueDatabase database, StoryDialogueDocument doc)
        {
            if (doc == null)
                return UnityEngine.Vector3.zero;

            if (doc.fallbackTargetPosition != UnityEngine.Vector3.zero)
                return doc.fallbackTargetPosition;

            string rootId = FindRootDocumentId(doc.documentId, database);
            var root = database.FindDocument(rootId);
            return root != null ? root.fallbackTargetPosition : UnityEngine.Vector3.zero;
        }

        public static bool HasPlaceableNpc(StoryDialogueDocument doc)
        {
            if (doc == null || string.IsNullOrWhiteSpace(doc.npcId))
                return false;
            return !string.Equals(doc.npcId, "story_trigger", StringComparison.OrdinalIgnoreCase);
        }

        public static bool ShouldPlaceInScene(StoryDialogueDocument doc)
        {
            if (doc == null || !HasPlaceableNpc(doc))
                return false;
            return doc.isRootTask || doc.isBranchDocument;
        }

        public static UnityEngine.Vector3 GetBranchSpreadOffset(int index)
        {
            if (index <= 0)
                return new UnityEngine.Vector3(2.5f, 0f, 0f);

            float angle = index * 72f * UnityEngine.Mathf.Deg2Rad;
            float radius = 2f + index * 0.75f;
            return new UnityEngine.Vector3(
                UnityEngine.Mathf.Cos(angle) * radius,
                0f,
                UnityEngine.Mathf.Sin(angle) * radius);
        }
    }
}
