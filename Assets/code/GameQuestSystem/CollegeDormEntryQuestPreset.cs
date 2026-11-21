using System.Collections.Generic;

namespace GameQuestSystem
{
    /// <summary>
    /// 进入任意书院场景后，先提示玩家前往寝室；真正的布置教学在 dorm 场景内接取。
    /// </summary>
    public static class CollegeDormEntryQuestPreset
    {
        public const string QuestId = "college_enter_dorm";

        public static QuestChainDefinition CreateChain()
        {
            return new QuestChainDefinition
            {
                questId = QuestId,
                questName = Loc.Choose("前往寝室", "Go to Your Dorm"),
                category = QuestCategory.Side,
                shortDescription = Loc.Choose(
                    "书院的大门已开，寝室还在前方等你。",
                    "The college gate is open; your dorm still waits ahead."),
                longDescription = Loc.Choose(
                    "进入令学院任一书院场景后自动接取。找到「进入寝室」的传送入口，踏入 dorm，这段引路便告完成。",
                    "Auto-accepted in any Ling College scene. Find the dorm teleport entrance and enter the dorm scene to finish."),
                finalRewardGold = 0,
                acceptTriggers = new List<QuestAcceptTrigger>
                {
                    EnterScene("lingcollegeA"),
                    EnterScene("lingcollegeB"),
                    EnterScene("lingcollegeC")
                },
                subTasks = new List<QuestSubTask>
                {
                    new QuestSubTask
                    {
                        subTaskId = "enter_dorm",
                        subTaskName = Loc.Choose("进入寝室", "Enter Dorm"),
                        shortDescription = Loc.Choose(
                            "穿过书院走廊，找到通往寝室的入口。",
                            "Cross the college halls and find the dorm entrance."),
                        longDescription = Loc.Choose(
                            "寝室入口通常以传送点形式出现。踏入 dorm 场景的瞬间，本步自动完成。",
                            "The dorm entrance is usually a teleport. The moment you enter the dorm scene, this step completes."),
                        triggerType = QuestTriggerType.ManualEvent,
                        triggerEventId = "enter_scene:dorm",
                        targetCount = 1,
                        rewardGold = 0,
                        finishLocationName = Loc.Choose("寝室", "Dorm")
                    }
                }
            };
        }

        static QuestAcceptTrigger EnterScene(string sceneName)
        {
            return new QuestAcceptTrigger
            {
                triggerType = QuestAcceptTriggerType.EnterScene,
                triggerId = sceneName
            };
        }
    }
}
