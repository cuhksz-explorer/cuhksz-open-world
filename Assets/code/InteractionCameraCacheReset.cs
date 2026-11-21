using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 场景切换后清空交互脚本里缓存的 FirstPersonCamera，避免仍指向已销毁的寝室相机。
/// </summary>
public static class InteractionCameraCacheReset
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Additive)
            return;
        ClearAll();
    }

    public static void ClearAll()
    {
        var managers = Object.FindObjectsOfType<CanvasInteractionUIManager>(true);
        for (int i = 0; i < managers.Length; i++)
            managers[i].ClearCachedFirstPersonCamera();

        var legacy = Object.FindObjectsOfType<InteractWithMouseAndMovementLock>(true);
        for (int i = 0; i < legacy.Length; i++)
            legacy[i].ClearCachedFirstPersonCamera();
    }
}
