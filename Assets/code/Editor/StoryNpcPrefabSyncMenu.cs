#if UNITY_EDITOR
using System.IO;
using GameQuestSystem;
using UnityEditor;
using UnityEngine;

public static class StoryNpcPrefabSyncMenu
{
    const string DatabasePath = Content1StoryImporterMenu.DatabasePath;

    [MenuItem("Tools/Quest System/同步剧情 NPC Prefab 引用", false, 62)]
    [MenuItem("Tools/Quest System/剧情NPC/同步剧情 NPC Prefab 引用", false, 62)]
    public static void SyncPrefabReferences()
    {
        var database = AssetDatabase.LoadAssetAtPath<StoryDialogueDatabase>(DatabasePath);
        if (database == null || database.npcs == null)
        {
            Debug.LogError("[StoryNpcPrefabSync] 未找到剧情数据库: " + DatabasePath);
            return;
        }

        int updated = 0;
        for (int i = 0; i < database.npcs.Count; i++)
        {
            var npc = database.npcs[i];
            if (npc == null || string.IsNullOrWhiteSpace(npc.prefabPath))
                continue;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(npc.prefabPath);
            if (prefab == null)
            {
                Debug.LogWarning("[StoryNpcPrefabSync] 预制体不存在: " + npc.prefabPath);
                continue;
            }

            if (npc.prefab == prefab)
                continue;

            npc.prefab = prefab;
            updated++;
        }

        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();
        Debug.Log($"[StoryNpcPrefabSync] 已同步 {updated} 个剧情 NPC prefab 引用。");
    }

    public static void AssignPrefabFromPath(StoryNpcDefinition npc)
    {
        if (npc == null || string.IsNullOrWhiteSpace(npc.prefabPath))
            return;

        if (!File.Exists(npc.prefabPath))
            return;

        npc.prefab = AssetDatabase.LoadAssetAtPath<GameObject>(npc.prefabPath);
    }
}
#endif
