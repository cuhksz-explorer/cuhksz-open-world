#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 一键将当前打开场景的 gameplay UI 改为与 undergarden 一致（完整版：任务栏、联机、聊天、Phone HUD 等）。
/// </summary>
public static class UndergardenUiActiveSceneOneClick
{
    const string MenuPath = "Tools/场景配置/当前场景 - 一键套用 undergarden 全部 UI";
    const string TemplateScenePath = "Assets/scene/undergarden.unity";

    const string DialogBody =
        "将把当前场景的 UI 与 undergarden 对齐，包含：\n\n" +
        "· 简易任务栏 / 复杂任务栏（QuestSystemUI、QuestSystemManager）\n" +
        "· 好友聊天（FriendChatSidebarCanvas）\n" +
        "· 联机与 GameServer（GameServerSync、Multiplayer_Services）\n" +
        "· Phone / openpanel / map / HP / ST 等基础 HUD\n" +
        "· TargetGuide、交互提示、EventSystem、CanvasScaler\n\n" +
        "不会复制 undergarden 的 Canvas (1)～(8) 地图传送点，以及 MapSystem（MAPCONTROL / 大地图 / 传送点列表等；各场景自行配置）。\n\n" +
        "会先清除当前场景里同名的旧 UI 根节点，再从 undergarden 复制缺失部分并重绑引用。\n\n" +
        "目标场景：\n{0}\n\n是否继续？";

    [MenuItem(MenuPath, false, 0)]
    public static void ApplyToActiveSceneFromMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("套用 undergarden 全部 UI", "请先退出 Play Mode。", "确定");
            return;
        }

        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || string.IsNullOrEmpty(scene.path))
        {
            EditorUtility.DisplayDialog(
                "套用 undergarden 全部 UI",
                "请先打开一个已保存的场景（File → Save 后再执行）。",
                "确定");
            return;
        }

        if (string.Equals(scene.path, TemplateScenePath, StringComparison.OrdinalIgnoreCase))
        {
            EditorUtility.DisplayDialog(
                "套用 undergarden 全部 UI",
                "当前已是 undergarden 模板场景，请切换到其他场景后再执行。",
                "确定");
            return;
        }

        if (GameplayUiSceneExclusions.ShouldSkipScenePath(scene.path))
        {
            EditorUtility.DisplayDialog(
                "套用 undergarden 全部 UI",
                "当前场景属于登录/菜单等非 gameplay 场景，已跳过。",
                "确定");
            return;
        }

        if (!EditorUtility.DisplayDialog(
                "套用 undergarden 全部 UI",
                string.Format(DialogBody, scene.path),
                "继续",
                "取消"))
            return;

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("套用 undergarden 全部 UI");

        Scene templateScene = default;
        try
        {
            templateScene = EditorSceneManager.OpenScene(TemplateScenePath, OpenSceneMode.Additive);
            var report = ApplyFullUndergardenUi(scene, templateScene, purgeExistingUi: true);
            UndergardenUiSceneSetup.SaveTargetScene(scene, scene.path);

            EditorUtility.DisplayDialog(
                "套用 undergarden 全部 UI",
                LibraryFloor2UndergardenUiSetup.FormatSetupReport(report),
                "确定");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            EditorUtility.DisplayDialog("套用 undergarden 全部 UI 失败", ex.Message, "确定");
        }
        finally
        {
            if (templateScene.IsValid() && templateScene.isLoaded)
                EditorSceneManager.CloseScene(templateScene, true);
            Undo.CollapseUndoOperations(undoGroup);
        }
    }

    /// <summary>供其它编辑器脚本或 -executeMethod 调用。</summary>
    public static void ApplyToActiveSceneAndSave()
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || string.IsNullOrEmpty(scene.path))
            return;
        if (string.Equals(scene.path, TemplateScenePath, StringComparison.OrdinalIgnoreCase))
            return;
        if (GameplayUiSceneExclusions.ShouldSkipScenePath(scene.path))
            return;

        Scene templateScene = default;
        try
        {
            templateScene = EditorSceneManager.OpenScene(TemplateScenePath, OpenSceneMode.Additive);
            ApplyFullUndergardenUi(scene, templateScene, purgeExistingUi: true);
            UndergardenUiSceneSetup.SaveTargetScene(scene, scene.path);
        }
        finally
        {
            if (templateScene.IsValid() && templateScene.isLoaded)
                EditorSceneManager.CloseScene(templateScene, true);
        }
    }

    public static LibraryFloor2UndergardenUiSetup.SetupReport ApplyFullUndergardenUi(
        Scene targetScene,
        Scene templateScene,
        bool purgeExistingUi)
    {
        if (!targetScene.IsValid() || !templateScene.IsValid())
            throw new InvalidOperationException("目标场景或 undergarden 模板场景无效。");

        EditorSceneManager.SetActiveScene(targetScene);

        if (purgeExistingUi)
            UndergardenUiSceneSetup.PurgeExistingUiSystems(targetScene);

        return LibraryFloor2UndergardenUiSetup.ConfigureScene(targetScene, templateScene);
    }
}
#endif
