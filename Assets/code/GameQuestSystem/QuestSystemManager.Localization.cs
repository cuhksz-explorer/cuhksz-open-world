using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameQuestSystem
{
    public partial class QuestSystemManager
    {
        void OnEnable() => Loc.OnLanguageChanged += ReLocalizeKnownPresetTexts;
        void OnDisable() => Loc.OnLanguageChanged -= ReLocalizeKnownPresetTexts;

        void ReLocalizeKnownPresetTexts()
        {
            if (questChains == null || questChains.Count == 0)
                return;

            ReLocalizePresetChain(NewUserTutorialQuestPreset.QuestId, NewUserTutorialQuestPreset.CreateChain);
            ReLocalizePresetChain(CollegeDormEntryQuestPreset.QuestId, CollegeDormEntryQuestPreset.CreateChain);
            ReLocalizePresetChain(LingCollegeBDormQuestPreset.QuestId, LingCollegeBDormQuestPreset.CreateChain);
            ReLocalizePresetChain(LlmWanderingNpcQuestPreset.QuestId, LlmWanderingNpcQuestPreset.CreateChain);
            ReLocalizeContent1StoryChains();

            OnQuestChanged?.Invoke();
        }

        void ReLocalizeContent1StoryChains()
        {
            var database = storyQuestDatabase;
            if (database == null)
                database = FindObjectOfType<StoryQuestRuntime>(true)?.Database;
            if (database == null)
                return;

            var built = StoryQuestChainFactory.BuildQuestChains(database);
            for (int i = 0; i < built.Count; i++)
            {
                var fresh = built[i];
                if (fresh == null || string.IsNullOrWhiteSpace(fresh.questId))
                    continue;
                if (!fresh.questId.StartsWith(StoryQuestChainFactory.StoryQuestPrefix, StringComparison.OrdinalIgnoreCase))
                    continue;
                ReLocalizePresetChain(fresh.questId, () => fresh);
            }
        }

        static void ReLocalizePresetChain(QuestChainDefinition existing, QuestChainDefinition fresh)
        {
            if (existing == null || fresh == null)
                return;

            existing.questName = fresh.questName;
            existing.shortDescription = fresh.shortDescription;
            existing.longDescription = fresh.longDescription;

            if (existing.subTasks == null || fresh.subTasks == null)
                return;

            var byId = new Dictionary<string, QuestSubTask>(fresh.subTasks.Count, StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < fresh.subTasks.Count; i++)
            {
                var sub = fresh.subTasks[i];
                if (sub != null && !string.IsNullOrWhiteSpace(sub.subTaskId))
                    byId[sub.subTaskId] = sub;
            }

            for (int i = 0; i < existing.subTasks.Count; i++)
            {
                var sub = existing.subTasks[i];
                if (sub == null || string.IsNullOrWhiteSpace(sub.subTaskId))
                    continue;
                if (!byId.TryGetValue(sub.subTaskId, out var src))
                    continue;

                sub.subTaskName = src.subTaskName;
                sub.shortDescription = src.shortDescription;
                sub.longDescription = src.longDescription;
                sub.finishLocationName = src.finishLocationName;
            }
        }

        void ReLocalizePresetChain(string questId, Func<QuestChainDefinition> create)
        {
            if (string.IsNullOrWhiteSpace(questId) || create == null)
                return;

            QuestChainDefinition existing = null;
            for (int i = 0; i < questChains.Count; i++)
            {
                var chain = questChains[i];
                if (chain != null && string.Equals(chain.questId, questId, StringComparison.OrdinalIgnoreCase))
                {
                    existing = chain;
                    break;
                }
            }

            if (existing == null)
                return;

            ReLocalizePresetChain(existing, create());
        }
    }
}
