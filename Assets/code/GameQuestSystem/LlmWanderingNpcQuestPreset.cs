using System.Collections.Generic;
using UnityEngine;

namespace GameQuestSystem
{
    /// <summary>
    /// M0 主线整章完成后接取：寻找游走 LLM NPC 并完成一次对话。
    /// </summary>
    public static class LlmWanderingNpcQuestPreset
    {
        public const string QuestId = "side_find_wandering_llm_npc";
        public const string M0CompleteAcceptEventId = "content1_m0_complete";
        public const string TalkCompleteEventId = "llm_wandering_npc_talked";

        public static QuestChainDefinition CreateChain()
        {
            return new QuestChainDefinition
            {
                questId = QuestId,
                questName = Loc.Choose("游走的神秘人", "Wandering Stranger"),
                category = QuestCategory.Side,
                shortDescription = Loc.Choose(
                    "在校园里找到自由游走的 NPC，与他对话一次。",
                    "Find the freely wandering NPC on campus and talk to them once."),
                longDescription =
                    Loc.Choose(
                        "序章（M0）主线全部完成后自动接取。\n" +
                        "在场景中寻找随机游走的 NPC，靠近后按 F 打开对话，发送任意问题并完成一轮回复即可。",
                        "Auto-accepted after completing the M0 main story prologue.\n" +
                        "Find the randomly wandering NPC, press F to talk, ask any question, and receive a full reply."),
                finalRewardGold = 80,
                acceptTriggers = new List<QuestAcceptTrigger>
                {
                    new QuestAcceptTrigger
                    {
                        triggerType = QuestAcceptTriggerType.QuestEvent,
                        triggerId = M0CompleteAcceptEventId
                    }
                },
                subTasks = new List<QuestSubTask>
                {
                    new QuestSubTask
                    {
                        subTaskId = "find_and_talk",
                        subTaskName = Loc.Choose("与游走的神秘人对话", "Talk to the Wandering Stranger"),
                        shortDescription = Loc.Choose(
                            "找到游走 NPC，按 F 对话并收到一次完整回复。",
                            "Find the wandering NPC, press F to talk, and receive a full reply."),
                        longDescription = Loc.Choose(
                            "靠近 NPC 后按 F 打开千问对话界面，输入问题并等待 NPC 回复结束。",
                            "Approach the NPC and press F to open the Qwen dialogue UI, ask a question, and wait for the reply."),
                        triggerType = QuestTriggerType.ManualEvent,
                        triggerEventId = TalkCompleteEventId,
                        targetCount = 1,
                        rewardGold = 80,
                        finishLocationName = Loc.Choose("游走 NPC", "Wandering NPC")
                    }
                }
            };
        }
    }
}
