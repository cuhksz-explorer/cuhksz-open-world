using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 场景加载后统一强制相机视角策略，避免多个 CameraSwitcher 或晚于 Start 的逻辑把视角切回第三人称。
/// </summary>
public static class CameraViewSceneEnforcer
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        ScheduleEnforceAfterAllStarts();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Additive)
            return;
        ScheduleEnforceAfterAllStarts();
    }

    public static void EnforceForActiveScene()
    {
        var switchers = Object.FindObjectsOfType<CameraSwitcher>(true);
        for (int i = 0; i < switchers.Length; i++)
        {
            if (switchers[i] != null)
                switchers[i].FinalizeSceneViewAfterAllComponentsStarted();
        }
    }

    /// <summary>在全部 Start 执行完后再强制策略，避免 FirstPersonCamera.Start 关掉刚启用的相机。</summary>
    public static void ScheduleEnforceAfterAllStarts()
    {
        var go = new GameObject("[CameraViewSceneEnforcer]");
        go.hideFlags = HideFlags.HideAndDontSave;
        go.AddComponent<DeferredEnforceRunner>();
    }

    sealed class DeferredEnforceRunner : MonoBehaviour
    {
        System.Collections.IEnumerator Start()
        {
            yield return null;
            EnforceForActiveScene();
            GameplayCameraInputRestorer.RestoreNow();
            GameplayCameraInputRestorer.ScheduleRestore(4);
            yield return null;
            GameplayCameraInputRestorer.RestoreNow();
            if (GameplayCameraInputRestorer.IsActiveLingCollegeAScene())
                GameplayCameraInputRestorer.ApplyLingCollegeACameraFix();
            yield return null;
            GameplayCameraInputRestorer.ApplyLingCollegeACameraFix();
            Destroy(gameObject);
        }
    }
}
