using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 登录面板上的协议/隐私弹层（可滚动正文，打开时从第一条开始显示）。
/// </summary>
public class LguHubAuthLegalDialog : MonoBehaviour
{
    const string FontResourcesPath = "Fonts & Materials/AlibabaPuHuiTi-3-75-SemiBold SDF";
    const string OverlayName = "LegalDialogOverlay";

    ScrollRect _scrollRect;
    RectTransform _contentRoot;
    TMP_Text _bodyText;
    TMP_Text _titleText;

    public static void Show(Transform host, string title, string body)
    {
        if (host == null)
            return;

        var existing = host.Find(OverlayName);
        if (existing != null)
        {
            var dlg = existing.GetComponent<LguHubAuthLegalDialog>();
            if (dlg != null)
            {
                dlg.Open(title, body);
                return;
            }
            Object.Destroy(existing.gameObject);
        }

        var go = new GameObject(OverlayName, typeof(RectTransform));
        go.transform.SetParent(host, false);
        var rt = go.GetComponent<RectTransform>();
        Stretch(rt);

        var dialog = go.AddComponent<LguHubAuthLegalDialog>();
        dialog.Build(rt);
        dialog.Open(title, body);
    }

    void Build(RectTransform root)
    {
        var dim = CreateChild(root, "Dim", Vector2.zero, Vector2.one);
        var dimImg = dim.gameObject.AddComponent<Image>();
        dimImg.color = new Color(0f, 0f, 0f, 0.45f);
        dimImg.raycastTarget = true;

        var card = CreateChild(root, "Card", new Vector2(0.08f, 0.1f), new Vector2(0.92f, 0.9f));
        var cardImg = card.gameObject.AddComponent<Image>();
        cardImg.color = Color.white;
        cardImg.raycastTarget = true;

        var titleBar = CreateChild(card, "TitleBar", new Vector2(0f, 0.88f), new Vector2(1f, 1f));
        var titleBg = titleBar.gameObject.AddComponent<Image>();
        titleBg.color = new Color(0.96f, 0.96f, 0.96f, 1f);
        titleBg.raycastTarget = true;

        var titleGo = new GameObject("Title", typeof(RectTransform));
        titleGo.transform.SetParent(titleBar, false);
        Stretch(titleGo.GetComponent<RectTransform>(), new Vector2(16f, 0f), new Vector2(-56f, 0f));
        _titleText = titleGo.AddComponent<TextMeshProUGUI>();
        _titleText.fontSize = 22;
        _titleText.fontStyle = FontStyles.Bold;
        _titleText.color = new Color(0.12f, 0.12f, 0.12f, 1f);
        _titleText.alignment = TextAlignmentOptions.MidlineLeft;
        _titleText.raycastTarget = false;
        ApplyFont(_titleText);

        var closeBtn = CreateCloseButton(titleBar);
        closeBtn.onClick.AddListener(Close);

        var scrollRoot = CreateChild(card, "Scroll", new Vector2(0f, 0f), new Vector2(1f, 0.88f));
        _scrollRect = scrollRoot.gameObject.AddComponent<ScrollRect>();
        _scrollRect.horizontal = false;
        _scrollRect.vertical = true;
        _scrollRect.movementType = ScrollRect.MovementType.Elastic;
        _scrollRect.scrollSensitivity = 30f;
        _scrollRect.inertia = true;

        var viewport = CreateChild(scrollRoot, "Viewport", Vector2.zero, Vector2.one);
        var vpImg = viewport.gameObject.AddComponent<Image>();
        vpImg.color = Color.white;
        vpImg.raycastTarget = true;
        viewport.gameObject.AddComponent<RectMask2D>();

        _contentRoot = CreateChild(viewport, "Content", new Vector2(0f, 1f), new Vector2(1f, 1f));
        _contentRoot.pivot = new Vector2(0.5f, 1f);
        _contentRoot.anchorMin = new Vector2(0f, 1f);
        _contentRoot.anchorMax = new Vector2(1f, 1f);
        _contentRoot.anchoredPosition = Vector2.zero;
        _contentRoot.sizeDelta = new Vector2(0f, 0f);

        var contentLe = _contentRoot.gameObject.AddComponent<LayoutElement>();
        contentLe.minWidth = 0f;
        contentLe.flexibleWidth = 1f;

        var contentFitter = _contentRoot.gameObject.AddComponent<ContentSizeFitter>();
        contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var bodyGo = new GameObject("Body", typeof(RectTransform));
        bodyGo.transform.SetParent(_contentRoot, false);
        var bodyRt = bodyGo.GetComponent<RectTransform>();
        bodyRt.anchorMin = new Vector2(0f, 1f);
        bodyRt.anchorMax = new Vector2(1f, 1f);
        bodyRt.pivot = new Vector2(0.5f, 1f);
        bodyRt.anchoredPosition = Vector2.zero;
        bodyRt.offsetMin = new Vector2(20f, 0f);
        bodyRt.offsetMax = new Vector2(-20f, 0f);

        _bodyText = bodyGo.AddComponent<TextMeshProUGUI>();
        _bodyText.fontSize = 17;
        _bodyText.color = new Color(0.25f, 0.25f, 0.25f, 1f);
        _bodyText.alignment = TextAlignmentOptions.TopLeft;
        _bodyText.enableWordWrapping = true;
        _bodyText.overflowMode = TextOverflowModes.Overflow;
        _bodyText.raycastTarget = false;
        ApplyFont(_bodyText);

        var bodyFitter = bodyGo.AddComponent<ContentSizeFitter>();
        bodyFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        bodyFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        _scrollRect.viewport = viewport;
        _scrollRect.content = _contentRoot;
    }

    void Open(string title, string body)
    {
        if (_titleText != null)
            _titleText.text = title;
        if (_bodyText != null)
            _bodyText.text = body;

        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        Canvas.ForceUpdateCanvases();
        if (_contentRoot != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(_contentRoot);
        ScrollToTop();
    }

    void ScrollToTop()
    {
        if (_scrollRect == null)
            return;

        _scrollRect.velocity = Vector2.zero;
        _scrollRect.StopMovement();
        Canvas.ForceUpdateCanvases();
        if (_contentRoot != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(_contentRoot);

        // 1 = 顶部，0 = 底部
        _scrollRect.verticalNormalizedPosition = 1f;
    }

    void Close()
    {
        gameObject.SetActive(false);
    }

    static Button CreateCloseButton(RectTransform parent)
    {
        var go = new GameObject("Btn_Close", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 0.5f);
        rt.anchorMax = new Vector2(1f, 0.5f);
        rt.pivot = new Vector2(1f, 0.5f);
        rt.sizeDelta = new Vector2(44f, 44f);
        rt.anchoredPosition = new Vector2(-6f, 0f);

        var img = go.AddComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0.01f);
        img.raycastTarget = true;
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;

        var tgo = new GameObject("Text", typeof(RectTransform));
        tgo.transform.SetParent(go.transform, false);
        Stretch(tgo.GetComponent<RectTransform>());
        var txt = tgo.AddComponent<TextMeshProUGUI>();
        txt.text = "×";
        txt.fontSize = 28;
        txt.alignment = TextAlignmentOptions.Center;
        txt.color = new Color(0.35f, 0.35f, 0.35f, 1f);
        txt.raycastTarget = false;
        ApplyFont(txt);
        return btn;
    }

    static RectTransform CreateChild(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return rt;
    }

    static void Stretch(RectTransform rt, Vector2 offsetMin = default, Vector2 offsetMax = default)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
    }

    static void ApplyFont(TMP_Text tmp)
    {
        var f = Resources.Load<TMP_FontAsset>(FontResourcesPath);
        if (f != null)
            tmp.font = f;
    }
}
