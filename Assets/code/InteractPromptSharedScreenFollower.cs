using TMPro;
using UnityEngine;

/// <summary>
/// 将 InteractPrompt_Shared 从屏幕底部固定位置，改为跟随本帧 Offer 的世界锚点（NPC/载具等胸口高度）。
/// </summary>
[DefaultExecutionOrder(10002)]
[DisallowMultipleComponent]
public class InteractPromptSharedScreenFollower : MonoBehaviour
{
    [Tooltip("世界锚点再抬高（米），对准角色胸口/站牌高度")]
    public float worldHeightOffset = 1.45f;

    [Tooltip("屏幕像素上再抬高一点，避免挡住模型")]
    public Vector2 screenPixelOffset = new Vector2(0f, InteractPromptSharedStyle.ScreenPixelOffsetY);

    [Tooltip("目标在相机背后时隐藏面板")]
    public bool hideWhenBehindCamera = true;

    [Tooltip("限制在屏幕安全区内")]
    public bool clampToScreen = true;

    public float screenEdgePadding = InteractPromptSharedStyle.ScreenEdgePadding;

    RectTransform _panelRt;
    Canvas _rootCanvas;
    Transform _cachedParent;
    TextMeshProUGUI _nameTmp;
    TextMeshProUGUI _typeTmp;

    Vector2 _defaultAnchorMin;
    Vector2 _defaultAnchorMax;
    Vector2 _defaultPivot;
    Vector2 _defaultAnchoredPosition;
    bool _capturedDefaultLayout;

    void Awake()
    {
        _panelRt = transform as RectTransform;
        InteractPromptSharedStyle.ApplyIfNeeded(_panelRt);
        _rootCanvas = GetComponentInParent<Canvas>();
        _nameTmp = GetComponentInChildren<TextMeshProUGUI>(true);
        foreach (var tmp in GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (string.Equals(tmp.name, "InteractNameText", System.StringComparison.Ordinal))
                _nameTmp = tmp;
            if (string.Equals(tmp.name, "InteractTypeText", System.StringComparison.Ordinal))
                _typeTmp = tmp;
        }
        CaptureDefaultLayout();
    }

    void LateUpdate()
    {
        if (_panelRt == null || !_panelRt.gameObject.activeInHierarchy)
            return;

        int groupId = InteractPromptSharedLabelFrame.ResolveOfferGroupId(_nameTmp, _typeTmp);
        if (groupId == 0 || !InteractPromptSharedLabelFrame.TryGetActiveWorldAnchor(groupId, out Vector3 worldAnchor))
        {
            RestoreDefaultLayout();
            return;
        }

        worldAnchor.y += worldHeightOffset;
        if (!TryPlaceAtWorldPoint(worldAnchor))
            RestoreDefaultLayout();
    }

    public void CaptureDefaultLayout()
    {
        if (_panelRt == null || _capturedDefaultLayout)
            return;
        _defaultAnchorMin = _panelRt.anchorMin;
        _defaultAnchorMax = _panelRt.anchorMax;
        _defaultPivot = _panelRt.pivot;
        _defaultAnchoredPosition = _panelRt.anchoredPosition;
        _capturedDefaultLayout = true;
        _cachedParent = _panelRt.parent;
        _rootCanvas = GetComponentInParent<Canvas>();
    }

    public void RecaptureDefaultLayoutAfterReparent()
    {
        _panelRt = transform as RectTransform;
        _rootCanvas = GetComponentInParent<Canvas>();
        _cachedParent = _panelRt != null ? _panelRt.parent : null;
        if (_panelRt == null)
            return;
        _defaultAnchorMin = _panelRt.anchorMin;
        _defaultAnchorMax = _panelRt.anchorMax;
        _defaultPivot = _panelRt.pivot;
        _defaultAnchoredPosition = _panelRt.anchoredPosition;
        _capturedDefaultLayout = true;
    }

    public void RestoreDefaultLayout()
    {
        if (_panelRt == null || !_capturedDefaultLayout)
            return;
        _panelRt.anchorMin = _defaultAnchorMin;
        _panelRt.anchorMax = _defaultAnchorMax;
        _panelRt.pivot = _defaultPivot;
        _panelRt.anchoredPosition = _defaultAnchoredPosition;
    }

    bool TryPlaceAtWorldPoint(Vector3 worldPos)
    {
        if (_panelRt != null && _cachedParent != _panelRt.parent)
            RecaptureDefaultLayoutAfterReparent();

        Camera cam = ResolveCamera();
        if (cam == null)
            return false;

        Vector3 screenPos = cam.WorldToScreenPoint(worldPos);
        if (screenPos.z < 0f)
        {
            if (hideWhenBehindCamera)
                return false;
            screenPos.x = Screen.width - screenPos.x;
            screenPos.y = Screen.height - screenPos.y;
        }

        if (clampToScreen)
        {
            screenPos.x = Mathf.Clamp(screenPos.x, screenEdgePadding, Screen.width - screenEdgePadding);
            screenPos.y = Mathf.Clamp(screenPos.y, screenEdgePadding, Screen.height - screenEdgePadding);
        }

        screenPos.x += screenPixelOffset.x;
        screenPos.y += screenPixelOffset.y;

        if (_rootCanvas == null)
            _rootCanvas = GetComponentInParent<Canvas>();
        if (_rootCanvas == null)
            return false;

        RectTransform canvasRt = _rootCanvas.transform as RectTransform;
        if (canvasRt == null)
            return false;

        Camera canvasCam = _rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : _rootCanvas.worldCamera;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRt, screenPos, canvasCam, out Vector2 localPoint))
            return false;

        _panelRt.anchorMin = _panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        _panelRt.pivot = new Vector2(0.5f, 0f);
        _panelRt.anchoredPosition = localPoint;
        return true;
    }

    static Camera ResolveCamera()
    {
        var switcher = Object.FindObjectOfType<CameraSwitcher>(true);
        if (switcher != null)
        {
            if (switcher.firstPersonCamera != null && switcher.firstPersonCamera.isActiveAndEnabled)
            {
                Camera fp = switcher.firstPersonCamera.GetComponent<Camera>();
                if (fp != null && fp.enabled)
                    return fp;
            }
            if (switcher.thirdPersonCamera != null && switcher.thirdPersonCamera.enabled)
                return switcher.thirdPersonCamera;
        }
        return Camera.main;
    }
}
