using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Loading 场景登录成功后，从屏幕顶部滑下的提示条（5:1 宽长比，黑白金配色与 LGU 登录面板一致）。
/// </summary>
public class LguHubLoginSuccessBanner : MonoBehaviour
{
    const string FontResourcesPath = "Fonts & Materials/AlibabaPuHuiTi-3-75-SemiBold SDF";
    const float BannerWidth = 750f;
    const float BannerAspect = 5f;
    const float TopMargin = 28f;
    const float SlideDuration = 0.42f;
    const float HoldDuration = 1f;
    const float HiddenExtraOffset = 36f;

    [SerializeField] int canvasSortOrder = 32003;
    [SerializeField] int messageFontSize = 34;

    RectTransform _bannerRect;
    CanvasGroup _canvasGroup;
    TMP_Text _messageText;
    Coroutine _animRoutine;
    bool _built;

    public bool IsVisible { get; private set; }

    void Awake()
    {
        EnsureBuilt();
    }

    public void Show(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
            return;

        EnsureBuilt();
        if (_bannerRect == null || _messageText == null)
            return;

        gameObject.SetActive(true);
        _messageText.text = BuildMessage(username);

        if (_animRoutine != null)
            StopCoroutine(_animRoutine);
        _animRoutine = StartCoroutine(CoShowAndRetract());
    }

    public void Hide(bool immediate = false)
    {
        if (!_built || _bannerRect == null)
        {
            IsVisible = false;
            return;
        }

        if (_animRoutine != null)
        {
            StopCoroutine(_animRoutine);
            _animRoutine = null;
        }

        if (immediate || !gameObject.activeInHierarchy)
        {
            float hiddenY = BannerHeight + HiddenExtraOffset;
            _bannerRect.anchoredPosition = new Vector2(0f, hiddenY);
            if (_canvasGroup != null)
                _canvasGroup.alpha = 0f;
            IsVisible = false;
            gameObject.SetActive(false);
            return;
        }

        _animRoutine = StartCoroutine(CoSlideOut());
    }

    float BannerHeight => BannerWidth / BannerAspect;

    static string BuildMessage(string username)
    {
        string name = username.Trim();
        string gold = ColorUtility.ToHtmlStringRGB(LguHubAuthUILayout.Gold);
        return $"<color=#{gold}>{name}</color><color=#{ColorUtility.ToHtmlStringRGB(LguHubAuthUILayout.TextBlack)}> 已经登陆成功</color>";
    }

    void EnsureBuilt()
    {
        if (_built)
            return;
        _built = true;

        var canvas = gameObject.GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }
        canvas.sortingOrder = canvasSortOrder;

        if (gameObject.GetComponent<CanvasScaler>() == null)
        {
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
        }

        _canvasGroup = gameObject.GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = false;

        var outer = new GameObject("BannerOuter", typeof(RectTransform), typeof(Image));
        outer.transform.SetParent(transform, false);
        _bannerRect = outer.GetComponent<RectTransform>();
        _bannerRect.anchorMin = new Vector2(0.5f, 1f);
        _bannerRect.anchorMax = new Vector2(0.5f, 1f);
        _bannerRect.pivot = new Vector2(0.5f, 1f);
        _bannerRect.sizeDelta = new Vector2(BannerWidth, BannerHeight);
        _bannerRect.anchoredPosition = new Vector2(0f, BannerHeight + HiddenExtraOffset);

        var outerImage = outer.GetComponent<Image>();
        outerImage.color = LguHubAuthUILayout.Gold;
        outerImage.raycastTarget = false;

        var inner = new GameObject("BannerInner", typeof(RectTransform), typeof(Image));
        inner.transform.SetParent(outer.transform, false);
        var innerRt = inner.GetComponent<RectTransform>();
        innerRt.anchorMin = Vector2.zero;
        innerRt.anchorMax = Vector2.one;
        innerRt.offsetMin = new Vector2(2f, 2f);
        innerRt.offsetMax = new Vector2(-2f, -2f);
        var innerImage = inner.GetComponent<Image>();
        innerImage.color = LguHubAuthUILayout.BgWhite;
        innerImage.raycastTarget = false;

        var labelGo = new GameObject("Message", typeof(RectTransform));
        labelGo.transform.SetParent(inner.transform, false);
        var labelRt = labelGo.GetComponent<RectTransform>();
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = new Vector2(24f, 0f);
        labelRt.offsetMax = new Vector2(-24f, 0f);

        _messageText = labelGo.AddComponent<TextMeshProUGUI>();
        _messageText.alignment = TextAlignmentOptions.Center;
        _messageText.fontSize = messageFontSize;
        _messageText.color = LguHubAuthUILayout.TextBlack;
        _messageText.enableWordWrapping = false;
        _messageText.overflowMode = TextOverflowModes.Ellipsis;
        _messageText.raycastTarget = false;
        _messageText.richText = true;

        var font = Resources.Load<TMP_FontAsset>(FontResourcesPath);
        if (font != null)
            _messageText.font = font;

        _canvasGroup.alpha = 0f;
        gameObject.SetActive(false);
    }

    IEnumerator CoShowAndRetract()
    {
        float hiddenY = BannerHeight + HiddenExtraOffset;
        float shownY = -TopMargin;
        float elapsed = 0f;

        while (elapsed < SlideDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / SlideDuration);
            t = 1f - Mathf.Pow(1f - t, 3f);
            float y = Mathf.Lerp(hiddenY, shownY, t);
            _bannerRect.anchoredPosition = new Vector2(0f, y);
            if (_canvasGroup != null)
                _canvasGroup.alpha = t;
            yield return null;
        }

        _bannerRect.anchoredPosition = new Vector2(0f, shownY);
        if (_canvasGroup != null)
            _canvasGroup.alpha = 1f;
        IsVisible = true;

        yield return new WaitForSecondsRealtime(HoldDuration);

        elapsed = 0f;
        while (elapsed < SlideDuration * 0.85f)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / (SlideDuration * 0.85f));
            t = t * t;
            float y = Mathf.Lerp(shownY, hiddenY, t);
            _bannerRect.anchoredPosition = new Vector2(0f, y);
            if (_canvasGroup != null)
                _canvasGroup.alpha = 1f - t;
            yield return null;
        }

        Hide(immediate: true);
        _animRoutine = null;
    }

    IEnumerator CoSlideOut()
    {
        float hiddenY = BannerHeight + HiddenExtraOffset;
        float startY = _bannerRect.anchoredPosition.y;
        float elapsed = 0f;

        while (elapsed < SlideDuration * 0.85f)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / (SlideDuration * 0.85f));
            t = t * t;
            float y = Mathf.Lerp(startY, hiddenY, t);
            _bannerRect.anchoredPosition = new Vector2(0f, y);
            if (_canvasGroup != null)
                _canvasGroup.alpha = 1f - t;
            yield return null;
        }

        Hide(immediate: true);
        _animRoutine = null;
    }
}
