using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>LGUHUB 原生 UI 构建辅助（参考网站主色）。</summary>
public static class LguHubUiBuilder
{
    public const string FontResourcesPath = "Fonts & Materials/AlibabaPuHuiTi-3-75-SemiBold SDF";

    public static readonly Color Primary = new Color(0.388f, 0.404f, 0.945f, 1f);
    public static readonly Color Bg = new Color(0.941f, 0.949f, 0.961f, 1f);
    public static readonly Color Card = Color.white;
    public static readonly Color TextMain = new Color(0.118f, 0.161f, 0.231f, 1f);
    public static readonly Color TextMuted = new Color(0.392f, 0.455f, 0.545f, 1f);

    public static TMP_FontAsset DefaultFont =>
        Resources.Load<TMP_FontAsset>(FontResourcesPath);

    public static GameObject CreateCanvasRoot(string name, int sortOrder = 3500)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortOrder;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }
        return go;
    }

    public static void ApplyFont(TMP_Text tmp)
    {
        if (tmp == null)
            return;
        var font = DefaultFont;
        if (font != null)
            tmp.font = font;
    }

    public static Image AddPanel(Transform parent, Color color, Vector2 anchorMin, Vector2 anchorMax)
    {
        var go = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var img = go.GetComponent<Image>();
        img.color = color;
        return img;
    }

    public static TMP_Text AddText(Transform parent, string text, int fontSize, TextAlignmentOptions align, Color color)
    {
        var go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = fontSize;
        t.alignment = align;
        t.color = color;
        t.raycastTarget = false;
        t.enableWordWrapping = true;
        t.overflowMode = TextOverflowModes.Ellipsis;
        ApplyFont(t);
        return t;
    }

    public static Button AddButton(
        Transform parent,
        string label,
        Color bg,
        UnityEngine.Events.UnityAction onClick,
        Color? textColor = null)
    {
        var go = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = bg;
        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        if (onClick != null)
            btn.onClick.AddListener(onClick);
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(160, 40);
        var labelColor = textColor ?? Color.white;
        var txt = AddText(go.transform, label, 18, TextAlignmentOptions.Center, labelColor);
        txt.raycastTarget = false;
        txt.enableWordWrapping = false;
        return btn;
    }

    public static TMP_InputField AddInput(Transform parent, string placeholder, bool multiline = false)
    {
        var go = new GameObject("Input", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        var le = go.GetComponent<LayoutElement>();
        le.minHeight = multiline ? 120 : 44;
        le.preferredHeight = multiline ? 120 : 44;
        le.flexibleWidth = 1f;
        le.minWidth = 120f;

        var img = go.GetComponent<Image>();
        img.color = Color.white;
        var input = go.AddComponent<TMP_InputField>();
        var textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(go.transform, false);
        StretchFull(textGo.GetComponent<RectTransform>(), new Vector2(12, 8), new Vector2(-12, -8));
        var text = textGo.AddComponent<TextMeshProUGUI>();
        text.fontSize = 20;
        text.color = TextMain;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        ApplyFont(text);

        var phGo = new GameObject("Placeholder", typeof(RectTransform));
        phGo.transform.SetParent(go.transform, false);
        StretchFull(phGo.GetComponent<RectTransform>(), new Vector2(12, 8), new Vector2(-12, -8));
        var ph = phGo.AddComponent<TextMeshProUGUI>();
        ph.text = placeholder;
        ph.fontSize = 20;
        ph.color = TextMuted;
        ph.fontStyle = FontStyles.Italic;
        ph.alignment = TextAlignmentOptions.MidlineLeft;
        ApplyFont(ph);

        input.textComponent = text;
        input.placeholder = ph;
        input.textViewport = textGo.GetComponent<RectTransform>();
        if (multiline)
            input.lineType = TMP_InputField.LineType.MultiLineNewline;

        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(400, multiline ? 120 : 44);
        return input;
    }

    public static ScrollRect AddScroll(Transform parent, out RectTransform content)
    {
        var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        scrollGo.transform.SetParent(parent, false);
        StretchFull(scrollGo.GetComponent<RectTransform>());

        var scrollImg = scrollGo.GetComponent<Image>();
        scrollImg.color = new Color(0, 0, 0, 0.02f);
        var scroll = scrollGo.GetComponent<ScrollRect>();

        var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewport.transform.SetParent(scrollGo.transform, false);
        viewport.GetComponent<Image>().color = Color.white;
        viewport.GetComponent<Mask>().showMaskGraphic = false;
        StretchFull(viewport.GetComponent<RectTransform>());

        var contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        contentGo.transform.SetParent(viewport.transform, false);
        content = contentGo.GetComponent<RectTransform>();
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = new Vector2(1, 1);
        content.pivot = new Vector2(0.5f, 1);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(0, 0);

        var vlg = contentGo.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 12;
        vlg.padding = new RectOffset(16, 16, 16, 16);
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        var fitter = contentGo.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = viewport.GetComponent<RectTransform>();
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        return scroll;
    }

    public static RectTransform CreateToolbar(Transform parent, float height = 56f)
    {
        var toolbar = new GameObject("Toolbar", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        toolbar.transform.SetParent(parent, false);
        var tRt = toolbar.GetComponent<RectTransform>();
        tRt.anchorMin = new Vector2(0, 1);
        tRt.anchorMax = new Vector2(1, 1);
        tRt.pivot = new Vector2(0.5f, 1);
        tRt.anchoredPosition = Vector2.zero;
        tRt.sizeDelta = new Vector2(0, height);

        var tLe = toolbar.GetComponent<LayoutElement>();
        tLe.minHeight = height;
        tLe.preferredHeight = height;
        tLe.flexibleHeight = 0f;

        var hlg = toolbar.GetComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(12, 12, 8, 8);
        hlg.spacing = 10;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        return tRt;
    }

    public static RectTransform CreateBodyHost(Transform parent, float topInset = 56f)
    {
        var host = new GameObject("BodyHost", typeof(RectTransform));
        host.transform.SetParent(parent, false);
        var rt = host.GetComponent<RectTransform>();
        StretchFull(rt, Vector2.zero, new Vector2(0, -topInset));
        return rt;
    }

    public static void AddLayoutHeight(GameObject go, float height, float flexibleWidth = 0f)
    {
        var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
        le.minHeight = height;
        le.preferredHeight = height;
        le.flexibleHeight = 0f;
        if (flexibleWidth > 0f)
        {
            le.flexibleWidth = flexibleWidth;
            le.minWidth = 100f;
        }
    }

    public static void StretchFull(RectTransform rt, Vector2 offsetMin = default, Vector2 offsetMax = default)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
    }
}
