using System.Collections;

using UnityEngine;



namespace PhoneUI

{

    /// <summary>

    /// 打开手机：第三人称正面镜头；关闭后恢复跟随相机，默认不切回第一人称（避免角色“消失”）。

    /// </summary>

    [DefaultExecutionOrder(10000)]
    public class PhonePresentationController : MonoBehaviour

    {

        [Header("Player")]

        public Transform playerTransform;

        public Animator animator;

        public Transform phoneViewAnchor;

        public GameObject phoneProp;



        [Header("Camera")]

        public CameraSwitcher cameraSwitcher;

        public Camera thirdPersonCamera;

        public Transform phoneViewTarget;



        [Header("Animation")]

        public string openPhoneTrigger = "OpenPhone";

        public string closePhoneTrigger = "ClosePhone";

        public string usingPhoneBool = "UsingPhone";

        [Tooltip("镜头到位后再播掏手机动画。")]

        public bool deferOpenTriggerUntilCameraDone = true;

        [Tooltip("入场动画结束后再显示手持手机模型。")]

        public bool showPropAfterOpenAnim = true;

        public float openAnimDuration = 1.2f;

        public float closeAnimDuration = 1f;

        public float presentationDuration = 0.4f;

        public float cameraLerpDuration = 0.35f;

        [Tooltip("Presentation 期间将 W/A/S/D/Run 置 false，避免与手机层冲突。")]

        public bool suppressLocomotionAnimatorBools = true;



        [Header("Look At")]

        public Vector3 lookAtOffset = new Vector3(0f, 1.35f, 0f);

        public float cameraSideOffset = 2.2f;

        public float lookAtSideOffset = 0.9f;

        [Tooltip("true：镜头在角色正前方（自拍视角）；false：沿用侧面偏移。")]
        public bool useFrontFacingCamera = false;

        public Vector3 anchorLocalPosition = new Vector3(0f, 1.45f, 1.85f);

        [Header("Compose (character on screen left, half body)")]
        [Tooltip("正面半身构图：相机在角色前方略偏右，人物落在画面左侧（非侧面轮廓）。")]
        public bool useComposeFraming = true;
        [Tooltip("相机在角色前方距离（越大越远）。")]
        public float composeCameraForwardDistance = 2.05f;
        [Tooltip("相机相对角色向右偏移（越大人物越靠画面左侧）。")]
        public float composeCameraRightBias = 1.38f;
        [Tooltip("注视点高度（与相机同高时为平视）。")]
        public float composeLookHeight = 2f;
        [Tooltip("相机镜头高度（世界空间 Y，相对角色脚底）。")]
        public float composeCameraHeight = 2f;
        [Tooltip("水平构图绕角色竖直轴右转角度（度）。")]
        public float composeYawOffsetDegrees = 30f;
        [Tooltip("打开时让人物转向镜头。")]
        public bool composeRotatePlayerToCamera = true;
        [Tooltip("Presentation 期间第三人称 FOV。")]
        public float composeFieldOfView = 42f;
        public Vector3 composeAnchorLocalPosition = new Vector3(0f, 1.45f, 0.2f);

        void Awake()
        {
            ApplyComposeDefaults();
        }

        void ApplyComposeDefaults()
        {
            useComposeFraming = true;
            composeCameraForwardDistance = 2f;
            composeCameraRightBias = 1.55f;
            composeCameraHeight = 2f;
            composeLookHeight = 2f;
            composeYawOffsetDegrees = 30f;
            composeFieldOfView = 40f;
        }

        [Header("Close")]

        [Tooltip("关闭手机后是否切回打开前的第一人称。默认 false，保持第三人称可见角色。")]

        public bool restoreFirstPersonViewOnClose = true;



        bool _active;

        bool _holdPhoneCamera;

        bool _wasFirstPerson;

        Vector3 _camPosSaved;

        Quaternion _camRotSaved;

        Coroutine _routine;

        bool _savedAllowThirdPersonMouse;

        WideViewCamera _wideViewCamera;

        bool _wideViewWasEnabled = true;

        bool _locomotionSuppressed;
        float _fovSaved = 60f;
        bool _fovSavedValid;
        Quaternion _playerRotSaved;
        bool _playerRotSavedValid;
        Vector3 _composeWorldOffset;
        bool _composeFramingLocked;
        Quaternion _composePlayerFacing;
        bool _composePlayerFacingLocked;

        static bool s_hasFirstPersonSnapshot;
        static bool s_pendingFirstPersonRestore;
        static FirstPersonCamera.WalkingRigSnapshot s_firstPersonSnapshot;

        static PhonePresentationController s_active;

        bool _isClosing;

        public bool IsPresentationActive => _active || _holdPhoneCamera || _isClosing;

        public static bool IsBlockingGameplayCamera =>
            (s_active != null && s_active.IsPresentationActive)
            || PhoneSessionController.IsPhoneOpen;

        public static bool TryConsumePendingFirstPersonRestore(FirstPersonCamera fps)
        {
            if (!s_pendingFirstPersonRestore || fps == null || !s_hasFirstPersonSnapshot)
                return false;

            fps.RestoreWalkingRigSnapshot(s_firstPersonSnapshot);
            fps.ExtendRigRestoreHold(180);
            s_pendingFirstPersonRestore = false;
            s_hasFirstPersonSnapshot = false;
            return true;
        }

        static void CaptureFirstPersonSnapshot(FirstPersonCamera fps)
        {
            if (fps == null)
            {
                s_hasFirstPersonSnapshot = false;
                s_pendingFirstPersonRestore = false;
                return;
            }

            s_firstPersonSnapshot = fps.CaptureWalkingRigSnapshot();
            s_hasFirstPersonSnapshot = true;
            s_pendingFirstPersonRestore = true;
        }



        public void BeginPresentation(System.Action onReady)

        {

            if (_routine != null)

                StopCoroutine(_routine);

            _routine = StartCoroutine(CoBegin(onReady));

        }



        void OnEnable()
        {
            s_active = this;
        }

        void OnDisable()
        {
            if (s_active == this)
                s_active = null;
            _isClosing = false;
        }

        public void EndPresentation(System.Action onDone)

        {

            if (_routine != null)

                StopCoroutine(_routine);

            _isClosing = true;
            _holdPhoneCamera = false;

            if (!isActiveAndEnabled)

            {

                FinishPresentationImmediate(onDone);

                return;

            }

            _routine = StartCoroutine(CoEnd(onDone));

        }



        /// <summary>Animation Event：入场 clip 末尾可调用以显示 Prop。</summary>

        public void OnPhonePropShow()

        {

            if (phoneProp != null)

                phoneProp.SetActive(true);

        }



        void LateUpdate()

        {

            if (!_holdPhoneCamera || thirdPersonCamera == null)

                return;

            UpdatePhoneCamera(1f);

            if (composeRotatePlayerToCamera && _composePlayerFacingLocked && playerTransform != null)

                playerTransform.rotation = _composePlayerFacing;

        }



        IEnumerator CoBegin(System.Action onReady)

        {

            _active = true;

            _holdPhoneCamera = false;

            ResolveReferences();

            EnsurePhoneViewAnchor();

            _composeFramingLocked = false;
            _composePlayerFacingLocked = false;

            ApplyComposeDefaults();

            if (useComposeFraming)

                CacheComposeWorldOffset();



            if (phoneProp != null && !showPropAfterOpenAnim)

                phoneProp.SetActive(true);



            SetUsingPhone(false);

            SuppressLocomotionBools(true);



            CacheWideViewCamera();

            if (_wideViewCamera != null)

            {

                _wideViewWasEnabled = _wideViewCamera.enabled;

                _wideViewCamera.enabled = false;

            }



            if (cameraSwitcher != null)

            {

                _wasFirstPerson = cameraSwitcher.IsFirstPersonActive;

                if (_wasFirstPerson)

                    CaptureFirstPersonSnapshot(cameraSwitcher.firstPersonCamera);

                _savedAllowThirdPersonMouse = cameraSwitcher.allowThirdPersonMouseControl;

                cameraSwitcher.allowThirdPersonMouseControl = false;

                if (_wasFirstPerson)

                    cameraSwitcher.ApplyThirdPersonForPhonePresentation();

            }



            if (thirdPersonCamera != null)

            {

                thirdPersonCamera.enabled = true;

                _camPosSaved = thirdPersonCamera.transform.position;

                _camRotSaved = thirdPersonCamera.transform.rotation;

                _fovSaved = thirdPersonCamera.fieldOfView;

                _fovSavedValid = true;

                if (useComposeFraming)

                    thirdPersonCamera.fieldOfView = composeFieldOfView;

            }

            if (composeRotatePlayerToCamera && playerTransform != null)

            {

                _playerRotSaved = playerTransform.rotation;

                _playerRotSavedValid = true;

            }



            float camDur = cameraLerpDuration;

            float elapsed = 0f;

            while (elapsed < camDur)

            {

                elapsed += Time.unscaledDeltaTime;

                float k = camDur > 0f ? Mathf.Clamp01(elapsed / camDur) : 1f;

                k = k * k * (3f - 2f * k);

                UpdatePhoneCamera(k);

                yield return null;

            }



            UpdatePhoneCamera(1f);

            ApplyComposePlayerFacingOnce();



            if (deferOpenTriggerUntilCameraDone)

                FireOpenPhoneTrigger();



            if (showPropAfterOpenAnim && phoneProp != null)

                phoneProp.SetActive(true);



            float animWait = HasPhoneAnimatorParameters() ? Mathf.Max(0f, openAnimDuration) : 0f;

            if (animWait > 0f)

            {

                elapsed = 0f;

                while (elapsed < animWait)

                {

                    elapsed += Time.unscaledDeltaTime;

                    yield return null;

                }

            }



            SetUsingPhone(true);

            _holdPhoneCamera = true;

            onReady?.Invoke();

            _routine = null;

        }



        IEnumerator CoEnd(System.Action onDone)

        {

            _holdPhoneCamera = false;

            SetUsingPhone(false);

            FireClosePhoneTrigger();

            if (TryFinishFirstPersonCloseImmediately(onDone))

                yield break;



            float animWait = Mathf.Max(0f, closeAnimDuration);

            if (animWait > 0f)

            {

                float elapsed = 0f;

                while (elapsed < animWait)

                {

                    elapsed += Time.unscaledDeltaTime;

                    yield return null;

                }

            }



            if (phoneProp != null)

                phoneProp.SetActive(false);



            Vector3 startPos = thirdPersonCamera != null ? thirdPersonCamera.transform.position : _camPosSaved;

            Quaternion startRot = thirdPersonCamera != null ? thirdPersonCamera.transform.rotation : _camRotSaved;

            float camElapsed = 0f;

            while (camElapsed < cameraLerpDuration)

            {

                camElapsed += Time.unscaledDeltaTime;

                float k = Mathf.Clamp01(camElapsed / cameraLerpDuration);

                k = k * k * (3f - 2f * k);

                if (thirdPersonCamera != null)

                {

                    thirdPersonCamera.transform.position = Vector3.Lerp(startPos, _camPosSaved, k);

                    thirdPersonCamera.transform.rotation = Quaternion.Slerp(startRot, _camRotSaved, k);

                }

                yield return null;

            }



            if (thirdPersonCamera != null)

            {

                thirdPersonCamera.transform.position = _camPosSaved;

                thirdPersonCamera.transform.rotation = _camRotSaved;

                if (_fovSavedValid)

                    thirdPersonCamera.fieldOfView = _fovSaved;

            }

            _composePlayerFacingLocked = false;

            if (_wideViewCamera != null)
                _wideViewCamera.enabled = _wideViewWasEnabled;

            if (cameraSwitcher != null)

            {

                cameraSwitcher.allowThirdPersonMouseControl = _savedAllowThirdPersonMouse;

                var fps = cameraSwitcher.firstPersonCamera;

                if (restoreFirstPersonViewOnClose && _wasFirstPerson && fps != null)

                {
                    RestoreFirstPersonWalkingState(fps);

                    cameraSwitcher.RestoreFirstPersonAfterPhoneUi(fps);
                }

                else if (_wasFirstPerson)

                {

                    s_pendingFirstPersonRestore = true;

                    if (thirdPersonCamera != null)

                        thirdPersonCamera.enabled = true;

                    if (fps != null)

                        fps.Deactivate();

                    cameraSwitcher.RestoreThirdPersonFollowAndInputIfActive();

                }

                else

                {

                    if (thirdPersonCamera != null)

                        thirdPersonCamera.enabled = true;

                    if (fps != null)

                        fps.Deactivate();

                    cameraSwitcher.RestoreThirdPersonFollowAndInputIfActive();

                }

            }



            SuppressLocomotionBools(false);

            _active = false;
            _isClosing = false;
            _composeFramingLocked = false;
            _composePlayerFacingLocked = false;

            onDone?.Invoke();

            _routine = null;

        }



        void FireOpenPhoneTrigger()

        {

            if (animator == null)

                return;

            PhoneAnimatorUtil.ResetTriggerIfExists(animator, closePhoneTrigger);

            PhoneAnimatorUtil.SetTriggerIfExists(animator, openPhoneTrigger);

        }



        void FireClosePhoneTrigger()

        {

            if (animator == null)

                return;

            PhoneAnimatorUtil.ResetTriggerIfExists(animator, openPhoneTrigger);

            PhoneAnimatorUtil.SetTriggerIfExists(animator, closePhoneTrigger);

        }



        void SetUsingPhone(bool value)

        {

            PhoneAnimatorUtil.SetBoolIfExists(animator, usingPhoneBool, value);

        }



        void SuppressLocomotionBools(bool suppress)

        {

            if (!suppressLocomotionAnimatorBools || animator == null)

                return;



            if (suppress)

            {

                _locomotionSuppressed = true;

                PhoneAnimatorUtil.SetBoolIfExists(animator, "W", false);

                PhoneAnimatorUtil.SetBoolIfExists(animator, "A", false);

                PhoneAnimatorUtil.SetBoolIfExists(animator, "S", false);

                PhoneAnimatorUtil.SetBoolIfExists(animator, "D", false);

                PhoneAnimatorUtil.SetBoolIfExists(animator, "Run", false);

            }

            else if (_locomotionSuppressed)

            {

                _locomotionSuppressed = false;

            }

        }



        void UpdatePhoneCamera(float t)

        {

            if (thirdPersonCamera == null)

                return;

            EnsurePhoneViewAnchor();

            if (phoneViewAnchor == null)

                return;



            Transform player = playerTransform != null ? playerTransform : phoneViewAnchor.parent;

            Transform lookTarget = phoneViewTarget != null ? phoneViewTarget : playerTransform;

            if (lookTarget == null)

                return;



            Vector3 lookPoint;
            Vector3 targetPos;

            if (useComposeFraming)
            {
                if (!_composeFramingLocked)
                    CacheComposeWorldOffset();

                lookPoint = lookTarget.position + Vector3.up * composeLookHeight;
                targetPos = lookPoint + _composeWorldOffset;
            }
            else if (useFrontFacingCamera)
            {
                lookPoint = lookTarget.position + lookAtOffset;
                targetPos = phoneViewAnchor.position;
            }
            else
            {
                Vector3 right = player != null ? player.right : phoneViewAnchor.right;
                lookPoint = lookTarget.position + lookAtOffset;
                targetPos = phoneViewAnchor.position + right * cameraSideOffset;
                lookPoint -= right * lookAtSideOffset;
            }



            var toLook = lookPoint - targetPos;

            if (toLook.sqrMagnitude < 0.0001f)

                return;

            Quaternion targetRot = Quaternion.LookRotation(toLook, Vector3.up);

            if (_holdPhoneCamera && _composeFramingLocked)
            {
                thirdPersonCamera.transform.position = targetPos;
                thirdPersonCamera.transform.rotation = targetRot;
                return;
            }

            thirdPersonCamera.transform.position = Vector3.Lerp(_camPosSaved, targetPos, t);

            thirdPersonCamera.transform.rotation = Quaternion.Slerp(_camRotSaved, targetRot, t);

        }

        void CacheComposeWorldOffset()
        {
            if (playerTransform == null)
                return;

            Transform lookTarget = phoneViewTarget != null ? phoneViewTarget : playerTransform;
            Vector3 lookPoint = lookTarget.position + Vector3.up * composeLookHeight;
            _composeWorldOffset = ComputeComposeWorldOffset();
            _composeFramingLocked = true;
        }

        /// <summary>相机相对注视点的世界偏移：注视与相机同高时为平视；水平方向可绕 Y 轴偏转。</summary>
        Vector3 ComputeComposeWorldOffset()
        {
            Vector3 fwd = FlatHorizontal(playerTransform.forward);
            Vector3 right = FlatHorizontal(playerTransform.right);
            float verticalDelta = composeCameraHeight - composeLookHeight;
            Vector3 horizontal = fwd * composeCameraForwardDistance + right * composeCameraRightBias;
            if (Mathf.Abs(composeYawOffsetDegrees) > 0.01f)
                horizontal = Quaternion.AngleAxis(composeYawOffsetDegrees, Vector3.up) * horizontal;
            return Vector3.up * verticalDelta + horizontal;
        }

        void ApplyComposePlayerFacingOnce()
        {
            if (!composeRotatePlayerToCamera || playerTransform == null || !_composeFramingLocked)
                return;

            Vector3 faceDir = FlatHorizontal(_composeWorldOffset);
            if (faceDir.sqrMagnitude < 0.0001f)
                return;

            playerTransform.rotation = Quaternion.LookRotation(faceDir, Vector3.up);
            _composePlayerFacing = playerTransform.rotation;
            _composePlayerFacingLocked = true;
        }



        void CacheWideViewCamera()

        {

            if (_wideViewCamera != null)

                return;

            if (thirdPersonCamera != null)

                _wideViewCamera = thirdPersonCamera.GetComponent<WideViewCamera>();

            if (_wideViewCamera == null && thirdPersonCamera != null)

                _wideViewCamera = thirdPersonCamera.GetComponentInParent<WideViewCamera>();

        }



        void EnsurePhoneViewAnchor()

        {

            if (playerTransform == null)

                return;

            if (phoneViewAnchor == null || phoneViewAnchor == playerTransform)

            {

                var anchor = playerTransform.Find("PhoneViewAnchor");

                if (anchor != null)

                    phoneViewAnchor = anchor;

                else

                {

                    var anchorGo = new GameObject("PhoneViewAnchor");

                    anchorGo.transform.SetParent(playerTransform, false);

                    anchorGo.transform.localRotation = Quaternion.identity;

                    phoneViewAnchor = anchorGo.transform;

                }

            }

            if (phoneViewAnchor == null)

                return;

            phoneViewAnchor.localPosition = useComposeFraming

                ? composeAnchorLocalPosition

                : anchorLocalPosition;

        }



        bool HasPhoneAnimatorParameters()

        {

            if (animator == null)

                return false;

            return PhoneAnimatorUtil.HasParameter(animator, openPhoneTrigger, AnimatorControllerParameterType.Trigger)

                   || PhoneAnimatorUtil.HasParameter(animator, usingPhoneBool, AnimatorControllerParameterType.Bool);

        }

        void RestoreFirstPersonWalkingState(FirstPersonCamera fps)
        {
            if (fps == null)
                return;

            _composePlayerFacingLocked = false;

            if (s_hasFirstPersonSnapshot)
            {
                fps.RestoreWalkingRigSnapshot(s_firstPersonSnapshot);
                fps.ExtendRigRestoreHold(180);
            }
            else if (_playerRotSavedValid && playerTransform != null)
                playerTransform.rotation = _playerRotSaved;

            if (thirdPersonCamera != null)
            {
                thirdPersonCamera.transform.position = _camPosSaved;
                thirdPersonCamera.transform.rotation = _camRotSaved;
                if (_fovSavedValid)
                    thirdPersonCamera.fieldOfView = _fovSaved;
            }

            CacheWideViewCamera();
            if (_wideViewCamera != null)
                _wideViewCamera.SyncFromFirstPersonView(fps, false);
        }

        bool TryFinishFirstPersonCloseImmediately(System.Action onDone)

        {

            if (!restoreFirstPersonViewOnClose || !_wasFirstPerson || cameraSwitcher == null)

                return false;

            var fps = cameraSwitcher.firstPersonCamera;

            if (fps == null)

                return false;

            if (phoneProp != null)

                phoneProp.SetActive(false);

            if (_wideViewCamera != null)
                _wideViewCamera.enabled = false;

            RestoreFirstPersonWalkingState(fps);

            cameraSwitcher.allowThirdPersonMouseControl = _savedAllowThirdPersonMouse;
            cameraSwitcher.RestoreFirstPersonAfterPhoneUi(fps);

            SuppressLocomotionBools(false);

            _active = false;
            _isClosing = false;
            _composeFramingLocked = false;

            onDone?.Invoke();

            _routine = null;

            return true;

        }



        void FinishPresentationImmediate(System.Action onDone)

        {

            if (TryFinishFirstPersonCloseImmediately(onDone))

                return;

            if (phoneProp != null)

                phoneProp.SetActive(false);



            if (_wideViewCamera != null)

                _wideViewCamera.enabled = _wideViewWasEnabled;



            if (thirdPersonCamera != null)

            {

                thirdPersonCamera.transform.position = _camPosSaved;

                thirdPersonCamera.transform.rotation = _camRotSaved;

                if (_fovSavedValid)

                    thirdPersonCamera.fieldOfView = _fovSaved;

            }

            if (_playerRotSavedValid && playerTransform != null)

                playerTransform.rotation = _playerRotSaved;



            if (cameraSwitcher != null)

                cameraSwitcher.allowThirdPersonMouseControl = _savedAllowThirdPersonMouse;



            SuppressLocomotionBools(false);

            _active = false;
            _isClosing = false;
            _composeFramingLocked = false;
            _composePlayerFacingLocked = false;

            onDone?.Invoke();

            _routine = null;

        }

        static Vector3 FlatHorizontal(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward;
        }

        void ResolveReferences()

        {

            if (playerTransform == null)

            {

                var pm = FindObjectOfType<PlayerMovement>();

                if (pm != null)

                    playerTransform = pm.transform;

            }



            if (cameraSwitcher == null && playerTransform != null)

                cameraSwitcher = playerTransform.GetComponentInChildren<CameraSwitcher>(true);



            if (thirdPersonCamera == null && cameraSwitcher != null)

                thirdPersonCamera = cameraSwitcher.thirdPersonCamera;



            if (thirdPersonCamera == null)

                thirdPersonCamera = Camera.main;



            if (animator == null && playerTransform != null)

                animator = playerTransform.GetComponentInChildren<Animator>(true);



            if (phoneViewTarget == null)

                phoneViewTarget = playerTransform;



            EnsurePhoneViewAnchor();

        }

    }

}


