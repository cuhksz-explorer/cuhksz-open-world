using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 运行时/编辑器构建 LLM NPC 对话 Canvas（视觉参考 StoryDialogueUI 底部条）。
/// </summary>
public static class LlmNpcDialogueUiFactory
{
    const string DefaultFontResourcesPath = "Fonts & Materials/AlibabaPuHuiTi-3-75-SemiBold SDF";
    const int SortingOrder = 960;

    public struct BuiltUi
    {
        public GameObject root;
        public Canvas canvas;
        public GameObject dialoguePanel;
        public LLMChatClient_LocalQwen chatClient;
        public Button closeButton;
        public TMP_InputField inputField;
        public TMP_Text resultText;
    }

    public static BuiltUi BuildUnder(Transform parent, string npcDisplayName)
    {
        var rootGo = new GameObject("LlmNpcDialogueCanvas");
        rootGo.transform.SetParent(parent, false);
        rootGo.transform.localPosition = Vector3.zero;
        rootGo.transform.localRotation = Quaternion.identity;
        rootGo.transform.localScale = Vector3.one;

        var canvas = rootGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;

        var scaler = rootGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        rootGo.AddComponent<GraphicRaycaster>();

        var rootRt = rootGo.GetComponent<RectTransform>();
        StretchFull(rootRt);

        var font = Resources.Load<TMP_FontAsset>(DefaultFontResourcesPath);

        var bottomPanel = CreatePanel("LlmDialogueBottomPanel", rootRt, new Color(0.03f, 0.025f, 0.02f, 0.42f));
        AnchorStretch(bottomPanel, 0.03f, 0.035f, 0.97f, 0.38f, 0f, 0f, 0f, 0f);
        AddOutline(bottomPanel.gameObject, new Color(1f, 0.74f, 0.36f, 0.18f), new Vector2(1.4f, -1.4f));

        string title = string.IsNullOrWhiteSpace(npcDisplayName)
            ? Loc.Choose("与 NPC 对话", "Talk to NPC")
            : Loc.F("与 {0} 对话", "Talk to {0}", npcDisplayName);

        var titleText = CreateTmp("Title", bottomPanel, title, 22, FontStyles.Bold, TextAlignmentOptions.Center, font);
        titleText.color = new Color(1f, 0.84f, 0.54f, 0.95f);
        AnchorStretch(titleText.rectTransform, 0f, 0.88f, 1f, 1f, 48f, -8f, -48f, -4f);

        var scrollGo = new GameObject("ResultScroll");
        scrollGo.transform.SetParent(bottomPanel, false);
        var scrollRt = scrollGo.AddComponent<RectTransform>();
        AnchorStretch(scrollRt, 0f, 0.38f, 1f, 0.86f, 48f, 8f, -48f, -8f);
        var scroll = scrollGo.AddComponent<ScrollRect>();
        scroll.horizontal = false;

        var viewport = CreatePanel("Viewport", scrollRt, new Color(0f, 0f, 0f, 0.12f));
        StretchFull(viewport);
        scroll.viewport = viewport;

        var contentRt = new GameObject("Content").AddComponent<RectTransform>();
        contentRt.SetParent(viewport, false);
        contentRt.anchorMin = new Vector2(0f, 1f);
        contentRt.anchorMax = new Vector2(1f, 1f);
        contentRt.pivot = new Vector2(0.5f, 1f);
        contentRt.offsetMin = new Vector2(12f, 0f);
        contentRt.offsetMax = new Vector2(-12f, 0f);
        scroll.content = contentRt;

        var resultText = CreateTmp("ResultText", contentRt, Loc.Choose("（在此显示 NPC 回复）", "(NPC reply appears here)"), 26, FontStyles.Normal, TextAlignmentOptions.TopLeft, font);
        resultText.color = Color.white;
        var resultLe = resultText.gameObject.AddComponent<LayoutElement>();
        resultLe.minHeight = 120f;
        var contentFitter = contentRt.gameObject.AddComponent<ContentSizeFitter>();
        contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var resultFitter = resultText.gameObject.AddComponent<ContentSizeFitter>();
        resultFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var inputRow = CreatePanel("InputRow", bottomPanel, new Color(0f, 0f, 0f, 0f));
        AnchorStretch(inputRow, 0.04f, 0.12f, 0.96f, 0.36f, 0f, 0f, 0f, 0f);

        var inputBg = CreatePanel("QuestionInputBg", inputRow, new Color(0.12f, 0.1f, 0.08f, 0.55f));
        AnchorStretch(inputBg, 0f, 0f, 0.72f, 1f, 0f, 0f, -8f, 0f);

        var inputGo = new GameObject("QuestionInput");
        inputGo.transform.SetParent(inputBg, false);
        var inputRt = inputGo.AddComponent<RectTransform>();
        StretchFull(inputRt);
        var inputImg = inputGo.AddComponent<Image>();
        inputImg.color = new Color(1f, 1f, 1f, 0.06f);
        var inputField = inputGo.AddComponent<TMP_InputField>();

        var inputTextArea = new GameObject("Text Area");
        inputTextArea.transform.SetParent(inputGo.transform, false);
        var textAreaRt = inputTextArea.AddComponent<RectTransform>();
        StretchFull(textAreaRt);

        var placeholder = CreateTmp("Placeholder", textAreaRt, Loc.Choose("输入你想问的话…", "Type your question…"), 22, FontStyles.Italic, TextAlignmentOptions.MidlineLeft, font);
        placeholder.color = new Color(1f, 1f, 1f, 0.35f);
        StretchFull(placeholder.rectTransform);
        placeholder.rectTransform.offsetMin = new Vector2(14f, 6f);
        placeholder.rectTransform.offsetMax = new Vector2(-14f, -6f);

        var inputText = CreateTmp("Text", textAreaRt, "", 22, FontStyles.Normal, TextAlignmentOptions.MidlineLeft, font);
        StretchFull(inputText.rectTransform);
        inputText.rectTransform.offsetMin = new Vector2(14f, 6f);
        inputText.rectTransform.offsetMax = new Vector2(-14f, -6f);

        inputField.textViewport = textAreaRt;
        inputField.textComponent = inputText;
        inputField.placeholder = placeholder;

        var sendBtn = CreateButton("SendButton", inputRow, Loc.Choose("发送", "Send"), font, new Color(0.34f, 0.29f, 0.24f, 0.85f));
        AnchorStretch(sendBtn.GetComponent<RectTransform>(), 0.74f, 0.05f, 0.9f, 0.95f, 0f, 0f, 0f, 0f);

        var closeBtn = CreateButton("CloseButton", inputRow, Loc.Choose("关闭 (Esc)", "Close (Esc)"), font, new Color(0.28f, 0.22f, 0.18f, 0.75f));
        AnchorStretch(closeBtn.GetComponent<RectTransform>(), 0.91f, 0.05f, 1f, 0.95f, 0f, 0f, 0f, 0f);

        var hint = CreateTmp("Hint", bottomPanel, Loc.Choose("按 Enter 发送 · Esc 关闭", "Enter to send · Esc to close"), 16, FontStyles.Normal, TextAlignmentOptions.Right, font);
        hint.color = new Color(1f, 1f, 1f, 0.55f);
        AnchorStretch(hint.rectTransform, 0.5f, 0f, 1f, 0.12f, 0f, 4f, -24f, 0f);

        var chat = rootGo.AddComponent<LLMChatClient_LocalQwen>();
        chat.inputField = inputField;
        chat.resultText = resultText;
        chat.sendButton = sendBtn;

        rootGo.SetActive(false);
        bottomPanel.gameObject.SetActive(true);

        return new BuiltUi
        {
            root = rootGo,
            canvas = canvas,
            dialoguePanel = bottomPanel.gameObject,
            chatClient = chat,
            closeButton = closeBtn,
            inputField = inputField,
            resultText = resultText
        };
    }

    static RectTransform CreatePanel(string name, Transform parent, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        return go.GetComponent<RectTransform>();
    }

    static TextMeshProUGUI CreateTmp(string name, Transform parent, string text, float size, FontStyles style,
        TextAlignmentOptions align, TMP_FontAsset font)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        if (font != null)
            tmp.font = font;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.alignment = align;
        tmp.enableWordWrapping = true;
        return tmp;
    }

    static Button CreateButton(string name, Transform parent, string label, TMP_FontAsset font, Color bg)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = bg;
        var btn = go.AddComponent<Button>();
        var txt = CreateTmp("Label", go.transform, label, 20, FontStyles.Bold, TextAlignmentOptions.Center, font);
        txt.color = new Color(1f, 0.96f, 0.88f, 0.96f);
        StretchFull(txt.rectTransform);
        return btn;
    }

    static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void AnchorStretch(RectTransform rt, float xmin, float ymin, float xmax, float ymax,
        float left, float bottom, float right, float top)
    {
        rt.anchorMin = new Vector2(xmin, ymin);
        rt.anchorMax = new Vector2(xmax, ymax);
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
    }

    static void AddOutline(GameObject go, Color color, Vector2 distance)
    {
        var outline = go.GetComponent<Outline>() ?? go.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = distance;
    }
}
