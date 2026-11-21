using UnityEngine.SceneManagement;

/// <summary>
/// 登录/菜单等非 gameplay 场景：不参与 undergarden UI 批量与运行时 CanvasScaler 改写。
/// </summary>
public static class GameplayUiSceneExclusions
{
    static readonly string[] SkipSceneNameContains =
    {
        "Loadingscene",
        "MainMenu",
        "Benchmark",
        "Mirror/Examples",
    };

    public static bool ShouldSkipScene(Scene scene)
    {
        if (!scene.IsValid())
            return true;
        return ShouldSkipSceneName(scene.name) || ShouldSkipScenePath(scene.path);
    }

    public static bool ShouldSkipScenePath(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
            return false;

        string fileName = System.IO.Path.GetFileNameWithoutExtension(assetPath);
        return ShouldSkipSceneName(fileName);
    }

    public static bool ShouldSkipSceneName(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
            return false;

        for (int i = 0; i < SkipSceneNameContains.Length; i++)
        {
            if (sceneName.IndexOf(SkipSceneNameContains[i], System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
    }
}
