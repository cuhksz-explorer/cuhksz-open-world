using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 首次注册：左上角三头像 + 左下 3D 预览 + 右下昵称设置。
/// </summary>
public class PlayerCharacterRegisterUI : MonoBehaviour
{
    static readonly string[] AvatarCharacterIds = { "hero_female", "hero_male_a", "hero_male_b" };
    const float PreviewLookTargetLocalY = 0.92f;
    const int PreviewRenderWidth = 768;
    const int PreviewRenderHeight = 1024;
    const float PreviewTargetHeight = 1.72f;
    const float PreviewCameraDistance = 2.65f;
    const float PreviewDragSensitivity = 0.45f;
    const float PreviewZoomStep = 0.08f;
    const float PreviewZoomMin = 0.72f;
    const float PreviewZoomMax = 1.55f;
    [SerializeField] GameServerSync serverSync;
    [SerializeField] PlayerCharacterCatalog catalog;

    GameObject _root;
    RawImage _previewImage;
    InputField _nicknameInput;
    Text _statusText;
    Button _confirmButton;
    Button _randomButton;
    Button[] _avatarButtons;
    Image[] _avatarFrames;
    Text _accountIdText;
    Text _characterIdText;
    Text _characterNameText;
    Text _bioText;

    Transform _previewStageRoot;
    Transform _previewLookTarget;
    Camera _previewCamera;
    RenderTexture _previewTexture;
    GameObject _previewInstance;
    float _previewYawOffset;
    float _previewZoom = 1f;
    int _selectedIndex;

    Action _onCompleted;

    static Color Gold => LguHubAuthUILayout.Gold;
    static Color TextBlack => LguHubAuthUILayout.TextBlack;
    static Color TextMuted => LguHubAuthUILayout.TextMuted;
    static Color PanelWhite => LguHubAuthUILayout.BgWhite;
    static Color InputBg => LguHubAuthUILayout.InputBg;
    static Color BtnDark => LguHubAuthUILayout.PrimaryButtonBg;
    static Color BtnDarkHover => LguHubAuthUILayout.PrimaryButtonHighlight;

    void Awake()
    {
        if (serverSync == null)
            serverSync = GameServerSync.Instance ?? FindObjectOfType<GameServerSync>(true);
        if (catalog == null)
            catalog = PlayerCharacterCatalog.LoadDefault();
        // 不在 Awake 预构建：catalog 可能尚未就绪，会导致头像区空白且无法重建。
    }

    public bool HasAvailableOptions()
    {
        if (catalog == null)
            catalog = PlayerCharacterCatalog.LoadDefault();
        if (catalog == null)
            return false;
        catalog.EnsureDefaultOptionsIfEmpty();
        for (int i = 0; i < PlayerCharacterCatalog.DefaultCharacterIds.Length; i++)
        {
            var option = catalog.GetById(PlayerCharacterCatalog.DefaultCharacterIds[i]);
            if (option == null || catalog.LoadPrefab(option) == null)
                return false;
        }
        return true;
    }

    void OnEnable() => Loc.OnLanguageChanged += RefreshLocalizedUi;
    void OnDisable() => Loc.OnLanguageChanged -= RefreshLocalizedUi;

    void RefreshLocalizedUi()
    {
        if (_root == null || !_root.activeInHierarchy)
            return;
        RefreshLocalizedStaticLabels();
    }

    void RefreshLocalizedStaticLabels()
    {
        if (_statusText != null && string.IsNullOrEmpty(_nicknameInput?.text))
            _statusText.text = Loc.Choose("选择角色并设置昵称（仅首次注册需要）", "Choose a character and set nickname (first registration only)");
    }

    void OnDestroy()
    {
        DestroyPreviewStage();
    }

    public void Show(Action onCompleted)
    {
        _onCompleted = onCompleted;
        if (catalog == null)
            catalog = PlayerCharacterCatalog.LoadDefault();
        if (!HasAvailableOptions())
        {
            Debug.LogError("[PlayerCharacterRegisterUI] 没有可用角色，请确认 Resources/PlayerCharacters 下已有 prefab。");
            _onCompleted?.Invoke();
            return;
        }

        EnsureUiBuilt(forceRebuild: true);
        if (_root == null || _avatarButtons == null || _avatarButtons.Length == 0)
        {
            Debug.LogError("[PlayerCharacterRegisterUI] UI 构建失败，请检查 Console 中是否有异常。");
            return;
        }
        _selectedIndex = 0;
        if (_nicknameInput != null)
            _nicknameInput.text = "";
        if (_statusText != null)
            _statusText.text = Loc.Choose("选择角色并设置昵称（仅首次注册需要）", "Choose a character and set nickname (first registration only)");
        RefreshAvatarSelection();
        RefreshCharacterInfoPanel();
        RefreshPreviewModel();
        BindPreviewTexture();
        StartCoroutine(CoRefreshPreviewNextFrame());
        UiLifecycleSafe.SetActiveIfAlive(_root, true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Hide()
    {
        UiLifecycleSafe.SetActiveIfAlive(_root, false);
        DestroyPreviewStage();
    }

    IEnumerator CoRefreshPreviewNextFrame()
    {
        yield return null;
        RefreshPreviewModel();
        BindPreviewTexture();
        RenderPreviewFrame();
        yield return null;
        var option = GetOptionSafe(_selectedIndex);
        if (_previewInstance != null && option != null)
            AlignModelToPreviewCenter(_previewInstance, option);
        ApplyPreviewView();
    }

    void BindPreviewTexture()
    {
        if (_previewImage != null && _previewTexture != null)
        {
            _previewImage.texture = _previewTexture;
            _previewImage.color = Color.white;
            _previewImage.uvRect = new Rect(0f, 0f, 1f, 1f);
        }

        if (_previewCamera != null && _previewTexture != null)
            _previewCamera.targetTexture = _previewTexture;
    }

    void RenderPreviewFrame()
    {
        if (_previewCamera == null || !_previewCamera.enabled)
            return;
        _previewCamera.Render();
    }

    void TearDownUi()
    {
        if (_root != null)
        {
            Destroy(_root);
            _root = null;
        }
        _previewImage = null;
        _nicknameInput = null;
        _statusText = null;
        _confirmButton = null;
        _randomButton = null;
        _avatarButtons = null;
        _avatarFrames = null;
        _accountIdText = null;
        _characterIdText = null;
        _characterNameText = null;
        _bioText = null;
    }

    void EnsureUiBuilt(bool forceRebuild = false)
    {
        if (forceRebuild)
            TearDownUi();
        if (_root != null)
            return;

        if (catalog == null)
            catalog = PlayerCharacterCatalog.LoadDefault();

        _root = new GameObject("CharacterNicknameRegisterUI");
        _root.transform.SetParent(transform, false);

        var canvas = _root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50000;
        var scaler = _root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        _root.AddComponent<GraphicRaycaster>();

        var dim = CreateUiObject<Image>("Dim", _root.transform);
        dim.color = new Color(0f, 0f, 0f, 0.78f);
        StretchFull(dim.rectTransform);

        var panel = CreateUiObject<Image>("Panel", _root.transform);
        panel.color = PanelWhite;
        var panelRt = panel.rectTransform;
        panelRt.anchorMin = new Vector2(0.06f, 0.07f);
        panelRt.anchorMax = new Vector2(0.94f, 0.93f);
        panelRt.offsetMin = Vector2.zero;
        panelRt.offsetMax = Vector2.zero;

        AddGoldCorner(panel.transform, true, true, false, false);
        AddGoldCorner(panel.transform, false, true, true, false);
        AddGoldCorner(panel.transform, true, false, false, true);
        AddGoldCorner(panel.transform, false, false, true, true);

        var title = CreateText("Title", panel.transform, Loc.Choose("创建你的角色", "Create Your Character"), 40, TextAnchor.UpperCenter);
        title.color = TextBlack;
        title.fontStyle = FontStyle.Bold;
        StretchTop(title.rectTransform, 22f, 64f);

        var titleLine = CreateUiObject<Image>("TitleLine", panel.transform);
        titleLine.color = Gold;
        var titleLineRt = titleLine.rectTransform;
        titleLineRt.anchorMin = new Vector2(0.5f, 1f);
        titleLineRt.anchorMax = new Vector2(0.5f, 1f);
        titleLineRt.pivot = new Vector2(0.5f, 1f);
        titleLineRt.anchoredPosition = new Vector2(0f, -82f);
        titleLineRt.sizeDelta = new Vector2(220f, 3f);

        var leftArea = CreateUiObject<Image>("LeftArea", panel.transform);
        leftArea.color = InputBg;
        var leftRt = leftArea.rectTransform;
        leftRt.anchorMin = new Vector2(0.03f, 0.08f);
        leftRt.anchorMax = new Vector2(0.58f, 0.86f);
        leftRt.offsetMin = Vector2.zero;
        leftRt.offsetMax = Vector2.zero;

        AddBorder(leftArea.transform, LguHubAuthUILayout.InputBorder, 2f);

        BuildAvatarRow(leftArea.transform);
        BuildPreviewArea(leftArea.transform);

        var rightArea = CreateUiObject<Image>("RightArea", panel.transform);
        rightArea.color = InputBg;
        var rightRt = rightArea.rectTransform;
        rightRt.anchorMin = new Vector2(0.62f, 0.08f);
        rightRt.anchorMax = new Vector2(0.97f, 0.86f);
        rightRt.offsetMin = Vector2.zero;
        rightRt.offsetMax = Vector2.zero;

        AddBorder(rightArea.transform, LguHubAuthUILayout.InputBorder, 2f);

        BuildProfileArea(rightArea.transform);

        UiLifecycleSafe.SetActiveIfAlive(_root, false);
    }

    void BuildAvatarRow(Transform parent)
    {
        if (catalog != null)
            catalog.EnsureDefaultOptionsIfEmpty();

        _avatarButtons = new Button[AvatarCharacterIds.Length];
        _avatarFrames = new Image[AvatarCharacterIds.Length];

        var row = new GameObject("AvatarRow");
        row.transform.SetParent(parent, false);
        var rowRt = row.AddComponent<RectTransform>();
        rowRt.anchorMin = new Vector2(0f, 1f);
        rowRt.anchorMax = new Vector2(1f, 1f);
        rowRt.pivot = new Vector2(0f, 1f);
        rowRt.anchoredPosition = new Vector2(24f, -20f);
        rowRt.sizeDelta = new Vector2(-48f, 132f);

        float x = 0f;
        const float size = 112f;
        const float gap = 18f;
        for (int i = 0; i < AvatarCharacterIds.Length; i++)
        {
            var option = catalog != null ? catalog.GetById(AvatarCharacterIds[i]) : null;
            var btnGo = CreateButton("Avatar_" + AvatarCharacterIds[i], row.transform);
            var btnRt = btnGo.GetComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(0f, 0.5f);
            btnRt.anchorMax = new Vector2(0f, 0.5f);
            btnRt.pivot = new Vector2(0f, 0.5f);
            btnRt.anchoredPosition = new Vector2(x, 0f);
            btnRt.sizeDelta = new Vector2(size, size);

            var frame = btnGo.GetComponent<Image>();
            frame.color = PanelWhite;
            _avatarFrames[i] = frame;

            var portrait = CreateUiObject<Image>("Portrait", btnGo.transform);
            portrait.color = new Color(0.94f, 0.94f, 0.94f, 1f);
            StretchWithPadding(portrait.rectTransform, 6f);

            if (option != null && option.portrait != null)
            {
                portrait.sprite = option.portrait;
                portrait.color = Color.white;
            }
            else if (option != null)
            {
                string glyph = !string.IsNullOrEmpty(option.avatarShortLabel)
                    ? option.avatarShortLabel
                    : GetAvatarFallbackGlyph(option);
                var initial = CreateText("Initial", btnGo.transform, glyph, 44, TextAnchor.MiddleCenter);
                initial.color = TextBlack;
                initial.fontStyle = FontStyle.Bold;
                StretchFull(initial.rectTransform);
            }

            var label = CreateText("Label", btnGo.transform, option != null ? PlayerCharacterCatalog.GetLocalizedDisplayName(option) : Loc.F("角色{0}", "Character {0}", i + 1), 17, TextAnchor.LowerCenter);
            label.color = TextMuted;
            var labelRt = label.rectTransform;
            labelRt.anchorMin = new Vector2(0f, 0f);
            labelRt.anchorMax = new Vector2(1f, 0f);
            labelRt.pivot = new Vector2(0.5f, 0f);
            labelRt.anchoredPosition = new Vector2(0f, -28f);
            labelRt.sizeDelta = new Vector2(0f, 24f);

            int captured = i;
            btnGo.onClick.AddListener(() => OnAvatarClicked(captured));
            _avatarButtons[i] = btnGo;
            x += size + gap;
        }
    }

    void BuildPreviewArea(Transform parent)
    {
        var previewBg = CreateUiObject<Image>("PreviewBg", parent);
        previewBg.color = new Color(0.12f, 0.12f, 0.12f, 1f);
        previewBg.raycastTarget = true;
        var rt = previewBg.rectTransform;
        rt.anchorMin = new Vector2(0.04f, 0.05f);
        rt.anchorMax = new Vector2(0.96f, 0.72f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var previewInput = previewBg.gameObject.AddComponent<PlayerCharacterPreviewInput>();
        previewInput.Initialize(this);

        var rawGo = new GameObject("PreviewImage");
        rawGo.transform.SetParent(previewBg.transform, false);
        _previewImage = rawGo.AddComponent<RawImage>();
        _previewImage.color = Color.white;
        var rawRt = _previewImage.rectTransform;
        rawRt.anchorMin = new Vector2(0.5f, 0.5f);
        rawRt.anchorMax = new Vector2(0.5f, 0.5f);
        rawRt.pivot = new Vector2(0.5f, 0.5f);
        rawRt.anchoredPosition = Vector2.zero;
        rawRt.sizeDelta = Vector2.zero;
        var fitter = rawGo.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fitter.aspectRatio = (float)PreviewRenderWidth / PreviewRenderHeight;
    }

    void BuildProfileArea(Transform parent)
    {
        var sectionTitle = CreateText("SectionTitle", parent, Loc.Choose("角色档案", "Character Profile"), 30, TextAnchor.UpperLeft);
        sectionTitle.color = TextBlack;
        sectionTitle.fontStyle = FontStyle.Bold;
        var sectionRt = sectionTitle.rectTransform;
        sectionRt.anchorMin = new Vector2(0.08f, 1f);
        sectionRt.anchorMax = new Vector2(0.92f, 1f);
        sectionRt.pivot = new Vector2(0f, 1f);
        sectionRt.anchoredPosition = new Vector2(0f, -20f);
        sectionRt.sizeDelta = new Vector2(0f, 40f);

        _accountIdText = CreateInfoLine(parent, "AccountIdLine", Loc.Choose("账号 ID", "Account ID"), "", 0.78f);
        _characterIdText = CreateInfoLine(parent, "CharacterIdLine", Loc.Choose("角色编号", "Character ID"), "", 0.68f);
        _characterNameText = CreateInfoLine(parent, "CharacterNameLine", Loc.Choose("角色名称", "Character Name"), "", 0.58f);

        var bioTitle = CreateText("BioTitle", parent, Loc.Choose("角色介绍", "Bio"), 22, TextAnchor.UpperLeft);
        bioTitle.color = Gold;
        bioTitle.fontStyle = FontStyle.Bold;
        var bioTitleRt = bioTitle.rectTransform;
        bioTitleRt.anchorMin = new Vector2(0.08f, 0.50f);
        bioTitleRt.anchorMax = new Vector2(0.92f, 0.50f);
        bioTitleRt.pivot = new Vector2(0f, 1f);
        bioTitleRt.anchoredPosition = Vector2.zero;
        bioTitleRt.sizeDelta = new Vector2(0f, 28f);

        var bioGo = CreateText("BioBody", parent, "", 20, TextAnchor.UpperLeft);
        bioGo.color = TextBlack;
        bioGo.horizontalOverflow = HorizontalWrapMode.Wrap;
        bioGo.verticalOverflow = VerticalWrapMode.Overflow;
        _bioText = bioGo;
        var bioRt = bioGo.rectTransform;
        bioRt.anchorMin = new Vector2(0.08f, 0.24f);
        bioRt.anchorMax = new Vector2(0.92f, 0.46f);
        bioRt.offsetMin = Vector2.zero;
        bioRt.offsetMax = Vector2.zero;

        var nickTitle = CreateText("NickTitle", parent, Loc.Choose("设置昵称", "Set Nickname"), 24, TextAnchor.UpperLeft);
        nickTitle.color = TextBlack;
        nickTitle.fontStyle = FontStyle.Bold;
        var nickTitleRt = nickTitle.rectTransform;
        nickTitleRt.anchorMin = new Vector2(0.08f, 0.22f);
        nickTitleRt.anchorMax = new Vector2(0.92f, 0.22f);
        nickTitleRt.pivot = new Vector2(0f, 1f);
        nickTitleRt.anchoredPosition = Vector2.zero;
        nickTitleRt.sizeDelta = new Vector2(0f, 32f);

        var inputGo = CreateUiObject<Image>("NicknameInput", parent);
        inputGo.color = PanelWhite;
        AddBorder(inputGo.transform, LguHubAuthUILayout.InputBorder, 2f);
        var inputRt = inputGo.rectTransform;
        inputRt.anchorMin = new Vector2(0.08f, 0f);
        inputRt.anchorMax = new Vector2(0.92f, 0f);
        inputRt.pivot = new Vector2(0.5f, 0f);
        inputRt.anchoredPosition = new Vector2(-36f, 118f);
        inputRt.sizeDelta = new Vector2(-72f, 58f);

        _nicknameInput = inputGo.gameObject.AddComponent<InputField>();
        var text = CreateText("Text", inputGo.transform, "", 28, TextAnchor.MiddleLeft);
        text.color = TextBlack;
        StretchWithPadding(text.rectTransform, 16f, 8f);
        var placeholder = CreateText("Placeholder", inputGo.transform, Loc.Choose("输入昵称（1-24字符）", "Enter nickname (1–24 chars)"), 24, TextAnchor.MiddleLeft);
        placeholder.color = TextMuted;
        StretchWithPadding(placeholder.rectTransform, 16f, 8f);
        _nicknameInput.textComponent = text;
        _nicknameInput.placeholder = placeholder;

        _randomButton = CreateButton("RandomButton", parent);
        var randomRt = _randomButton.GetComponent<RectTransform>();
        randomRt.anchorMin = new Vector2(0.92f, 0f);
        randomRt.anchorMax = new Vector2(0.92f, 0f);
        randomRt.pivot = new Vector2(1f, 0f);
        randomRt.anchoredPosition = new Vector2(-8f, 118f);
        randomRt.sizeDelta = new Vector2(58f, 58f);
        _randomButton.GetComponent<Image>().color = BtnDark;
        var randomLabel = CreateText("Text", _randomButton.transform, "🎲", 24, TextAnchor.MiddleCenter);
        randomLabel.color = Gold;
        _randomButton.onClick.AddListener(OnRandomNicknameClicked);

        _statusText = CreateText("Status", parent, Loc.Choose("选择角色并设置昵称（仅首次注册需要）", "Choose a character and set nickname (first registration only)"), 18, TextAnchor.LowerCenter);
        var statusRt = _statusText.rectTransform;
        statusRt.anchorMin = new Vector2(0.08f, 0f);
        statusRt.anchorMax = new Vector2(0.92f, 0f);
        statusRt.pivot = new Vector2(0.5f, 0f);
        statusRt.anchoredPosition = new Vector2(0f, 72f);
        statusRt.sizeDelta = new Vector2(0f, 40f);
        _statusText.color = Gold;

        _confirmButton = CreateButton("ConfirmButton", parent);
        var confirmRt = _confirmButton.GetComponent<RectTransform>();
        confirmRt.anchorMin = new Vector2(0.5f, 0f);
        confirmRt.anchorMax = new Vector2(0.5f, 0f);
        confirmRt.pivot = new Vector2(0.5f, 0f);
        confirmRt.anchoredPosition = new Vector2(0f, 16f);
        confirmRt.sizeDelta = new Vector2(280f, 54f);
        StylePrimaryButton(_confirmButton, Loc.Choose("确认并进入", "Confirm & Enter"));
        _confirmButton.onClick.AddListener(OnConfirmClicked);
    }

    Text CreateInfoLine(Transform parent, string name, string label, string value, float anchorY)
    {
        var row = new GameObject(name, typeof(RectTransform));
        row.transform.SetParent(parent, false);
        var rowRt = row.GetComponent<RectTransform>();
        rowRt.anchorMin = new Vector2(0.08f, anchorY);
        rowRt.anchorMax = new Vector2(0.92f, anchorY);
        rowRt.pivot = new Vector2(0f, 1f);
        rowRt.anchoredPosition = Vector2.zero;
        rowRt.sizeDelta = new Vector2(0f, 34f);

        var labelText = CreateText("Label", row.transform, label, 20, TextAnchor.MiddleLeft);
        labelText.color = TextMuted;
        var labelRt = labelText.rectTransform;
        labelRt.anchorMin = new Vector2(0f, 0f);
        labelRt.anchorMax = new Vector2(0.34f, 1f);
        labelRt.offsetMin = Vector2.zero;
        labelRt.offsetMax = Vector2.zero;

        var valueText = CreateText("Value", row.transform, value, 22, TextAnchor.MiddleLeft);
        valueText.color = TextBlack;
        valueText.fontStyle = FontStyle.Bold;
        var valueRt = valueText.rectTransform;
        valueRt.anchorMin = new Vector2(0.34f, 0f);
        valueRt.anchorMax = new Vector2(1f, 1f);
        valueRt.offsetMin = Vector2.zero;
        valueRt.offsetMax = Vector2.zero;
        return valueText;
    }

    void BuildNicknameArea(Transform parent)
    {
        BuildProfileArea(parent);
    }

    PlayerCharacterCatalog.Option GetOptionSafe(int index)
    {
        if (catalog == null || index < 0 || index >= AvatarCharacterIds.Length)
            return null;
        return catalog.GetById(AvatarCharacterIds[index]);
    }

    void OnAvatarClicked(int index)
    {
        _selectedIndex = index;
        RefreshAvatarSelection();
        RefreshCharacterInfoPanel();
        RefreshPreviewModel();
        StartCoroutine(CoRefreshPreviewNextFrame());
    }

    void RefreshCharacterInfoPanel()
    {
        if (catalog != null)
            catalog.EnsureDefaultOptionsIfEmpty();

        var option = GetOptionSafe(_selectedIndex) ?? catalog?.GetDefault();
        string account = serverSync != null ? serverSync.SavedUsername : "";
        if (string.IsNullOrEmpty(account) && GameServerSync.Instance != null)
            account = GameServerSync.Instance.SavedUsername;

        if (_accountIdText != null)
            _accountIdText.text = string.IsNullOrEmpty(account) ? "—" : account;
        if (_characterIdText != null)
            _characterIdText.text = option != null ? option.id : "—";
        if (_characterNameText != null)
            _characterNameText.text = option != null ? PlayerCharacterCatalog.GetLocalizedDisplayName(option) : "—";
        if (_bioText != null)
            _bioText.text = option != null
                ? PlayerCharacterCatalog.GetLocalizedBio(option)
                : Loc.Choose("暂无角色介绍。", "No character bio yet.");
    }

    static string GetAvatarFallbackGlyph(PlayerCharacterCatalog.Option option)
    {
        if (option == null || string.IsNullOrEmpty(option.id))
            return "?";
        if (option.id.EndsWith("_b", StringComparison.OrdinalIgnoreCase)) return Loc.Choose("博", "PhD");
        if (option.id.EndsWith("_a", StringComparison.OrdinalIgnoreCase)) return Loc.Choose("研", "Grad");
        if (option.id.Contains("female", StringComparison.OrdinalIgnoreCase)) return Loc.Choose("大", "UG");
        return "?";
    }

    void RefreshAvatarSelection()
    {
        if (_avatarFrames == null)
            return;
        for (int i = 0; i < _avatarFrames.Length; i++)
        {
            if (_avatarFrames[i] == null)
                continue;
            bool selected = i == _selectedIndex;
            _avatarFrames[i].color = selected ? Gold : PanelWhite;
        }
    }

    void RefreshPreviewModel()
    {
        EnsurePreviewStage();
        if (_previewStageRoot == null)
            return;

        if (_previewInstance != null)
            Destroy(_previewInstance);

        ResetPreviewInteraction();

        var option = GetOptionSafe(_selectedIndex) ?? catalog?.GetDefault();
        if (option == null)
            return;

        var prefab = catalog != null ? catalog.LoadPrefab(option) : null;
        if (prefab == null)
        {
            Debug.LogWarning("[PlayerCharacterRegisterUI] 无法加载角色 prefab: " + option.id);
            return;
        }

        _previewInstance = Instantiate(prefab, _previewStageRoot);
        PlayerCharacterCatalog.PreparePrefabInstance(_previewInstance, option);
        SetLayerRecursively(_previewInstance, _previewCamera != null ? _previewCamera.gameObject.layer : 0);
        FitPreviewModel(_previewInstance, option);
        ApplyPreviewView();
    }

    void ResetPreviewInteraction()
    {
        _previewYawOffset = 0f;
        _previewZoom = 1f;
    }

    internal void HandlePreviewDrag(float deltaX)
    {
        if (_previewInstance == null)
            return;
        _previewYawOffset -= deltaX * PreviewDragSensitivity;
        ApplyPreviewView();
    }

    internal void HandlePreviewScroll(float scrollY)
    {
        if (_previewInstance == null || Mathf.Approximately(scrollY, 0f))
            return;
        _previewZoom = Mathf.Clamp(_previewZoom + scrollY * PreviewZoomStep, PreviewZoomMin, PreviewZoomMax);
        ApplyPreviewView();
    }

    void ApplyPreviewView()
    {
        if (_previewInstance == null)
            return;

        var option = GetOptionSafe(_selectedIndex) ?? catalog?.GetDefault();
        if (option == null)
            return;

        _previewInstance.transform.localRotation = Quaternion.Euler(
            option.previewPitchDegrees,
            option.previewYawDegrees + _previewYawOffset,
            0f);

        if (_previewCamera != null)
        {
            float distance = PreviewCameraDistance / _previewZoom;
            _previewCamera.transform.localPosition = new Vector3(0f, PreviewLookTargetLocalY + 0.08f, distance);
            if (_previewLookTarget != null)
                _previewCamera.transform.LookAt(_previewLookTarget);
        }

        RenderPreviewFrame();
    }

    void FitPreviewModel(GameObject modelRoot, PlayerCharacterCatalog.Option option)
    {
        if (modelRoot == null || option == null)
            return;

        modelRoot.transform.localRotation = Quaternion.Euler(
            option.previewPitchDegrees,
            option.previewYawDegrees,
            0f);
        modelRoot.transform.localScale = Vector3.one;
        modelRoot.transform.localPosition = Vector3.zero;

        var renderers = modelRoot.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
        {
            modelRoot.transform.localScale = Vector3.one * Mathf.Max(0.01f, option.previewScale);
            modelRoot.transform.localPosition = option.previewOffset;
            return;
        }

        float fitScale = option.previewScale;
        if (TryGetLocalBounds(modelRoot.transform, renderers, out Bounds localBounds))
        {
            float height = Mathf.Max(localBounds.size.y, 0.01f);
            const float targetHeight = PreviewTargetHeight;
            fitScale = (targetHeight / height) * Mathf.Max(0.01f, option.previewScale);
        }

        modelRoot.transform.localScale = Vector3.one * fitScale;
        AlignModelToPreviewCenter(modelRoot, option);
    }

    void AlignModelToPreviewCenter(GameObject modelRoot, PlayerCharacterCatalog.Option option)
    {
        var renderers = modelRoot.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
            return;

        Bounds worldBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                worldBounds.Encapsulate(renderers[i].bounds);
        }

        Vector3 targetWorld = _previewStageRoot.position
            + new Vector3(0f, PreviewLookTargetLocalY, 0f)
            + option.previewOffset;

        if (worldBounds.size.sqrMagnitude >= 0.0001f)
        {
            modelRoot.transform.position += targetWorld - worldBounds.center;
            return;
        }

        if (!TryGetLocalBounds(modelRoot.transform, renderers, out Bounds localBounds))
            return;

        Vector3 targetLocal = new Vector3(0f, PreviewLookTargetLocalY, 0f) + option.previewOffset;
        modelRoot.transform.localPosition = targetLocal - Vector3.Scale(localBounds.center, modelRoot.transform.localScale);
    }

    static bool TryGetLocalBounds(Transform root, Renderer[] renderers, out Bounds localBounds)
    {
        localBounds = new Bounds();
        bool hasBounds = false;

        for (int i = 0; i < renderers.Length; i++)
        {
            var renderer = renderers[i];
            if (renderer == null)
                continue;

            Bounds meshBounds;
            if (renderer is SkinnedMeshRenderer skinned && skinned.sharedMesh != null)
                meshBounds = skinned.sharedMesh.bounds;
            else if (renderer.TryGetComponent(out MeshFilter meshFilter) && meshFilter.sharedMesh != null)
                meshBounds = meshFilter.sharedMesh.bounds;
            else
                continue;

            EncapsulateBoundsCorners(root, renderer.transform, meshBounds, ref localBounds, ref hasBounds);
        }

        return hasBounds && localBounds.size.sqrMagnitude > 0.0001f;
    }

    static void EncapsulateBoundsCorners(
        Transform root,
        Transform rendererTransform,
        Bounds meshBounds,
        ref Bounds localBounds,
        ref bool hasBounds)
    {
        var center = meshBounds.center;
        var extents = meshBounds.extents;
        for (int xi = -1; xi <= 1; xi += 2)
        {
            for (int yi = -1; yi <= 1; yi += 2)
            {
                for (int zi = -1; zi <= 1; zi += 2)
                {
                    var corner = center + Vector3.Scale(extents, new Vector3(xi, yi, zi));
                    var localCorner = root.InverseTransformPoint(rendererTransform.TransformPoint(corner));
                    if (!hasBounds)
                    {
                        localBounds = new Bounds(localCorner, Vector3.zero);
                        hasBounds = true;
                    }
                    else
                    {
                        localBounds.Encapsulate(localCorner);
                    }
                }
            }
        }
    }

    void EnsurePreviewStage()
    {
        if (_previewStageRoot == null)
        {
            _previewStageRoot = new GameObject("PlayerRegisterPreviewStage").transform;
            _previewStageRoot.SetPositionAndRotation(new Vector3(-500f, -500f, -500f), Quaternion.identity);
            DontDestroyOnLoad(_previewStageRoot.gameObject);

            var lightGo = new GameObject("PreviewLight");
            lightGo.transform.SetParent(_previewStageRoot, false);
            lightGo.transform.localRotation = Quaternion.Euler(35f, -35f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;

            var fillGo = new GameObject("PreviewFillLight");
            fillGo.transform.SetParent(_previewStageRoot, false);
            fillGo.transform.localRotation = Quaternion.Euler(10f, 120f, 0f);
            var fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.intensity = 0.45f;

            var lookTargetGo = new GameObject("PreviewLookTarget");
            lookTargetGo.transform.SetParent(_previewStageRoot, false);
            lookTargetGo.transform.localPosition = new Vector3(0f, PreviewLookTargetLocalY, 0f);
            _previewLookTarget = lookTargetGo.transform;

            var camGo = new GameObject("PreviewCamera");
            camGo.transform.SetParent(_previewStageRoot, false);
            camGo.transform.localPosition = new Vector3(0f, PreviewLookTargetLocalY + 0.08f, PreviewCameraDistance);
            _previewCamera = camGo.AddComponent<Camera>();
            _previewCamera.transform.LookAt(_previewLookTarget);
            _previewCamera.clearFlags = CameraClearFlags.SolidColor;
            _previewCamera.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 1f);
            _previewCamera.fieldOfView = 34f;
            _previewCamera.nearClipPlane = 0.05f;
            _previewCamera.farClipPlane = 20f;
            _previewCamera.enabled = true;
        }

        if (_previewTexture == null)
        {
            _previewTexture = CreatePreviewRenderTexture();
        }
        else if (_previewTexture.width != PreviewRenderWidth || _previewTexture.height != PreviewRenderHeight)
        {
            _previewTexture.Release();
            Destroy(_previewTexture);
            _previewTexture = CreatePreviewRenderTexture();
        }

        if (_previewCamera != null)
        {
            _previewCamera.targetTexture = _previewTexture;
            _previewCamera.aspect = (float)PreviewRenderWidth / PreviewRenderHeight;
        }

        BindPreviewTexture();
    }

    static RenderTexture CreatePreviewRenderTexture()
    {
        var texture = new RenderTexture(PreviewRenderWidth, PreviewRenderHeight, 16, RenderTextureFormat.ARGB32);
        texture.Create();
        return texture;
    }

    void DestroyPreviewStage()
    {
        if (_previewInstance != null)
        {
            Destroy(_previewInstance);
            _previewInstance = null;
        }
        if (_previewStageRoot != null)
        {
            Destroy(_previewStageRoot.gameObject);
            _previewStageRoot = null;
            _previewLookTarget = null;
            _previewCamera = null;
        }
        if (_previewTexture != null)
        {
            _previewTexture.Release();
            Destroy(_previewTexture);
            _previewTexture = null;
        }
    }

    void OnRandomNicknameClicked()
    {
        if (_nicknameInput != null)
            _nicknameInput.text = MenuLoginPortalFlow.GenerateRandomNicknamePublic();
        if (_statusText != null)
            _statusText.text = Loc.Choose("已生成随机昵称，可直接确认或继续修改", "Random nickname generated — confirm or edit");
    }

    void OnConfirmClicked()
    {
        if (serverSync == null)
            serverSync = GameServerSync.Instance ?? FindObjectOfType<GameServerSync>(true);
        if (serverSync == null)
            return;

        var option = GetOptionSafe(_selectedIndex) ?? catalog?.GetDefault();
        if (option == null || string.IsNullOrEmpty(option.id))
        {
            if (_statusText != null)
                _statusText.text = Loc.Choose("未配置可选角色，请先运行编辑器菜单导入", "No characters configured — run editor import menu");
            return;
        }

        string nick = _nicknameInput != null ? _nicknameInput.text.Trim() : "";
        if (string.IsNullOrEmpty(nick))
        {
            if (_statusText != null)
                _statusText.text = Loc.Choose("昵称不能为空", "Nickname cannot be empty");
            return;
        }
        if (nick.Length > 24)
        {
            if (_statusText != null)
                _statusText.text = Loc.Choose("昵称最多 24 个字符", "Nickname max 24 characters");
            return;
        }

        if (_confirmButton != null)
            _confirmButton.interactable = false;
        if (_randomButton != null)
            _randomButton.interactable = false;
        if (_statusText != null)
            _statusText.text = Loc.Choose("保存角色与昵称中...", "Saving character and nickname...");

        StartCoroutine(CoSubmitProfile(nick, option.id));
    }

    IEnumerator CoSubmitProfile(string nick, string characterId)
    {
        bool ok = false;
        string err = "";
        yield return StartCoroutine(serverSync.CoSetMyProfile(
            nick,
            characterId,
            _ => ok = true,
            e => err = e ?? Loc.Choose("保存失败", "Save failed")));

        if (!ok)
        {
            if (_statusText != null)
                _statusText.text = Loc.F("保存失败：{0}", "Save failed: {0}", err);
            if (_confirmButton != null)
                _confirmButton.interactable = true;
            if (_randomButton != null)
                _randomButton.interactable = true;
            yield break;
        }

        Hide();
        _onCompleted?.Invoke();
    }

    static void StylePrimaryButton(Button btn, string label)
    {
        if (btn == null) return;
        var img = btn.GetComponent<Image>();
        if (img != null) img.color = BtnDark;
        var colors = btn.colors;
        colors.normalColor = BtnDark;
        colors.highlightedColor = BtnDarkHover;
        colors.pressedColor = new Color(0.18f, 0.18f, 0.18f, 1f);
        colors.selectedColor = BtnDark;
        btn.colors = colors;

        var txt = btn.GetComponentInChildren<Text>(true);
        if (txt == null)
        {
            txt = CreateText("Text", btn.transform, label, 26, TextAnchor.MiddleCenter);
            StretchFull(txt.rectTransform);
        }
        else
        {
            txt.text = label;
            txt.fontSize = 26;
        }
        txt.color = Color.white;
    }

    static void AddBorder(Transform parent, Color color, float thickness)
    {
        CreateBorderEdge(parent, "BorderLeft", color, thickness, true, false, false, false);
        CreateBorderEdge(parent, "BorderRight", color, thickness, false, true, false, false);
        CreateBorderEdge(parent, "BorderTop", color, thickness, false, false, true, false);
        CreateBorderEdge(parent, "BorderBottom", color, thickness, false, false, false, true);
    }

    static void CreateBorderEdge(
        Transform parent, string name, Color color, float thickness,
        bool left, bool right, bool top, bool bottom)
    {
        var edge = CreateUiObject<Image>(name, parent);
        edge.color = color;
        var rt = edge.rectTransform;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        if (left)
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(thickness, 0f);
        }
        else if (right)
        {
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.sizeDelta = new Vector2(thickness, 0f);
        }
        else if (top)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.sizeDelta = new Vector2(0f, thickness);
        }
        else if (bottom)
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.sizeDelta = new Vector2(0f, thickness);
        }
    }

    static void AddGoldCorner(Transform parent, bool left, bool top, bool right, bool bottom)
    {
        bool valid = (left && top) || (right && top) || (left && bottom) || (right && bottom);
        if (!valid)
            return;

        var decor = new GameObject("CornerDecor", typeof(RectTransform));
        decor.transform.SetParent(parent, false);
        var rt = decor.GetComponent<RectTransform>();
        const float len = 28f;
        const float thick = 3f;
        const float inset = 8f;
        rt.sizeDelta = new Vector2(len, len);

        if (left && top)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(inset, -inset);
        }
        else if (right && top)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-inset, -inset);
        }
        else if (left && bottom)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(inset, inset);
        }
        else if (right && bottom)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-inset, inset);
        }

        var h = CreateUiObject<Image>("BarH", decor.transform);
        h.color = LguHubAuthUILayout.CornerGold;
        var hrt = h.rectTransform;
        hrt.anchorMin = hrt.anchorMax = new Vector2(left ? 0f : 1f, top ? 1f : 0f);
        hrt.pivot = new Vector2(left ? 0f : 1f, top ? 1f : 0f);
        hrt.anchoredPosition = Vector2.zero;
        hrt.sizeDelta = new Vector2(len, thick);

        var v = CreateUiObject<Image>("BarV", decor.transform);
        v.color = LguHubAuthUILayout.CornerGold;
        var vrt = v.rectTransform;
        vrt.anchorMin = vrt.anchorMax = new Vector2(left ? 0f : 1f, top ? 1f : 0f);
        vrt.pivot = new Vector2(left ? 0f : 1f, top ? 1f : 0f);
        vrt.anchoredPosition = Vector2.zero;
        vrt.sizeDelta = new Vector2(thick, len);
    }

    static Button CreateButton(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var image = go.AddComponent<Image>();
        image.color = Color.white;
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = image;
        return btn;
    }

    static T CreateUiObject<T>(string name, Transform parent) where T : Component
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.AddComponent<T>();
    }

    static Text CreateText(string name, Transform parent, string value, int fontSize, TextAnchor anchor)
    {
        var text = CreateUiObject<Text>(name, parent);
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = anchor;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void StretchWithPadding(RectTransform rt, float horizontal, float vertical = -1f)
    {
        if (vertical < 0f)
            vertical = horizontal;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(horizontal, vertical);
        rt.offsetMax = new Vector2(-horizontal, -vertical);
    }

    static void StretchTop(RectTransform rt, float top, float height)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -top);
        rt.sizeDelta = new Vector2(0f, height);
    }

    static void SetLayerRecursively(GameObject go, int layer)
    {
        if (go == null)
            return;
        go.layer = layer;
        var t = go.transform;
        for (int i = 0; i < t.childCount; i++)
            SetLayerRecursively(t.GetChild(i).gameObject, layer);
    }
}

sealed class PlayerCharacterPreviewInput : MonoBehaviour, IDragHandler, IScrollHandler
{
    PlayerCharacterRegisterUI _owner;

    public void Initialize(PlayerCharacterRegisterUI owner)
    {
        _owner = owner;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_owner == null)
            return;
        _owner.HandlePreviewDrag(eventData.delta.x);
    }

    public void OnScroll(PointerEventData eventData)
    {
        if (_owner == null)
            return;
        _owner.HandlePreviewScroll(eventData.scrollDelta.y);
    }
}
