#if UNITY_EDITOR
using GameQuestSystem;
using UnityEditor;
using UnityEngine;

public static class QuestNewUserOnboardingSetup
{
    [MenuItem("Tools/Quest System/One Click Setup New User Tutorial")]
    public static void SetupNewUserTutorialQuest()
    {
        var manager = Object.FindObjectOfType<QuestSystemManager>(true);
        if (manager == null)
        {
            var managerGo = new GameObject("QuestSystemManager");
            manager = managerGo.AddComponent<QuestSystemManager>();
        }

        manager.AddOrReplaceQuestChain(NewUserTutorialQuestPreset.CreateChain());
        QuestGroundGuidePath.EnsureExists();
        EditorUtility.SetDirty(manager);

        EditorUtility.DisplayDialog(
            "新手教学",
            $"已写入 {NewUserTutorialQuestPreset.SubTaskCount} 步新手教学（含「坐下休息」）。\n" +
            "任务说明沿用原有简要任务栏；贴地引导线会在「前往引导点」等步骤自动显示。\n\n" +
            "若场景尚未配置椅子交互，请使用：\nTools → 椅子交互 → 集成到项目",
            "确定");
    }

    [MenuItem("Tools/Quest System/Setup New User Tutorial + Chair Integration")]
    public static void SetupNewUserTutorialWithChairIntegration()
    {
        if (!ChairSitEditorGuard.EnsureNotPlaying())
            return;

        SetupNewUserTutorialQuest();
        ChairSitOneClickWizard.RunFullSetupWithBenchesPublic();
        AfkDanceSetupWizard.RunSetup();

        EditorUtility.DisplayDialog(
            "新手教学 + 椅子交互",
            $"已写入 {NewUserTutorialQuestPreset.SubTaskCount} 步新手教学，并完成椅子/待机动画配置。\n请 Ctrl+S 保存场景后 Play 测试。",
            "确定");
    }
}
#endif
