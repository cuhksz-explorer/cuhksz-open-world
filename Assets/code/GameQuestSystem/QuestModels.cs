using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameQuestSystem
{
    public enum QuestTriggerType
    {
        ReachLocation = 0,
        ManualEvent = 1,
        PurchaseProduct = 2,
        Counter = 3
    }

    public enum QuestCategory
    {
        Main = 0,
        Side = 1
    }

    /// <summary>支线接取条件类型（满足任一即可接取该支线）。</summary>
    public enum QuestAcceptTriggerType
    {
        /// <summary>代码调用 TryAcceptQuest / ReportSideQuestAcceptTrigger。</summary>
        Manual = 0,
        /// <summary>triggerId = 场景名（与 enter_scene: 相同规则）。</summary>
        EnterScene = 1,
        /// <summary>triggerId = NPC 标识（对话结束时由交互系统上报）。</summary>
        NpcDialogue = 2,
        /// <summary>triggerId = Canvas 根物体名（打开时由 SideQuestCanvasOpenTrigger 上报）。</summary>
        CanvasOpen = 3,
        /// <summary>triggerId = 事件名（与 ReportEvent 相同，用于自定义脚本触发接取）。</summary>
        QuestEvent = 4
    }

    [Serializable]
    public class QuestAcceptTrigger
    {
        public QuestAcceptTriggerType triggerType = QuestAcceptTriggerType.EnterScene;
        [Tooltip("场景名 / NPC ID / Canvas 名 / 事件名")]
        public string triggerId = "";
    }

    [Serializable]
    public class QuestSubTask
    {
        public string subTaskId = "sub_001";
        public string subTaskName = "新手引导";
        [TextArea(2, 4)] public string shortDescription = "跟随指引到达目标地点。";
        [TextArea(3, 8)] public string longDescription = "这是详细任务描述，展示在全屏任务界面。";
        public QuestTriggerType triggerType = QuestTriggerType.ReachLocation;
        [Tooltip("ManualEvent / PurchaseProduct / Counter 的事件ID")]
        public string triggerEventId = "";
        public string targetSceneName = "";
        public Transform targetTransform;
        public Vector3 fallbackTargetPosition;
        public float autoCompleteDistance = 5f;
        [Tooltip("Counter 触发类型需要的目标数量")]
        public int targetCount = 1;
        public int rewardGold = 20;
        public string finishLocationName = "目标地点";

        public QuestSubTask()
        {
            subTaskName = Loc.Choose("新手引导", "Tutorial Guide");
            shortDescription = Loc.Choose("跟随指引到达目标地点。", "Follow the guide to reach the destination.");
            longDescription = Loc.Choose("这是详细任务描述，展示在全屏任务界面。", "Full quest description shown in the quest screen.");
            finishLocationName = Loc.Choose("目标地点", "Destination");
        }
    }

    [Serializable]
    public class QuestChainDefinition
    {
        public string questId = "main_tutorial";
        public string questName = "新手主线";
        public QuestCategory category = QuestCategory.Main;
        [TextArea(2, 4)] public string shortDescription = "完成全部分任务。";
        [TextArea(3, 8)] public string longDescription = "这是主线任务的整体详细描述。";
        public int finalRewardGold = 100;
        public List<QuestSubTask> subTasks = new List<QuestSubTask>();

        [Header("支线接取（仅 category=Side 生效）")]
        [Tooltip("满足列表中任一条件即自动接取；为空则仅能通过 TryAcceptQuest 手动接取。")]
        public List<QuestAcceptTrigger> acceptTriggers = new List<QuestAcceptTrigger>();

        [Header("主线章节（仅 category=Main 可选）")]
        [Tooltip("主线章节序号，0 表示不参与章节门控。")]
        public int mainChapterIndex;
        [Tooltip("须全部完成的支线 questId，本主线才会解锁并可推进。")]
        public string[] requiredSideQuestIds;

        public QuestChainDefinition()
        {
            questName = Loc.Choose("新手主线", "Tutorial Main");
            shortDescription = Loc.Choose("完成全部分任务。", "Complete all subtasks.");
            longDescription = Loc.Choose("这是主线任务的整体详细描述。", "Overall description for the main quest line.");
        }
    }

    [Serializable]
    public class QuestChainProgress
    {
        public string questId;
        /// <summary>支线为 false 直到接取；主线默认为 true。</summary>
        public bool accepted;
        public int currentSubTaskIndex;
        public bool questCompleted;
        public bool[] subTaskCompleted;
        public int[] subTaskCounter;

        public QuestChainProgress()
        {
        }

        public QuestChainProgress(QuestChainDefinition definition, bool accepted = true)
        {
            questId = definition != null ? definition.questId : "";
            this.accepted = accepted;
            currentSubTaskIndex = 0;
            questCompleted = false;
            int count = definition != null && definition.subTasks != null ? definition.subTasks.Count : 0;
            subTaskCompleted = new bool[Mathf.Max(0, count)];
            subTaskCounter = new int[Mathf.Max(0, count)];
        }
    }

    [Serializable]
    public class QuestSavePayload
    {
        public List<QuestChainProgress> questProgressList = new List<QuestChainProgress>();
        /// <summary>简易任务栏当前追踪的任务 questId。</summary>
        public string trackedQuestId = "";
    }
}
