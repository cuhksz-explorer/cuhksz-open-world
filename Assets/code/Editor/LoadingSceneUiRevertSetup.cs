#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 将 Loadingscene 的 UI Canvas 恢复为登录场景默认缩放（不参与 gameplay UI 批量）。
/// </summary>
public static class LoadingSceneUiRevertSetup
{
    const string MenuPath = "Tools/UI/恢复 Loadingscene 登录 UI（原样）";
    const string ScenePath = "Assets/scene/Loadingscene.scene";

    [MenuItem(MenuPath, false, 11)]
    public static void RevertFromMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Loadingscene UI", "请先退出 Play Mode。", "确定");
            return;
        }

        if (!EditorUtility.DisplayDialog(
                "恢复 Loadingscene UI",
                "将把 Loadingscene 内所有 Screen Space Canvas 恢复为：\n" +
                "Scale With Screen Size · 1920×1080 · Match = 0\n\n" +
                "不会套用 undergarden Phone/HUD 规则。是否继续？",
                "恢复",
                "取消"))
            return;

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        int count = ApplyLoginCanvasDefaults(scene);
        EditorSceneManager.SaveScene(scene);

        EditorUtility.DisplayDialog(
            "Loadingscene UI",
            $"已恢复 {count} 个 Canvas 的缩放设置并保存场景。",
            "确定");
    }

    public static int ApplyLoginCanvasDefaults(Scene scene)
    {
        if (!scene.IsValid())
            return 0;

        int count = 0;
        foreach (var canvas in Resources.FindObjectsOfTypeAll<Canvas>())
        {
            if (canvas == null || canvas.gameObject.scene != scene)
                continue;
            if (canvas.renderMode == RenderMode.WorldSpace)
                continue;

            var scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null)
                scaler = canvas.gameObject.AddComponent<CanvasScaler>();

            Undo.RecordObject(scaler, "Revert Loadingscene CanvasScaler");
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0f;
            scaler.referencePixelsPerUnit = 100f;
            scaler.scaleFactor = 1f;

            var rt = canvas.GetComponent<RectTransform>();
            if (rt != null && rt.localScale.sqrMagnitude < 0.001f)
                rt.localScale = Vector3.one;

            canvas.pixelPerfect = false;
            EditorUtility.SetDirty(scaler);
            EditorUtility.SetDirty(canvas);
            count++;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        return count;
    }
}
#endif
