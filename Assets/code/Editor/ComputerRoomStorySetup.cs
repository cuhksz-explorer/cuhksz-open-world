#if UNITY_EDITOR
using System;
using System.Linq;
using GameQuestSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 将 M1 机房剧情配置到 computerroom，并放置对应剧情 NPC（Hierarchy 可见、可手动调位）。
/// </summary>
public static class ComputerRoomStorySetup
{
    const string MenuPath = "Tools/场景配置/computerroom - 配置剧情 NPC（M1 机房）";
    const string DatabasePath = "Assets/code/GameQuestSystem/Content1StoryDatabase.asset";
    const string ScenePath = "Assets/scene/computerroom.unity";
    const string StoryNpcRootName = "StoryNPCs";

    static readonly ComputerRoomStoryLocations.Entry[] Entries =
    {
        ComputerRoomStoryLocations.M1_1_1,
        ComputerRoomStoryLocations.M1_1_3,
        ComputerRoomStoryLocations.M1_2_1,
        ComputerRoomStoryLocations.M1_4_1,
        ComputerRoomStoryLocations.M1_4_3,
        ComputerRoomStoryLocations.M1_5_1
    };

    [MenuItem(MenuPath, false, 4)]
    public static void ConfigureFromMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("computerroom 剧情", "请先退出 Play Mode。", "确定");
            return;
        }

        var database = AssetDatabase.LoadAssetAtPath<StoryDialogueDatabase>(DatabasePath);
        if (database == null)
        {
            EditorUtility.DisplayDialog("computerroom 剧情", "未找到 Content1StoryDatabase.asset。", "确定");
            return;
        }

        int docs = PatchDatabase(database);
        int quests = 0;
        int npcs = 0;

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (scene.IsValid())
        {
            quests = PatchQuestSystemManager(scene);
            npcs = PlaceStoryNpcs(scene, database);
            EditorSceneManager.SaveScene(scene);
        }

        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog(
            "computerroom 剧情",
            $"已更新剧情文档 {docs} 条。\n已更新任务追踪 {quests} 条。\n已放置/更新 NPC {npcs} 个。\n\n" +
            "请在 Hierarchy 的 StoryNPCs 下微调各 NPC 坐标。",
            "确定");
    }

    public static int PatchDatabase(StoryDialogueDatabase database)
    {
        int count = 0;
        foreach (var entry in Entries)
        {
            var loc = ComputerRoomStoryLocations.ToDefinition(entry);
            UpsertLocation(database, loc);

            var doc = database.documents.FirstOrDefault(d =>
                string.Equals(d.documentId, entry.DocumentId, StringComparison.OrdinalIgnoreCase));
            if (doc == null)
                continue;

            doc.locationId = loc.locationId;
            doc.targetSceneName = loc.sceneName;
            doc.fallbackTargetPosition = loc.position;
            doc.finishLocationName = loc.displayName;
            doc.needsLocationConfirmation = false;
            if (string.IsNullOrWhiteSpace(doc.npcId))
                doc.npcId = entry.NpcId;

            var npcEntry = database.npcs.FirstOrDefault(n => n != null && n.npcId == entry.NpcId);
            if (npcEntry != null)
            {
                npcEntry.defaultSceneName = loc.sceneName;
                npcEntry.defaultPosition = loc.position;
                npcEntry.needsPositionConfirmation = false;
            }

            count++;
        }

        EditorUtility.SetDirty(database);
        return count;
    }

    static void UpsertLocation(StoryDialogueDatabase database, StoryLocationDefinition loc)
    {
        var existing = database.locations.FirstOrDefault(l =>
            l != null && string.Equals(l.locationId, loc.locationId, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            existing.displayName = loc.displayName;
            existing.sceneName = loc.sceneName;
            existing.position = loc.position;
            existing.needsConfirmation = loc.needsConfirmation;
            return;
        }

        database.locations.Add(loc);
    }

    static int PatchQuestSystemManager(Scene scene)
    {
        int count = 0;
        foreach (var manager in UnityEngine.Object.FindObjectsOfType<QuestSystemManager>(true))
        {
            if (manager.gameObject.scene != scene)
                continue;

            var so = new SerializedObject(manager);
            var chains = so.FindProperty("questChains");
            bool dirty = false;

            for (int c = 0; c < chains.arraySize; c++)
            {
                var subtasks = chains.GetArrayElementAtIndex(c).FindPropertyRelative("subTasks");
                for (int i = 0; i < subtasks.arraySize; i++)
                {
                    var st = subtasks.GetArrayElementAtIndex(i);
                    string taskName = st.FindPropertyRelative("subTaskName").stringValue;
                    if (!ComputerRoomStoryLocations.TryGetEntry(taskName, out var entry))
                        continue;

                    var loc = ComputerRoomStoryLocations.ToDefinition(entry);
                    st.FindPropertyRelative("targetSceneName").stringValue = loc.sceneName;
                    st.FindPropertyRelative("fallbackTargetPosition").vector3Value = loc.position;
                    st.FindPropertyRelative("finishLocationName").stringValue = loc.displayName;
                    dirty = true;
                    count++;
                }
            }

            if (dirty)
            {
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(manager);
            }
        }

        return count;
    }

    static int PlaceStoryNpcs(Scene scene, StoryDialogueDatabase database)
    {
        var root = EnsureStoryNpcRoot(scene);
        int count = 0;

        foreach (var entry in Entries)
        {
            var existing = UnityEngine.Object.FindObjectsOfType<StoryQuestNpc>(true)
                .FirstOrDefault(n => n != null && n.gameObject.scene == scene
                    && string.Equals(n.documentId, entry.DocumentId, StringComparison.OrdinalIgnoreCase));

            GameObject go;
            if (existing != null)
            {
                go = existing.gameObject;
            }
            else
            {
                var npcEntry = database.FindNpc(entry.NpcId);
                if (npcEntry == null || string.IsNullOrWhiteSpace(npcEntry.prefabPath))
                {
                    Debug.LogWarning($"[computerroom] 未找到 NPC 预制体：{entry.NpcId}");
                    continue;
                }

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(npcEntry.prefabPath);
                if (prefab == null)
                {
                    Debug.LogWarning($"[computerroom] 预制体路径无效：{npcEntry.prefabPath}");
                    continue;
                }

                go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                go.name = "StoryNPC_" + entry.NpcDisplayName + "_" + entry.DocumentId;
                Undo.RegisterCreatedObjectUndo(go, "Place computerroom story NPC");
            }

            if (root != null)
                go.transform.SetParent(root.transform, true);

            go.transform.position = entry.Position;
            var storyNpc = go.GetComponent<StoryQuestNpc>() ?? go.AddComponent<StoryQuestNpc>();
            storyNpc.documentId = entry.DocumentId;
            storyNpc.displayName = entry.NpcDisplayName;
            storyNpc.hideAfterStoryCompleted = true;
            storyNpc.hidePrefabVisualInPlayMode =
                StoryQuestNpcVisualUtility.IsStoryTriggerDisplayName(entry.NpcDisplayName);

            EditorUtility.SetDirty(go);
            count++;
        }

        return count;
    }

    static GameObject EnsureStoryNpcRoot(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name == StoryNpcRootName)
                return root;
        }

        var go = new GameObject(StoryNpcRootName);
        SceneManager.MoveGameObjectToScene(go, scene);
        Undo.RegisterCreatedObjectUndo(go, "Create StoryNPCs root");
        return go;
    }
}
#endif
