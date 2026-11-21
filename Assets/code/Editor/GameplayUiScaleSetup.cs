#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 对当前已打开场景应用 undergarden UI 缩放标准。
/// </summary>
public static class GameplayUiScaleSetup
{
    const string MenuPath = "Tools/UI/配置当前场景 UI 屏幕自适应（undergarden 标准）";

    [MenuItem(MenuPath, false, 10)]
    public static void ConfigureActiveScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("UI 屏幕自适应", "请先退出 Play Mode。", "确定");
            return;
        }

        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || string.IsNullOrEmpty(scene.path))
        {
            EditorUtility.DisplayDialog("UI 屏幕自适应", "请先打开要配置的场景。", "确定");
            return;
        }

        if (GameplayUiSceneExclusions.ShouldSkipScene(scene))
        {
            EditorUtility.DisplayDialog(
                "UI 屏幕自适应",
                $"场景「{scene.name}」为登录/菜单场景，不参与 gameplay UI 自适应。\n（Loadingscene 保持原样）",
                "确定");
            return;
        }

        int count = ApplyToScene(scene);
        EditorSceneManager.MarkSceneDirty(scene);

        EditorUtility.DisplayDialog(
            "UI 屏幕自适应",
            $"已按 undergarden 标准配置 {count} 个 Canvas。\n\nPhone/HUD：800×600，match=0\n其余：1920×1080，match=0.5",
            "确定");
    }

    public static int ApplyToScene(Scene scene)
    {
        if (!scene.IsValid() || GameplayUiSceneExclusions.ShouldSkipScene(scene))
            return 0;

        int count = 0;
        foreach (var canvas in Resources.FindObjectsOfTypeAll<Canvas>())
        {
            if (canvas == null || canvas.gameObject.scene != scene)
                continue;
            if (canvas.renderMode == RenderMode.WorldSpace)
                continue;

            Undo.RecordObject(canvas.gameObject, "Undergarden UI Scale");
            UndergardenUiScaleProfile.ApplyToCanvas(canvas);
            EditorUtility.SetDirty(canvas);
            count++;
        }

        return count;
    }
}
#endif
