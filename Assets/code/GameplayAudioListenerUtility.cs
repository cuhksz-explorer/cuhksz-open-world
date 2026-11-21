using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 保证场景中始终有一个可用的 AudioListener（行走相机优先）。
/// </summary>
public static class GameplayAudioListenerUtility
{
    public static bool HasEnabledListener()
    {
        var listeners = Object.FindObjectsOfType<AudioListener>(true);
        for (int i = 0; i < listeners.Length; i++)
        {
            var al = listeners[i];
            if (al == null || !al.enabled)
                continue;
            if (!al.gameObject.activeInHierarchy)
                continue;
            return true;
        }

        return false;
    }

    /// <summary>优先通过 CameraSwitcher 恢复；否则启用 MainCamera / 任意相机上的 Listener。</summary>
    public static void EnsureGameplayListener()
    {
        if (TeleportSceneLoadOverlay.IsShowing)
            return;

        var switchers = Object.FindObjectsOfType<CameraSwitcher>(true);
        for (int i = 0; i < switchers.Length; i++)
        {
            var sw = switchers[i];
            if (sw == null || sw.firstPersonCamera == null || sw.thirdPersonCamera == null)
                continue;
            sw.RefreshGameplayAudioListeners();
            if (HasEnabledListener())
                return;
        }

        if (HasEnabledListener())
            return;

        AudioListener keep = null;
        if (Camera.main != null)
            keep = Camera.main.GetComponent<AudioListener>();

        if (keep == null)
        {
            var cameras = Object.FindObjectsOfType<Camera>(true);
            for (int i = 0; i < cameras.Length; i++)
            {
                var cam = cameras[i];
                if (cam == null || !cam.enabled || !cam.gameObject.activeInHierarchy)
                    continue;
                keep = cam.GetComponent<AudioListener>();
                if (keep != null)
                    break;
            }
        }

        if (keep == null)
        {
            var listeners = Object.FindObjectsOfType<AudioListener>(true);
            if (listeners.Length > 0)
                keep = listeners[0];
        }

        if (keep == null)
            return;

        var all = Object.FindObjectsOfType<AudioListener>(true);
        for (int i = 0; i < all.Length; i++)
        {
            var al = all[i];
            if (al == null)
                continue;
            al.enabled = al == keep;
        }
    }
}
