using DormMall;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 场景切换 / 离开寝室后恢复行走视角：第一人称锁定光标、清除 suppressMouseLook。
/// 解决寝室 UI（背包、放置、编辑）未正常关闭时，回到书院大厅无法鼠标转视角的问题。
/// </summary>
public static class GameplayCameraInputRestorer
{
    static int _pendingRestoreFrames;

    /// <summary>离开寝室后的若干帧内，即便目标场景允许第三人称也优先恢复为第一人称。</summary>
    public static bool PreferFirstPersonAfterDormExit { get; set; }

    public static void ScheduleRestore(int framesAfterLoad = 3)
    {
        _pendingRestoreFrames = Mathf.Max(_pendingRestoreFrames, framesAfterLoad);
        EnsureRunner();
    }

    public static void MarkLeavingDorm()
    {
        PreferFirstPersonAfterDormExit = true;
        DismissPersistentDormGameplayUi();
        ScheduleRestore(6);
    }

    /// <summary>关闭跨场景残留的寝室 UI，避免光标/ suppressMouseLook 卡在非行走状态。</summary>
    public static void DismissPersistentDormGameplayUi()
    {
        if (InventoryUI.Instance != null)
            InventoryUI.Instance.Close();

        if (PlacementModeController.Instance != null)
            PlacementModeController.Instance.ForceExitIfActive();

        if (PlacedFurnitureEditController.Instance != null)
            PlacedFurnitureEditController.Instance.ForceExitEditModeIfNeeded();

        CloseOpenInteractionDialogues();
    }

    static void CloseOpenInteractionDialogues()
    {
        var managers = Object.FindObjectsOfType<CanvasInteractionUIManager>(true);
        for (int i = 0; i < managers.Length; i++)
        {
            if (managers[i] != null && managers[i].IsDialogueOpen)
                managers[i].CloseDialogueFromExternal();
        }
    }

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

        if (!DormSceneConfig.IsDormScene(scene.name))
            DismissPersistentDormGameplayUi();

        bool needsExtraCollegeFix = PreferFirstPersonAfterDormExit
            || IsLingCollegeAScene(scene.name);
        if (IsLingCollegeAScene(scene.name))
            ApplyLingCollegeACameraFix();
        ScheduleRestore(needsExtraCollegeFix ? 12 : 3);
    }

    public static bool IsActiveLingCollegeAScene()
    {
        return IsLingCollegeAScene(SceneManager.GetActiveScene().name);
    }

    static bool IsLingCollegeAScene(string unitySceneName)
    {
        return string.Equals(
            CameraViewScenePolicy.NormalizeSceneName(unitySceneName),
            "lingcollegeA",
            System.StringComparison.OrdinalIgnoreCase);
    }

    static void EnsureRunner()
    {
        if (DeferredRestoreRunner.Instance != null)
            return;
        var go = new GameObject("[GameplayCameraInputRestorer]");
        go.hideFlags = HideFlags.HideAndDontSave;
        go.AddComponent<DeferredRestoreRunner>();
    }

    public static void RestoreNow(bool clearDormExitPreference = false)
    {
        bool blockCursorLock = GameplayUiHotkeyGuard.BlocksCameraMouseLook();
        bool allowThird = CameraViewScenePolicy.IsThirdPersonAllowedInActiveScene();
        bool forceFirst = PreferFirstPersonAfterDormExit || !allowThird;

        var fpsCameras = Object.FindObjectsOfType<FirstPersonCamera>(true);
        for (int i = 0; i < fpsCameras.Length; i++)
        {
            var fps = fpsCameras[i];
            if (fps == null)
                continue;
            fps.suppressMouseLook = false;
        }

        var switchers = Object.FindObjectsOfType<CameraSwitcher>(true);
        for (int i = 0; i < switchers.Length; i++)
        {
            var sw = switchers[i];
            if (sw == null)
                continue;

            if (forceFirst)
                sw.ForceFirstPersonOnly();
            else if (allowThird)
                sw.ApplyDefaultThirdPersonView();
            else
                sw.ApplySceneCameraViewPolicy();

            if (!blockCursorLock)
            {
                sw.RestoreFirstPersonMouseLookIfActive();
                sw.RestoreThirdPersonMouseLookIfActive();
            }
        }

        RefreshPlayerMovementCameraBindings();
        EnforceGameplayCameraRig(forceFirst, allowThird, blockCursorLock);

        if (IsLingCollegeAScene(SceneManager.GetActiveScene().name))
            ApplyLingCollegeACameraFix();

        if (clearDormExitPreference && forceFirst)
            PreferFirstPersonAfterDormExit = false;
    }

    /// <summary>
    /// lingcollegeA：Camera 3D 为玩家子物体，寝室返回后第三人称易误开导致「能走动、鼠标不转」。
    /// </summary>
    public static void ApplyLingCollegeACameraFix()
    {
        var switchers = Object.FindObjectsOfType<CameraSwitcher>(true);
        for (int i = 0; i < switchers.Length; i++)
        {
            if (switchers[i] != null)
                switchers[i].EnforceCollegeHallWalkingCamera();
        }

        GameObject playerGo = null;
        SceneTeleportHelper.TryFindLocalPlayer(out playerGo);

        FirstPersonCamera fps = null;
        if (playerGo != null)
        {
            var all = Object.FindObjectsOfType<FirstPersonCamera>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].playerObject == playerGo)
                {
                    fps = all[i];
                    break;
                }
            }
        }

        if (fps == null && switchers.Length > 0 && switchers[0] != null)
            fps = switchers[0].firstPersonCamera;

        if (fps != null && playerGo != null)
            fps.EnsureGameplayReady(playerGo);
        else if (fps != null)
            fps.ReconcileWalkingControlState();

        RefreshPlayerMovementCameraBindings();
        InteractionCameraCacheReset.ClearAll();
    }

    /// <summary>
    /// 保证 Game 视图由「跟随玩家且可环视」的第一人称相机渲染，关闭误开启的其它 MainCamera。
    /// </summary>
    static void EnforceGameplayCameraRig(bool forceFirstPerson, bool allowThirdPerson, bool blockCursorLock)
    {
        if (TeleportSceneLoadOverlay.IsShowing)
            return;

        GameObject playerGo = null;
        SceneTeleportHelper.TryFindLocalPlayer(out playerGo);

        CameraSwitcher activeSwitcher = ResolveCameraSwitcher(playerGo);
        bool useFirstPersonPrimary = forceFirstPerson
            || !allowThirdPerson
            || (activeSwitcher != null && activeSwitcher.IsFirstPersonActive);

        FirstPersonCamera primaryFps = ResolvePrimaryFirstPersonCamera(playerGo, forceFirstPerson);
        Camera primaryCam = null;
        Camera allowedThirdCam = null;

        if (activeSwitcher != null)
            allowedThirdCam = activeSwitcher.thirdPersonCamera;

        if (useFirstPersonPrimary)
        {
            if (primaryFps != null)
            {
                if (playerGo != null && !blockCursorLock)
                    primaryFps.EnsureGameplayReady(playerGo);
                else if (!blockCursorLock)
                    primaryFps.EnsureGameplayReady();
                primaryCam = primaryFps.GetComponent<Camera>();
            }
        }
        else
        {
            if (primaryFps != null)
                primaryFps.Deactivate();
            if (allowedThirdCam != null)
                primaryCam = allowedThirdCam;
        }

        if (primaryCam == null)
            return;

        DisableLegacySceneMainCamera(primaryCam, allowedThirdCam);

        var allCameras = Object.FindObjectsOfType<Camera>(true);
        for (int i = 0; i < allCameras.Length; i++)
        {
            var cam = allCameras[i];
            if (cam == null || cam == primaryCam)
                continue;

            if (!forceFirstPerson && cam == allowedThirdCam && cam.enabled)
                continue;

            if (!cam.enabled || !cam.gameObject.activeInHierarchy)
                continue;

            if (IsOverlayOrUtilityCamera(cam))
                continue;

            // 多个 MainCamera 抢 Game 视图时，只保留绑定玩家的第一人称相机
            if (cam.CompareTag("MainCamera"))
                cam.enabled = false;

            // lingcollegeA 等：第三人称 Camera 3D 无 MainCamera 标签但仍会抢画面，且 WideView 已停更
            if (forceFirstPerson && (cam == allowedThirdCam || cam.GetComponent<WideViewCamera>() != null))
                cam.enabled = false;
        }

        if (useFirstPersonPrimary && primaryFps != null && !blockCursorLock)
            primaryFps.ReconcileWalkingControlState();
        else if (!primaryCam.enabled)
            primaryCam.enabled = true;
    }

    static CameraSwitcher ResolveCameraSwitcher(GameObject playerGo)
    {
        var switchers = Object.FindObjectsOfType<CameraSwitcher>(true);
        if (switchers.Length == 0)
            return null;
        if (playerGo == null)
            return switchers[0];
        for (int i = 0; i < switchers.Length; i++)
        {
            var sw = switchers[i];
            if (sw != null && sw.firstPersonCamera != null && sw.firstPersonCamera.playerObject == playerGo)
                return sw;
        }
        return switchers[0];
    }

    /// <summary>uppergarden 等场景根节点遗留的 Main Camera（固定坐标）会干扰 Game 视图。</summary>
    static void DisableLegacySceneMainCamera(Camera primaryCam, Camera allowedThirdCam)
    {
        var tagged = GameObject.FindGameObjectWithTag("MainCamera");
        if (tagged == null)
            return;

        var cam = tagged.GetComponent<Camera>();
        if (cam == null || !cam.enabled || cam == primaryCam || cam == allowedThirdCam)
            return;

        if (cam.GetComponent<WideViewCamera>() != null || cam.GetComponent<FirstPersonCamera>() != null)
            return;

        cam.enabled = false;
        var listener = cam.GetComponent<AudioListener>();
        if (listener != null)
            listener.enabled = false;
    }

    static FirstPersonCamera ResolvePrimaryFirstPersonCamera(GameObject playerGo, bool forceFirstPerson)
    {
        var switchers = Object.FindObjectsOfType<CameraSwitcher>(true);
        for (int i = 0; i < switchers.Length; i++)
        {
            var sw = switchers[i];
            if (sw != null && sw.firstPersonCamera != null)
                return sw.firstPersonCamera;
        }

        if (playerGo == null)
            return null;

        var all = Object.FindObjectsOfType<FirstPersonCamera>(true);
        for (int i = 0; i < all.Length; i++)
        {
            var fps = all[i];
            if (fps != null && fps.playerObject == playerGo)
                return fps;
        }

        return all.Length > 0 ? all[0] : null;
    }

    static bool IsOverlayOrUtilityCamera(Camera cam)
    {
        if (cam == null)
            return true;
        Transform t = cam.transform;
        while (t != null)
        {
            string n = t.name;
            if (n != null && (n.Contains("TeleportSceneLoadOverlay") || n.Contains("Loading") || n.Contains("Recorder")))
                return true;
            t = t.parent;
        }
        return cam.targetTexture != null;
    }

    static void RefreshPlayerMovementCameraBindings()
    {
        var movements = Object.FindObjectsOfType<PlayerMovement>(true);
        for (int i = 0; i < movements.Length; i++)
        {
            var pm = movements[i];
            if (pm == null)
                continue;
            pm.ApplyWalkingFirstPersonCameraBinding();
        }
    }

    sealed class DeferredRestoreRunner : MonoBehaviour
    {
        public static DeferredRestoreRunner Instance { get; private set; }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        void Update()
        {
            if (_pendingRestoreFrames <= 0)
            {
                Destroy(gameObject);
                return;
            }

            _pendingRestoreFrames--;
            bool lastFrame = _pendingRestoreFrames <= 0;
            RestoreNow(clearDormExitPreference: lastFrame);
        }
    }
}
