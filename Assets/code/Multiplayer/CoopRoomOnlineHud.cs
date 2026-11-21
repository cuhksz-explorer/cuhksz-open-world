using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 联机会话进行中时，在屏幕右上角显示「房间在线人数」（数据来自 <see cref="CoopPlayerRoomStats"/> 的 SyncVar）。
/// </summary>
public class CoopRoomOnlineHud : MonoBehaviour
{
    const string TmpFontPath = "Fonts & Materials/AlibabaPuHuiTi-3-75-SemiBold SDF";

    static CoopRoomOnlineHud _instance;
    static int _lastSyncedCount = -1;

    Canvas _canvas;
    TextMeshProUGUI _label;

    public static void ApplySyncedCount(int count)
    {
        _lastSyncedCount = count;
        EnsureInstance();
        if (_instance != null)
            _instance.RefreshLabelText();
    }

    static void EnsureInstance()
    {
        if (_instance != null) return;
        var go = new GameObject("CoopRoomOnlineHud");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<CoopRoomOnlineHud>();
        _instance.BuildUi();
    }

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
    }

    void OnEnable() => Loc.OnLanguageChanged += OnLanguageChanged;
    void OnDisable() => Loc.OnLanguageChanged -= OnLanguageChanged;
    void OnLanguageChanged() => RefreshLabelText();

    void BuildUi()
    {
        _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 93;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        gameObject.AddComponent<GraphicRaycaster>();

        var panel = new GameObject("Panel");
        panel.transform.SetParent(transform, false);
        var rt = panel.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-16f, -16f);
        rt.sizeDelta = new Vector2(280f, 44f);

        var bg = panel.AddComponent<Image>();
        bg.color = MultiplayerUiPalette.BgWhite;

        var textGo = new GameObject("Text");
        textGo.transform.SetParent(panel.transform, false);
        _label = textGo.AddComponent<TextMeshProUGUI>();
        _label.fontSize = 22;
        _label.alignment = TextAlignmentOptions.MidlineRight;
        _label.color = MultiplayerUiPalette.TextBlack;
        var font = Resources.Load<TMP_FontAsset>(TmpFontPath);
        if (font != null)
            _label.font = font;

        var tr = textGo.GetComponent<RectTransform>();
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = new Vector2(12f, 4f);
        tr.offsetMax = new Vector2(-12f, -4f);

        _canvas.enabled = false;
        RefreshLabelText();
    }

    void Update()
    {
        bool inCoop = CoopSessionState.InSession;
        if (_canvas != null)
            _canvas.enabled = inCoop;

        if (inCoop && _lastSyncedCount >= 0)
            RefreshLabelText();

        if (inCoop && _label != null)
            _label.color = MultiplayerUiPalette.GetSuccessBlinkColor(Time.unscaledTime);
    }

    void RefreshLabelText()
    {
        if (_label == null) return;
        if (_lastSyncedCount < 0)
            _label.text = Loc.Choose("房间在线：…", "Room online: …");
        else
            _label.text = Loc.F("房间在线：{0} 人", "Room online: {0}", _lastSyncedCount);
    }

    void OnDestroy()
    {
        Loc.OnLanguageChanged -= OnLanguageChanged;
        if (_instance == this)
            _instance = null;
    }
}
