using UnityEngine;

/// <summary>
/// 坐下时把当前主视角（第一/第三人称）与椅子/角色朝向对齐。
/// </summary>
public static class ChairSitCameraAlign
{
    public static void AlignForSitFacing(GameObject player, Quaternion facing)
    {
        if (player == null)
            return;

        if (GameplayCameraCursorRouting.IsThirdPersonPrimaryForPlayer(player))
        {
            var wide = ResolveWideViewCamera(player);
            if (wide != null)
                wide.AlignOrbitToWorldFacing(facing, true);
            return;
        }

        var fps = ResolveFirstPersonCamera(player);
        if (fps == null || !fps.IsActive)
            return;

        Vector3 flatFwd = facing * Vector3.forward;
        flatFwd.y = 0f;
        if (flatFwd.sqrMagnitude > 0.0001f)
            fps.ApplyWalkingLookFromWorldDirection(flatFwd.normalized);
    }

    static WideViewCamera ResolveWideViewCamera(GameObject player)
    {
        var switchers = Object.FindObjectsOfType<CameraSwitcher>(true);
        for (int i = 0; i < switchers.Length; i++)
        {
            var sw = switchers[i];
            if (sw == null || sw.thirdPersonCamera == null)
                continue;
            if (sw.firstPersonCamera != null && sw.firstPersonCamera.playerObject == player)
                return sw.thirdPersonCamera.GetComponent<WideViewCamera>();
        }

        if (switchers.Length > 0 && switchers[0].thirdPersonCamera != null)
            return switchers[0].thirdPersonCamera.GetComponent<WideViewCamera>();

        foreach (var wide in Object.FindObjectsOfType<WideViewCamera>(true))
        {
            if (wide != null && wide.target == player.transform)
                return wide;
        }

        return null;
    }

    static FirstPersonCamera ResolveFirstPersonCamera(GameObject player)
    {
        var switchers = Object.FindObjectsOfType<CameraSwitcher>(true);
        for (int i = 0; i < switchers.Length; i++)
        {
            var sw = switchers[i];
            if (sw != null && sw.firstPersonCamera != null && sw.firstPersonCamera.playerObject == player)
                return sw.firstPersonCamera;
        }

        foreach (var fps in Object.FindObjectsOfType<FirstPersonCamera>(true))
        {
            if (fps != null && fps.playerObject == player)
                return fps;
        }

        return null;
    }
}
