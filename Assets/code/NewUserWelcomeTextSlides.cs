using System;
using System.Collections;
using System.Security.Cryptography;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 新账号注册并完成角色/昵称设置后，在进入游戏加载层之前播放四段全屏黑底白字（仅播一次/用户）。
/// </summary>
[DefaultExecutionOrder(-45)]
public class NewUserWelcomeTextSlides : MonoBehaviour
{
    public static NewUserWelcomeTextSlides Instance { get; private set; }

    public static bool IsPlaying { get; private set; }

    const string PrefsPrefix = "NewUserWelcomeSlidesWatched_v1_";
    const string FontResourcesPath = "Fonts & Materials/AlibabaPuHuiTi-3-75-SemiBold SDF";

    static readonly string[] DefaultLinesZh =
    {
        "欢迎来到香港中文大学（深圳）",
        "作为一名（大一/研一/博一新生），你有很多东西需要探索，也有很多东西需要学习",
        "博文约礼，港中深已经度过了它的第十年，欢迎加入这个大家庭",
        "请尽情探索整个校园吧",
    };

    static readonly string[] DefaultLinesEn =
    {
        "Welcome to The Chinese University of Hong Kong, Shenzhen",
        "As a new undergraduate, graduate, or doctoral student, there is much to explore and learn",
        "Through breadth and depth — CUHK-SZ marks its tenth year. Welcome to the family",
        "Feel free to explore the entire campus",
    };

    [Header("文案")]
    [SerializeField] string[] lines;

    [Header("样式")]
    [SerializeField] int canvasSortOrder = 32755;
    [SerializeField] float messageFontSize = 80f;
    [SerializeField] Color backgroundColor = Color.black;
    [SerializeField] Color textColor = Color.white;
    [Tooltip("左右留白占屏宽比例")]
    [Range(0.04f, 0.2f)]
    [SerializeField] float horizontalPaddingRatio = 0.08f;
    [Tooltip("上下留白占屏高比例")]
    [Range(0.04f, 0.2f)]
    [SerializeField] float verticalPaddingRatio = 0.1f;

    [Header("行为")]
    [SerializeField] bool dontDestroyOnLoad = true;
    [SerializeField] bool pauseTimeScaleWhilePlaying = true;
    [SerializeField] bool allowAutoAdvance = true;
    [SerializeField] float autoAdvanceSeconds = 6f;
    [SerializeField] string advanceHint = "点击任意处继续";

    Canvas _canvas;
    CanvasGroup _canvasGroup;
    Image _background;
    TMP_Text _messageText;
    TMP_Text _hintText;
    bool _built;

    Coroutine _playRoutine;
    Action _onComplete;
    string _activeUsername;
    float _savedTimeScale = 1f;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (dontDestroyOnLoad)
            DontDestroyOnLoad(gameObject);

        EnsureBuilt();
        SetVisible(false);
    }

    void OnEnable() => Loc.OnLanguageChanged += OnLanguageChanged;
    void OnDisable() => Loc.OnLanguageChanged -= OnLanguageChanged;
    void OnLanguageChanged() => RefreshLocalizedUi();

    void RefreshLocalizedUi()
    {
        if (_hintText != null)
            _hintText.text = Loc.Choose(advanceHint, "Click anywhere to continue");
        if (IsPlaying && _messageText != null)
        {
            var lines = GetLines();
            // keep current slide index approximated by not changing mid-play unless needed
        }
    }

    void OnDestroy()
    {
        Loc.OnLanguageChanged -= OnLanguageChanged;
        if (Instance == this)
            Instance = null;
    }

    public static bool ShouldPlay(string username)
    {
        return !string.IsNullOrWhiteSpace(username) && !HasWatched(username);
    }

    public static void Play(string username, Action onComplete)
    {
        if (!ShouldPlay(username))
        {
            onComplete?.Invoke();
            return;
        }

        EnsureInstance();
        Instance.InternalPlay(username, onComplete);
    }

    /// <summary>按 P 切换账号时中断欢迎幻灯片并恢复时间缩放。</summary>
    public static void ForceStopIfPlaying()
    {
        if (!IsPlaying || Instance == null)
            return;

        if (Instance._playRoutine != null)
        {
            Instance.StopCoroutine(Instance._playRoutine);
            Instance._playRoutine = null;
        }

        Instance.SetVisible(false);
        Instance.RestoreTimeScale();
        IsPlaying = false;
        Instance._onComplete = null;
        Instance._activeUsername = "";
    }

    static void EnsureInstance()
    {
        if (Instance != null)
            return;

        var go = new GameObject("NewUserWelcomeTextSlides");
        go.AddComponent<NewUserWelcomeTextSlides>();
    }

    void InternalPlay(string username, Action onComplete)
    {
        if (_playRoutine != null)
            StopCoroutine(_playRoutine);

        _activeUsername = username?.Trim() ?? "";
        _onComplete = onComplete;
        _playRoutine = StartCoroutine(CoPlaySlides());
    }

    IEnumerator CoPlaySlides()
    {
        IsPlaying = true;
        if (pauseTimeScaleWhilePlaying)
        {
            _savedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }

        EnsureBuilt();
        SetVisible(true);

        var scriptLines = GetLines();
        for (int i = 0; i < scriptLines.Length; i++)
        {
            if (_messageText != null)
                _messageText.text = scriptLines[i];

            float elapsed = 0f;
            bool advanced = false;
            while (!advanced)
            {
                if (AnyAdvanceInputDown())
                    advanced = true;
                else if (allowAutoAdvance && autoAdvanceSeconds > 0f)
                {
                    elapsed += Time.unscaledDeltaTime;
                    if (elapsed >= autoAdvanceSeconds)
                        advanced = true;
                }

                yield return null;
            }
        }

        MarkWatched(_activeUsername);
        SetVisible(false);
        RestoreTimeScale();
        IsPlaying = false;

        var cb = _onComplete;
        _onComplete = null;
        _playRoutine = null;
        cb?.Invoke();
    }

    static bool AnyAdvanceInputDown()
    {
        if (Input.anyKeyDown)
            return true;
        if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
            return true;
        if (Input.touchCount > 0)
        {
            var t = Input.GetTouch(0);
            if (t.phase == TouchPhase.Began)
                return true;
        }

        return false;
    }

    string[] GetLines()
    {
        if (lines != null && lines.Length > 0)
        {
            var copy = new string[lines.Length];
            for (int i = 0; i < lines.Length; i++)
                copy[i] = lines[i] ?? "";
            return copy;
        }

        return Loc.IsEnglish ? DefaultLinesEn : DefaultLinesZh;
    }

    void EnsureBuilt()
    {
        if (_built)
            return;
        _built = true;

        _canvas = gameObject.GetComponent<Canvas>();
        if (_canvas == null)
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }
        _canvas.sortingOrder = canvasSortOrder;

        var scaler = gameObject.GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
        }

        if (gameObject.GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();

        _canvasGroup = gameObject.GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();

        var bgGo = new GameObject("Background");
        bgGo.transform.SetParent(transform, false);
        var bgRt = bgGo.AddComponent<RectTransform>();
        StretchFull(bgRt);
        _background = bgGo.AddComponent<Image>();
        _background.color = backgroundColor;
        _background.raycastTarget = true;

        var msgGo = new GameObject("Message");
        msgGo.transform.SetParent(transform, false);
        var msgRt = msgGo.AddComponent<RectTransform>();
        StretchWithPadding(msgRt, horizontalPaddingRatio, verticalPaddingRatio);
        _messageText = msgGo.AddComponent<TextMeshProUGUI>();
        _messageText.alignment = TextAlignmentOptions.Center;
        _messageText.horizontalAlignment = HorizontalAlignmentOptions.Center;
        _messageText.verticalAlignment = VerticalAlignmentOptions.Middle;
        _messageText.enableWordWrapping = true;
        _messageText.fontSize = messageFontSize;
        _messageText.color = textColor;
        _messageText.raycastTarget = false;
        TryApplyFont(_messageText);

        var hintGo = new GameObject("Hint");
        hintGo.transform.SetParent(transform, false);
        var hintRt = hintGo.AddComponent<RectTransform>();
        hintRt.anchorMin = new Vector2(0.5f, 0f);
        hintRt.anchorMax = new Vector2(0.5f, 0f);
        hintRt.pivot = new Vector2(0.5f, 0f);
        hintRt.sizeDelta = new Vector2(900f, 48f);
        hintRt.anchoredPosition = new Vector2(0f, 48f);
        _hintText = hintGo.AddComponent<TextMeshProUGUI>();
        _hintText.text = Loc.Choose(advanceHint, "Click anywhere to continue");
        _hintText.fontSize = 28f;
        _hintText.alignment = TextAlignmentOptions.Center;
        _hintText.color = new Color(0.75f, 0.75f, 0.75f, 0.9f);
        _hintText.raycastTarget = false;
        TryApplyFont(_hintText);
    }

    static void TryApplyFont(TMP_Text tmp)
    {
        var font = Resources.Load<TMP_FontAsset>(FontResourcesPath);
        if (font == null || tmp == null)
            return;
        tmp.font = font;
        tmp.fontSharedMaterial = font.material;
    }

    static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static void StretchWithPadding(RectTransform rt, float padXRatio, float padYRatio)
    {
        rt.anchorMin = new Vector2(padXRatio, padYRatio);
        rt.anchorMax = new Vector2(1f - padXRatio, 1f - padYRatio);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    void SetVisible(bool visible)
    {
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = visible ? 1f : 0f;
            _canvasGroup.interactable = visible;
            _canvasGroup.blocksRaycasts = visible;
        }
    }

    void RestoreTimeScale()
    {
        if (!pauseTimeScaleWhilePlaying)
            return;
        Time.timeScale = Mathf.Approximately(_savedTimeScale, 0f) ? 1f : _savedTimeScale;
    }

    public static bool HasWatched(string username)
    {
        string key = PrefsKeyForUser(username);
        return !string.IsNullOrEmpty(key) && PlayerPrefs.GetInt(key, 0) == 1;
    }

    static void MarkWatched(string username)
    {
        string key = PrefsKeyForUser(username);
        if (string.IsNullOrEmpty(key))
            return;
        PlayerPrefs.SetInt(key, 1);
        PlayerPrefs.Save();
    }

    static string PrefsKeyForUser(string username)
    {
        if (string.IsNullOrEmpty(username))
            return null;
        using (var sha1 = SHA1.Create())
        {
            var bytes = Encoding.UTF8.GetBytes(username);
            var hash = sha1.ComputeHash(bytes);
            var sb = new StringBuilder(16);
            for (int i = 0; i < 8; i++)
                sb.Append(hash[i].ToString("x2"));
            return PrefsPrefix + sb;
        }
    }
}
