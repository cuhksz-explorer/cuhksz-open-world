using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 每个场景加载后，按 undergarden 标准刷新 Screen Space Canvas 的 CanvasScaler。
/// </summary>
public static class GameplayUiScaleBootstrap
{
    static bool _hooked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if (_hooked)
            return;
        _hooked = true;
        SceneManager.sceneLoaded += OnSceneLoaded;
        ApplyAllLoadedScenes();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.IsValid() && scene.isLoaded)
            ApplyScene(scene);
    }

    static void ApplyAllLoadedScenes()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (scene.isLoaded)
                ApplyScene(scene);
        }
    }

    public static void ApplyScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return;
        if (GameplayUiSceneExclusions.ShouldSkipScene(scene))
            return;

        var roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            var canvases = roots[i].GetComponentsInChildren<Canvas>(true);
            for (int c = 0; c < canvases.Length; c++)
                UndergardenUiScaleProfile.ApplyToCanvas(canvases[c]);
        }

        InteractPromptSharedUiLocator.EnsureGameplayHudCanvasVisible();
        GameplaySettingsButtonWiring.WireScene(scene);
    }
}
