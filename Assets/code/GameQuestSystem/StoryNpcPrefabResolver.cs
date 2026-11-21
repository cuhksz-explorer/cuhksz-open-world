using UnityEngine;

namespace GameQuestSystem
{
    public static class StoryNpcPrefabResolver
    {
        public static GameObject Resolve(StoryDialogueDatabase database, StoryNpcDefinition npcDef)
        {
            if (npcDef == null)
                return null;

            if (npcDef.prefab != null)
                return npcDef.prefab;

#if UNITY_EDITOR
            if (!string.IsNullOrWhiteSpace(npcDef.prefabPath))
            {
                var editorPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(npcDef.prefabPath);
                if (editorPrefab != null)
                    return editorPrefab;
            }
#else
            var hotPrefab = HotUpdateAssetLoader.TryLoadPrefab(npcDef.prefabPath);
            if (hotPrefab != null)
                return hotPrefab;
#endif

            if (database?.npcs == null || string.IsNullOrWhiteSpace(npcDef.npcId))
                return null;

            for (int i = 0; i < database.npcs.Count; i++)
            {
                var other = database.npcs[i];
                if (other == null || other == npcDef)
                    continue;
                if (!string.Equals(other.npcId, npcDef.npcId, System.StringComparison.OrdinalIgnoreCase))
                    continue;
                if (other.prefab != null)
                    return other.prefab;
            }

            return null;
        }

        public static GameObject Resolve(StoryNpcDefinition npcDef)
        {
            return Resolve(null, npcDef);
        }
    }
}
