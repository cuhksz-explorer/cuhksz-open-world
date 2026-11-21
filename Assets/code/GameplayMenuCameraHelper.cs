using UnityEngine;

/// <summary>
/// 左上角设置 / 旧版 Phone 面板关闭后，恢复第三人称 WideView 跟随与光标状态。
/// </summary>
public static class GameplayMenuCameraHelper
{
    public static void RestoreThirdPersonAfterPhoneUi()
    {
        var switchers = Object.FindObjectsOfType<CameraSwitcher>(true);
        for (int i = 0; i < switchers.Length; i++)
        {
            var sw = switchers[i];
            if (sw == null || !sw.IsThirdPersonGameplayActive)
                continue;

            sw.RestoreThirdPersonFollowAndInputIfActive();
        }
    }

    public static void RestoreAfterMenuClosed()
    {
        var switchers = Object.FindObjectsOfType<CameraSwitcher>(true);
        for (int i = 0; i < switchers.Length; i++)
        {
            var sw = switchers[i];
            if (sw == null)
                continue;

            if (sw.IsThirdPersonGameplayActive)
            {
                sw.RestoreThirdPersonFollowAndInputIfActive();
            }
            else
                sw.RestoreFirstPersonMouseLookIfActive();
        }

        GameplayCameraInputRestorer.RestoreNow();
    }
}
