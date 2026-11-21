using GameQuestSystem;
using PhoneUI;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// 目标引导 UI：
/// 1) 在目标上方显示引导图标（Screen Space Overlay，穿透模型）
/// 2) 显示与目标的距离
/// 默认跟随「当前任务」的到达点；支持双视角：绑定 CameraSwitcher 或两台 Camera，自动用当前启用的那台做 WorldToScreen。
/// </summary>
public class TargetGuideUI : MonoBehaviour
{
    [Header("目标来源")]
    [Tooltip("开启后每帧从 QuestSystemManager 取当前子任务的目标坐标（到达点或剧情 NPC/交互点 + 场景匹配）。")]
    [SerializeField] private bool followActiveQuestReachLocation = true;
    [SerializeField] private QuestSystemManager questManager;
    [Tooltip("关闭「跟随任务」时使用；或作为任务无效时的备用目标。")]
    [SerializeField] private Transform manualTargetOverride;

    [Header("玩家")]
    [SerializeField] private Transform player;

    [Header("世界坐标")]
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 2.2f, 0f);

    [Header("相机（双视角）")]
    [Tooltip("推荐：拖场景里的 CameraSwitcher，将按 V 键切换的第一/三人称自动选用对应 Camera。")]
    [SerializeField] private CameraSwitcher cameraSwitcher;
    [Tooltip("无 CameraSwitcher 时：分别拖第一人称、第三人称 Camera；按 enabled 选用当前视角。")]
    [SerializeField] private Camera firstPersonCamera;
    [SerializeField] private Camera thirdPersonCamera;
    [Tooltip("以上都未配置时的备用单机位（可为空，则用 Camera.main）。")]
    [SerializeField] private Camera guideCamera;

    [Header("图标 UI（Screen Space Overlay）")]
    [SerializeField] private RectTransform iconRect;
    [SerializeField] private Image iconImage;
    [SerializeField] private Sprite guideIcon;
    [SerializeField] private bool hideWhenTargetBehindCamera = false;
    [SerializeField] private bool clampToScreenEdge = true;
    [SerializeField] private float screenEdgePadding = 26f;

    [Header("清晰度（加粗 + 描边）")]
    [SerializeField] private bool enhanceReadability = true;
    [SerializeField] private bool boldDistanceText = true;
    [SerializeField] private bool addIconOutline = true;
    [SerializeField] private bool addDistanceTextOutline = true;
    [SerializeField] private Color outlineColor = new Color(0f, 0f, 0f, 0.88f);
    [SerializeField] private Vector2 iconOutlineDistance = new Vector2(2.5f, -2.5f);
    [SerializeField] private Vector2 distanceTextOutlineDistance = new Vector2(1.5f, -1.5f);

    [Header("距离文本")]
    [SerializeField] private Text distanceText;
    [Tooltip("图标底边到距离文字顶部的间距（像素）。子物体挂在图标下时随图标移动；否则每帧贴在图标下方。")]
    [SerializeField] private float distanceBelowIconPixels = 10f;
    [Tooltip("仅显示米数时可留空；需要前后缀再填。")]
    [SerializeField] private string distancePrefix = "";
    [SerializeField] private string distanceSuffix = "";

    bool _distanceTextIsChildOfIcon;
    bool _sceneHooksRegistered;
    string _lastDistanceText = "";
    float _nextPlayerSearchTime;

    static Camera _vehicleCameraOverride;

    /// <summary>驾驶 Carsystem 载具时由 <see cref="InteractableVehicle"/> 注入车载 Camera，供任务引导 WorldToScreen 使用。</summary>
    public static void SetVehicleCameraOverride(Camera vehicleCamera)
    {
        _vehicleCameraOverride = vehicleCamera;
    }

    /// <summary>下车或载具销毁时清除车载 Camera 覆盖。</summary>
    public static void ClearVehicleCameraOverride(Camera vehicleCamera = null)
    {
        if (vehicleCamera == null || _vehicleCameraOverride == vehicleCamera)
            _vehicleCameraOverride = null;
    }

    void Awake()
    {
        EnsureGuideRootNotUnderOpenPanel();
        RebindSceneReferences(force: true);
        RegisterSceneHooks();

        if (iconImage != null && guideIcon != null)
            iconImage.sprite = guideIcon;

        if (iconRect != null && distanceText != null)
        {
            _distanceTextIsChildOfIcon = distanceText.transform.IsChildOf(iconRect);
            if (_distanceTextIsChildOfIcon)
                ApplyDistanceTextLayoutUnderIcon();
            else
            {
                distanceText.rectTransform.pivot = new Vector2(0.5f, 1f);
                distanceText.alignment = TextAnchor.UpperCenter;
            }
        }

        ApplyReadabilityStyle();
    }

    void OnEnable()
    {
        RegisterSceneHooks();
        RebindSceneReferences(force: true);
    }

    void OnDestroy()
    {
        if (_sceneHooksRegistered)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            _sceneHooksRegistered = false;
        }
    }

    void RegisterSceneHooks()
    {
        if (_sceneHooksRegistered)
            return;
        SceneManager.sceneLoaded += OnSceneLoaded;
        _sceneHooksRegistered = true;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ClearVehicleCameraOverride();
        SetClusterVisible(false);
        RebindSceneReferences(force: true);
    }

    /// <summary>
    /// 引导 UI 不应挂在 openpanel 下：展开完整任务栏（J）会隐藏 openpanel，导致距离/图标一并消失。
    /// </summary>
    void EnsureGuideRootNotUnderOpenPanel()
    {
        Transform cursor = transform;
        while (cursor != null)
        {
            if (string.Equals(cursor.gameObject.name, "openpanel", System.StringComparison.OrdinalIgnoreCase))
            {
                Canvas canvas = GetComponentInParent<Canvas>();
                if (canvas != null && canvas.transform != cursor)
                    transform.SetParent(canvas.transform, true);
                break;
            }
            cursor = cursor.parent;
        }
    }

    void RebindSceneReferences(bool force)
    {
        if (force || !IsUnityObjectAlive(cameraSwitcher))
            cameraSwitcher = FindObjectOfType<CameraSwitcher>();

        if (force || !IsUnityObjectAlive(player))
        {
            player = null;
            var p = GameObject.FindWithTag("Player");
            if (p != null)
                player = p.transform;
        }

        if (force || !IsUnityObjectAlive(questManager))
        {
            questManager = QuestSystemManager.Instance;
            if (questManager == null)
                questManager = FindObjectOfType<QuestSystemManager>(true);
        }

        if (force || !IsUnityObjectAlive(firstPersonCamera))
        {
            if (cameraSwitcher != null && cameraSwitcher.firstPersonCamera != null)
                firstPersonCamera = cameraSwitcher.firstPersonCamera.GetComponent<Camera>();
        }

        if (force || !IsUnityObjectAlive(thirdPersonCamera))
        {
            if (cameraSwitcher != null)
                thirdPersonCamera = cameraSwitcher.thirdPersonCamera;
        }

        if (force || !IsUnityObjectAlive(guideCamera))
            guideCamera = Camera.main;
    }

    static bool IsUnityObjectAlive(Object obj)
    {
        return obj != null;
    }

    void ApplyReadabilityStyle()
    {
        if (!enhanceReadability) return;

        if (distanceText != null)
        {
            if (boldDistanceText)
                distanceText.fontStyle = FontStyle.Bold;
            if (addDistanceTextOutline)
                EnsureOutline(distanceText, distanceTextOutlineDistance, outlineColor);
        }

        if (iconImage != null && addIconOutline)
            EnsureOutline(iconImage, iconOutlineDistance, outlineColor);
    }

    static void EnsureOutline(Graphic graphic, Vector2 distance, Color color)
    {
        if (graphic == null) return;
        var outline = graphic.GetComponent<Outline>();
        if (outline == null)
            outline = graphic.gameObject.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = distance;
        outline.useGraphicAlpha = true;
    }

    void ApplyDistanceTextLayoutUnderIcon()
    {
        if (distanceText == null || iconRect == null) return;
        var tr = distanceText.rectTransform;
        tr.anchorMin = tr.anchorMax = new Vector2(0.5f, 0.5f);
        tr.pivot = new Vector2(0.5f, 1f);
        distanceText.alignment = TextAnchor.UpperCenter;
        float halfIcon = iconRect.rect.height * 0.5f;
        tr.anchoredPosition = new Vector2(0f, -(halfIcon + distanceBelowIconPixels));
    }

    void LateUpdate()
    {
        if (ImmersiveModeSettings.IsEnabled)
        {
            SetClusterVisible(false);
            return;
        }

        RebindSceneReferences(force: false);

        Camera activeCam = ResolveActiveGuideCamera();
        if (activeCam == null)
        {
            SetClusterVisible(false);
            UpdateDistanceTextInvalid();
            return;
        }

        if (!TryResolveGuideWorldPosition(out Vector3 guideWorld))
        {
            SetClusterVisible(false);
            UpdateDistanceTextInvalid();
            return;
        }

        UpdateGuideIcon(guideWorld, activeCam);
        SyncDistanceTextPosition();
        UpdateDistanceText(guideWorld);
    }

    /// <summary>
    /// 驾驶中优先用车载 Camera；否则与第一/三人称切换一致（CameraSwitcher）；最后 guideCamera / main。
    /// </summary>
    Camera ResolveActiveGuideCamera()
    {
        if (_vehicleCameraOverride != null && _vehicleCameraOverride.enabled)
            return _vehicleCameraOverride;

        if (cameraSwitcher != null &&
            cameraSwitcher.firstPersonCamera != null &&
            cameraSwitcher.thirdPersonCamera != null)
        {
            Camera fpCam = cameraSwitcher.firstPersonCamera.GetComponent<Camera>();
            Camera tpCam = cameraSwitcher.thirdPersonCamera;

            if (cameraSwitcher.IsFirstPersonActive)
            {
                if (fpCam != null && fpCam.enabled)
                    return fpCam;
            }
            else
            {
                if (tpCam != null && tpCam.enabled)
                    return tpCam;
            }

            if (fpCam != null && fpCam.enabled) return fpCam;
            if (tpCam != null && tpCam.enabled) return tpCam;
        }

        if (firstPersonCamera != null && firstPersonCamera.enabled)
            return firstPersonCamera;
        if (thirdPersonCamera != null && thirdPersonCamera.enabled)
            return thirdPersonCamera;
        if (firstPersonCamera != null)
            return firstPersonCamera;
        if (thirdPersonCamera != null)
            return thirdPersonCamera;

        if (guideCamera != null)
            return guideCamera;

        return Camera.main;
    }

    bool TryResolveGuideWorldPosition(out Vector3 worldPos)
    {
        worldPos = default;

        if (followActiveQuestReachLocation)
        {
            if (!IsUnityObjectAlive(questManager))
            {
                questManager = QuestSystemManager.Instance;
                if (questManager == null)
                    questManager = FindObjectOfType<QuestSystemManager>(true);
            }
            if (questManager != null && questManager.TryGetActiveQuestReachLocationWorldPosition(out worldPos))
                return true;
        }

        if (manualTargetOverride != null)
        {
            worldPos = manualTargetOverride.position;
            return true;
        }

        return false;
    }

    void UpdateGuideIcon(Vector3 worldAnchor, Camera cam)
    {
        Vector3 worldPos = worldAnchor + worldOffset;
        Vector3 screenPos = cam.WorldToScreenPoint(worldPos);
        bool behind = screenPos.z < 0f;

        if (behind && hideWhenTargetBehindCamera)
        {
            SetClusterVisible(false);
            return;
        }

        SetClusterVisible(true);

        if (behind)
        {
            screenPos.x = Screen.width - screenPos.x;
            screenPos.y = Screen.height - screenPos.y;
        }

        if (clampToScreenEdge)
        {
            screenPos.x = Mathf.Clamp(screenPos.x, screenEdgePadding, Screen.width - screenEdgePadding);
            screenPos.y = Mathf.Clamp(screenPos.y, screenEdgePadding, Screen.height - screenEdgePadding);
        }

        if (iconRect != null)
            iconRect.position = new Vector3(screenPos.x, screenPos.y, 0f);
    }

    /// <summary>
    /// 距离文本不是图标子物体时，每帧贴在图标下方（屏幕空间）。
    /// </summary>
    void SyncDistanceTextPosition()
    {
        if (distanceText == null || iconRect == null || _distanceTextIsChildOfIcon)
            return;

        var tr = distanceText.rectTransform;
        float halfIcon = iconRect.rect.height * 0.5f;
        tr.position = iconRect.position + new Vector3(0f, -(halfIcon + distanceBelowIconPixels), 0f);
    }

    void UpdateDistanceText(Vector3 targetWorldFlat)
    {
        if (distanceText == null) return;

        if (followActiveQuestReachLocation && questManager != null)
        {
            float d = questManager.GetDistanceToActiveTarget();
            if (d >= 0f)
            {
                SetDistanceText(distancePrefix + Mathf.RoundToInt(d) + distanceSuffix);
                return;
            }
        }

        if ((player == null || !IsUnityObjectAlive(player)) && Time.unscaledTime >= _nextPlayerSearchTime)
        {
            player = null;
            _nextPlayerSearchTime = Time.unscaledTime + 0.5f;
            var p = GameObject.FindWithTag("Player");
            if (p != null) player = p.transform;
        }
        if (player == null)
        {
            UpdateDistanceTextInvalid();
            return;
        }

        float dist = Vector3.Distance(player.position, targetWorldFlat);
        SetDistanceText(distancePrefix + Mathf.RoundToInt(dist) + distanceSuffix);
    }

    void UpdateDistanceTextInvalid()
    {
        SetDistanceText(distancePrefix + "--");
    }

    void SetDistanceText(string value)
    {
        if (distanceText == null || _lastDistanceText == value)
            return;

        _lastDistanceText = value;
        distanceText.text = value;
    }

    void SetIconVisible(bool visible)
    {
        if (iconImage != null)
            iconImage.enabled = visible;
        else if (iconRect != null && iconRect.gameObject.activeSelf != visible)
            iconRect.gameObject.SetActive(visible);
    }

    void SetClusterVisible(bool visible)
    {
        if (iconRect != null)
        {
            if (iconRect.gameObject.activeSelf != visible)
                iconRect.gameObject.SetActive(visible);
        }

        if (iconRect == null)
            SetIconVisible(visible);

        if (distanceText != null)
            distanceText.gameObject.SetActive(visible);
    }
}
