using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameQuestSystem
{
    public partial class QuestSystemManager
    {
        string _trackedQuestId = "";

        public string TrackedQuestId => _trackedQuestId ?? "";

        public event Action<string> OnTrackedQuestChanged;

        /// <summary>简易任务栏显示的任务（默认主线）。</summary>
        public QuestChainDefinition GetTrackedQuest() => FindQuestDefinition(_trackedQuestId);

        public QuestChainProgress GetTrackedProgress() => FindProgress(_trackedQuestId);

        public QuestSubTask GetTrackedSubTask() => GetCurrentSubTaskForQuest(_trackedQuestId);

        /// <summary>兼容旧代码：等同 GetTrackedQuest。</summary>
        public QuestChainDefinition GetActiveQuest() => GetTrackedQuest();

        public QuestChainProgress GetActiveProgress() => GetTrackedProgress();

        public QuestSubTask GetActiveSubTask() => GetTrackedSubTask();

        public bool IsQuestAccepted(string questId)
        {
            var p = FindProgress(questId);
            return p != null && p.accepted;
        }

        public bool IsQuestCompleted(string questId)
        {
            var p = FindProgress(questId);
            return p != null && p.questCompleted;
        }

        public bool IsMainQuestUnlocked(QuestChainDefinition mainQuest)
        {
            if (mainQuest == null || mainQuest.category != QuestCategory.Main)
                return true;
            if (!ArePriorMainChaptersCompleted(mainQuest))
                return false;
            return MainChapterGate.ArePrerequisitesMet(mainQuest, IsQuestCompleted);
        }

        /// <summary>
        /// 按 Quest Chains 列表顺序，当前应进行的主线大章（第一个未完成且前置主线已全部完成）。
        /// 例：完成新手教学后才轮到 M0，完成 M0 后才轮到 M1。
        /// </summary>
        public QuestChainDefinition GetCurrentMainStoryChapter()
        {
            for (int i = 0; i < questChains.Count; i++)
            {
                var def = questChains[i];
                if (def == null || def.category != QuestCategory.Main)
                    continue;
                if (!ArePriorMainChaptersCompleted(def))
                    return null;
                if (!IsQuestCompleted(def.questId))
                    return def;
            }

            return null;
        }

        public bool IsCurrentMainStoryChapter(QuestChainDefinition def)
        {
            if (def == null || def.category != QuestCategory.Main)
                return false;
            var current = GetCurrentMainStoryChapter();
            return current != null &&
                   string.Equals(current.questId, def.questId, StringComparison.Ordinal);
        }

        /// <summary>当前大章之前的主线（按配置顺序）是否均已完结。</summary>
        bool ArePriorMainChaptersCompleted(QuestChainDefinition mainQuest)
        {
            if (mainQuest == null || mainQuest.category != QuestCategory.Main)
                return true;

            for (int i = 0; i < questChains.Count; i++)
            {
                var q = questChains[i];
                if (q == mainQuest)
                    return true;
                if (q == null || q.category != QuestCategory.Main)
                    continue;
                if (!IsQuestCompleted(q.questId))
                    return false;
            }

            return true;
        }

        public IReadOnlyList<QuestChainDefinition> GetAcceptedQuests()
        {
            var list = new List<QuestChainDefinition>();
            for (int i = 0; i < questChains.Count; i++)
            {
                var def = questChains[i];
                if (def == null) continue;
                if (!IsQuestAccepted(def.questId)) continue;
                if (def.category == QuestCategory.Main && !IsMainQuestUnlocked(def))
                    continue;
                list.Add(def);
            }
            return list;
        }

        /// <summary>任务日志（J）列表：已接取且未完成。</summary>
        public IReadOnlyList<QuestChainDefinition> GetJournalQuests()
        {
            var list = new List<QuestChainDefinition>();
            for (int i = 0; i < questChains.Count; i++)
            {
                var def = questChains[i];
                if (def == null)
                    continue;
                if (!IsQuestAccepted(def.questId))
                    continue;
                if (IsQuestCompleted(def.questId))
                    continue;
                if (def.category == QuestCategory.Main)
                {
                    if (!IsMainQuestUnlocked(def) || !IsCurrentMainStoryChapter(def))
                        continue;
                }

                list.Add(def);
            }
            return list;
        }

        public bool TryAcceptQuest(string questId, bool allowMain = false)
        {
            var def = FindQuestDefinition(questId);
            if (def == null)
                return false;

            if (def.category == QuestCategory.Side)
                return TryAcceptSideQuestInternal(def);

            if (!allowMain)
                return false;

            var p = FindProgress(def.questId);
            if (p == null)
            {
                p = new QuestChainProgress(def, accepted: true);
                _progressList.Add(p);
            }
            else
                p.accepted = true;

            EnsureDefaultTrackedQuestIfEmpty();
            RaiseQuestChanged();
            MarkPendingDirtyAndPersistLocal();
            return true;
        }

        bool TryAcceptSideQuestInternal(QuestChainDefinition def)
        {
            if (def == null || def.category != QuestCategory.Side)
                return false;

            if (IsQuestCompleted(def.questId))
                return false;

            var p = FindProgress(def.questId);
            if (p != null && p.accepted)
                return false;

            if (p == null)
            {
                p = new QuestChainProgress(def, accepted: true);
                _progressList.Add(p);
            }
            else
                p.accepted = true;

            if (verboseLog)
                Debug.Log("[QuestSystem] 已接取支线: " + def.questName);

            if (string.Equals(def.questId, CollegeDormEntryQuestPreset.QuestId, StringComparison.Ordinal))
            {
                _trackedQuestId = def.questId;
                OnTrackedQuestChanged?.Invoke(_trackedQuestId);
            }

            EnsureDefaultTrackedQuestIfEmpty();
            RaiseQuestChanged();
            MarkPendingDirtyAndPersistLocal();
            StartCoroutine(CoSaveStateToServer());
            return true;
        }

        public void SetTrackedQuest(string questId)
        {
            if (string.IsNullOrWhiteSpace(questId))
                return;

            var def = FindQuestDefinition(questId);
            if (def == null || !IsQuestAccepted(questId))
                return;

            if (def.category == QuestCategory.Main)
            {
                if (!IsMainQuestUnlocked(def) || !IsCurrentMainStoryChapter(def))
                    return;
            }

            var prog = FindProgress(questId);
            if (prog != null && prog.questCompleted)
                return;

            if (string.Equals(_trackedQuestId, questId, StringComparison.Ordinal))
                return;

            _trackedQuestId = questId;
            OnTrackedQuestChanged?.Invoke(_trackedQuestId);
            RaiseQuestChanged();
            MarkPendingDirtyAndPersistLocal();
        }

        public void EvaluateSideQuestAcceptTriggers(QuestAcceptTriggerType type, string triggerId)
        {
            if (string.IsNullOrWhiteSpace(triggerId))
                return;

            string id = triggerId.Trim();
            for (int i = 0; i < questChains.Count; i++)
            {
                var def = questChains[i];
                if (def == null || def.category != QuestCategory.Side)
                    continue;
                if (IsQuestCompleted(def.questId) || IsQuestAccepted(def.questId))
                    continue;
                if (def.acceptTriggers == null || def.acceptTriggers.Count == 0)
                    continue;

                for (int t = 0; t < def.acceptTriggers.Count; t++)
                {
                    var tr = def.acceptTriggers[t];
                    if (tr == null || tr.triggerType != type)
                        continue;
                    if (!TriggerIdMatches(tr.triggerId, id, type))
                        continue;
                    TryAcceptSideQuestInternal(def);
                    break;
                }
            }
        }

        static bool TriggerIdMatches(string configured, string fired, QuestAcceptTriggerType type)
        {
            if (string.IsNullOrWhiteSpace(configured))
                return string.IsNullOrWhiteSpace(fired);

            string a = configured.Trim();
            string b = fired.Trim();
            if (type == QuestAcceptTriggerType.EnterScene)
                return SceneNameMatches(b, a);
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }

        QuestChainDefinition FindQuestDefinition(string questId)
        {
            if (string.IsNullOrWhiteSpace(questId))
                return null;
            for (int i = 0; i < questChains.Count; i++)
            {
                if (questChains[i] != null &&
                    string.Equals(questChains[i].questId, questId, StringComparison.Ordinal))
                    return questChains[i];
            }
            return null;
        }

        void EnsureDefaultTrackedQuestIfEmpty()
        {
            var def = GetTrackedQuest();
            var prog = GetTrackedProgress();
            if (def != null && prog != null && !prog.questCompleted && IsQuestAccepted(def.questId))
            {
                if (def.category != QuestCategory.Main || IsCurrentMainStoryChapter(def))
                {
                    if (GetTrackedSubTask() != null)
                        return;
                }
            }

            string pick = PickDefaultTrackedQuestId();
            if (!string.IsNullOrEmpty(pick))
                _trackedQuestId = pick;
        }

        string PickDefaultTrackedQuestId()
        {
            var currentMain = GetCurrentMainStoryChapter();
            if (currentMain != null)
            {
                var p = FindProgress(currentMain.questId);
                if (p != null && p.accepted && !p.questCompleted)
                    return currentMain.questId;
            }

            for (int i = 0; i < questChains.Count; i++)
            {
                var q = questChains[i];
                if (q == null || q.category != QuestCategory.Side)
                    continue;
                var p = FindProgress(q.questId);
                if (p != null && p.accepted && !p.questCompleted)
                    return q.questId;
            }

            return "";
        }

        void ApplyTrackedQuestIdFromSave(string trackedId)
        {
            _trackedQuestId = trackedId ?? "";
            EnsureDefaultTrackedQuestIfEmpty();
        }

        string GetTrackedQuestIdForSave() => _trackedQuestId ?? "";

        QuestSubTask GetCurrentSubTaskForQuest(string questId)
        {
            var def = FindQuestDefinition(questId);
            var p = FindProgress(questId);
            if (def == null || p == null || p.questCompleted || !p.accepted)
                return null;
            if (def.category == QuestCategory.Main && !IsMainQuestUnlocked(def))
                return null;
            if (def.subTasks == null || def.subTasks.Count == 0)
                return null;
            int idx = Mathf.Clamp(p.currentSubTaskIndex, 0, def.subTasks.Count - 1);
            return def.subTasks[idx];
        }

        bool IsQuestRunnable(QuestChainDefinition def)
        {
            if (def == null)
                return false;
            var p = FindProgress(def.questId);
            if (p == null || !p.accepted || p.questCompleted)
                return false;
            if (def.category == QuestCategory.Main && !IsMainQuestUnlocked(def))
                return false;
            return true;
        }

        /// <summary>任务日志中查看某条已接取任务的当前子任务。</summary>
        public QuestSubTask GetSubTaskForQuest(string questId) => GetCurrentSubTaskForQuest(questId);
    }
}
