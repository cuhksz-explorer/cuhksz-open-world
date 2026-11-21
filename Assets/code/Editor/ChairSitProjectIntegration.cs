#if UNITY_EDITOR
using GameQuestSystem;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 将队友的椅子交互与待机动画一键集成到当前项目：
/// 配置玩家动画、扫描并融合当前场景椅子、更新新手教程。
/// </summary>
public static class ChairSitProjectIntegration
{
    const string MenuPath = "Tools/椅子交互/集成到项目（动画 + 本场景椅子 + 待机动画）";

    [MenuItem(MenuPath, false, -1)]
    [MenuItem("Tools/Chair Sit/Integrate Into Project", false, -1)]
    public static void IntegrateIntoProject()
    {
        if (!ChairSitEditorGuard.EnsureNotPlaying())
            return;

        if (!EditorUtility.DisplayDialog(
                "椅子交互 · 项目集成",
                "将依次执行：\n" +
                "1. 导入坐下动画并给玩家挂 PlayerSitSequence\n" +
                "2. 为当前场景所有 bench/chair/seat 等添加 InteractableChair\n" +
                "3. 配置 10 秒无操作随机待机动画\n" +
                "4. 更新新手教程（增加「坐下椅子」步骤）\n\n" +
                "请确认当前已打开需要配置椅子的场景，并 Ctrl+S 保存。",
                "开始集成",
                "取消"))
            return;

        ChairSitOneClickWizard.RunFullSetupWithBenchesPublic();
        AfkDanceSetupWizard.RunSetup();
        UpdateNewUserTutorialWithChairStep();

        EditorUtility.DisplayDialog(
            "椅子交互 · 项目集成",
            "集成完成。\n\n" +
            "· 靠近椅子/长椅按 F 坐下，再按 F 或 Esc 站起\n" +
            "· 10 秒无操作会随机播放待机动画\n" +
            "· 新手教程已增加「坐下休息」步骤\n\n" +
            "请 Ctrl+S 保存场景后 Play 测试。",
            "确定");
    }

    [MenuItem("Tools/椅子交互/仅融合当前场景椅子", false, 1)]
    [MenuItem("Tools/Chair Sit/Setup Chairs In Current Scene Only", false, 1)]
    public static void SetupChairsInCurrentSceneOnly()
    {
        if (!ChairSitEditorGuard.EnsureNotPlaying())
            return;
        InteractableChairSetup.AddChairInteractionAllBenches();
    }

    public static void UpdateNewUserTutorialWithChairStep()
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
    }
}
#endif
