using UnityEngine;

/// <summary>
/// 地图 / 场景内传送后对齐相机：第一人称时禁止直接改 Camera.main 世界坐标（会破坏 CameraHolder 枢轴）。
/// </summary>
public static class MapTeleportCameraSnap
{
    static FirstPersonCamera.WalkingRigSnapshot _fpsRigBeforeTeleport;
    static bool _hasFpsRigBeforeTeleport;

    public static void BeforePlayerMoves(PlayerMovement player)
    {
        if (player == null)
            return;

        player.RefreshFirstPersonCameraBinding();
        _hasFpsRigBeforeTeleport = false;

        if (IsWalkingFirstPersonViewActive(player))
        {
            FirstPersonCamera fps = ResolveFirstPersonCamera(player);
            if (fps != null)
            {
                _fpsRigBeforeTeleport = fps.CaptureWalkingRigSnapshot();
                _hasFpsRigBeforeTeleport = true;
            }
        }
    }

    public static void AfterPlayerMoved(PlayerMovement player, CameraFollow cameraFollow = null)
    {
        if (player == null)
            return;

        player.RefreshFirstPersonCameraBinding();
        if (IsWalkingFirstPersonViewActive(player))
        {
            FirstPersonCamera fps = ResolveFirstPersonCamera(player);
            if (fps == null)
            {
                _hasFpsRigBeforeTeleport = false;
                return;
            }

            fps.SnapRigAfterWorldTeleport();
            if (_hasFpsRigBeforeTeleport)
            {
                fps.RestoreWalkingRigSnapshot(_fpsRigBeforeTeleport, restorePlayerYaw: false);
                fps.ExtendRigRestoreHold(8);
            }

            _hasFpsRigBeforeTeleport = false;
            return;
        }

        if (cameraFollow == null)
            cameraFollow = ResolveCameraFollow();

        SnapLegacyThirdPersonOrFollow(player, cameraFollow);
    }

    /// <summary>
    /// 当前是否为走路第一人称视角（含 CameraSwitcher 判定，避免仅 IsActive 为 false 时漏快照）。
    /// </summary>
    public static bool IsWalkingFirstPersonViewActive(PlayerMovement player)
    {
        if (player == null)
            return false;

        player.RefreshFirstPersonCameraBinding();

        var switchers = Object.FindObjectsOfType<CameraSwitcher>(true);
        for (int i = 0; i < switchers.Length; i++)
        {
            CameraSwitcher sw = switchers[i];
            if (sw != null && sw.IsFirstPersonActive)
                return true;
        }

        FirstPersonCamera fps = ResolveFirstPersonCamera(player);
        return fps != null && IsFirstPersonGameplayCamera(fps);
    }

    public static bool ShouldUseLegacyFollowCameraSnap(PlayerMovement player)
    {
        return !IsWalkingFirstPersonViewActive(player);
    }

    static bool IsFirstPersonGameplayCamera(FirstPersonCamera fps)
    {
        return fps != null && (fps.IsActive || fps.IsRendering);
    }

    static FirstPersonCamera ResolveFirstPersonCamera(PlayerMovement player)
    {
        if (player == null)
            return null;

        FirstPersonCamera fps = player.externalFirstPersonLook;
        if (fps == null)
            fps = player.GetComponentInChildren<FirstPersonCamera>(true);
        return fps;
    }

    public static CameraFollow ResolveCameraFollow()
    {
        if (Camera.main != null && Camera.main.GetComponent<FirstPersonCamera>() == null)
        {
            var onMain = Camera.main.GetComponent<CameraFollow>();
            if (onMain != null)
                return onMain;
        }

        var all = Object.FindObjectsOfType<CameraFollow>(true);
        for (int i = 0; i < all.Length; i++)
        {
            CameraFollow follow = all[i];
            if (follow == null || follow.GetComponent<FirstPersonCamera>() != null)
                continue;
            return follow;
        }

        return null;
    }

    static void SnapLegacyThirdPersonOrFollow(PlayerMovement player, CameraFollow cameraFollow)
    {
        var wide = Object.FindObjectOfType<WideViewCamera>(true);
        if (wide != null && wide.isActiveAndEnabled && wide.target != null)
        {
            wide.SetTargetAndTeleport(player.transform);
            if (cameraFollow != null)
                cameraFollow.NotifyTargetTeleported(player.transform);
            return;
        }

        if (cameraFollow != null)
        {
            cameraFollow.SetTarget(player.transform);
            return;
        }

        if (Camera.main == null)
            return;

        if (Camera.main.GetComponent<FirstPersonCamera>() != null)
            return;

        var fpsOnMain = Camera.main.GetComponent<FirstPersonCamera>();
        if (fpsOnMain != null)
            return;

        var wideOnMain = Camera.main.GetComponent<WideViewCamera>();
        if (wideOnMain != null)
        {
            wideOnMain.SetTargetAndTeleport(player.transform);
            return;
        }

        Camera.main.transform.position = player.transform.position + new Vector3(0f, 5f, -10f);
    }
}
