#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using GameQuestSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 将 content1 根任务与分支文档中的剧情 NPC 放置到对应场景 Hierarchy（StoryNPCs 根节点下）。
/// 已放置的 documentId 不会移动或覆盖（保留手动调位）。
/// </summary>
public static class StoryNpcBulkPlacementMenu
{
    const string DatabasePath = "Assets/code/GameQuestSystem/Content1StoryDatabase.asset";
    const string StoryNpcRootName = "StoryNPCs";

    [MenuItem("Tools/Quest System/放置全部剧情 NPC 到场景（Hierarchy 可见）", false, 62)]
    public static void PlaceAllStoryNpcsInScenes()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("放置剧情 NPC", "请先退出 Play Mode。", "确定");
            return;
        }

        var database = AssetDatabase.LoadAssetAtPath<StoryDialogueDatabase>(DatabasePath);
        if (database == null)
        {
            EditorUtility.DisplayDialog("放置剧情 NPC", "未找到 Content1StoryDatabase.asset。", "确定");
            return;
        }

        StoryNpcPrefabSyncMenu.SyncPrefabReferences();
        var parentMap = StoryDocumentLocationResolver.BuildChoiceParentMap(database);
        PatchDocumentLocations(database, parentMap);
        EditorUtility.SetDirty(database);

        var scenePathCache = BuildScenePathCache();
        var tasksByScene = GroupPlacementDocumentsByScene(database, parentMap);
        var placedDocumentIds = CollectPlacedDocumentIdsFromSceneAssets();
        var branchSpreadCounter = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        string originalScene = SceneManager.GetActiveScene().path;
        int scenesTouched = 0;
        int placed = 0;
        int placedBranches = 0;
        int skipped = 0;
        var missingPrefabs = new List<string>();

        try
        {
            foreach (var pair in tasksByScene.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
            {
                string sceneName = pair.Key;
                if (!scenePathCache.TryGetValue(NormalizeSceneName(sceneName), out string scenePath))
                {
                    Debug.LogWarning("[StoryNpcBulkPlacement] 未找到场景资产: " + sceneName);
                    continue;
                }

                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                if (!scene.IsValid())
                    continue;

                var root = EnsureStoryNpcRoot(scene);
                bool sceneDirty = false;

                foreach (var doc in pair.Value)
                {
                    switch (PlaceOrUpdateNpc(scene, root, database, doc, parentMap, branchSpreadCounter,
                                placedDocumentIds, missingPrefabs))
                    {
                        case PlaceResult.Placed:
                            placed++;
                            if (doc.isBranchDocument)
                                placedBranches++;
                            sceneDirty = true;
                            break;
                        case PlaceResult.SkippedExisting:
                            skipped++;
                            break;
                    }
                }

                if (sceneDirty)
                {
                    scenesTouched++;
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
            }
        }
        finally
        {
            if (!string.IsNullOrEmpty(originalScene) && File.Exists(originalScene))
                EditorSceneManager.OpenScene(originalScene, OpenSceneMode.Single);
        }

        AssetDatabase.SaveAssets();

        string missingText = missingPrefabs.Count == 0
            ? ""
            : "\n\n缺少预制体：\n" + string.Join("\n", missingPrefabs.Take(8)) +
              (missingPrefabs.Count > 8 ? $"\n…等 {missingPrefabs.Count} 条" : "");

        EditorUtility.DisplayDialog(
            "放置剧情 NPC",
            $"新放置 {placed} 个（其中分支 {placedBranches} 个），跳过已有 {skipped} 个（保留你调整过的坐标）。\n" +
            $"改动了 {scenesTouched} 个场景。\n" +
            "根任务与分支文档 NPC 均在 Hierarchy → StoryNPCs 下。" + missingText,
            "确定");
    }

    enum PlaceResult
    {
        Skipped,
        SkippedExisting,
        Placed
    }

    static Dictionary<string, List<StoryDialogueDocument>> GroupPlacementDocumentsByScene(
        StoryDialogueDatabase database,
        Dictionary<string, string> parentMap)
    {
        var map = new Dictionary<string, List<StoryDialogueDocument>>(StringComparer.OrdinalIgnoreCase);
        if (database?.documents == null)
            return map;

        for (int i = 0; i < database.documents.Count; i++)
        {
            var doc = database.documents[i];
            if (!StoryDocumentLocationResolver.ShouldPlaceInScene(doc))
                continue;

            string sceneName = ResolveSceneNameForPlacement(database, doc, parentMap);
            if (string.IsNullOrWhiteSpace(sceneName))
                continue;

            string key = NormalizeSceneName(sceneName);
            if (!map.TryGetValue(key, out var list))
            {
                list = new List<StoryDialogueDocument>();
                map[key] = list;
            }

            list.Add(doc);
        }

        foreach (var list in map.Values)
        {
            list.Sort((a, b) => string.Compare(a.documentId, b.documentId, StringComparison.OrdinalIgnoreCase));
        }

        return map;
    }

    static string ResolveSceneNameForPlacement(
        StoryDialogueDatabase database,
        StoryDialogueDocument doc,
        Dictionary<string, string> parentMap)
    {
        if (!string.IsNullOrWhiteSpace(doc.targetSceneName))
            return doc.targetSceneName;

        var loc = Content1StoryImporterMenu.ResolveStoryTaskLocation(doc);
        if (!string.IsNullOrWhiteSpace(loc?.sceneName))
            return loc.sceneName;

        string rootId = StoryDocumentLocationResolver.FindRootDocumentId(doc.documentId, database, parentMap);
        var root = database.FindDocument(rootId);
        if (root == null)
            return "";

        if (!string.IsNullOrWhiteSpace(root.targetSceneName))
            return root.targetSceneName;

        var rootLoc = Content1StoryImporterMenu.ResolveStoryTaskLocation(root);
        return rootLoc?.sceneName ?? "";
    }

    static void PatchDocumentLocations(StoryDialogueDatabase database, Dictionary<string, string> parentMap)
    {
        if (database?.documents == null)
            return;

        for (int i = 0; i < database.documents.Count; i++)
        {
            var doc = database.documents[i];
            if (!StoryDocumentLocationResolver.ShouldPlaceInScene(doc))
                continue;

            var loc = Content1StoryImporterMenu.ResolveStoryTaskLocation(doc);
            bool needsInherit = loc == null
                                || string.IsNullOrWhiteSpace(loc.sceneName)
                                || loc.position == Vector3.zero;

            if (needsInherit)
            {
                string rootId = StoryDocumentLocationResolver.FindRootDocumentId(doc.documentId, database, parentMap);
                var root = database.FindDocument(rootId);
                var rootLoc = root != null ? Content1StoryImporterMenu.ResolveStoryTaskLocation(root) : null;
                if (rootLoc != null)
                    loc = rootLoc;
            }

            if (loc == null)
                continue;

            doc.locationId = string.IsNullOrWhiteSpace(loc.locationId)
                ? "branch_" + SanitizeLocationId(doc.documentId)
                : loc.locationId;
            doc.targetSceneName = loc.sceneName;
            if (doc.fallbackTargetPosition == Vector3.zero)
                doc.fallbackTargetPosition = loc.position;
            if (string.IsNullOrWhiteSpace(doc.finishLocationName))
                doc.finishLocationName = loc.displayName;
            doc.needsLocationConfirmation = loc.needsConfirmation;
        }
    }

    static string SanitizeLocationId(string documentId)
    {
        if (string.IsNullOrWhiteSpace(documentId))
            return "unknown";
        return Regex.Replace(documentId.Trim().ToLowerInvariant(), @"[^\p{L}\p{Nd}]+", "_").Trim('_');
    }

    static PlaceResult PlaceOrUpdateNpc(
        Scene scene,
        GameObject root,
        StoryDialogueDatabase database,
        StoryDialogueDocument doc,
        Dictionary<string, string> parentMap,
        Dictionary<string, int> branchSpreadCounter,
        HashSet<string> placedDocumentIds,
        List<string> missingPrefabs)
    {
        if (doc == null || string.IsNullOrWhiteSpace(doc.documentId))
            return PlaceResult.Skipped;

        if (placedDocumentIds.Contains(doc.documentId))
            return PlaceResult.SkippedExisting;

        var npcDef = database.FindNpc(doc.npcId);
        if (npcDef == null)
        {
            missingPrefabs.Add(doc.documentId + " → " + doc.npcId);
            return PlaceResult.Skipped;
        }

        Vector3 position = ResolvePlacementPosition(database, doc, parentMap, branchSpreadCounter);
        string displayName = string.IsNullOrWhiteSpace(npcDef.displayName) ? doc.npcId : npcDef.displayName;
        string goName = "StoryNPC_" + displayName + "_" + doc.documentId;

        var existing = UnityEngine.Object.FindObjectsOfType<StoryQuestNpc>(true)
            .FirstOrDefault(n => n != null
                                 && n.gameObject.scene == scene
                                 && string.Equals(n.documentId, doc.documentId, StringComparison.OrdinalIgnoreCase));

        if (existing != null)
        {
            if (StoryNpcSpawnHelper.IsRuntimePlaceholder(existing.gameObject))
            {
                position = existing.transform.position;
                Undo.DestroyObjectImmediate(existing.gameObject);
            }
            else
            {
                return PlaceResult.SkippedExisting;
            }
        }

        var prefab = StoryNpcPrefabResolver.Resolve(database, npcDef);
        if (prefab == null)
        {
            missingPrefabs.Add(doc.documentId + " → " + (npcDef.prefabPath ?? doc.npcId));
            return PlaceResult.Skipped;
        }

        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        if (go == null)
            return PlaceResult.Skipped;

        go.name = goName;
        if (root != null)
            go.transform.SetParent(root.transform, true);
        go.transform.position = position;

        var storyNpc = go.GetComponent<StoryQuestNpc>() ?? go.AddComponent<StoryQuestNpc>();
        storyNpc.documentId = doc.documentId;
        storyNpc.displayName = displayName;
        storyNpc.interactDistance = 6f;
        storyNpc.hideAfterStoryCompleted = !doc.isBranchDocument;
        storyNpc.hidePrefabVisualInPlayMode =
            StoryQuestNpcVisualUtility.IsStoryTriggerDisplayName(displayName);

        Undo.RegisterCreatedObjectUndo(go, "Place Story NPC");
        EditorUtility.SetDirty(storyNpc);
        placedDocumentIds.Add(doc.documentId);
        return PlaceResult.Placed;
    }

    static Vector3 ResolvePlacementPosition(
        StoryDialogueDatabase database,
        StoryDialogueDocument doc,
        Dictionary<string, string> parentMap,
        Dictionary<string, int> branchSpreadCounter)
    {
        Vector3 basePos = doc.fallbackTargetPosition;
        if (basePos == Vector3.zero)
            basePos = StoryDocumentLocationResolver.ResolveFallbackPosition(database, doc);

        if (doc.isRootTask)
            return basePos + GetStoryNpcOffset(doc.documentId);

        string rootId = StoryDocumentLocationResolver.FindRootDocumentId(doc.documentId, database, parentMap);
        string anchorKey = NormalizeSceneName(doc.targetSceneName) + "|" + rootId;
        if (!branchSpreadCounter.TryGetValue(anchorKey, out int spreadIndex))
            spreadIndex = 0;
        branchSpreadCounter[anchorKey] = spreadIndex + 1;

        return basePos + GetStoryNpcOffset(rootId) + StoryDocumentLocationResolver.GetBranchSpreadOffset(spreadIndex);
    }

    static HashSet<string> CollectPlacedDocumentIdsFromSceneAssets()
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pattern = new Regex(@"documentId\s*\n\s+value:\s*(.+)", RegexOptions.Multiline);
        foreach (string guid in AssetDatabase.FindAssets("t:Scene"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                continue;

            string text = File.ReadAllText(path);
            foreach (Match match in pattern.Matches(text))
            {
                string id = match.Groups[1].Value.Trim().Trim('"');
                if (id.Length >= 4 && id[0] == 'M' && char.IsDigit(id[1]))
                    ids.Add(id);
            }
        }

        return ids;
    }

    static GameObject EnsureStoryNpcRoot(Scene scene)
    {
        foreach (var rootGo in scene.GetRootGameObjects())
        {
            if (rootGo != null && rootGo.name == StoryNpcRootName)
                return rootGo;
        }

        var go = new GameObject(StoryNpcRootName);
        SceneManager.MoveGameObjectToScene(go, scene);
        Undo.RegisterCreatedObjectUndo(go, "Create StoryNPCs root");
        return go;
    }

    static Dictionary<string, string> BuildScenePathCache()
    {
        var cache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string guid in AssetDatabase.FindAssets("t:Scene"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path))
                continue;
            string name = Path.GetFileNameWithoutExtension(path);
            if (string.IsNullOrEmpty(name))
                continue;
            cache[NormalizeSceneName(name)] = path;
        }

        return cache;
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
        if (s.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)
            || s.EndsWith(".scene", StringComparison.OrdinalIgnoreCase))
            s = Path.GetFileNameWithoutExtension(s);
        return s.Trim();
    }
}
#endif
