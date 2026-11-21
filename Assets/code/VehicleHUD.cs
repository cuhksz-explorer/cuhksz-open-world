using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

/// <summary>
/// 载具 HUD：驾驶中显示速度与操控说明。靠近上车提示默认由 <see cref="VehicleController"/>
/// 走 <c>InteractPrompt_Shared</c>；仅当场景无共用面板时才会通过 <see cref="OfferPrompt"/> 懒创建本 Canvas 上的靠近条。
/// </summary>
public class VehicleHUD : MonoBehaviour
{
    const string DefaultAlibabaTmpResourcesPath = "Fonts & Materials/AlibabaPuHuiTi-3-75-SemiBold SDF";
    const string LegacyAlibabaFontResourcesPath = "Fonts/AlibabaPuHuiTi-3-75-SemiBold";

    [Header("字体（默认阿里巴巴普惠体，与背包/交互提示一致）")]
    [Tooltip("留空则从 Resources 加载 TMP SDF：" + DefaultAlibabaTmpResourcesPath)]
    public TMP_FontAsset hudFontAsset;

    [Tooltip("无 TMP SDF 时可选：拖 Inspector 里的 Alibaba PuHui Unity Font；也会尝试 Resources 路径 " + LegacyAlibabaFontResourcesPath)]
    public Font hudUnityFontFallback;

    TMP_FontAsset _resolvedHudFont;

    // ---- UI 元素 ----
    Canvas _canvas;
    GameObject _promptPanel;
    TextMeshProUGUI _promptNameText;
    TextMeshProUGUI _promptActionText;
    GameObject _speedPanel;
    TextMeshProUGUI _speedValueText;
    GameObject _controlsPanel;
    TextMeshProUGUI _controlsText; // 用于运行时更新操控面板内容（含救援键提示）

    // ---- 状态 ----
    bool _driving;
    bool _hasOffer;
    float _bestDist;
    string _bestName;
    string _bestAction;

    void Awake()
    {
        ResolveHudFont();
        BuildUI();
    }

    void ResolveHudFont()
    {
        if (hudFontAsset != null)
        {
            _resolvedHudFont = hudFontAsset;
            return;
        }

        _resolvedHudFont = Resources.Load<TMP_FontAsset>(DefaultAlibabaTmpResourcesPath);
        if (_resolvedHudFont != null)
            return;

        Font legacy = hudUnityFontFallback;
        if (legacy == null)
            legacy = Resources.Load<Font>(LegacyAlibabaFontResourcesPath);

        if (legacy != null)
        {
            _resolvedHudFont = TMP_FontAsset.CreateFontAsset(
                legacy,
                48,
                5,
                GlyphRenderMode.SDFAA,
                1024,
                1024,
                AtlasPopulationMode.Dynamic);
            if (_resolvedHudFont != null)
                _resolvedHudFont.name = "VehicleHUD_AlibabaPuHui_Runtime";
            return;
        }

        Debug.LogWarning(
            "[VehicleHUD] 未加载到阿里巴巴普惠体（TMP 或 Unity Font），TextMeshPro 将使用项目默认字体。");
    }

    #region UI 构建

    void BuildUI()
    {
        _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 95;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        gameObject.AddComponent<GraphicRaycaster>();

        // 靠近提示：仅在被 OfferPrompt 调用时创建（与 InteractPrompt_Shared 共用时可不出现第二套 UI）
        BuildSpeedPanel();
        BuildControlsPanel();

        UiLifecycleSafe.SetActiveIfAlive(_speedPanel, false);
        UiLifecycleSafe.SetActiveIfAlive(_controlsPanel, false);
    }

    void BuildPromptPanel()
    {
        _promptPanel = MakePanel("VH_Prompt", transform);
        var rt = Rect(_promptPanel);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.35f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(240, 72);

        _promptNameText = MakeHudText(_promptPanel.transform, "Name", "",
            22, FontStyles.Bold, TextAlignmentOptions.Center);
        SetStretch(_promptNameText.rectTransform, 12, 0, -12, -6, 0f, 0.5f, 1f, 1f);

        _promptActionText = MakeHudText(_promptPanel.transform, "Action", "",
            17, FontStyles.Normal, TextAlignmentOptions.Center);
        _promptActionText.color = new Color(0.82f, 0.82f, 0.82f);
        SetStretch(_promptActionText.rectTransform, 12, 6, -12, 0, 0f, 0f, 1f, 0.5f);
    }

    void BuildSpeedPanel()
    {
        _speedPanel = MakePanel("VH_Speed", transform);
        var rt = Rect(_speedPanel);
        rt.anchorMin = rt.anchorMax = Vector2.zero;
        rt.pivot = Vector2.zero;
        rt.sizeDelta = new Vector2(210, 82);
        rt.anchoredPosition = new Vector2(20, 20);

        var label = MakeHudText(_speedPanel.transform, "Label", "当前速度",
            15, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        label.color = new Color(0.72f, 0.72f, 0.72f);
        SetStretch(label.rectTransform, 14, 0, -14, -6, 0f, 0.55f, 1f, 1f);

        _speedValueText = MakeHudText(_speedPanel.transform, "Value", "0 km/h",
            30, FontStyles.Bold, TextAlignmentOptions.BottomLeft);
        SetStretch(_speedValueText.rectTransform, 14, 8, -14, 0, 0f, 0f, 1f, 0.55f);
    }

    void BuildControlsPanel()
    {
        _controlsPanel = MakePanel("VH_Controls", transform);
        var rt = Rect(_controlsPanel);
        rt.anchorMin = rt.anchorMax = new Vector2(1, 0);
        rt.pivot = new Vector2(1, 0);
        rt.sizeDelta = new Vector2(220, 210); // 加高以容纳救援键提示行
        rt.anchoredPosition = new Vector2(-20, 20);

        string content =
            "<b>操控方式</b>\n\n" +
            "W  —  前进\n" +
            "S  —  倒车\n" +
            "A  —  左转\n" +
            "D  —  右转\n" +
            "Esc — 下车";

        _controlsText = MakeHudText(_controlsPanel.transform, "List", content,
            16, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        _controlsText.lineSpacing = 5;
        SetStretch(_controlsText.rectTransform, 16, 10, -16, -10, 0, 0, 1, 1);
    }

    #endregion

    #region 公共接口

    /// <summary>
    /// 每帧由在范围内的 VehicleController 调用；距离最近的载具提示优先显示。
    /// </summary>
    public void OfferPrompt(float dist, string name, string action)
    {
        EnsureProximityPromptBuilt();
        if (!_hasOffer || dist < _bestDist)
        {
            _bestDist = dist;
            _bestName = name;
            _bestAction = action;
            _hasOffer = true;
        }
    }

    void EnsureProximityPromptBuilt()
    {
        if (_promptPanel != null) return;
        BuildPromptPanel();
        UiLifecycleSafe.SetActiveIfAlive(_promptPanel, false);
    }

    public void EnterDriving()
    {
        _driving = true;
        UiLifecycleSafe.SetActiveIfAlive(_promptPanel, false);
        UiLifecycleSafe.SetActiveIfAlive(_speedPanel, true);
        UiLifecycleSafe.SetActiveIfAlive(_controlsPanel, true);
    }

    public void ExitDriving()
    {
        _driving = false;
        UiLifecycleSafe.SetActiveIfAlive(_speedPanel, false);
        UiLifecycleSafe.SetActiveIfAlive(_controlsPanel, false);
    }

    public void UpdateSpeed(float kmh)
    {
        if (_speedValueText)
            _speedValueText.text = $"{kmh:F0} km/h";
    }

    /// <summary>
    /// 运行时更新操控面板文字（由 VehicleController 在上车时调用，包含救援键提示）。
    /// </summary>
    public void UpdateControlsContent(string content)
    {
        if (_controlsText)
            _controlsText.text = content;
    }

    #endregion

    void LateUpdate()
    {
        if (!isActiveAndEnabled)
            return;

        if (!_driving && _promptPanel != null)
        {
            if (_hasOffer)
            {
                UiLifecycleSafe.SetActiveIfAlive(_promptPanel, true);
                if (_promptNameText) _promptNameText.text = _bestName;
                if (_promptActionText) _promptActionText.text = _bestAction;
            }
            else
                UiLifecycleSafe.SetActiveIfAlive(_promptPanel, false);
        }

        _hasOffer = false;
        _bestDist = float.MaxValue;
    }

    #region UI 工具方法

    static GameObject MakePanel(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.55f);
        return go;
    }

    TextMeshProUGUI MakeHudText(Transform parent, string name, string text,
        float size, FontStyles style, TextAlignmentOptions align)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        if (_resolvedHudFont != null)
            tmp.font = _resolvedHudFont;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.alignment = align;
        tmp.color = Color.white;
        tmp.enableWordWrapping = true;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.richText = true;
        return tmp;
    }

    static RectTransform Rect(GameObject go) => go.GetComponent<RectTransform>();

    static void SetStretch(RectTransform rt,
        float left, float bottom, float right, float top,
        float anchorMinX, float anchorMinY, float anchorMaxX, float anchorMaxY)
    {
        rt.anchorMin = new Vector2(anchorMinX, anchorMinY);
        rt.anchorMax = new Vector2(anchorMaxX, anchorMaxY);
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(right, top);
    }

    #endregion
}
