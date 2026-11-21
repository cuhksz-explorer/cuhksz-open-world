#if UNITY_EDITOR
using System;
using System.Linq;
using GameQuestSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 将 M1-3-1 / M2-0-1 / M2-2-1 配置到 libraryfloor1，并放置对应剧情 NPC。
/// </summary>
public static class LibraryFloor1StorySetup
{
    const string MenuPath = "Tools/场景配置/libraryfloor1 - 配置剧情点位（M1-3-1 / M2-0-1 / M2-2-1）";
    const string DatabasePath = "Assets/code/GameQuestSystem/Content1StoryDatabase.asset";
    const string ScenePath = "Assets/scene/libraryfloor1.scene";

    static readonly StorySpot[] Spots =
    {
        LibraryFloor1StoryLocations.M1_3_1,
        LibraryFloor1StoryLocations.M2_0_1,
        LibraryFloor1StoryLocations.M2_2_1
    };

    [MenuItem(MenuPath, false, 3)]
    public static void ConfigureFromMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("libraryfloor1 剧情", "请先退出 Play Mode。", "确定");
            return;
        }

        var database = AssetDatabase.LoadAssetAtPath<StoryDialogueDatabase>(DatabasePath);
        if (database == null)
        {
            EditorUtility.DisplayDialog("libraryfloor1 剧情", "未找到 Content1StoryDatabase.asset。", "确定");
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
            "libraryfloor1 剧情",
            $"已更新剧情文档 {docs} 条。\n已更新任务追踪 {quests} 条。\n已放置/更新 NPC {npcs} 个。\n\n场景：{LibraryFloor1StoryLocations.SceneName}",
            "确定");
    }

    public static int PatchDatabase(StoryDialogueDatabase database)
    {
        int count = 0;
        foreach (var spot in Spots)
        {
            if (!TryGetDocumentId(spot, out string documentId))
                continue;

            var loc = LibraryFloor1StoryLocations.ToDefinition(spot);
            UpsertLocation(database, loc);

            var doc = database.documents.FirstOrDefault(d =>
                string.Equals(d.documentId, documentId, StringComparison.OrdinalIgnoreCase));
            if (doc == null)
                continue;

            doc.locationId = loc.locationId;
            doc.targetSceneName = loc.sceneName;
            doc.fallbackTargetPosition = loc.position;
            doc.finishLocationName = loc.displayName;
            doc.needsLocationConfirmation = false;
            if (string.IsNullOrWhiteSpace(doc.npcId))
                doc.npcId = spot.NpcId;

            var npcEntry = database.npcs.FirstOrDefault(n => n != null && n.npcId == spot.NpcId);
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

    static bool TryGetDocumentId(StorySpot spot, out string documentId)
    {
        documentId = null;
        if (spot.LocationId == LibraryFloor1StoryLocations.M1_3_1.LocationId)
            documentId = "M1-3-1";
        else if (spot.LocationId == LibraryFloor1StoryLocations.M2_0_1.LocationId)
            documentId = "M2-0-1";
        else if (spot.LocationId == LibraryFloor1StoryLocations.M2_2_1.LocationId)
            documentId = "M2-2-1";
        return !string.IsNullOrEmpty(documentId);
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
                    if (!LibraryFloor1StoryLocations.TryGetSpot(taskName, out var spot))
                        continue;

                    var loc = LibraryFloor1StoryLocations.ToDefinition(spot);
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
        int count = 0;
        foreach (var spot in Spots)
        {
            if (!TryGetDocumentId(spot, out string documentId))
                continue;

            var existing = UnityEngine.Object.FindObjectsOfType<StoryQuestNpc>(true)
                .FirstOrDefault(n => n != null && n.gameObject.scene == scene
                    && string.Equals(n.documentId, documentId, StringComparison.OrdinalIgnoreCase));

            GameObject go;
            if (existing != null)
            {
                go = existing.gameObject;
            }
            else
            {
                var npcEntry = database.FindNpc(spot.NpcId);
                if (npcEntry == null || string.IsNullOrWhiteSpace(npcEntry.prefabPath))
                {
                    Debug.LogWarning($"[libraryfloor1] 未找到 NPC 预制体：{spot.NpcId}");
                    continue;
                }

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(npcEntry.prefabPath);
                if (prefab == null)
                {
                    Debug.LogWarning($"[libraryfloor1] 预制体路径无效：{npcEntry.prefabPath}");
                    continue;
                }

                go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                go.name = "StoryNPC_" + spot.NpcDisplayName + "_" + documentId;
                Undo.RegisterCreatedObjectUndo(go, "Place libraryfloor1 story NPC");
            }

            go.transform.position = spot.Position;
            var storyNpc = go.GetComponent<StoryQuestNpc>() ?? go.AddComponent<StoryQuestNpc>();
            storyNpc.documentId = documentId;
            storyNpc.displayName = spot.NpcDisplayName;
            storyNpc.hideAfterStoryCompleted = true;

            EditorUtility.SetDirty(go);
            count++;
        }

        return count;
    }
}
#endif
