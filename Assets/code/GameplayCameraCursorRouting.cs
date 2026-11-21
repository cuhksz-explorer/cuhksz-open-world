using UnityEngine;

/// <summary>
/// 决定当前应由第一人称还是第三人称处理 Esc / 点击恢复光标，避免双相机同帧各切一次导致「解锁后仍无法点 UI」。
/// </summary>
public static class GameplayCameraCursorRouting
{
    public static bool ShouldHandleCursorForFirstPerson(FirstPersonCamera fps)
    {
        if (fps == null || !fps.IsActive)
            return false;

        var sw = ResolveSwitcherForPlayer(fps.playerObject);
        if (sw != null)
            return sw.IsFirstPersonActive;

        var cam = fps.GetComponent<Camera>();
        return cam != null && cam.enabled;
    }

    public static bool ShouldHandleCursorForThirdPerson(WideViewCamera wide)
    {
        if (wide == null || !wide.isActiveAndEnabled)
            return false;

        var cam = wide.GetComponent<Camera>();
        if (cam == null || !cam.enabled)
            return false;

        var sw = wide.GetComponent<CameraSwitcher>();
        if (sw == null)
            sw = Object.FindObjectOfType<CameraSwitcher>(true);
        if (sw != null)
            return sw.IsThirdPersonGameplayActive;

        foreach (var fps in Object.FindObjectsOfType<FirstPersonCamera>(true))
        {
            if (fps != null && fps.IsActive)
                return false;
        }

        return true;
    }

    public static bool IsThirdPersonPrimaryForPlayer(GameObject player)
    {
        var sw = ResolveSwitcherForPlayer(player);
        if (sw != null)
            return sw.IsThirdPersonGameplayActive;
        return CameraViewScenePolicy.IsThirdPersonAllowedInActiveScene();
    }

    static CameraSwitcher ResolveSwitcherForPlayer(GameObject player)
    {
        var switchers = Object.FindObjectsOfType<CameraSwitcher>(true);
        if (switchers.Length == 0)
            return null;
        if (player == null)
            return switchers[0];
        for (int i = 0; i < switchers.Length; i++)
        {
            var sw = switchers[i];
            if (sw != null && sw.firstPersonCamera != null && sw.firstPersonCamera.playerObject == player)
                return sw;
        }

        return switchers[0];
    }
}
