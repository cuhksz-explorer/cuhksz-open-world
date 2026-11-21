using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PhoneUI
{
    /// <summary>
    /// 沉浸模式：隐藏常驻 HUD（小地图、任务栏、追踪光标、血条/体力），互动触发的 UI 不受影响。
    /// </summary>
    public static class ImmersiveModeSettings
    {
        const string PrefsKey = "GameplayImmersiveMode_v1";

        public static bool IsEnabled { get; private set; }
        public static event Action<bool> Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            IsEnabled = PlayerPrefs.GetInt(PrefsKey, 0) == 1;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            if (IsEnabled)
                PhoneUiGameplayHudHider.RefreshPersistentHudVisibility();
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!IsEnabled)
                return;
            PhoneUiGameplayHudHider.RefreshPersistentHudVisibility();
        }

        public static void SetEnabled(bool enabled)
        {
            if (IsEnabled == enabled)
                return;

            IsEnabled = enabled;
            PlayerPrefs.SetInt(PrefsKey, enabled ? 1 : 0);
            PlayerPrefs.Save();
            Changed?.Invoke(enabled);
            PhoneUiGameplayHudHider.RefreshPersistentHudVisibility();
        }
    }
}
