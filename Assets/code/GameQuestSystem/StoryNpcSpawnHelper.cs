using UnityEngine;

namespace GameQuestSystem
{
    public static class StoryNpcSpawnHelper
    {
        public static bool IsRuntimePlaceholder(GameObject go)
        {
            if (go == null)
                return false;

            if (go.transform.Find(StoryNpcVisualProfile.VisualChildName) != null)
                return false;

            var meshFilter = go.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null)
                return go.name.StartsWith("StoryNPC_Runtime_", System.StringComparison.Ordinal);

            string meshName = meshFilter.sharedMesh.name;
            return meshName == "Capsule"
                   || meshName == "Sphere"
                   || go.name.StartsWith("StoryNPC_Runtime_", System.StringComparison.Ordinal);
        }

        public static GameObject SpawnStoryNpc(
            StoryDialogueDatabase database,
            StoryDialogueDocument doc,
            Vector3 worldPosition)
        {
            if (doc == null)
                return null;

            var npcDef = database != null ? database.FindNpc(doc.npcId) : null;
            string displayName = npcDef != null && !string.IsNullOrWhiteSpace(npcDef.displayName)
                ? npcDef.displayName
                : "剧情人物";

            var prefab = StoryNpcPrefabResolver.Resolve(database, npcDef);
            GameObject go;
            if (prefab != null)
            {
                go = Object.Instantiate(prefab);
            }
            else
            {
                Debug.LogWarning(
                    "[StoryQuest] 未找到剧情 NPC 预制体，使用胶囊占位: " +
                    doc.documentId + " / " + (doc.npcId ?? "?") +
                    "。请在编辑器执行 Tools → Quest System → 同步剧情场景坐标。");
                go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            }

            go.name = "StoryNPC_" + displayName + "_" + doc.documentId;
            go.transform.position = worldPosition;

            var storyNpc = go.GetComponent<StoryQuestNpc>();
            if (storyNpc == null)
                storyNpc = go.AddComponent<StoryQuestNpc>();
            storyNpc.documentId = doc.documentId;
            storyNpc.displayName = displayName;
            storyNpc.interactDistance = 6f;
            storyNpc.hideAfterStoryCompleted = true;
            storyNpc.hidePrefabVisualInPlayMode =
                StoryQuestNpcVisualUtility.IsStoryTriggerDisplayName(displayName);
            storyNpc.ApplyTriggerOnlyAppearanceIfNeeded();
            return go;
        }
    }
}
