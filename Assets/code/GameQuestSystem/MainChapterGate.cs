using System;

namespace GameQuestSystem
{
    /// <summary>
    /// 主线章节门控：指定支线全部完成后，对应主线章节才可推进。
    /// 可在 QuestChainDefinition.requiredSideQuestIds 配置，或通过本类注册（供后续扩展）。
    /// </summary>
    public static class MainChapterGate
    {
        static IMainChapterProgressGate _customGate;

        /// <summary>注册自定义门控（会与 requiredSideQuestIds 同时生效，二者都通过才解锁）。</summary>
        public static void RegisterCustomGate(IMainChapterProgressGate gate)
        {
            _customGate = gate;
        }

        public static void ClearCustomGate()
        {
            _customGate = null;
        }

        /// <summary>
        /// 注册额外门控（与任务定义上的 requiredSideQuestIds 合并判断）。
        /// key = 主线 questId，value = 须完成的支线 questId 列表。
        /// </summary>
        public static void RegisterRequiredSideQuests(string mainQuestId, params string[] sideQuestIds)
        {
            MainChapterGateRegistry.Register(mainQuestId, sideQuestIds);
        }

        public static bool ArePrerequisitesMet(
            QuestChainDefinition mainQuest,
            Func<string, bool> isSideQuestCompleted)
        {
            if (mainQuest == null || mainQuest.category != QuestCategory.Main)
                return true;

            if (mainQuest.requiredSideQuestIds != null)
            {
                for (int i = 0; i < mainQuest.requiredSideQuestIds.Length; i++)
                {
                    string sideId = mainQuest.requiredSideQuestIds[i];
                    if (string.IsNullOrWhiteSpace(sideId))
                        continue;
                    if (isSideQuestCompleted == null || !isSideQuestCompleted(sideId.Trim()))
                        return false;
                }
            }

            if (!MainChapterGateRegistry.AreRegisteredPrerequisitesMet(mainQuest.questId, isSideQuestCompleted))
                return false;

            if (_customGate != null && QuestSystemManager.Instance != null &&
                !_customGate.CanAdvanceMainQuest(mainQuest, QuestSystemManager.Instance))
                return false;

            return true;
        }

        public static bool IsSideQuestCompleted(string sideQuestId, Func<string, QuestChainProgress> findProgress)
        {
            if (string.IsNullOrWhiteSpace(sideQuestId) || findProgress == null)
                return false;
            var p = findProgress(sideQuestId.Trim());
            return p != null && p.questCompleted;
        }
    }

    /// <summary>运行时注册的主线前置支线表（接口预留，供以后章节系统调用）。</summary>
    static class MainChapterGateRegistry
    {
        static readonly System.Collections.Generic.Dictionary<string, string[]> _extra =
            new System.Collections.Generic.Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        public static void Register(string mainQuestId, string[] sideQuestIds)
        {
            if (string.IsNullOrWhiteSpace(mainQuestId))
                return;
            _extra[mainQuestId.Trim()] = sideQuestIds ?? Array.Empty<string>();
        }

        public static bool AreRegisteredPrerequisitesMet(
            string mainQuestId,
            Func<string, bool> isSideQuestCompleted)
        {
            if (string.IsNullOrWhiteSpace(mainQuestId) || isSideQuestCompleted == null)
                return true;
            if (!_extra.TryGetValue(mainQuestId.Trim(), out var ids) || ids == null || ids.Length == 0)
                return true;

            for (int i = 0; i < ids.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(ids[i]))
                    continue;
                if (!isSideQuestCompleted(ids[i].Trim()))
                    return false;
            }

            return true;
        }
    }
}
