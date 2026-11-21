#if UNITY_EDITOR
using System;
using System.Linq;
using GameQuestSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>将剧情数据库与 QuestSystemManager 中的场景坐标同步为最新配置（无需重新解析 docx）。</summary>
public static class StoryLocationSyncMenu
{
    const string DatabasePath = "Assets/code/GameQuestSystem/Content1StoryDatabase.asset";
    const string SupplementDocId = "M3-2-1R";
    static readonly Vector3 SeminarLearningRoomPosition = new Vector3(24.9084282f, 0.656000018f, -37.0740013f);

    [MenuItem("Tools/Quest System/同步剧情场景坐标", false, 61)]
    public static void SyncStoryLocations()
    {
        var database = AssetDatabase.LoadAssetAtPath<StoryDialogueDatabase>(DatabasePath);
        if (database == null)
        {
            EditorUtility.DisplayDialog("同步剧情场景坐标", "未找到 Content1StoryDatabase.asset。", "确定");
            return;
        }

        EnsureSupplementDocument(database);
        StoryNpcPrefabSyncMenu.SyncPrefabReferences();
        int docCount = PatchDocuments(database);
        EditorUtility.SetDirty(database);

        int questCount = PatchQuestSystemManagers();
        int placedCount = PlaceMissingStoryNpcsInLoadedScenes(database);
        int npcCount = RepositionStoryNpcsInLoadedScenes();
        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog(
            "同步剧情场景坐标",
            $"已更新 {docCount} 条剧情文档、{questCount} 个 QuestSystemManager。\n" +
            $"新放置剧情 NPC：{placedCount} 个；已校正坐标：{npcCount} 个。\n" +
            "M4/M5 坐标已按最新配置写入。M4-1 请在 learning room 场景确认 NPC 是否在正确场景。",
            "确定");
    }

    static int PatchDocuments(StoryDialogueDatabase database)
    {
        int n = 0;
        foreach (var doc in database.documents)
        {
            if (doc == null || string.IsNullOrWhiteSpace(doc.documentId))
                continue;

            ApplyLocation(doc, Content1StoryImporterMenu.ResolveStoryTaskLocation(doc));
            n++;
        }
        return n;
    }

    static void ApplyLocation(StoryDialogueDocument doc, StoryLocationDefinition loc)
    {
        if (doc == null || loc == null)
            return;

        doc.locationId = loc.locationId;
        doc.targetSceneName = loc.sceneName;
        doc.fallbackTargetPosition = loc.position;
        doc.finishLocationName = loc.displayName;
        doc.needsLocationConfirmation = loc.needsConfirmation;
    }

    static void EnsureSupplementDocument(StoryDialogueDatabase database)
    {
        if (database.documents.Any(d => string.Equals(d.documentId, SupplementDocId, StringComparison.OrdinalIgnoreCase)))
            return;

        var doc = new StoryDialogueDocument
        {
            documentId = SupplementDocId,
            title = SupplementDocId,
            sourceAssetPath = "Assets/content1/M3/M3-2-1R（内置）.txt",
            chapterId = "M3",
            category = QuestCategory.Side,
            isRootTask = true,
            isBranchDocument = false,
            needsLocationConfirmation = false,
            completeEventId = "content1_story_complete_m3_2_1r",
            npcId = "npc_林见鹿",
            locationId = "seminar_room_placeholder",
            targetSceneName = "learning room",
            fallbackTargetPosition = SeminarLearningRoomPosition,
            rewardGold = 65,
            finishLocationName = "TXC行政楼/研讨室",
            pages = new System.Collections.Generic.List<StoryDialoguePage>
            {
                Page(1, "", "社团教室的门合上之后，走廊里只剩下消防指示牌幽绿的微光。林见鹿给林渡发来一条极短的消息：「研讨室。现在。」"),
                Page(2, "林见鹿", "你不觉得我们刚才都在演同一场戏吗？赵承勋需要舞台，社团需要体面，而你需要一个答案。"),
                Page(3, "林渡", "你叫我来，是想让我帮你保密，还是想让我相信制度还能自我修复？"),
                Page(4, "林见鹿", "审计如果只剩下「揭发」，社团就只剩恐惧；如果只剩下「遮盖」，就只剩骗子。我想知道——你会把今晚的数字带去哪里。"),
                Page(5, "", "研讨室的白板还留着下午讨论赛题时的残影。窗外没有观众，只有空调的低鸣，像某种耐心的质问。"),
                Page(6, "林渡", "我会把复印件留给你。但下一次，要走程序。公开不是羞辱，是让愿意留下的人还坐得住。"),
                Page(7, "林见鹿", "……好。至少今晚，我们还愿意坐在同一张桌子前。"),
            }
        };
        database.documents.Add(doc);
    }

    static StoryDialoguePage Page(int num, string speaker, string text)
    {
        return new StoryDialoguePage { pageNumber = num, speakerName = speaker, text = text };
    }

    static int PatchQuestSystemManagers()
    {
        int managers = 0;
        foreach (var manager in UnityEngine.Object.FindObjectsOfType<QuestSystemManager>(true))
        {
            var so = new SerializedObject(manager);
            var chains = so.FindProperty("questChains");
            bool dirty = false;

            for (int c = 0; c < chains.arraySize; c++)
            {
                var chain = chains.GetArrayElementAtIndex(c);
                string questId = chain.FindPropertyRelative("questId").stringValue;
                if (!questId.StartsWith("content1_", StringComparison.OrdinalIgnoreCase))
                    continue;

                var subtasks = chain.FindPropertyRelative("subTasks");
                for (int i = 0; i < subtasks.arraySize; i++)
                {
                    var st = subtasks.GetArrayElementAtIndex(i);
                    string taskName = st.FindPropertyRelative("subTaskName").stringValue;
                    if (string.IsNullOrWhiteSpace(taskName) || !IsStoryTaskId(taskName))
                        continue;

                    var loc = Content1StoryImporterMenu.ResolveStoryTaskLocation(taskName);
                    if (loc == null)
                        continue;

                    st.FindPropertyRelative("targetSceneName").stringValue = loc.sceneName;
                    st.FindPropertyRelative("fallbackTargetPosition").vector3Value = loc.position;
                    st.FindPropertyRelative("finishLocationName").stringValue = loc.displayName;
                    dirty = true;
                }
            }

            if (dirty)
            {
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(manager);
                managers++;
            }
        }

        if (managers > 0)
        {
            for (int i = 0; i < SceneManager.loadedSceneCount; i++)
                EditorSceneManager.MarkSceneDirty(SceneManager.GetSceneAt(i));
        }

        return managers;
    }

    static int PlaceMissingStoryNpcsInLoadedScenes(StoryDialogueDatabase database)
    {
        if (database?.documents == null)
            return 0;

        int placed = 0;
        var existing = UnityEngine.Object.FindObjectsOfType<StoryQuestNpc>(true);

        for (int i = 0; i < database.documents.Count; i++)
        {
            var doc = database.documents[i];
            if (doc == null || !StoryDocumentLocationResolver.ShouldPlaceInScene(doc))
                continue;

            string targetScene = StoryDocumentLocationResolver.ResolveTargetSceneName(database, doc);
            if (string.IsNullOrWhiteSpace(targetScene))
                continue;

            bool alreadyPlaced = false;
            for (int n = 0; n < existing.Length; n++)
            {
                var npc = existing[n];
                if (npc == null || !npc.gameObject.scene.IsValid())
                    continue;
                if (!string.Equals(npc.documentId, doc.documentId, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!string.Equals(
                        NormalizeSceneName(npc.gameObject.scene.name),
                        NormalizeSceneName(targetScene),
                        StringComparison.OrdinalIgnoreCase))
                    continue;

                alreadyPlaced = true;
                if (StoryNpcSpawnHelper.IsRuntimePlaceholder(npc.gameObject))
                {
                    Undo.DestroyObjectImmediate(npc.gameObject);
                    alreadyPlaced = false;
                }
                break;
            }

            if (alreadyPlaced)
                continue;

            bool sceneLoaded = false;
            for (int s = 0; s < SceneManager.loadedSceneCount; s++)
            {
                var scene = SceneManager.GetSceneAt(s);
                if (!scene.IsValid() || !scene.isLoaded)
                    continue;
                if (!string.Equals(
                        NormalizeSceneName(scene.name),
                        NormalizeSceneName(targetScene),
                        StringComparison.OrdinalIgnoreCase))
                    continue;

                sceneLoaded = true;
                break;
            }

            if (!sceneLoaded)
                continue;

            var npcDef = database.FindNpc(doc.npcId);
            if (npcDef == null)
                continue;

            var prefab = StoryNpcPrefabResolver.Resolve(database, npcDef);
            if (prefab == null)
            {
                Debug.LogWarning("[StoryLocationSync] 缺少剧情 NPC 预制体: " + doc.documentId + " → " + npcDef.prefabPath);
                continue;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (go == null)
                continue;

            go.name = "StoryNPC_" + npcDef.displayName + "_" + doc.documentId;
            go.transform.position = doc.fallbackTargetPosition != Vector3.zero
                ? doc.fallbackTargetPosition + GetStoryNpcOffset(doc.documentId)
                : StoryDocumentLocationResolver.ResolveFallbackPosition(database, doc) + GetStoryNpcOffset(doc.documentId);

            var storyNpc = go.GetComponent<StoryQuestNpc>();
            if (storyNpc == null)
                storyNpc = go.AddComponent<StoryQuestNpc>();
            storyNpc.documentId = doc.documentId;
            storyNpc.displayName = npcDef.displayName;
            storyNpc.interactDistance = 6f;
            storyNpc.hideAfterStoryCompleted = !doc.isBranchDocument;
            storyNpc.hidePrefabVisualInPlayMode =
                StoryQuestNpcVisualUtility.IsStoryTriggerDisplayName(npcDef.displayName);

            Undo.RegisterCreatedObjectUndo(go, "Place Story NPC");
            EditorUtility.SetDirty(storyNpc);
            placed++;
        }

        if (placed > 0)
        {
            for (int i = 0; i < SceneManager.loadedSceneCount; i++)
                EditorSceneManager.MarkSceneDirty(SceneManager.GetSceneAt(i));
        }

        return placed;
    }

    static int RepositionStoryNpcsInLoadedScenes()
    {
        int count = 0;
        foreach (var npc in UnityEngine.Object.FindObjectsOfType<StoryQuestNpc>(true))
        {
            if (npc == null || string.IsNullOrWhiteSpace(npc.documentId))
                continue;

            if (!npc.gameObject.scene.IsValid())
                continue;

            var loc = Content1StoryImporterMenu.ResolveStoryTaskLocation(npc.documentId);
            if (loc == null)
                continue;

            if (!string.Equals(
                    NormalizeSceneName(npc.gameObject.scene.name),
                    NormalizeSceneName(loc.sceneName),
                    StringComparison.OrdinalIgnoreCase))
                continue;

            npc.transform.position = loc.position + GetStoryNpcOffset(npc.documentId);
            EditorUtility.SetDirty(npc);
            count++;
        }

        return count;
    }

    static bool IsStoryTaskId(string id)
    {
        return id.StartsWith("M", StringComparison.OrdinalIgnoreCase)
               && id.Length >= 5
               && char.IsDigit(id[1]);
    }

    static Vector3 GetStoryNpcOffset(string documentId)
    {
        if (ComputerRoomStoryLocations.UsesConfirmedPosition(documentId))
            return Vector3.zero;
        if (UndergardenStoryLocations.UsesConfirmedPosition(documentId))
            return Vector3.zero;
        if (documentId == "M0-2-1")
            return new Vector3(-2f, 0f, 0f);
        if (documentId == "M0-1-3" || documentId == "M0-3-1")
            return new Vector3(2f, 0f, 0f);
        return new Vector3(1.5f, 0f, 1.5f);
    }

    static string NormalizeSceneName(string sceneRef)
    {
        if (string.IsNullOrWhiteSpace(sceneRef))
            return "";
        string s = sceneRef.Trim().Replace('\\', '/');
        int slash = s.LastIndexOf('/');
        if (slash >= 0 && slash < s.Length - 1)
            s = s.Substring(slash + 1);
        if (s.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
            s = s.Substring(0, s.Length - 6);
        return s.Trim();
    }
}
#endif
