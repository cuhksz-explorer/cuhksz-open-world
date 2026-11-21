using System.Collections.Generic;

using UnityEngine;

using UnityEngine.SceneManagement;



namespace GameQuestSystem

{

    public partial class QuestSystemManager

    {

        [Header("剧情任务（uppergarden 等未配置 Quest Chains 的场景会自动补全）")]

        [SerializeField] StoryDialogueDatabase storyQuestDatabase;

        [Header("剧情热更新")]
        [Tooltip("启动时从 GameServer 拉取 /api/content/manifest 与 story_database.json，覆盖内置剧情数据。")]
        [SerializeField] bool enableContentHotUpdate = false;
        [SerializeField] bool contentHotUpdateVerboseLog = true;



        public StoryDialogueDatabase BundledStoryQuestDatabase => storyQuestDatabase;

        public void AssignStoryDatabaseIfEmpty(StoryDialogueDatabase database)

        {

            if (database == null)

                return;

            if (storyQuestDatabase == null)

                storyQuestDatabase = database;

        }



        void EnsureStoryQuestInfrastructure()

        {

            EnsureCoreQuestChainsBootstrapped();

            EnsureStoryQuestChainsBootstrapped();

            EnsureStoryQuestRuntimeExists();

        }



        void EnsureCoreQuestChainsBootstrapped()

        {

            if (questChains == null)

                questChains = new List<QuestChainDefinition>();



            int existingIdx = -1;

            for (int i = 0; i < questChains.Count; i++)

            {

                var chain = questChains[i];

                if (chain != null && chain.questId == NewUserTutorialQuestPreset.QuestId)

                {

                    existingIdx = i;

                    break;

                }

            }



            if (existingIdx < 0)

            {

                questChains.Insert(0, NewUserTutorialQuestPreset.CreateChain());

            }

            else

            {

                var existing = questChains[existingIdx];

                if (existing.subTasks == null || existing.subTasks.Count != NewUserTutorialQuestPreset.SubTaskCount)

                {

                    questChains[existingIdx] = NewUserTutorialQuestPreset.CreateChain();

                    ResetNewUserTutorialProgressIfIncomplete();

                    if (verboseLog)

                        Debug.Log("[QuestSystem] 新手教学任务链已升级至 v2（14 步）。");

                }

            }



            bool hasCollegeDormEntry = false;

            for (int i = 0; i < questChains.Count; i++)

            {

                var chain = questChains[i];

                if (chain != null && chain.questId == CollegeDormEntryQuestPreset.QuestId)

                {

                    hasCollegeDormEntry = true;

                    break;

                }

            }

            if (!hasCollegeDormEntry)

                questChains.Add(CollegeDormEntryQuestPreset.CreateChain());



            EnsureProgressIntegrity();

            EnsureDefaultTrackedQuestIfEmpty();

            if (verboseLog && existingIdx < 0)

                Debug.Log("[QuestSystem] 已注入全局新手教学任务链。");

        }



        void ResetNewUserTutorialProgressIfIncomplete()

        {

            var prog = FindProgress(NewUserTutorialQuestPreset.QuestId);

            if (prog == null || prog.questCompleted)

                return;



            prog.currentSubTaskIndex = 0;

            prog.questCompleted = false;

            int n = NewUserTutorialQuestPreset.SubTaskCount;

            prog.subTaskCompleted = new bool[n];

            prog.subTaskCounter = new int[n];

            ResetNewUserTutorialTransientFlags();

        }



        void EnsureStoryQuestChainsBootstrapped()

        {

            var database = ResolveStoryQuestDatabase();

            if (database == null)

                return;



            Content1StoryDocxLoader.HydrateDatabaseIfNeeded(database);



            bool hasStoryChain = false;

            for (int i = 0; i < questChains.Count; i++)

            {

                var chain = questChains[i];

                if (chain != null &&

                    !string.IsNullOrEmpty(chain.questId) &&

                    chain.questId.StartsWith(StoryQuestChainFactory.StoryQuestPrefix, System.StringComparison.OrdinalIgnoreCase))

                {

                    hasStoryChain = true;

                    break;

                }

            }



            if (hasStoryChain)
            {
                SyncStorySubTasksFromDatabase(database);
                EnsureProgressIntegrity();
                EnsureDefaultTrackedQuestIfEmpty();
                return;
            }



            var built = StoryQuestChainFactory.BuildQuestChains(database);

            for (int i = 0; i < built.Count; i++)

            {

                if (built[i] != null)

                    AddOrReplaceQuestChain(built[i]);

            }



            EnsureProgressIntegrity();

            if (verboseLog && built.Count > 0)

                Debug.Log("[QuestSystem] 已从剧情数据库补全 " + built.Count + " 条 content1 任务链。");

        }



        void SyncStorySubTasksFromDatabase(StoryDialogueDatabase database)
        {
            if (database == null || questChains == null)
                return;

            var built = StoryQuestChainFactory.BuildQuestChains(database);
            for (int i = 0; i < built.Count; i++)
            {
                var freshChain = built[i];
                if (freshChain == null || freshChain.subTasks == null)
                    continue;

                QuestChainDefinition existingChain = null;
                for (int c = 0; c < questChains.Count; c++)
                {
                    var chain = questChains[c];
                    if (chain != null &&
                        string.Equals(chain.questId, freshChain.questId, System.StringComparison.OrdinalIgnoreCase))
                    {
                        existingChain = chain;
                        break;
                    }
                }

                if (existingChain == null || existingChain.subTasks == null)
                    continue;

                existingChain.category = freshChain.category;
                existingChain.questName = freshChain.questName;
                existingChain.shortDescription = freshChain.shortDescription;
                existingChain.longDescription = freshChain.longDescription;

                for (int s = 0; s < freshChain.subTasks.Count; s++)
                {
                    var freshSub = freshChain.subTasks[s];
                    if (freshSub == null)
                        continue;

                    for (int e = 0; e < existingChain.subTasks.Count; e++)
                    {
                        var existingSub = existingChain.subTasks[e];
                        if (existingSub == null ||
                            !string.Equals(existingSub.subTaskId, freshSub.subTaskId, System.StringComparison.Ordinal))
                            continue;

                        existingSub.shortDescription = freshSub.shortDescription;
                        existingSub.longDescription = freshSub.longDescription;
                        existingSub.triggerType = freshSub.triggerType;
                        existingSub.triggerEventId = freshSub.triggerEventId;
                        existingSub.targetSceneName = freshSub.targetSceneName;
                        existingSub.finishLocationName = freshSub.finishLocationName;
                        existingSub.fallbackTargetPosition = freshSub.fallbackTargetPosition;
                        existingSub.rewardGold = freshSub.rewardGold;
                        break;
                    }
                }
            }
        }



        StoryDialogueDocument FindStoryDocumentByCompleteEvent(string completeEventId)
        {
            if (string.IsNullOrWhiteSpace(completeEventId))
                return null;

            var database = ResolveStoryQuestDatabase();
            if (database?.documents == null)
                return null;

            Content1StoryDocxLoader.HydrateDatabaseIfNeeded(database);

            for (int i = 0; i < database.documents.Count; i++)
            {
                var doc = database.documents[i];
                if (doc != null &&
                    string.Equals(doc.completeEventId, completeEventId, System.StringComparison.OrdinalIgnoreCase))
                    return doc;
            }

            return null;
        }



        void EnsureStoryQuestRuntimeExists()

        {

            if (StoryQuestRuntime.Instance != null)

                return;



            var database = ResolveStoryQuestDatabase();

            if (database == null)

                return;



            var go = new GameObject("StoryQuestRuntime");

            var runtime = go.AddComponent<StoryQuestRuntime>();

            runtime.SetDatabase(database);



            if (verboseLog)

                Debug.Log("[QuestSystem] 已自动创建 StoryQuestRuntime（当前场景缺少剧情运行时）。");

        }



        StoryDialogueDatabase ResolveStoryQuestDatabase()

        {

            if (ContentHotUpdateService.CachedRuntimeDatabase != null)

                return ContentHotUpdateService.CachedRuntimeDatabase;

            if (storyQuestDatabase != null)

                return storyQuestDatabase;

            if (StoryQuestRuntime.Instance != null && StoryQuestRuntime.Instance.Database != null)

                return StoryQuestRuntime.Instance.Database;

            return null;

        }



        void TryApplyCachedStoryHotUpdate()

        {

            if (!enableContentHotUpdate || storyQuestDatabase == null)

                return;



            var cached = ContentHotUpdateService.TryLoadCachedDatabase(storyQuestDatabase);

            if (cached == null)

                return;



            ApplyStoryDatabaseAndRefresh(cached, "本地缓存");

        }

        System.Collections.IEnumerator CoFetchStoryHotUpdate()

        {

            if (!enableContentHotUpdate)

                yield break;



            string baseUrl = gameServerSync != null

                ? gameServerSync.BaseUrlTrimmed

                : GameServerEndpoint.DefaultBaseUrl;



            yield return ContentHotUpdateService.EnsureLatestContentCoroutine(

                baseUrl,

                storyQuestDatabase);



            if (!ContentHotUpdateService.LastFetchOk || ContentHotUpdateService.CachedRuntimeDatabase == null)

            {

                if (contentHotUpdateVerboseLog && !string.IsNullOrEmpty(ContentHotUpdateService.LastError))

                    Debug.Log("[QuestSystem] 剧情热更新未更新: " + ContentHotUpdateService.LastError);

                yield break;

            }



            ApplyStoryDatabaseAndRefresh(

                ContentHotUpdateService.CachedRuntimeDatabase,

                "远程 v" + ContentHotUpdateService.ActiveContentVersion);

        }



        void ApplyStoryDatabaseAndRefresh(StoryDialogueDatabase database, string sourceLabel)

        {

            if (database == null)

                return;



            if (StoryQuestRuntime.Instance != null)

                StoryQuestRuntime.Instance.SetDatabase(database);



            EnsureStoryQuestInfrastructure();

            RaiseQuestChanged();



            if (contentHotUpdateVerboseLog)

                Debug.Log("[QuestSystem] 已应用剧情热更新 (" + sourceLabel + ")。");

        }



        void OnResourcesHotUpdated()

        {

            if (ContentHotUpdateService.CachedRuntimeDatabase != null)

                ApplyStoryDatabaseAndRefresh(ContentHotUpdateService.CachedRuntimeDatabase, "资源补全");

            else

                EnsureStoryQuestInfrastructure();

        }

    }

}


