using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 电梯楼层输入面板（单例）。按 F 打开后输入楼层并确认传送。
/// </summary>
public class ElevatorFloorInputUI : MonoBehaviour
{
    const string AlibabaTmpResourcesPath = "Fonts & Materials/AlibabaPuHuiTi-3-75-SemiBold SDF";
    const int UiLayoutVersion = 2;
    /// <summary>面板占屏幕比例（宽高各 80%，大于 75% 要求）。</summary>
    const float PanelScreenCoverage = 0.8f;

    static ElevatorFloorInputUI _instance;

    [Header("可选：Inspector 绑定；留空则运行时自动生成")]
    public TMP_FontAsset uiFont;
    public GameObject panelRoot;
    public TMP_InputField floorInput;
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI hintText;
    public Button confirmButton;
    public Button cancelButton;

    Action<int> _onConfirm;
    int _minFloor;
    int _maxFloor;
    int _currentFloor;
    int _builtLayoutVersion;

    PlayerMovement _playerMovement;
    FirstPersonCamera _firstPersonCamera;
    bool _playerMovementWasEnabled;
    bool _movementPausedBefore;
    bool _firstPersonSuppressMouseLookBefore;
    bool _firstPersonCursorLockedBefore;
    CursorLockMode _cursorLockBefore;
    bool _cursorVisibleBefore;
    bool _gameplayLocked;

    public static bool BlocksGameplayInput => Instance != null && Instance.IsOpen;

    public static ElevatorFloorInputUI Instance
    {
        get
        {
            if (_instance != null)
                return _instance;
            _instance = FindObjectOfType<ElevatorFloorInputUI>(true);
            if (_instance != null)
                return _instance;

            var go = new GameObject("[ElevatorFloorInputUI]");
            _instance = go.AddComponent<ElevatorFloorInputUI>();
            DontDestroyOnLoad(go);
            _instance.EnsureUiBuilt();
            return _instance;
        }
    }

    public bool IsOpen => panelRoot != null && panelRoot.activeSelf;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureUiBuilt();
        Hide();
    }

    void OnEnable() => Loc.OnLanguageChanged += OnLanguageChanged;
    void OnDisable() => Loc.OnLanguageChanged -= OnLanguageChanged;
    void OnLanguageChanged()
    {
        RefreshLocalizedStaticTexts();
        RefreshHint();
    }

    void RefreshLocalizedStaticTexts()
    {
        if (titleText != null && (string.IsNullOrWhiteSpace(titleText.text) || titleText.text == "电梯" || titleText.text == "Elevator"))
            titleText.text = Loc.Choose("电梯", "Elevator");
        if (floorInput != null && floorInput.placeholder is TMP_Text ph)
            ph.text = Loc.Choose("输入目标楼层", "Enter target floor");
        SetButtonChildText(confirmButton, Loc.Choose("确认", "Confirm"));
        SetButtonChildText(cancelButton, Loc.Choose("取消", "Cancel"));
    }

    static void SetButtonChildText(Button btn, string label)
    {
        if (btn == null) return;
        var tmp = btn.GetComponentInChildren<TextMeshProUGUI>(true);
        if (tmp != null) tmp.text = label;
    }

    void OnDestroy()
    {
        Loc.OnLanguageChanged -= OnLanguageChanged;
        if (_instance == this)
        {
            RestoreGameplayInput();
            _instance = null;
        }
    }

    public void Show(string elevatorName, int minFloor, int maxFloor, Action<int> onConfirm)
    {
        Show(elevatorName, minFloor, maxFloor, 0, onConfirm);
    }

    public void Show(string elevatorName, int minFloor, int maxFloor, int currentFloor, Action<int> onConfirm)
    {
        EnsureUiBuilt();
        _onConfirm = onConfirm;
        _minFloor = minFloor;
        _maxFloor = maxFloor;
        _currentFloor = currentFloor;

        if (titleText != null)
            titleText.text = string.IsNullOrWhiteSpace(elevatorName) ? Loc.Choose("电梯", "Elevator") : elevatorName;
        RefreshHint();
        if (floorInput != null)
        {
            floorInput.text = "";
            floorInput.contentType = TMP_InputField.ContentType.IntegerNumber;
        }

        LockGameplayInput();
        panelRoot.SetActive(true);
        if (floorInput != null)
        {
            floorInput.ActivateInputField();
            floorInput.Select();
        }
    }

    public void Hide()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);
        if (floorInput != null)
            floorInput.DeactivateInputField();
        _onConfirm = null;
        RestoreGameplayInput();
    }

    void EnsureUiBuilt()
    {
        if (_builtLayoutVersion == UiLayoutVersion && panelRoot != null && floorInput != null && confirmButton != null)
        {
            ApplyFontToAll();
            return;
        }

        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);

        panelRoot = null;
        floorInput = null;
        titleText = null;
        hintText = null;
        confirmButton = null;
        cancelButton = null;

        BuildRuntimeUi();
        _builtLayoutVersion = UiLayoutVersion;
    }

    TMP_FontAsset ResolveFont()
    {
        if (uiFont != null)
            return uiFont;
        if (_resolvedFont == null)
            _resolvedFont = Resources.Load<TMP_FontAsset>(AlibabaTmpResourcesPath);
        return _resolvedFont;
    }

    TMP_FontAsset _resolvedFont;

    void BuildRuntimeUi()
    {
        float margin = (1f - PanelScreenCoverage) * 0.5f;

        var canvasGo = new GameObject("ElevatorInputCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 31000;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        panelRoot = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panelRoot.transform.SetParent(canvasGo.transform, false);
        var panelRt = panelRoot.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(margin, margin);
        panelRt.anchorMax = new Vector2(1f - margin, 1f - margin);
        panelRt.offsetMin = Vector2.zero;
        panelRt.offsetMax = Vector2.zero;
        var panelImg = panelRoot.GetComponent<Image>();
        panelImg.color = new Color(0.06f, 0.07f, 0.1f, 0.94f);

        titleText = CreateTmp(panelRoot.transform, "Title", 0.08f, 0.72f, 0.92f, 0.92f, 56, FontStyles.Bold);
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.text = Loc.Choose("电梯", "Elevator");

        hintText = CreateTmp(panelRoot.transform, "Hint", 0.08f, 0.54f, 0.92f, 0.7f, 36, FontStyles.Normal);
        hintText.alignment = TextAlignmentOptions.Center;
        hintText.color = new Color(0.88f, 0.9f, 0.95f);

        floorInput = CreateInputField(panelRoot.transform, 0.2f, 0.34f, 0.8f, 0.5f);
        confirmButton = CreateButton(panelRoot.transform, Loc.Choose("确认", "Confirm"), 0.12f, 0.1f, 0.46f, 0.28f, OnClickConfirm);
        cancelButton = CreateButton(panelRoot.transform, Loc.Choose("取消", "Cancel"), 0.54f, 0.1f, 0.88f, 0.28f, Hide);

        ApplyFontToAll();
        panelRoot.SetActive(false);
    }

    void ApplyFontToAll()
    {
        var font = ResolveFont();
        if (font == null)
            return;

        ApplyFont(titleText, font);
        ApplyFont(hintText, font);
        if (floorInput != null)
        {
            ApplyFont(floorInput.textComponent, font);
            if (floorInput.placeholder is TMP_Text placeholderTmp)
                ApplyFont(placeholderTmp, font);
        }
        if (confirmButton != null)
            ApplyFont(confirmButton.GetComponentInChildren<TextMeshProUGUI>(), font);
        if (cancelButton != null)
            ApplyFont(cancelButton.GetComponentInChildren<TextMeshProUGUI>(), font);
    }

    static void ApplyFont(TMP_Text tmp, TMP_FontAsset font)
    {
        if (tmp == null || font == null)
            return;
        tmp.font = font;
    }

    static void SetStretchRect(RectTransform rt, float minX, float minY, float maxX, float maxY)
    {
        rt.anchorMin = new Vector2(minX, minY);
        rt.anchorMax = new Vector2(maxX, maxY);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static TextMeshProUGUI CreateTmp(Transform parent, string name, float minX, float minY, float maxX, float maxY, float fontSize, FontStyles style)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        SetStretchRect(go.GetComponent<RectTransform>(), minX, minY, maxX, maxY);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.color = Color.white;
        tmp.enableAutoSizing = false;
        return tmp;
    }

    static TMP_InputField CreateInputField(Transform parent, float minX, float minY, float maxX, float maxY)
    {
        var go = new GameObject("FloorInput", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        SetStretchRect(go.GetComponent<RectTransform>(), minX, minY, maxX, maxY);
        go.GetComponent<Image>().color = new Color(0.12f, 0.14f, 0.18f, 1f);

        var textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(go.transform, false);
        StretchFill(textGo.GetComponent<RectTransform>(), 24, 16);
        var text = textGo.AddComponent<TextMeshProUGUI>();
        text.fontSize = 42;
        text.alignment = TextAlignmentOptions.Center;

        var placeholderGo = new GameObject("Placeholder", typeof(RectTransform));
        placeholderGo.transform.SetParent(go.transform, false);
        StretchFill(placeholderGo.GetComponent<RectTransform>(), 24, 16);
        var ph = placeholderGo.AddComponent<TextMeshProUGUI>();
        ph.text = Loc.Choose("输入目标楼层", "Enter target floor");
        ph.fontSize = 36;
        ph.color = new Color(1f, 1f, 1f, 0.35f);
        ph.alignment = TextAlignmentOptions.Center;

        var input = go.AddComponent<TMP_InputField>();
        input.textComponent = text;
        input.placeholder = ph;
        input.contentType = TMP_InputField.ContentType.IntegerNumber;
        return input;
    }

    static void StretchFill(RectTransform rt, float padX, float padY)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(padX, padY);
        rt.offsetMax = new Vector2(-padX, -padY);
    }

    static Button CreateButton(Transform parent, string label, float minX, float minY, float maxX, float maxY, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        SetStretchRect(go.GetComponent<RectTransform>(), minX, minY, maxX, maxY);
        go.GetComponent<Image>().color = new Color(0.2f, 0.42f, 0.72f, 1f);

        var labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(go.transform, false);
        StretchFill(labelGo.GetComponent<RectTransform>(), 8, 8);
        var tmp = labelGo.AddComponent<TextMeshProUGUI>();
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.text = label;
        tmp.fontSize = 34;
        tmp.color = Color.white;

        var btn = go.GetComponent<Button>();
        btn.onClick.AddListener(onClick);
        return btn;
    }

    void LockGameplayInput()
    {
        if (_gameplayLocked)
            return;
        _gameplayLocked = true;

        if (_playerMovement == null)
            _playerMovement = FindObjectOfType<PlayerMovement>(true);
        if (_firstPersonCamera == null)
            _firstPersonCamera = FindObjectOfType<FirstPersonCamera>(true);

        if (_playerMovement != null)
        {
            _playerMovementWasEnabled = _playerMovement.enabled;
            _movementPausedBefore = _playerMovement.isMovementPaused;
            _playerMovement.isMovementPaused = true;
            _playerMovement.enabled = false;
        }

        if (_firstPersonCamera != null)
        {
            _firstPersonSuppressMouseLookBefore = _firstPersonCamera.suppressMouseLook;
            _firstPersonCursorLockedBefore = _firstPersonCamera.IsCursorLocked;
            _firstPersonCamera.suppressMouseLook = true;
            _firstPersonCamera.SetCursorLocked(false);
        }

        _cursorLockBefore = Cursor.lockState;
        _cursorVisibleBefore = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void RestoreGameplayInput()
    {
        if (!_gameplayLocked)
            return;
        _gameplayLocked = false;

        if (_playerMovement != null)
        {
            _playerMovement.enabled = _playerMovementWasEnabled;
            _playerMovement.isMovementPaused = _movementPausedBefore;
        }

        if (_firstPersonCamera != null)
        {
            _firstPersonCamera.suppressMouseLook = _firstPersonSuppressMouseLookBefore;
            _firstPersonCamera.SetCursorLocked(_firstPersonCursorLockedBefore);
        }

        Cursor.lockState = _cursorLockBefore;
        Cursor.visible = _cursorVisibleBefore;
    }

    void Update()
    {
        if (!IsOpen)
            return;

        if (Input.GetKeyDown(KeyCode.Escape))
            Hide();

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            OnClickConfirm();
    }

    void OnClickConfirm()
    {
        if (_onConfirm == null)
        {
            Hide();
            return;
        }

        string raw = floorInput != null ? floorInput.text.Trim() : "";
        if (!int.TryParse(raw, out int floor))
        {
            if (hintText != null)
                hintText.text = Loc.Choose("请输入有效数字", "Please enter a valid number");
            return;
        }

        if (floor < _minFloor || floor > _maxFloor)
        {
            if (hintText != null)
                hintText.text = Loc.F("可抵达楼层：{0} - {1}", "Available floors: {0} - {1}", _minFloor, _maxFloor);
            return;
        }

        if (_currentFloor > 0 && floor == _currentFloor)
        {
            if (hintText != null)
                hintText.text = Loc.Choose("你已在当前楼层", "You are already on this floor");
            return;
        }

        var cb = _onConfirm;
        Hide();
        cb?.Invoke(floor);
    }

    void RefreshHint()
    {
        if (hintText == null)
            return;
        if (_currentFloor > 0)
            hintText.text = Loc.F("当前 {0} 层 · 可前往 {1} - {2} 层", "Floor {0} · Go to {1} - {2}", _currentFloor, _minFloor, _maxFloor);
        else
            hintText.text = Loc.F("请输入目标楼层（{0} - {1}）", "Enter target floor ({0} - {1})", _minFloor, _maxFloor);
    }
}
