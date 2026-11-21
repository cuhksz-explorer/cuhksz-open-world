#if UNITY_EDITOR
using GameQuestSystem;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 将 dorm 寝室+放置教学任务链写入场景中的 QuestSystemManager。
/// </summary>
public static class ApplyLingCollegeBDormQuestMenu
{
    const string MenuPath = "Tools/Quest System/添加 dorm 寝室支线（放置教学）";

    [MenuItem(MenuPath, false, 52)]
    static void ApplyToQuestSystemManagerInScene()
    {
        var mgr = Object.FindObjectOfType<QuestSystemManager>(true);
        if (mgr == null)
        {
            EditorUtility.DisplayDialog(
                "Quest System",
                "当前打开的场景中未找到 QuestSystemManager。\n请先打开 dorm.scene（或其它含 QuestSystemManager 的场景）。",
                "确定");
            return;
        }

        Undo.RecordObject(mgr, "Apply dorm side quest chain");
        mgr.ApplyLingCollegeBDormTutorialPresetChain();
        EditorUtility.SetDirty(mgr);
        Debug.Log("[Quest] 已添加/更新 dorm 寝室支线到「" + mgr.gameObject.name + "」。请保存场景。");
    }
}
#endif
