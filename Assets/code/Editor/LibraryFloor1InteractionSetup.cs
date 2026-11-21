#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>
/// libraryfloor1：从 undergarden 补全交互 UI。批量配置请用 <see cref="UndergardenUiSceneSetup"/>。
/// </summary>
public static class LibraryFloor1InteractionSetup
{
    const string MenuPath = "Tools/场景配置/libraryfloor1 - 配置交互UI（undergarden模板）";
    const string ScenePath = "Assets/scene/libraryfloor1.scene";

    [MenuItem(MenuPath, false, 2)]
    public static void ConfigureFromMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("libraryfloor1 交互UI", "请先退出 Play Mode。", "确定");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        var scene = UndergardenUiSceneSetup.OpenTargetScene(ScenePath);
        var report = UndergardenUiSceneSetup.ConfigureScene(scene, replaceExistingUi: true);
        UndergardenUiSceneSetup.SaveTargetScene(scene, ScenePath);

        EditorUtility.DisplayDialog(
            "libraryfloor1 交互UI",
            UndergardenUiSceneSetup.FormatSetupReport(report),
            "确定");
    }

    public static void ConfigureAndSave()
    {
        var scene = UndergardenUiSceneSetup.OpenTargetScene(ScenePath);
        UndergardenUiSceneSetup.ConfigureScene(scene, replaceExistingUi: true);
        UndergardenUiSceneSetup.SaveTargetScene(scene, ScenePath);
    }

    public static UndergardenUiSceneSetup.SetupReport ConfigureScene(Scene targetScene)
    {
        return UndergardenUiSceneSetup.ConfigureScene(targetScene);
    }
}
#endif
