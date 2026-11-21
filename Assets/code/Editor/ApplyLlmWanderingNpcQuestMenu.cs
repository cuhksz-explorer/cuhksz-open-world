#if UNITY_EDITOR
using GameQuestSystem;
using UnityEditor;
using UnityEngine;

public static class ApplyLlmWanderingNpcQuestMenu
{
    const string MenuPath = "Tools/Quest System/添加「寻找游走 NPC」支线（M0 后触发）";

    [MenuItem(MenuPath, false, 53)]
    static void Apply()
    {
        var mgr = Object.FindObjectOfType<QuestSystemManager>(true);
        if (mgr == null)
        {
            EditorUtility.DisplayDialog(
                "Quest System",
                "当前场景中未找到 QuestSystemManager。\n请先运行 Tools/Quest System/One Click Setup。",
                "确定");
            return;
        }

        Undo.RecordObject(mgr, "Apply LLM wandering NPC side quest");
        mgr.ApplyLlmWanderingNpcSideQuestPreset();
        EditorUtility.SetDirty(mgr);
        Debug.Log("[Quest] 已添加/更新支线: " + LlmWanderingNpcQuestPreset.QuestId);
        EditorUtility.DisplayDialog(
            "支线任务",
            "已添加支线「游走的神秘人」。\n\n" +
            "接取条件：M0 主线（content1_m0）整章完成。\n" +
            "完成条件：与配置了 LlmNpcQuestReporter 的 LLM NPC 完成一轮对话。",
            "确定");
    }
}
#endif
