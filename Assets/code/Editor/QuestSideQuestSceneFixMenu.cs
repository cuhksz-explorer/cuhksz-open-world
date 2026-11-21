#if UNITY_EDITOR
using GameQuestSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 将场景中 QuestSystemManager 里的 dorm 教学链从主线改为支线。
/// </summary>
public static class QuestSideQuestSceneFixMenu
{
    const string MenuPath = "Tools/Quest System/将 dorm 寝室链改为主线→支线";

    [MenuItem(MenuPath)]
    static void ConvertDormChainToSideInOpenScenes()
    {
        int changed = 0;
        foreach (var mgr in Object.FindObjectsOfType<QuestSystemManager>(true))
        {
            if (mgr == null)
                continue;

            Undo.RecordObject(mgr, "Convert dorm quest to side");
            mgr.ApplyLingCollegeBDormTutorialPresetChain();
            EditorUtility.SetDirty(mgr);
            changed++;
        }

        if (changed > 0)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.IsValid())
                EditorSceneManager.MarkSceneDirty(scene);
        }

        EditorUtility.DisplayDialog(
            "Quest System",
            changed > 0
                ? $"已在 {changed} 个 QuestSystemManager 上更新 dorm 为支线配置。\n请保存场景。"
                : "当前场景未找到 QuestSystemManager。",
            "确定");
    }
}
#endif
