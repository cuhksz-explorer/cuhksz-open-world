using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace GameQuestSystem
{
    /// <summary>
    /// 运行时从 Content1StoryDatabase 构建 content1_* 剧情任务链（与编辑器导入逻辑一致）。
    /// </summary>
    public static class StoryQuestChainFactory
    {
        public const string StoryQuestPrefix = "content1_";

        public static List<QuestChainDefinition> BuildQuestChains(StoryDialogueDatabase database)
        {
            var result = new List<QuestChainDefinition>();
            if (database?.documents == null || database.documents.Count == 0)
                return result;

            var rootDocsByChapter = database.documents
                .Where(d => d != null && d.isRootTask && !string.IsNullOrWhiteSpace(d.chapterId))
                .GroupBy(d => d.chapterId.Trim(), StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => NaturalSortKey(g.Key), StringComparer.OrdinalIgnoreCase);

            foreach (var chapter in rootDocsByChapter)
            {
                bool main = IsMainChapter(chapter.Key);
                var chain = new QuestChainDefinition
                {
                    questId = StoryQuestPrefix + chapter.Key.ToLowerInvariant(),
                    questName = main
                        ? Loc.F("{0} 主线剧情", "{0} Main Story", chapter.Key)
                        : Loc.F("{0} 支线/人物剧情", "{0} Side Story", chapter.Key),
                    category = main ? QuestCategory.Main : QuestCategory.Side,
                    shortDescription = StoryQuestDescriptionTable.GetChapterShort(
                        chapter.Key,
                        main
                            ? Loc.Choose("推进你在未名之海中的主线旅程。", "Advance your main journey in The Nameless Sea.")
                            : Loc.Choose("完成校园人物与选择分支，获得更多金币奖励。", "Complete campus character arcs and earn more gold.")),
                    longDescription = StoryQuestDescriptionTable.GetChapterLong(
                        chapter.Key,
                        Loc.F(
                            "来自 content1/{0} 的剧情任务链。每个节点通过对应 NPC 的分页对话完成。",
                            "Story quest chain from content1/{0}. Complete each node through paginated NPC dialogue.",
                            chapter.Key)),
                    finalRewardGold = DetermineFinalRewardGold(chapter.Key),
                    subTasks = new List<QuestSubTask>()
                };

                var docs = chapter.OrderBy(d => NaturalSortKey(d.documentId), StringComparer.OrdinalIgnoreCase).ToList();
                for (int i = 0; i < docs.Count; i++)
                    chain.subTasks.Add(BuildSubTask(docs[i], database));

                result.Add(chain);
            }

            return result;
        }

        static QuestSubTask BuildSubTask(StoryDialogueDocument doc, StoryDialogueDatabase database)
        {
            return new QuestSubTask
            {
                subTaskId = "story_" + SanitizeId(doc.documentId),
                subTaskName = Loc.StoryTerm(string.IsNullOrWhiteSpace(doc.title) ? doc.documentId : doc.title),
                shortDescription = StoryQuestDescriptionTable.GetSubTaskShort(
                    doc.documentId,
                    Loc.F(
                        "与 {0} 对话，阅读剧情《{1}》。",
                        "Talk to {0} and read story \"{1}\".",
                        ResolveShortNpcName(doc, database), Loc.StoryTerm(doc.documentId))),
                longDescription = StoryQuestDescriptionTable.GetSubTaskLong(
                    doc.documentId,
                    Loc.F(
                        "前往 {0}，与剧情 NPC 交互。对白按页推进，分支选项只会触发一次奖励。",
                        "Go to {0} and interact with the story NPC. Dialogue advances page by page; branch rewards trigger once.",
                        Loc.StoryTerm(doc.finishLocationName))),
                triggerType = QuestTriggerType.ManualEvent,
                triggerEventId = doc.completeEventId,
                targetSceneName = doc.targetSceneName ?? "",
                targetTransform = null,
                fallbackTargetPosition = doc.fallbackTargetPosition,
                autoCompleteDistance = 5f,
                targetCount = 1,
                rewardGold = doc.rewardGold,
                finishLocationName = string.IsNullOrWhiteSpace(doc.finishLocationName)
                    ? Loc.Choose("目标地点", "Destination")
                    : Loc.StoryTerm(doc.finishLocationName)
            };
        }

        static string ResolveShortNpcName(StoryDialogueDocument doc, StoryDialogueDatabase database)
        {
            if (doc == null)
                return Loc.Choose("剧情触发点", "Story Trigger");

            if (database != null && !string.IsNullOrWhiteSpace(doc.npcId))
            {
                var npc = database.FindNpc(doc.npcId);
                if (npc != null && !string.IsNullOrWhiteSpace(npc.displayName))
                    return Loc.StoryTerm(npc.displayName);
            }

            if (doc?.pages == null)
                return Loc.Choose("剧情触发点", "Story Trigger");

            for (int i = 0; i < doc.pages.Count; i++)
            {
                var page = doc.pages[i];
                if (page == null || string.IsNullOrWhiteSpace(page.speakerName))
                    continue;
                string speaker = page.speakerName.Trim();
                if (speaker != StoryProtagonistNameUtility.DefaultProtagonistName)
                    return Loc.StoryTerm(speaker);
            }

            return Loc.Choose("剧情触发点", "Story Trigger");
        }

        static bool IsMainChapter(string chapter)
        {
            if (string.IsNullOrWhiteSpace(chapter))
                return false;
            switch (chapter.Trim().ToUpperInvariant())
            {
                case "M0":
                case "M1":
                case "M2":
                case "M3":
                case "M4":
                case "M5":
                    return true;
                default:
                    return false;
            }
        }

        static int DetermineFinalRewardGold(string chapterId)
        {
            switch (chapterId?.ToUpperInvariant())
            {
                case "M1": return 160;
                case "M2": return 420;
                case "M3": return 360;
                case "M4": return 520;
                case "M5": return 260;
                default: return IsMainChapter(chapterId) ? 180 : 360;
            }
        }

        static string SanitizeId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "unknown";
            return Regex.Replace(value.Trim().ToLowerInvariant(), @"[^a-z0-9_\-]+", "_");
        }

        static string NaturalSortKey(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";
            return Regex.Replace(value, @"\d+", m => m.Value.PadLeft(8, '0'));
        }
    }
}
