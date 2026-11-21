#if UNITY_EDITOR
using UnityEditor;

/// <summary>
/// 一键完成：3 角色 prefab 骨架/动作 + 所有场景 Player WASD 配置 + 换模支持。
/// </summary>
public static class PlayerCharacterAllInOneSetup
{
    const string MenuPath = "Tools/场景配置/一键配置所有场景角色（骨架+动作+换模）";
    const string GameServerMenuPath = "Tools/Game Server/一键配置所有场景角色（骨架+动作+换模）";

    [MenuItem(MenuPath, false, 2)]
    [MenuItem(GameServerMenuPath, false, 118)]
    public static void ConfigureEverything()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("一键配置角色", "请先退出 Play Mode。", "确定");
            return;
        }

        if (!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        if (!EditorUtility.DisplayDialog(
                "一键配置所有场景角色",
                "将依次执行：\n\n" +
                "1. 为 3 个角色 prefab 配置 Humanoid Avatar + WASD Animator\n" +
                "2. 批量处理所有游戏场景：\n" +
                "   • Player 标签与换模标记\n" +
                "   • KeyInputController（WASD 输入）\n" +
                "   • Animator + 动作控制器\n" +
                "   • 保留场景烘焙模型（换模成功后再隐藏）\n" +
                "3. 更新 PlayerCharacterCatalog 动作引用\n" +
                "4. 接线 Loadingscene + GameServer_Network\n\n" +
                "是否继续？",
                "继续",
                "取消"))
            return;

        EditorUtility.DisplayProgressBar("一键配置角色", "配置角色 prefab 骨架与动作...", 0.1f);
        try
        {
            PlayerCharacterEditorShared.ConfigureAllCharacterPrefabsMovement();
            PlayerCharacterEditorShared.WireCatalogMovementController();

            EditorUtility.DisplayProgressBar("一键配置角色", "配置所有场景 Player...", 0.35f);
            PlayerCharacterSceneOneClickSetup.ConfigureAllScenes(
                skipConfirm: true,
                progressBase: 0.35f,
                progressSpan: 0.55f,
                suppressCompletionDialog: true);

            EditorUtility.DisplayProgressBar("一键配置角色", "接线 Loadingscene...", 0.95f);
            PlayerCharacterSetupMenu.WireLoadingScenePublic();
            PlayerCharacterSetupMenu.WireGameServerNetworkPublic();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        EditorUtility.DisplayDialog(
            "一键配置完成",
            "已完成所有场景角色配置：\n\n" +
            "• 3 个角色 prefab：骨架 + WASD 动作\n" +
            "• 所有游戏场景：Player WASD + Animator\n" +
            "• 运行时按用户所选角色（本/研/博）换模\n\n" +
            "请运行游戏，选择不同角色进入场景测试 WASD 移动动画。",
            "确定");
    }
}
#endif
