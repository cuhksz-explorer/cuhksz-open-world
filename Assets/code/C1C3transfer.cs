using GameControls;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class CameraSwitcher : MonoBehaviour
{
    [Header("Camera References")]
    public FirstPersonCamera firstPersonCamera;
    public Camera thirdPersonCamera;

    [Header("Switch Button")]
    public Button switchButton;

    [Header("Third Person Mouse")]
    [Tooltip("第三人称时是否允许鼠标控制角色转向")]
    public bool allowThirdPersonMouseControl = false;

    [Header("Debug / Consistency")]
    public bool enableCameraDiagnosticsLog = false;
    public bool syncRenderSettingsBetweenCameras = true;

    bool isFirstPerson;
    FirstPersonCamera.WalkingRigSnapshot _fpsRigBeforeThirdPerson;
    bool _hasFpsRigBeforeThirdPerson;

    public bool IsFirstPersonActive =>
        firstPersonCamera != null && firstPersonCamera.IsActive;

    public bool IsThirdPersonGameplayActive =>
        firstPersonCamera != null && thirdPersonCamera != null && !IsFirstPersonActive;

    public string ViewModeForServer => IsFirstPersonActive ? "first" : "third";

    public void ApplySavedViewMode(string mode)
    {
        if (firstPersonCamera == null || thirdPersonCamera == null)
            return;

        bool wantFirst = string.Equals(mode, "first", System.StringComparison.OrdinalIgnoreCase);
        if (wantFirst == IsFirstPersonActive)
            return;

        ToggleCamera();
    }

    void Start()
    {
        if (firstPersonCamera == null)
        {
            Debug.LogError("[CameraSwitcher] 请赋值第一人称相机脚本");
            enabled = false;
            return;
        }

        if (thirdPersonCamera == null)
        {
            Debug.LogError("[CameraSwitcher] 请赋值第三人称相机组件");
            enabled = false;
            return;
        }

        if (switchButton != null)
            switchButton.onClick.AddListener(ToggleCamera);

        thirdPersonCamera.enabled = true;
        firstPersonCamera.Deactivate();
        isFirstPerson = false;

        if (syncRenderSettingsBetweenCameras)
            SyncCameraRenderSettings();
        ApplySingleGameplayAudioListener(false);
        RestoreThirdPersonMouseLookIfActive();
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (syncRenderSettingsBetweenCameras)
            SyncCameraRenderSettings();
    }

    public void RefreshGameplayAudioListeners()
    {
        ApplySingleGameplayAudioListener(IsFirstPersonActive);
    }

    public void SuppressWalkingFirstPersonForVehicle()
    {
        if (firstPersonCamera != null)
            firstPersonCamera.Deactivate();
        ApplySingleGameplayAudioListener(IsFirstPersonActive);
    }

    public void RestoreWalkingViewAfterVehicle(bool walkingWasFirstPerson)
    {
        if (firstPersonCamera == null || thirdPersonCamera == null)
        {
            ApplySingleGameplayAudioListener(IsFirstPersonActive);
            return;
        }

        if (walkingWasFirstPerson)
        {
            thirdPersonCamera.enabled = false;
            firstPersonCamera.Activate();
            isFirstPerson = true;
        }
        else
        {
            firstPersonCamera.Deactivate();
            thirdPersonCamera.enabled = true;
            isFirstPerson = false;
            RestoreThirdPersonMouseLookIfActive();
        }

        ApplySingleGameplayAudioListener(IsFirstPersonActive);
    }

    public void ApplyThirdPersonForPhonePresentation()
    {
        if (firstPersonCamera == null || thirdPersonCamera == null)
            return;

        var wide = thirdPersonCamera.GetComponent<WideViewCamera>();
        if (wide != null)
        {
            wide.BindFirstPersonCamera(firstPersonCamera);
            wide.SyncFromFirstPersonView(firstPersonCamera, false);
        }

        thirdPersonCamera.enabled = true;
        firstPersonCamera.Deactivate();
        isFirstPerson = false;
        ApplySingleGameplayAudioListener(false);
    }

    public void RestoreFirstPersonAfterPhoneUi(FirstPersonCamera fpsOverride = null)
    {
        var fps = fpsOverride != null ? fpsOverride : firstPersonCamera;
        if (fps == null || thirdPersonCamera == null)
            return;

        isFirstPerson = true;
        thirdPersonCamera.enabled = false;
        fps.EnableRenderingOnly();
        ApplySingleGameplayAudioListener(true);
        if (syncRenderSettingsBetweenCameras)
            SyncCameraRenderSettings();
    }

    public void ToggleCamera()
    {
        bool currentlyFirst = IsFirstPersonActive;

        if (currentlyFirst)
        {
            _fpsRigBeforeThirdPerson = firstPersonCamera.CaptureWalkingRigSnapshot();
            _hasFpsRigBeforeThirdPerson = true;

            var wide = thirdPersonCamera.GetComponent<WideViewCamera>();
            if (wide != null)
                wide.SyncFromFirstPersonView(firstPersonCamera);

            firstPersonCamera.Deactivate();
            thirdPersonCamera.enabled = true;
            isFirstPerson = false;
            RestoreThirdPersonFollowAndInputIfActive();
        }
        else
        {
            bool restoredFromPhone =
                PhoneUI.PhonePresentationController.TryConsumePendingFirstPersonRestore(firstPersonCamera);

            if (!restoredFromPhone && _hasFpsRigBeforeThirdPerson)
            {
                firstPersonCamera.RestoreWalkingRigSnapshot(_fpsRigBeforeThirdPerson);
                firstPersonCamera.ExtendRigRestoreHold(60);
            }
            else if (!restoredFromPhone && !firstPersonCamera.IsPreservingRestoredRig)
            {
                if (thirdPersonCamera != null && thirdPersonCamera.enabled)
                {
                    Vector3 look = thirdPersonCamera.transform.forward;
                    var wideBack = thirdPersonCamera.GetComponent<WideViewCamera>();
                    if (wideBack != null && wideBack.target != null)
                    {
                        Vector3 toTarget = wideBack.target.position - thirdPersonCamera.transform.position;
                        if (toTarget.sqrMagnitude > 1e-6f)
                            look = toTarget.normalized;
                    }

                    firstPersonCamera.ApplyWalkingLookFromWorldDirection(look);
                }
                else
                {
                    var wideBack = thirdPersonCamera.GetComponent<WideViewCamera>();
                    if (wideBack != null)
                        wideBack.ApplyOrbitToFirstPersonView(firstPersonCamera);
                }
            }

            thirdPersonCamera.enabled = false;
            firstPersonCamera.Activate();
            firstPersonCamera.EnsureGameplayReady();
            isFirstPerson = true;
        }

        if (syncRenderSettingsBetweenCameras)
            SyncCameraRenderSettings();
        ApplySingleGameplayAudioListener(isFirstPerson);
    }

    void Update()
    {
        if (GameplayUiHotkeyGuard.BlocksCameraMouseLook())
            return;

        if (GameInput.GetKeyDown(GameControlAction.ToggleCamera))
        {
            if (VehicleController.IsAnyVehicleDriven)
                return;
            ToggleCamera();
        }
    }

    void ApplySingleGameplayAudioListener(bool firstPersonActive)
    {
        if (TeleportSceneLoadOverlay.IsShowing)
            return;
        if (firstPersonCamera == null || thirdPersonCamera == null)
            return;

        AudioListener fpAl = firstPersonCamera.GetComponent<AudioListener>();
        if (fpAl == null)
            fpAl = firstPersonCamera.gameObject.AddComponent<AudioListener>();
        AudioListener tpAl = thirdPersonCamera.GetComponent<AudioListener>();
        if (tpAl == null)
            tpAl = thirdPersonCamera.gameObject.AddComponent<AudioListener>();

        foreach (var al in Object.FindObjectsOfType<AudioListener>(true))
        {
            if (al == null)
                continue;
            if (al == fpAl)
                al.enabled = firstPersonActive;
            else if (al == tpAl)
                al.enabled = !firstPersonActive;
            else
                al.enabled = false;
        }
    }

    void SyncCameraRenderSettings()
    {
        if (firstPersonCamera == null || thirdPersonCamera == null)
            return;

        var fp = firstPersonCamera.GetComponent<Camera>();
        var tp = thirdPersonCamera;
        if (fp == null || tp == null)
            return;

        fp.cullingMask = tp.cullingMask;
        fp.clearFlags = tp.clearFlags;
        fp.backgroundColor = tp.backgroundColor;
        fp.allowHDR = tp.allowHDR;
        fp.allowMSAA = tp.allowMSAA;
        fp.renderingPath = tp.renderingPath;
        fp.useOcclusionCulling = tp.useOcclusionCulling;
        fp.depthTextureMode = tp.depthTextureMode;
        fp.nearClipPlane = tp.nearClipPlane;
        fp.farClipPlane = tp.farClipPlane;
    }

    public void FinalizeSceneViewAfterAllComponentsStarted()
    {
        ApplySceneCameraViewPolicy();
    }

    public void ForceFirstPersonOnly()
    {
        if (firstPersonCamera == null || thirdPersonCamera == null)
            return;

        thirdPersonCamera.enabled = false;
        firstPersonCamera.Activate();
        isFirstPerson = true;
        if (syncRenderSettingsBetweenCameras)
            SyncCameraRenderSettings();
        ApplySingleGameplayAudioListener(true);
    }

    public void ApplyDefaultThirdPersonView()
    {
        if (firstPersonCamera == null || thirdPersonCamera == null)
            return;

        firstPersonCamera.Deactivate();
        thirdPersonCamera.enabled = true;
        isFirstPerson = false;
        if (syncRenderSettingsBetweenCameras)
            SyncCameraRenderSettings();
        ApplySingleGameplayAudioListener(false);
        RestoreThirdPersonFollowAndInputIfActive();
    }

    public void ApplySceneCameraViewPolicy()
    {
        if (GameplayCameraInputRestorer.PreferFirstPersonAfterDormExit
            || !CameraViewScenePolicy.IsThirdPersonAllowedInActiveScene())
        {
            ForceFirstPersonOnly();
            return;
        }

        ApplyDefaultThirdPersonView();
    }

    public void RestoreFirstPersonMouseLookIfActive()
    {
        if (!IsFirstPersonActive || firstPersonCamera == null)
            return;

        firstPersonCamera.suppressMouseLook = false;
        firstPersonCamera.SetCursorLocked(true);
    }

    public void RestoreThirdPersonMouseLookIfActive()
    {
        if (IsFirstPersonActive || thirdPersonCamera == null)
            return;

        var wide = thirdPersonCamera.GetComponent<WideViewCamera>();
        if (wide != null)
            wide.ApplyFirstPersonStyleMouseLookSettings();

        GameplayCursorLock.LockForGameplayView();
    }

    public void RestoreThirdPersonFollowAndInputIfActive()
    {
        if (IsFirstPersonActive || thirdPersonCamera == null)
            return;

        var wide = thirdPersonCamera.GetComponent<WideViewCamera>();
        if (wide != null)
        {
            wide.enabled = true;
            wide.BindFirstPersonCamera(firstPersonCamera);
            wide.ResumeGameplayFollowAfterExternalControl();
            wide.ApplyFirstPersonStyleMouseLookSettings();
        }

        RestoreThirdPersonMouseLookIfActive();
    }

    public void EnforceCollegeHallWalkingCamera()
    {
        ForceFirstPersonOnly();
        if (firstPersonCamera != null && firstPersonCamera.playerObject != null)
            firstPersonCamera.EnsureGameplayReady(firstPersonCamera.playerObject);
    }

    public void SwitchToFirstPersonForMenuUi()
    {
        if (firstPersonCamera == null || thirdPersonCamera == null || IsFirstPersonActive)
            return;

        var wide = thirdPersonCamera.GetComponent<WideViewCamera>();
        if (wide != null)
            wide.SyncFromFirstPersonView(firstPersonCamera, false);

        thirdPersonCamera.enabled = false;
        firstPersonCamera.EnableRenderingOnly();
        isFirstPerson = true;
        ApplySingleGameplayAudioListener(true);
    }

    public void RestoreThirdPersonAfterMenuUi()
    {
        if (firstPersonCamera == null || thirdPersonCamera == null || !IsFirstPersonActive)
            return;

        firstPersonCamera.Deactivate();
        thirdPersonCamera.enabled = true;
        isFirstPerson = false;

        var wide = thirdPersonCamera.GetComponent<WideViewCamera>();
        if (wide != null)
        {
            wide.enabled = true;
            wide.SyncFromFirstPersonView(firstPersonCamera, false);
            wide.ResumeGameplayFollowAfterExternalControl();
            wide.ApplyFirstPersonStyleMouseLookSettings();
        }

        ApplySingleGameplayAudioListener(false);
        RestoreThirdPersonFollowAndInputIfActive();
    }
}
