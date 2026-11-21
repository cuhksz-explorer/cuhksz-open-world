using System.Collections.Generic;
using UnityEngine;

namespace GameQuestSystem
{
    /// <summary>
    /// dorm 寝室场景：寝室商城 + 背包 + 放置模式 教学任务链（支线，进入 dorm 自动接取）。
    /// </summary>
    public static class LingCollegeBDormQuestPreset
    {
        public const string QuestId = "dorm_placement_tutorial";

        /// <summary>寝室引导点（世界坐标），5m 内完成第一步。与 dorm 场景默认出生点一致，可在编辑器中调整。</summary>
        public static readonly Vector3 DormTutorialWorldPosition = new Vector3(-606.65f, 5.5f, -218.45f);

        public const string TargetScene = "dorm";

        public static QuestChainDefinition CreateChain()
        {
            return new QuestChainDefinition
            {
                questId = QuestId,
                questName = Loc.Choose("寝室与布置入门", "Dorm & Placement Intro"),
                category = QuestCategory.Side,
                shortDescription = Loc.Choose(
                    "在寝室里学会购买、收纳与摆放——把房间变成自己的。",
                    "In your dorm, learn to buy, store, and place—make the room yours."),
                longDescription =
                    Loc.Choose(
                        "仅在 dorm 场景推进的支线。从引导点出发，打开商城、买下第一件物品、从背包进入放置模式，在绿色预览处右键确认——你的寝室会记住每一次摆放。",
                        "A side quest only in the dorm scene. From the guide point: open the shop, buy an item, enter placement from inventory, right-click on a green preview—your dorm remembers every placement."),
                finalRewardGold = 120,
                acceptTriggers = new List<QuestAcceptTrigger>
                {
                    new QuestAcceptTrigger
                    {
                        triggerType = QuestAcceptTriggerType.EnterScene,
                        triggerId = TargetScene
                    }
                },
                subTasks = new List<QuestSubTask>
                {
                    new QuestSubTask
                    {
                        subTaskId = "dorm_01",
                        subTaskName = Loc.Choose("到达寝室引导点", "Reach Dorm Guide Point"),
                        shortDescription = Loc.Choose(
                            "走到寝室出生点旁的光柱，安顿下来。",
                            "Walk to the beacon near your dorm spawn and settle in."),
                        longDescription = Loc.Choose(
                            "进入 dorm 后，在出生点约五米内驻足。这里是之后布置教学的起点。",
                            "After entering the dorm, stand within about five meters of spawn. This is where decoration lessons begin."),
                        triggerType = QuestTriggerType.ReachLocation,
                        triggerEventId = "",
                        targetSceneName = TargetScene,
                        targetTransform = null,
                        fallbackTargetPosition = DormTutorialWorldPosition,
                        autoCompleteDistance = 5f,
                        targetCount = 1,
                        rewardGold = 15,
                        finishLocationName = Loc.Choose("寝室引导点", "Dorm Guide Point")
                    },
                    new QuestSubTask
                    {
                        subTaskId = "dorm_02",
                        subTaskName = Loc.Choose("打开寝室商城", "Open Dorm Shop"),
                        shortDescription = Loc.Choose("按 T 打开寝室商城。", "Press T to open the dorm shop."),
                        longDescription = Loc.Choose(
                            "商城出售可放入背包、再摆进寝室的家具与小物。按 T 唤出界面。",
                            "The shop sells furniture and décor for inventory and placement. Press T to open it."),
                        triggerType = QuestTriggerType.ManualEvent,
                        triggerEventId = "dorm_mall_open",
                        targetSceneName = TargetScene,
                        autoCompleteDistance = 5f,
                        targetCount = 1,
                        rewardGold = 15,
                        finishLocationName = Loc.Choose("寝室商城", "Dorm Shop")
                    },
                    new QuestSubTask
                    {
                        subTaskId = "dorm_03",
                        subTaskName = Loc.Choose("购买一件商品", "Buy an Item"),
                        shortDescription = Loc.Choose("在商城中购买任意一件物品。", "Buy any item in the shop."),
                        longDescription = Loc.Choose(
                            "选择商品并完成支付，物品会进入背包。",
                            "Select an item and complete payment; it goes into your inventory."),
                        triggerType = QuestTriggerType.PurchaseProduct,
                        triggerEventId = "",
                        targetSceneName = TargetScene,
                        autoCompleteDistance = 5f,
                        targetCount = 1,
                        rewardGold = 25,
                        finishLocationName = Loc.Choose("商城", "Shop")
                    },
                    new QuestSubTask
                    {
                        subTaskId = "dorm_04",
                        subTaskName = Loc.Choose("确认背包中有物品", "Confirm Inventory Item"),
                        shortDescription = Loc.Choose(
                            "按 B 打开背包，列表中应出现刚购买的物品。",
                            "Press B to open inventory; the purchased item should appear in the list."),
                        longDescription = Loc.Choose(
                            "系统会检测背包中是否至少有一件物品。",
                            "The system checks that you have at least one item in inventory."),
                        triggerType = QuestTriggerType.ManualEvent,
                        triggerEventId = "inventory_has_item",
                        targetSceneName = TargetScene,
                        autoCompleteDistance = 5f,
                        targetCount = 1,
                        rewardGold = 15,
                        finishLocationName = Loc.Choose("背包", "Inventory")
                    },
                    new QuestSubTask
                    {
                        subTaskId = "dorm_05",
                        subTaskName = Loc.Choose("进入放置模式", "Enter Placement Mode"),
                        shortDescription = Loc.Choose(
                            "在背包中点击商品的放置按钮，进入摆放模式。",
                            "In inventory, click an item's place button to enter placement mode."),
                        longDescription = Loc.Choose(
                            "进入后可用鼠标移动视角，右键确认放置，Esc 取消。",
                            "Use the mouse to look around, right-click to confirm placement, Esc to cancel."),
                        triggerType = QuestTriggerType.ManualEvent,
                        triggerEventId = "placement_mode_enter",
                        targetSceneName = TargetScene,
                        autoCompleteDistance = 5f,
                        targetCount = 1,
                        rewardGold = 20,
                        finishLocationName = Loc.Choose("放置模式", "Placement Mode")
                    },
                    new QuestSubTask
                    {
                        subTaskId = "dorm_06",
                        subTaskName = Loc.Choose("完成一次放置", "Place an Item"),
                        shortDescription = Loc.Choose(
                            "将物品摆放到您的寝室区域内，按鼠标右键确认。",
                            "Place the item in your dorm area and right-click to confirm."),
                        longDescription = Loc.Choose(
                            "预览为绿色表示可放置；确认后物品会固定到寝室并自动保存。",
                            "A green preview means placement is valid; after confirming, the item is saved in your dorm."),
                        triggerType = QuestTriggerType.ManualEvent,
                        triggerEventId = "placement_success",
                        targetSceneName = TargetScene,
                        autoCompleteDistance = 5f,
                        targetCount = 1,
                        rewardGold = 30,
                        finishLocationName = Loc.Choose("布置完成", "Placement Complete")
                    }
                }
            };
        }
    }
}
