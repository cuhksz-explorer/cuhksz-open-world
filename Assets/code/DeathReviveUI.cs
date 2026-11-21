using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 全屏死亡提示：倒计时结束后可点击「复活」。开启期间 <see cref="BlocksGameplayInput"/> 为真，用于屏蔽移动/视角等。
/// </summary>
public class DeathReviveUI : MonoBehaviour
{
    const string TmpFontResource = "Fonts & Materials/AlibabaPuHuiTi-3-75-SemiBold SDF";

    public static bool BlocksGameplayInput { get; private set; }

    static DeathReviveUI _instance;

    Canvas _canvas;
    CanvasGroup _canvasGroup;
    TextMeshProUGUI _titleText;
    TextMeshProUGUI _deathReasonText;
    TextMeshProUGUI _countdownText;
    TextMeshProUGUI _hintText;
    Button _reviveButton;

    public static DeathReviveUI Ensure()
    {
        if (_instance != null)
            return _instance;

        var go = new GameObject("DeathReviveUI");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<DeathReviveUI>();
        _instance.Build();
        return _instance;
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

    void Build()
    {
        _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 31000;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.55f;
        gameObject.AddComponent<GraphicRaycaster>();

        _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        _canvasGroup.alpha = 0f;
        _canvas.enabled = false;

        var dim = new GameObject("Dim", typeof(RectTransform), typeof(Image));
        dim.transform.SetParent(transform, false);
        var dimRt = dim.GetComponent<RectTransform>();
        dimRt.anchorMin = Vector2.zero;
        dimRt.anchorMax = Vector2.one;
        dimRt.offsetMin = Vector2.zero;
        dimRt.offsetMax = Vector2.zero;
        dim.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.78f);

        var card = new GameObject("Card", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        card.transform.SetParent(transform, false);
        var cardRt = card.GetComponent<RectTransform>();
        // 放大背景卡片：几乎全屏，分辨率变化时自动拉伸。
        cardRt.anchorMin = Vector2.zero;
        cardRt.anchorMax = Vector2.one;
        cardRt.offsetMin = new Vector2(12f, 12f);
        cardRt.offsetMax = new Vector2(-12f, -12f);
        cardRt.pivot = new Vector2(0.5f, 0.5f);
        cardRt.anchoredPosition = Vector2.zero;
        card.GetComponent<Image>().color = new Color(0.07f, 0.08f, 0.11f, 0.97f);

        var vlg = card.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(48, 48, 40, 40);
        vlg.spacing = 22;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlHeight = true;
        vlg.childControlWidth = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        _titleText = CreateLayoutTmp(card.transform, "Title", Loc.Choose("你已阵亡", "You have fallen"), 46, TextAlignmentOptions.Center, 72f, 0f);
        _deathReasonText = CreateLayoutTmp(card.transform, "DeathReason", "", 30, TextAlignmentOptions.Center, 120f, 1f);
        _deathReasonText.enableWordWrapping = true;
        _deathReasonText.overflowMode = TextOverflowModes.Overflow;
        _countdownText = CreateLayoutTmp(card.transform, "Countdown", "", 38, TextAlignmentOptions.Center, 88f, 0f);
        _hintText = CreateLayoutTmp(card.transform, "Hint", "", 24, TextAlignmentOptions.Center, 100f, 0f);
        _hintText.enableWordWrapping = true;

        var btnGo = new GameObject("ReviveButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        btnGo.transform.SetParent(card.transform, false);
        var btnLe = btnGo.GetComponent<LayoutElement>();
        btnLe.preferredHeight = 72f;
        btnLe.minHeight = 64f;
        btnLe.flexibleWidth = 0f;
        btnLe.preferredWidth = 360f;
        var btnRt = btnGo.GetComponent<RectTransform>();
        btnRt.sizeDelta = new Vector2(360f, 72f);
        var btnImg = btnGo.GetComponent<Image>();
        btnImg.color = new Color(0.22f, 0.55f, 0.32f, 1f);
        _reviveButton = btnGo.GetComponent<Button>();
        _reviveButton.targetGraphic = btnImg;
        _reviveButton.interactable = false;

        var labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(btnGo.transform, false);
        var btnLabel = labelGo.AddComponent<TextMeshProUGUI>();
        btnLabel.text = Loc.Choose("复活", "Revive");
        btnLabel.fontSize = 32;
        btnLabel.alignment = TextAlignmentOptions.Center;
        btnLabel.color = Color.white;
        var lr = labelGo.GetComponent<RectTransform>();
        lr.anchorMin = Vector2.zero;
        lr.anchorMax = Vector2.one;
        lr.offsetMin = Vector2.zero;
        lr.offsetMax = Vector2.zero;

        var font = Resources.Load<TMP_FontAsset>(TmpFontResource);
        if (font != null)
        {
            _titleText.font = font;
            _deathReasonText.font = font;
            _countdownText.font = font;
            _hintText.font = font;
            btnLabel.font = font;
        }
    }

    static TextMeshProUGUI CreateLayoutTmp(Transform parent, string name, string text, float fontSize, TextAlignmentOptions align, float preferredHeight, float flexibleHeight)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        var le = go.GetComponent<LayoutElement>();
        le.preferredHeight = preferredHeight;
        le.minHeight = preferredHeight * 0.6f;
        le.flexibleHeight = flexibleHeight;
        le.flexibleWidth = 1f;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = align;
        tmp.color = Color.white;
        tmp.enableWordWrapping = true;
        tmp.raycastTarget = false;

        return tmp;
    }

    static void UnlockCursorForUi()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    static void TryRestoreFpsCursorLock()
    {
        var pm = Object.FindObjectOfType<PlayerMovement>();
        if (pm == null)
            return;
        if (pm.externalFirstPersonLook != null && pm.externalFirstPersonLook.IsActive)
            pm.externalFirstPersonLook.SetCursorLocked(true);
    }

    /// <summary>倒计时（秒）结束后启用复活按钮，点击后执行回调并隐藏。</summary>
    public IEnumerator CoRunDeathPanel(float waitSeconds, int coinCost, string deathReason, System.Action onReviveClicked)
    {
        BlocksGameplayInput = true;
        UnlockCursorForUi();

        try
        {
            _reviveButton.onClick.RemoveAllListeners();
            _reviveButton.interactable = false;
            _countdownText.text = "";

            string reason = string.IsNullOrWhiteSpace(deathReason)
                ? Loc.Choose("未知原因", "Unknown cause")
                : Loc.StoryTerm(deathReason.Trim());
            _deathReasonText.text = Loc.F("死因：{0}", "Cause: {0}", reason);

            _hintText.text = Loc.F(
                "复活需支付 {0} 金币\n不足时金币将清零并仍可复活",
                "Revive costs {0} gold\nIf short, gold resets to 0 but you can still revive",
                coinCost);

            _canvas.enabled = true;
            _canvasGroup.alpha = 1f;
            _canvasGroup.interactable = true;
            _canvasGroup.blocksRaycasts = true;

            float remain = Mathf.Max(0.01f, waitSeconds);
            while (remain > 0f)
            {
                _countdownText.text = Loc.F("请稍候… {0} 秒后可复活", "Please wait… {0}s until revive", Mathf.CeilToInt(remain));
                remain -= Time.unscaledDeltaTime;
                yield return null;
            }
            _countdownText.text = Loc.Choose("可以复活了", "You may revive now");

            bool clicked = false;
            _reviveButton.onClick.AddListener(() => { clicked = true; });
            _reviveButton.interactable = true;

            yield return new WaitUntil(() => clicked);

            _reviveButton.onClick.RemoveAllListeners();
            onReviveClicked?.Invoke();

            _canvas.enabled = false;
            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
        }
        finally
        {
            BlocksGameplayInput = false;
            TryRestoreFpsCursorLock();
        }
    }

    void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
            BlocksGameplayInput = false;
        }
    }
}
