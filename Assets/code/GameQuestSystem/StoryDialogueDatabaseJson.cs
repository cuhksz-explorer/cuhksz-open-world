using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameQuestSystem
{
    /// <summary>GameServer 热更新用的剧情数据库 JSON 结构（与 JsonUtility 兼容）。</summary>
    [Serializable]
    public class StoryDialogueDatabaseFileJson
    {
        public string contentVersion;
        public string generatedAt;
        public string sourceFolder;
        public StoryLocationDefinitionJson[] locations;
        public StoryNpcDefinitionJson[] npcs;
        public StoryDialogueDocumentJson[] documents;
    }

    [Serializable]
    public class StoryLocationDefinitionJson
    {
        public string locationId;
        public string displayName;
        public string sceneName;
        public Vector3 position;
        public bool needsConfirmation;
    }

    [Serializable]
    public class StoryNpcDefinitionJson
    {
        public string npcId;
        public string displayName;
        public string prefabPath;
        public string defaultSceneName;
        public Vector3 defaultPosition;
        public bool needsPositionConfirmation;
    }

    [Serializable]
    public class StoryDialogueDocumentJson
    {
        public string documentId;
        public string title;
        public string sourceAssetPath;
        public string chapterId;
        public QuestCategory category;
        public bool isRootTask;
        public bool isBranchDocument;
        public bool needsLocationConfirmation;
        public string completeEventId;
        public string npcId;
        public string locationId;
        public string targetSceneName;
        public Vector3 fallbackTargetPosition;
        public int rewardGold;
        public string finishLocationName;
        public StoryDialoguePageJson[] pages;
        public StoryDialogueChoiceJson[] choices;
    }

    [Serializable]
    public class StoryDialoguePageJson
    {
        public int pageNumber;
        public string speakerName;
        public string text;
    }

    [Serializable]
    public class StoryDialogueChoiceJson
    {
        public string label;
        public string targetDocumentId;
    }

    [Serializable]
    public class ContentManifestFileJson
    {
        public string contentVersion;
        public long updatedAtUnix;
        public ContentManifestStoryDatabaseJson storyDatabase;
        public ContentManifestBundleJson[] bundles;
    }

    [Serializable]
    public class ContentManifestBundleJson
    {
        public string name;
        public string fileName;
        /// <summary>bundle | json | catalog</summary>
        public string kind;
        public string sha256;
        public long size;
        public bool critical;
        public string url;
        public string[] assets;
    }

    [Serializable]
    public class ContentPatchRequestJson
    {
        public string[] missing;
    }

    [Serializable]
    public class ContentPatchResponseJson
    {
        public ContentPatchFileJson[] files;
    }

    [Serializable]
    public class ContentPatchFileJson
    {
        public string name;
        public string fileName;
        public string kind;
        public string sha256;
        public long size;
        public string url;
    }

    [Serializable]
    public class ContentManifestStoryDatabaseJson
    {
        public string version;
        public string sha256;
        public string url;
    }

    public static class StoryDialogueDatabaseJsonUtility
    {
        public static StoryDialogueDatabaseFileJson ToFileJson(StoryDialogueDatabase database, string contentVersion)
        {
            if (database == null)
                return null;

            return new StoryDialogueDatabaseFileJson
            {
                contentVersion = contentVersion ?? "",
                generatedAt = database.generatedAt ?? "",
                sourceFolder = database.sourceFolder ?? "",
                locations = ToLocationJsonArray(database.locations),
                npcs = ToNpcJsonArray(database.npcs),
                documents = ToDocumentJsonArray(database.documents)
            };
        }

        public static StoryDialogueDatabase FromFileJson(StoryDialogueDatabaseFileJson file, StoryDialogueDatabase prefabSource = null)
        {
            if (file == null)
                return null;

            var database = ScriptableObject.CreateInstance<StoryDialogueDatabase>();
            database.sourceFolder = file.sourceFolder ?? "";
            database.generatedAt = file.generatedAt ?? "";
            database.locations = FromLocationJsonArray(file.locations);
            database.npcs = FromNpcJsonArray(file.npcs);
            database.documents = FromDocumentJsonArray(file.documents);

            if (prefabSource != null)
                MergePrefabReferencesFrom(prefabSource, database);

            return database;
        }

        public static void MergePrefabReferencesFrom(StoryDialogueDatabase source, StoryDialogueDatabase target)
        {
            if (source == null || target == null || source.npcs == null || target.npcs == null)
                return;

            for (int i = 0; i < target.npcs.Count; i++)
            {
                var targetNpc = target.npcs[i];
                if (targetNpc == null || string.IsNullOrWhiteSpace(targetNpc.npcId))
                    continue;

                var sourceNpc = source.FindNpc(targetNpc.npcId);
                if (sourceNpc == null)
                    continue;

                if (sourceNpc.prefab != null)
                    targetNpc.prefab = sourceNpc.prefab;
                if (string.IsNullOrWhiteSpace(targetNpc.prefabPath) && !string.IsNullOrWhiteSpace(sourceNpc.prefabPath))
                    targetNpc.prefabPath = sourceNpc.prefabPath;
            }
        }

        static StoryLocationDefinitionJson[] ToLocationJsonArray(List<StoryLocationDefinition> list)
        {
            if (list == null || list.Count == 0)
                return Array.Empty<StoryLocationDefinitionJson>();

            var arr = new StoryLocationDefinitionJson[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                var src = list[i];
                if (src == null)
                    continue;
                arr[i] = new StoryLocationDefinitionJson
                {
                    locationId = src.locationId,
                    displayName = src.displayName,
                    sceneName = src.sceneName,
                    position = src.position,
                    needsConfirmation = src.needsConfirmation
                };
            }

            return arr;
        }

        static StoryNpcDefinitionJson[] ToNpcJsonArray(List<StoryNpcDefinition> list)
        {
            if (list == null || list.Count == 0)
                return Array.Empty<StoryNpcDefinitionJson>();

            var arr = new StoryNpcDefinitionJson[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                var src = list[i];
                if (src == null)
                    continue;
                arr[i] = new StoryNpcDefinitionJson
                {
                    npcId = src.npcId,
                    displayName = src.displayName,
                    prefabPath = src.prefabPath,
                    defaultSceneName = src.defaultSceneName,
                    defaultPosition = src.defaultPosition,
                    needsPositionConfirmation = src.needsPositionConfirmation
                };
            }

            return arr;
        }

        static StoryDialogueDocumentJson[] ToDocumentJsonArray(List<StoryDialogueDocument> list)
        {
            if (list == null || list.Count == 0)
                return Array.Empty<StoryDialogueDocumentJson>();

            var arr = new StoryDialogueDocumentJson[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                var src = list[i];
                if (src == null)
                    continue;
                arr[i] = new StoryDialogueDocumentJson
                {
                    documentId = src.documentId,
                    title = src.title,
                    sourceAssetPath = src.sourceAssetPath,
                    chapterId = src.chapterId,
                    category = src.category,
                    isRootTask = src.isRootTask,
                    isBranchDocument = src.isBranchDocument,
                    needsLocationConfirmation = src.needsLocationConfirmation,
                    completeEventId = src.completeEventId,
                    npcId = src.npcId,
                    locationId = src.locationId,
                    targetSceneName = src.targetSceneName,
                    fallbackTargetPosition = src.fallbackTargetPosition,
                    rewardGold = src.rewardGold,
                    finishLocationName = src.finishLocationName,
                    pages = ToPageJsonArray(src.pages),
                    choices = ToChoiceJsonArray(src.choices)
                };
            }

            return arr;
        }

        static StoryDialoguePageJson[] ToPageJsonArray(List<StoryDialoguePage> list)
        {
            if (list == null || list.Count == 0)
                return Array.Empty<StoryDialoguePageJson>();

            var arr = new StoryDialoguePageJson[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                var src = list[i];
                if (src == null)
                    continue;
                arr[i] = new StoryDialoguePageJson
                {
                    pageNumber = src.pageNumber,
                    speakerName = src.speakerName,
                    text = src.text
                };
            }

            return arr;
        }

        static StoryDialogueChoiceJson[] ToChoiceJsonArray(List<StoryDialogueChoice> list)
        {
            if (list == null || list.Count == 0)
                return Array.Empty<StoryDialogueChoiceJson>();

            var arr = new StoryDialogueChoiceJson[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                var src = list[i];
                if (src == null)
                    continue;
                arr[i] = new StoryDialogueChoiceJson
                {
                    label = src.label,
                    targetDocumentId = src.targetDocumentId
                };
            }

            return arr;
        }

        static List<StoryLocationDefinition> FromLocationJsonArray(StoryLocationDefinitionJson[] arr)
        {
            var list = new List<StoryLocationDefinition>();
            if (arr == null)
                return list;

            for (int i = 0; i < arr.Length; i++)
            {
                var src = arr[i];
                if (src == null)
                    continue;
                list.Add(new StoryLocationDefinition
                {
                    locationId = src.locationId,
                    displayName = src.displayName,
                    sceneName = src.sceneName,
                    position = src.position,
                    needsConfirmation = src.needsConfirmation
                });
            }

            return list;
        }

        static List<StoryNpcDefinition> FromNpcJsonArray(StoryNpcDefinitionJson[] arr)
        {
            var list = new List<StoryNpcDefinition>();
            if (arr == null)
                return list;

            for (int i = 0; i < arr.Length; i++)
            {
                var src = arr[i];
                if (src == null)
                    continue;
                list.Add(new StoryNpcDefinition
                {
                    npcId = src.npcId,
                    displayName = src.displayName,
                    prefabPath = src.prefabPath,
                    defaultSceneName = src.defaultSceneName,
                    defaultPosition = src.defaultPosition,
                    needsPositionConfirmation = src.needsPositionConfirmation
                });
            }

            return list;
        }

        static List<StoryDialogueDocument> FromDocumentJsonArray(StoryDialogueDocumentJson[] arr)
        {
            var list = new List<StoryDialogueDocument>();
            if (arr == null)
                return list;

            for (int i = 0; i < arr.Length; i++)
            {
                var src = arr[i];
                if (src == null)
                    continue;
                list.Add(new StoryDialogueDocument
                {
                    documentId = src.documentId,
                    title = src.title,
                    sourceAssetPath = src.sourceAssetPath,
                    chapterId = src.chapterId,
                    category = src.category,
                    isRootTask = src.isRootTask,
                    isBranchDocument = src.isBranchDocument,
                    needsLocationConfirmation = src.needsLocationConfirmation,
                    completeEventId = src.completeEventId,
                    npcId = src.npcId,
                    locationId = src.locationId,
                    targetSceneName = src.targetSceneName,
                    fallbackTargetPosition = src.fallbackTargetPosition,
                    rewardGold = src.rewardGold,
                    finishLocationName = src.finishLocationName,
                    pages = FromPageJsonArray(src.pages),
                    choices = FromChoiceJsonArray(src.choices)
                });
            }

            return list;
        }

        static List<StoryDialoguePage> FromPageJsonArray(StoryDialoguePageJson[] arr)
        {
            var list = new List<StoryDialoguePage>();
            if (arr == null)
                return list;

            for (int i = 0; i < arr.Length; i++)
            {
                var src = arr[i];
                if (src == null)
                    continue;
                list.Add(new StoryDialoguePage
                {
                    pageNumber = src.pageNumber,
                    speakerName = src.speakerName,
                    text = src.text
                });
            }

            return list;
        }

        static List<StoryDialogueChoice> FromChoiceJsonArray(StoryDialogueChoiceJson[] arr)
        {
            var list = new List<StoryDialogueChoice>();
            if (arr == null)
                return list;

            for (int i = 0; i < arr.Length; i++)
            {
                var src = arr[i];
                if (src == null)
                    continue;
                list.Add(new StoryDialogueChoice
                {
                    label = src.label,
                    targetDocumentId = src.targetDocumentId
                });
            }

            return list;
        }
    }
}
