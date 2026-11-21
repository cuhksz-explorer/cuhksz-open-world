using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 全屏黑底白字致谢层：花坛走近浏览（可滚动）与主线完结滚动片尾（两页）。
/// </summary>
[DefaultExecutionOrder(12000)]
public class GameCreditsScrollOverlay : MonoBehaviour
{
    public static GameCreditsScrollOverlay Instance { get; private set; }

    public static bool BlocksGameplayInput =>
        Instance != null && Instance._isOpen && Instance._blocksGameplay;

    public static bool IsBrowsePanelOpen =>
        Instance != null && Instance._isOpen && Instance._mode == DisplayMode.BrowsePanel;

    const string FontResourcesPath = "Fonts & Materials/AlibabaPuHuiTi-3-75-SemiBold SDF";
    const string RootName = "[GameCreditsScrollOverlay]";

    enum DisplayMode
    {
        BrowsePanel,
        MainStoryRoll
    }

    Canvas _canvas;
    CanvasGroup _rootGroup;
    GameObject _browseRoot;
    ScrollRect _browseScroll;
    RectTransform _browseContent;
    TMP_Text _browseTitle;
    TMP_Text _browseBody;

    GameObject _rollRoot;
    RectTransform _rollViewport;
    RectTransform _rollTextRect;
    TMP_Text _rollText;

    GameObject _epilogueRoot;
    TMP_Text _epilogueTitle;
    TMP_Text _epilogueBody;

    DisplayMode _mode;
    bool _isOpen;
    bool _blocksGameplay;
    Coroutine _routine;

    PlayerMovement _playerMovement;
    FirstPersonCamera _firstPersonCamera;
    bool _playerMovementWasEnabled = true;
    bool _playerMovementPausedBefore;
    bool _firstPersonSuppressBefore;
    bool _firstPersonCursorLockedBefore = true;
    CursorLockMode _cursorLockBefore = CursorLockMode.Locked;
    bool _cursorVisibleBefore;
    bool _browseCursorCaptured;
    Coroutine _browseLayoutRoutine;
    Coroutine _browseAutoScrollRoutine;
    float _browseManualPauseUntil;

    const float BrowseAutoScrollPixelsPerSecond = 42f;
    const float BrowseManualPauseSeconds = 2.5f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot() => EnsureExists();

    public static void EnsureExists()
    {
        if (Instance != null)
            return;
        var go = new GameObject(RootName);
        DontDestroyOnLoad(go);
        go.AddComponent<GameCreditsScrollOverlay>();
    }

    /// <summary>花坛等区域：走近后弹出，可滚动阅读。</summary>
    public static void ShowBrowsePanel()
    {
        EnsureExists();
        Instance.OpenBrowsePanel();
    }

    /// <summary>主线全部完成后：滚动片尾 + 支线提示页。</summary>
    public static void ShowMainStoryRoll()
    {
        EnsureExists();
        Instance.OpenMainStoryRoll();
    }

    /// <summary>关闭花坛浏览面板（不影响主线片尾）。</summary>
    public static void CloseBrowseIfOpen()
    {
        if (Instance == null || !Instance._isOpen || Instance._mode != DisplayMode.BrowsePanel)
            return;
        Instance.Close();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildUi();
        SetVisible(false);
    }

    void OnEnable() => Loc.OnLanguageChanged += RefreshLocalizedTexts;
    void OnDisable() => Loc.OnLanguageChanged -= RefreshLocalizedTexts;

    void RefreshLocalizedTexts()
    {
        if (_browseTitle != null && _mode == DisplayMode.BrowsePanel)
            _browseTitle.text = GameCreditsCatalog.Title;
        if (_browseBody != null && _mode == DisplayMode.BrowsePanel)
            _browseBody.text = GameCreditsCatalog.FullCreditsBody;
        if (_rollText != null && _mode == DisplayMode.MainStoryRoll)
            _rollText.text = GameCreditsCatalog.Title + "\n\n" + GameCreditsCatalog.FullCreditsBody;
        if (_epilogueTitle != null)
            _epilogueTitle.text = GameCreditsCatalog.MainStoryEpilogueTitle;
        if (_epilogueBody != null)
            _epilogueBody.text = GameCreditsCatalog.MainStoryEpilogueBody;
    }

    void OnDestroy()
    {
        Loc.OnLanguageChanged -= RefreshLocalizedTexts;
        if (Instance == this)
            Instance = null;
    }

    void Update()
    {
        if (!_isOpen)
            return;

        if (_mode != DisplayMode.BrowsePanel)
            return;

        if (Input.GetKeyDown(KeyCode.Escape))
            Close();
        else
            ApplyBrowseWheelScroll();
    }

    void BuildUi()
    {
        var canvasGo = new GameObject("Canvas", typeof(RectTransform));
        canvasGo.transform.SetParent(transform, false);
        _canvas = canvasGo.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 15000;
        canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGo.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920f, 1080f);
        canvasGo.AddComponent<GraphicRaycaster>();
        EnsureUiEventSystem();

        _rootGroup = canvasGo.AddComponent<CanvasGroup>();

        BuildBrowsePanel(canvasGo.transform);
        BuildRollPanel(canvasGo.transform);
        BuildEpiloguePanel(canvasGo.transform);
    }

    void BuildBrowsePanel(Transform parent)
    {
        _browseRoot = new GameObject("BrowsePanel", typeof(RectTransform));
        _browseRoot.transform.SetParent(parent, false);
        Stretch(_browseRoot.GetComponent<RectTransform>());

        var bg = _browseRoot.AddComponent<Image>();
        bg.color = Color.black;
        bg.raycastTarget = false;

        var titleGo = new GameObject("Title", typeof(RectTransform));
        titleGo.transform.SetParent(_browseRoot.transform, false);
        var titleRt = titleGo.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0.08f, 0.88f);
        titleRt.anchorMax = new Vector2(0.92f, 0.96f);
        titleRt.offsetMin = Vector2.zero;
        titleRt.offsetMax = Vector2.zero;
        _browseTitle = titleGo.AddComponent<TextMeshProUGUI>();
        ApplyFont(_browseTitle);
        _browseTitle.fontSize = 34;
        _browseTitle.fontStyle = FontStyles.Bold;
        _browseTitle.alignment = TextAlignmentOptions.Center;
        _browseTitle.color = Color.white;

        var hintGo = new GameObject("Hint", typeof(RectTransform));
        hintGo.transform.SetParent(_browseRoot.transform, false);
        var hintRt = hintGo.GetComponent<RectTransform>();
        hintRt.anchorMin = new Vector2(0.08f, 0.04f);
        hintRt.anchorMax = new Vector2(0.92f, 0.08f);
        hintRt.offsetMin = Vector2.zero;
        hintRt.offsetMax = Vector2.zero;
        var hint = hintGo.AddComponent<TextMeshProUGUI>();
        ApplyFont(hint);
        hint.text = Loc.Choose("自动滚动 · 滚轮/拖动可手动浏览 · 按 Esc 关闭", "Auto-scroll · wheel/drag to browse · Esc to close");
        hint.fontSize = 20;
        hint.alignment = TextAlignmentOptions.Center;
        hint.color = new Color(1f, 1f, 1f, 0.65f);

        var scrollGo = new GameObject("Scroll", typeof(RectTransform));
        scrollGo.transform.SetParent(_browseRoot.transform, false);
        var scrollRt = scrollGo.GetComponent<RectTransform>();
        scrollRt.anchorMin = new Vector2(0.1f, 0.1f);
        scrollRt.anchorMax = new Vector2(0.9f, 0.86f);
        scrollRt.offsetMin = Vector2.zero;
        scrollRt.offsetMax = Vector2.zero;

        _browseScroll = scrollGo.AddComponent<ScrollRect>();
        _browseScroll.horizontal = false;
        _browseScroll.vertical = true;
        _browseScroll.movementType = ScrollRect.MovementType.Clamped;
        _browseScroll.scrollSensitivity = 55f;
        _browseScroll.inertia = true;
        _browseScroll.decelerationRate = 0.135f;

        var viewport = new GameObject("Viewport", typeof(RectTransform));
        viewport.transform.SetParent(scrollGo.transform, false);
        Stretch(viewport.GetComponent<RectTransform>());
        var viewportImage = viewport.AddComponent<Image>();
        viewportImage.color = new Color(0f, 0f, 0f, 0.01f);
        viewportImage.raycastTarget = true;
        viewport.AddComponent<RectMask2D>();

        _browseContent = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
        _browseContent.SetParent(viewport.transform, false);
        _browseContent.anchorMin = new Vector2(0f, 1f);
        _browseContent.anchorMax = new Vector2(1f, 1f);
        _browseContent.pivot = new Vector2(0.5f, 1f);
        _browseContent.anchoredPosition = Vector2.zero;

        var bodyGo = new GameObject("Body", typeof(RectTransform));
        bodyGo.transform.SetParent(_browseContent, false);
        var bodyRt = bodyGo.GetComponent<RectTransform>();
        bodyRt.anchorMin = new Vector2(0f, 1f);
        bodyRt.anchorMax = new Vector2(1f, 1f);
        bodyRt.pivot = new Vector2(0.5f, 1f);
        bodyRt.offsetMin = new Vector2(24f, 0f);
        bodyRt.offsetMax = new Vector2(-24f, 0f);
        _browseBody = bodyGo.AddComponent<TextMeshProUGUI>();
        ApplyFont(_browseBody);
        _browseBody.fontSize = 24;
        _browseBody.lineSpacing = 4f;
        _browseBody.alignment = TextAlignmentOptions.TopLeft;
        _browseBody.color = Color.white;
        _browseBody.enableWordWrapping = true;
        _browseBody.overflowMode = TextOverflowModes.Overflow;
        _browseBody.raycastTarget = false;
        bodyGo.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        _browseScroll.viewport = viewport.GetComponent<RectTransform>();
        _browseScroll.content = _browseContent;
        viewport.AddComponent<CreditsBrowseScrollInput>().Initialize(this);
    }

    void BuildRollPanel(Transform parent)
    {
        _rollRoot = new GameObject("RollPanel", typeof(RectTransform));
        _rollRoot.transform.SetParent(parent, false);
        Stretch(_rollRoot.GetComponent<RectTransform>());
        _rollRoot.AddComponent<Image>().color = Color.black;

        _rollViewport = new GameObject("Viewport", typeof(RectTransform)).GetComponent<RectTransform>();
        _rollViewport.SetParent(_rollRoot.transform, false);
        Stretch(_rollViewport);
        _rollViewport.gameObject.AddComponent<RectMask2D>();

        var textGo = new GameObject("RollText", typeof(RectTransform));
        textGo.transform.SetParent(_rollViewport, false);
        _rollTextRect = textGo.GetComponent<RectTransform>();
        _rollTextRect.anchorMin = new Vector2(0.5f, 0f);
        _rollTextRect.anchorMax = new Vector2(0.5f, 0f);
        _rollTextRect.pivot = new Vector2(0.5f, 0f);
        _rollTextRect.sizeDelta = new Vector2(1200f, 100f);
        _rollText = textGo.AddComponent<TextMeshProUGUI>();
        ApplyFont(_rollText);
        _rollText.fontSize = 30;
        _rollText.lineSpacing = 8f;
        _rollText.alignment = TextAlignmentOptions.Center;
        _rollText.color = Color.white;
        _rollText.enableWordWrapping = true;
    }

    void BuildEpiloguePanel(Transform parent)
    {
        _epilogueRoot = new GameObject("EpiloguePanel", typeof(RectTransform));
        _epilogueRoot.transform.SetParent(parent, false);
        Stretch(_epilogueRoot.GetComponent<RectTransform>());
        _epilogueRoot.AddComponent<Image>().color = Color.black;

        var titleGo = new GameObject("Title", typeof(RectTransform));
        titleGo.transform.SetParent(_epilogueRoot.transform, false);
        var titleRt = titleGo.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0.1f, 0.58f);
        titleRt.anchorMax = new Vector2(0.9f, 0.72f);
        titleRt.offsetMin = Vector2.zero;
        titleRt.offsetMax = Vector2.zero;
        _epilogueTitle = titleGo.AddComponent<TextMeshProUGUI>();
        ApplyFont(_epilogueTitle);
        _epilogueTitle.fontSize = 42;
        _epilogueTitle.fontStyle = FontStyles.Bold;
        _epilogueTitle.alignment = TextAlignmentOptions.Center;
        _epilogueTitle.color = Color.white;

        var bodyGo = new GameObject("Body", typeof(RectTransform));
        bodyGo.transform.SetParent(_epilogueRoot.transform, false);
        var bodyRt = bodyGo.GetComponent<RectTransform>();
        bodyRt.anchorMin = new Vector2(0.12f, 0.28f);
        bodyRt.anchorMax = new Vector2(0.88f, 0.56f);
        bodyRt.offsetMin = Vector2.zero;
        bodyRt.offsetMax = Vector2.zero;
        _epilogueBody = bodyGo.AddComponent<TextMeshProUGUI>();
        ApplyFont(_epilogueBody);
        _epilogueBody.fontSize = 28;
        _epilogueBody.lineSpacing = 10f;
        _epilogueBody.alignment = TextAlignmentOptions.Center;
        _epilogueBody.color = Color.white;
        _epilogueBody.enableWordWrapping = true;

        var hintGo = new GameObject("Hint", typeof(RectTransform));
        hintGo.transform.SetParent(_epilogueRoot.transform, false);
        var hintRt = hintGo.GetComponent<RectTransform>();
        hintRt.anchorMin = new Vector2(0.1f, 0.08f);
        hintRt.anchorMax = new Vector2(0.9f, 0.14f);
        hintRt.offsetMin = Vector2.zero;
        hintRt.offsetMax = Vector2.zero;
        var hint = hintGo.AddComponent<TextMeshProUGUI>();
        ApplyFont(hint);
        hint.text = Loc.Choose("按任意键继续", "Press any key to continue");
        hint.fontSize = 22;
        hint.alignment = TextAlignmentOptions.Center;
        hint.color = new Color(1f, 1f, 1f, 0.7f);
    }

    void OpenBrowsePanel()
    {
        if (_isOpen && _mode == DisplayMode.BrowsePanel)
            return;

        StopRoutine();
        _mode = DisplayMode.BrowsePanel;
        _blocksGameplay = false;

        if (_browseTitle != null)
            _browseTitle.text = GameCreditsCatalog.Title;
        if (_browseBody != null)
            _browseBody.text = GameCreditsCatalog.FullCreditsBody;

        CaptureCursorForBrowse();
        SetPanelActive(browse: true, roll: false, epilogue: false);
        SetVisible(true);
        RestartBrowseLayoutRefresh();
        RestartBrowseAutoScroll();
    }

    void OpenMainStoryRoll()
    {
        if (_isOpen)
            return;

        StopRoutine();
        _mode = DisplayMode.MainStoryRoll;
        _blocksGameplay = true;
        CapturePlayerControl();
        SetPanelActive(browse: false, roll: true, epilogue: false);

        if (_rollText != null)
            _rollText.text = GameCreditsCatalog.Title + "\n\n" + GameCreditsCatalog.FullCreditsBody;

        SetVisible(true);
        _routine = StartCoroutine(CoMainStoryRoll());
    }

    IEnumerator CoMainStoryRoll()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(_rollTextRect);

        float textHeight = _rollText.preferredHeight + 48f;
        _rollTextRect.sizeDelta = new Vector2(1200f, textHeight);

        float viewHeight = _rollViewport.rect.height;
        float startY = -viewHeight * 0.15f;
        float endY = textHeight + viewHeight * 0.35f;
        _rollTextRect.anchoredPosition = new Vector2(0f, startY);

        const float scrollSpeed = 85f;
        float manualYOffset = 0f;
        float skipUnlockTime = Time.unscaledTime + 0.75f;
        while (_rollTextRect.anchoredPosition.y < endY)
        {
            float wheel = Input.mouseScrollDelta.y;
            if (Mathf.Abs(wheel) > 0.01f)
            {
                manualYOffset += wheel * 45f;
                skipUnlockTime = Time.unscaledTime + 1.2f;
            }

            if (Time.unscaledTime >= skipUnlockTime && Input.anyKeyDown && Mathf.Abs(wheel) < 0.01f)
                break;

            float y = Mathf.Min(
                _rollTextRect.anchoredPosition.y + scrollSpeed * Time.unscaledDeltaTime + manualYOffset,
                endY);
            manualYOffset = 0f;
            _rollTextRect.anchoredPosition = new Vector2(0f, y);
            yield return null;
        }

        yield return new WaitForSecondsRealtime(0.35f);
        ShowEpiloguePage();

        float epilogueUnlockTime = Time.unscaledTime + 0.5f;
        while (Time.unscaledTime < epilogueUnlockTime || !Input.anyKeyDown)
            yield return null;

        Close();
    }

    void ShowEpiloguePage()
    {
        if (_epilogueTitle != null)
            _epilogueTitle.text = GameCreditsCatalog.MainStoryEpilogueTitle;
        if (_epilogueBody != null)
            _epilogueBody.text = GameCreditsCatalog.MainStoryEpilogueBody;

        SetPanelActive(browse: false, roll: false, epilogue: true);
    }

    void Close()
    {
        bool wasBrowse = _mode == DisplayMode.BrowsePanel;
        StopRoutine();
        StopBrowseLayoutRefresh();
        StopBrowseAutoScroll();
        SetVisible(false);
        _isOpen = false;
        _blocksGameplay = false;
        if (wasBrowse)
            RestoreCursorForBrowse();
        else
            RestorePlayerControl();
    }

    void SetVisible(bool visible)
    {
        _isOpen = visible;
        if (_canvas != null)
            _canvas.enabled = visible;
        if (_rootGroup != null)
        {
            _rootGroup.alpha = visible ? 1f : 0f;
            _rootGroup.blocksRaycasts = visible;
            _rootGroup.interactable = visible;
        }
    }

    void SetPanelActive(bool browse, bool roll, bool epilogue)
    {
        if (_browseRoot != null)
            _browseRoot.SetActive(browse);
        if (_rollRoot != null)
            _rollRoot.SetActive(roll);
        if (_epilogueRoot != null)
            _epilogueRoot.SetActive(epilogue);
    }

    void StopRoutine()
    {
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }
    }

    void CapturePlayerControl()
    {
        if (_playerMovement == null)
            _playerMovement = FindObjectOfType<PlayerMovement>(true);
        if (_firstPersonCamera == null)
            _firstPersonCamera = FindObjectOfType<FirstPersonCamera>(true);

        if (_playerMovement != null)
        {
            _playerMovementWasEnabled = _playerMovement.enabled;
            _playerMovementPausedBefore = _playerMovement.isMovementPaused;
            _playerMovement.enabled = false;
            _playerMovement.isMovementPaused = true;
        }

        if (_firstPersonCamera != null)
        {
            _firstPersonSuppressBefore = _firstPersonCamera.suppressMouseLook;
            _firstPersonCursorLockedBefore = _firstPersonCamera.IsCursorLocked;
            _firstPersonCamera.suppressMouseLook = true;
            _firstPersonCamera.SetCursorLocked(false);
        }

        _cursorLockBefore = Cursor.lockState;
        _cursorVisibleBefore = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void RestorePlayerControl()
    {
        if (_playerMovement != null)
        {
            _playerMovement.enabled = _playerMovementWasEnabled;
            _playerMovement.isMovementPaused = _playerMovementPausedBefore;
        }

        if (_firstPersonCamera != null)
        {
            _firstPersonCamera.suppressMouseLook = _firstPersonSuppressBefore;
            _firstPersonCamera.SetCursorLocked(_firstPersonCursorLockedBefore);
        }
        else
        {
            Cursor.lockState = _cursorLockBefore;
            Cursor.visible = _cursorVisibleBefore;
        }
    }

    void CaptureCursorForBrowse()
    {
        if (_browseCursorCaptured)
            return;

        _browseCursorCaptured = true;
        if (_firstPersonCamera == null)
            _firstPersonCamera = FindObjectOfType<FirstPersonCamera>(true);

        if (_firstPersonCamera != null)
        {
            _firstPersonSuppressBefore = _firstPersonCamera.suppressMouseLook;
            _firstPersonCursorLockedBefore = _firstPersonCamera.IsCursorLocked;
            _firstPersonCamera.suppressMouseLook = true;
            _firstPersonCamera.SetCursorLocked(false);
        }

        _cursorLockBefore = Cursor.lockState;
        _cursorVisibleBefore = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void RestoreCursorForBrowse()
    {
        if (!_browseCursorCaptured)
            return;

        _browseCursorCaptured = false;
        if (_firstPersonCamera != null)
        {
            _firstPersonCamera.suppressMouseLook = _firstPersonSuppressBefore;
            _firstPersonCamera.SetCursorLocked(_firstPersonCursorLockedBefore);
            if (_firstPersonCamera.IsActive && _firstPersonCursorLockedBefore)
                GameplayCursorLock.LockForGameplayView();
        }
        else
        {
            Cursor.lockState = _cursorLockBefore;
            Cursor.visible = _cursorVisibleBefore;
        }
    }

    void RestartBrowseLayoutRefresh()
    {
        StopBrowseLayoutRefresh();
        _browseLayoutRoutine = StartCoroutine(CoRefreshBrowseScrollLayout());
    }

    void StopBrowseLayoutRefresh()
    {
        if (_browseLayoutRoutine == null)
            return;
        StopCoroutine(_browseLayoutRoutine);
        _browseLayoutRoutine = null;
    }

    IEnumerator CoRefreshBrowseScrollLayout()
    {
        for (int i = 0; i < 6; i++)
        {
            yield return null;
            RefreshBrowseScrollMetrics();
        }
    }

    void RestartBrowseAutoScroll()
    {
        StopBrowseAutoScroll();
        _browseManualPauseUntil = 0f;
        _browseAutoScrollRoutine = StartCoroutine(CoBrowseAutoScroll());
    }

    void StopBrowseAutoScroll()
    {
        if (_browseAutoScrollRoutine == null)
            return;
        StopCoroutine(_browseAutoScrollRoutine);
        _browseAutoScrollRoutine = null;
    }

    IEnumerator CoBrowseAutoScroll()
    {
        yield return CoRefreshBrowseScrollLayout();

        while (_isOpen && _mode == DisplayMode.BrowsePanel)
        {
            if (_browseScroll != null
                && Time.unscaledTime >= _browseManualPauseUntil
                && TryGetBrowseScrollableHeight(out float scrollable))
            {
                float delta = (BrowseAutoScrollPixelsPerSecond * Time.unscaledDeltaTime) / scrollable;
                _browseScroll.velocity = Vector2.zero;
                _browseScroll.verticalNormalizedPosition = Mathf.Clamp01(
                    _browseScroll.verticalNormalizedPosition - delta);
            }

            yield return null;
        }
    }

    internal void NotifyBrowseManualScroll()
    {
        _browseManualPauseUntil = Time.unscaledTime + BrowseManualPauseSeconds;
    }

    bool TryGetBrowseScrollableHeight(out float scrollable)
    {
        scrollable = 0f;
        if (_browseScroll == null || _browseContent == null || _browseScroll.viewport == null)
            return false;

        float viewportH = _browseScroll.viewport.rect.height;
        float contentH = _browseContent.rect.height;
        scrollable = contentH - viewportH;
        return scrollable > 1f;
    }

    void RefreshBrowseScrollMetrics()
    {
        if (_browseScroll == null || _browseBody == null || _browseContent == null)
            return;

        var viewport = _browseScroll.viewport;
        if (viewport == null)
            return;

        Canvas.ForceUpdateCanvases();

        float width = viewport.rect.width;
        if (width < 8f)
            width = 1200f;

        var bodyRt = _browseBody.rectTransform;
        float innerWidth = Mathf.Max(64f, width - 48f);
        bodyRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, innerWidth);
        _browseBody.ForceMeshUpdate(true, true);
        LayoutRebuilder.ForceRebuildLayoutImmediate(bodyRt);

        float textHeight = Mathf.Ceil(_browseBody.preferredHeight);
        bodyRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, textHeight);
        LayoutRebuilder.ForceRebuildLayoutImmediate(bodyRt);

        float viewportH = Mathf.Max(1f, viewport.rect.height);
        float contentH = Mathf.Max(textHeight + 48f, viewportH + 4f);
        _browseContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        _browseContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, contentH);

        _browseScroll.velocity = Vector2.zero;
        _browseScroll.StopMovement();
        if (_browseScroll.verticalNormalizedPosition > 0.99f)
            _browseScroll.verticalNormalizedPosition = 1f;
    }

    void ApplyBrowseWheelScroll()
    {
        if (_browseScroll == null || _browseContent == null || _browseScroll.viewport == null)
            return;

        float delta = Input.mouseScrollDelta.y;
        if (Mathf.Abs(delta) < 0.01f)
            return;

        if (!TryGetBrowseScrollableHeight(out float scrollable))
            return;

        NotifyBrowseManualScroll();
        float normDelta = (delta * _browseScroll.scrollSensitivity) / scrollable;
        _browseScroll.velocity = Vector2.zero;
        _browseScroll.verticalNormalizedPosition = Mathf.Clamp01(
            _browseScroll.verticalNormalizedPosition + normDelta);
    }

    static void EnsureUiEventSystem()
    {
        if (EventSystem.current != null && EventSystem.current.isActiveAndEnabled)
            return;

        var existing = FindObjectsOfType<EventSystem>(true);
        for (int i = 0; i < existing.Length; i++)
        {
            var es = existing[i];
            if (es == null || !es.isActiveAndEnabled)
                continue;
            if (!es.gameObject.activeInHierarchy)
                es.gameObject.SetActive(true);
            return;
        }

        var go = new GameObject("EventSystem_GameCredits");
        go.AddComponent<EventSystem>();
        go.AddComponent<StandaloneInputModule>();
        DontDestroyOnLoad(go);
    }

    static void Stretch(RectTransform rt, Vector2 offsetMin = default, Vector2 offsetMax = default)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
    }

    static void ApplyFont(TMP_Text text)
    {
        if (text == null)
            return;
        var font = Resources.Load<TMP_FontAsset>(FontResourcesPath);
        if (font != null)
            text.font = font;
    }
}

/// <summary>检测滚轮/拖拽，暂停自动滚动并交给 ScrollRect 处理。</summary>
sealed class CreditsBrowseScrollInput : MonoBehaviour, IScrollHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    GameCreditsScrollOverlay _owner;
    ScrollRect _scroll;

    public void Initialize(GameCreditsScrollOverlay owner)
    {
        _owner = owner;
        _scroll = GetComponentInParent<ScrollRect>();
    }

    public void OnScroll(PointerEventData eventData)
    {
        if (_owner == null || _scroll == null)
            return;

        _owner.NotifyBrowseManualScroll();
        _scroll.OnScroll(eventData);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        _owner?.NotifyBrowseManualScroll();
        _scroll?.OnBeginDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        _owner?.NotifyBrowseManualScroll();
        _scroll?.OnDrag(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        _owner?.NotifyBrowseManualScroll();
        _scroll?.OnEndDrag(eventData);
    }
}
