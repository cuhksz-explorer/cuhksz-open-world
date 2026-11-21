using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 运行时对话 UI 管理器：
/// - 靠近 NPC 按 F 进入对话
/// - 下方约 30% 显示对白
/// - 右侧中部显示选项，自动附加“离开”
/// - 对话时隐藏其他 UI，关闭后恢复
/// </summary>
[DefaultExecutionOrder(560)]
public class NpcDialogueUIManager : MonoBehaviour
{
    const string DefaultAlibabaTmpResourcesPath = "Fonts & Materials/AlibabaPuHuiTi-3-75-SemiBold SDF";

    static readonly List<NpcDialogueInteractor> s_interactors = new List<NpcDialogueInteractor>(64);
    static NpcDialogueUIManager s_instance;

    [Header("玩家引用")]
    public PlayerMovement playerMovement;
    public Transform playerTransform;
    public FirstPersonCamera firstPersonCamera;

    [Header("字体")]
    [Tooltip("留空则自动加载阿里巴巴普惠体 TMP SDF")]
    public TMP_FontAsset dialogueFont;

    [Header("外观")]
    public Color panelColor = new Color(0f, 0f, 0f, 0.62f);
    public Color optionButtonColor = new Color(1f, 1f, 1f, 0.1f);
    public Color optionTextColor = Color.white;
    public int canvasSortingOrder = 950;

    [Header("提示")]
    public bool showNearPrompt = true;

    readonly List<Button> _optionButtons = new List<Button>(8);
    readonly List<CanvasState> _hiddenCanvases = new List<CanvasState>(64);

    TMP_FontAsset _resolvedFont;
    Canvas _canvas;
    RectTransform _root;
    RectTransform _bottomPanel;
    RectTransform _optionsPanel;
    TextMeshProUGUI _nameText;
    TextMeshProUGUI _contentText;
    TextMeshProUGUI _nearPromptText;
    VerticalLayoutGroup _optionLayout;

    NpcDialogueInteractor _currentNpc;
    int _currentNodeIndex = -1;
    bool _isDialogueOpen;
    CursorLockMode _cursorLockBeforeDialogue;
    bool _cursorVisibleBeforeDialogue;
    bool _playerMovementWasEnabledBeforeDialogue;
    public bool IsDialogueOpen => _isDialogueOpen;

    struct CanvasState
    {
        public Canvas canvas;
        public bool enabled;
        public bool activeSelf;
    }

    public static void Register(NpcDialogueInteractor interactor)
    {
        if (interactor == null) return;
        if (!s_interactors.Contains(interactor))
            s_interactors.Add(interactor);
    }

    public static void Unregister(NpcDialogueInteractor interactor)
    {
        if (interactor == null) return;
        s_interactors.Remove(interactor);
    }

    void Awake()
    {
        if (s_instance != null && s_instance != this)
        {
            Destroy(gameObject);
            return;
        }

        s_instance = this;
        ResolveReferences();
        ResolveFont();
        BuildUi();
        SetDialogueVisible(false);
    }

    void OnEnable() => Loc.OnLanguageChanged += OnLanguageChanged;
    void OnDisable() => Loc.OnLanguageChanged -= OnLanguageChanged;

    void OnLanguageChanged()
    {
        if (_isDialogueOpen && _currentNpc != null)
            ShowNode(_currentNodeIndex);
    }

    void ResolveReferences()
    {
        if (playerMovement == null)
            playerMovement = FindObjectOfType<PlayerMovement>(true);
        if (playerTransform == null && playerMovement != null)
            playerTransform = playerMovement.transform;
        if (firstPersonCamera == null)
            firstPersonCamera = FindObjectOfType<FirstPersonCamera>(true);
    }

    void ResolveFont()
    {
        _resolvedFont = dialogueFont != null
            ? dialogueFont
            : Resources.Load<TMP_FontAsset>(DefaultAlibabaTmpResourcesPath);
    }

    void BuildUi()
    {
        _canvas = gameObject.GetComponent<Canvas>();
        if (_canvas == null) _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = canvasSortingOrder;

        var scaler = gameObject.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        if (gameObject.GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();

        _root = _canvas.GetComponent<RectTransform>();

        _bottomPanel = CreatePanel("DialogueBottomPanel", _root, panelColor);
        AnchorStretch(_bottomPanel, 0f, 0f, 1f, 0.32f, 0f, 0f, 0f, 0f);

        _nameText = CreateText("NpcName", _bottomPanel, "", 30, FontStyles.Bold, TextAlignmentOptions.Left);
        AnchorStretch(_nameText.rectTransform, 0f, 0.73f, 1f, 1f, 48f, -14f, -420f, 0f);

        _contentText = CreateText("DialogueContent", _bottomPanel, "", 32, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        _contentText.enableWordWrapping = true;
        AnchorStretch(_contentText.rectTransform, 0f, 0f, 1f, 0.78f, 48f, 24f, -48f, 0f);

        _optionsPanel = CreatePanel("DialogueOptionsPanel", _root, new Color(0f, 0f, 0f, 0.34f));
        AnchorStretch(_optionsPanel, 0.63f, 0.28f, 0.93f, 0.72f, 0f, 0f, 0f, 0f);

        _optionLayout = _optionsPanel.gameObject.AddComponent<VerticalLayoutGroup>();
        _optionLayout.spacing = 12f;
        _optionLayout.padding = new RectOffset(18, 18, 18, 18);
        _optionLayout.childControlHeight = true;
        _optionLayout.childControlWidth = true;
        _optionLayout.childForceExpandHeight = false;
        _optionLayout.childForceExpandWidth = true;

        var fitter = _optionsPanel.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        _nearPromptText = CreateText("NearPrompt", _root, "", 26, FontStyles.Bold, TextAlignmentOptions.Center);
        _nearPromptText.color = new Color(1f, 1f, 1f, 0.92f);
        AnchorStretch(_nearPromptText.rectTransform, 0.38f, 0.44f, 0.62f, 0.52f, 0f, 0f, 0f, 0f);
    }

    void Update()
    {
        if (playerTransform == null)
            return;

        if (_isDialogueOpen)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
                CloseDialogue(triggerExitEvent: true);
            return;
        }

        var nearby = FindNearestInRange();
        if (showNearPrompt && _nearPromptText != null)
        {
            _nearPromptText.text = nearby != null
                ? Loc.F("[{0}] 与 {1} {2}", "[{0}] Talk to {1} — {2}",
                    nearby.interactKey,
                    Loc.StoryTerm(nearby.npcDisplayName),
                    Loc.StoryTerm(nearby.interactionTypeName))
                : "";
        }

        if (nearby != null && Input.GetKeyDown(nearby.interactKey))
            OpenDialogue(nearby);
    }

    NpcDialogueInteractor FindNearestInRange()
    {
        NpcDialogueInteractor best = null;
        float bestDist = float.MaxValue;
        Vector3 playerPos = playerTransform.position;

        for (int i = s_interactors.Count - 1; i >= 0; i--)
        {
            var it = s_interactors[i];
            if (it == null)
            {
                s_interactors.RemoveAt(i);
                continue;
            }

            if (!it.isActiveAndEnabled || !it.CanStart())
                continue;

            float d = Vector3.Distance(playerPos, it.TriggerPosition);
            if (d <= it.interactDistance && d < bestDist)
            {
                bestDist = d;
                best = it;
            }
        }

        return best;
    }

    void OpenDialogue(NpcDialogueInteractor npc)
    {
        _currentNpc = npc;
        _currentNodeIndex = npc.startNodeIndex;

        HideOtherUiCanvases();
        TogglePlayerControl(false);
        SetDialogueVisible(true);
        npc.onDialogueStarted?.Invoke();

        _isDialogueOpen = true;
        NpcDialogueLookAtDriver.NotifyDialogueOpened();
        ShowNode(_currentNodeIndex);
    }

    void ShowNode(int nodeIndex)
    {
        if (_currentNpc == null)
        {
            CloseDialogue(false);
            return;
        }

        var node = _currentNpc.GetNode(nodeIndex);
        if (node == null)
        {
            _currentNpc.onDialogueCompleted?.Invoke();
            CloseDialogue(false);
            return;
        }

        _currentNodeIndex = nodeIndex;
        if (_nameText != null) _nameText.text = Loc.StoryTerm(_currentNpc.npcDisplayName);
        if (_contentText != null) _contentText.text = node.content ?? "";
        node.onNodeEntered?.Invoke();

        RebuildOptions(node);
    }

    void RebuildOptions(NpcDialogueNode node)
    {
        for (int i = 0; i < _optionButtons.Count; i++)
            if (_optionButtons[i] != null) Destroy(_optionButtons[i].gameObject);
        _optionButtons.Clear();

        if (node.options != null)
        {
            for (int i = 0; i < node.options.Length; i++)
            {
                int captured = i;
                var opt = node.options[i];
                if (opt == null) continue;

                Button btn = CreateOptionButton(
                    Loc.StoryTerm(string.IsNullOrWhiteSpace(opt.label) ? "继续" : opt.label));
                btn.onClick.AddListener(() => OnOptionChosen(node, captured, false));
                _optionButtons.Add(btn);
            }
        }

        if (_currentNpc != null && _currentNpc.appendExitOption)
        {
            Button exitBtn = CreateOptionButton(
                Loc.StoryTerm(string.IsNullOrWhiteSpace(_currentNpc.exitOptionText) ? "离开" : _currentNpc.exitOptionText));
            exitBtn.onClick.AddListener(() => OnOptionChosen(node, -1, true));
            _optionButtons.Add(exitBtn);
        }
    }

    void OnOptionChosen(NpcDialogueNode node, int optionIndex, bool isExitOption)
    {
        if (_currentNpc == null)
            return;

        if (isExitOption)
        {
            CloseDialogue(triggerExitEvent: true);
            return;
        }

        if (node.options == null || optionIndex < 0 || optionIndex >= node.options.Length)
        {
            _currentNpc.onDialogueCompleted?.Invoke();
            CloseDialogue(false);
            return;
        }

        var opt = node.options[optionIndex];
        opt?.onSelected?.Invoke();
        int next = opt != null ? opt.nextNodeIndex : -1;

        if (next < 0)
        {
            _currentNpc.onDialogueCompleted?.Invoke();
            CloseDialogue(false);
            return;
        }

        ShowNode(next);
    }

    void CloseDialogue(bool triggerExitEvent)
    {
        if (_currentNpc != null && triggerExitEvent)
            _currentNpc.onExitOptionChosen?.Invoke();

        if (_currentNpc != null)
            _currentNpc.onDialogueClosed?.Invoke();

        _currentNpc = null;
        _currentNodeIndex = -1;
        _isDialogueOpen = false;
        NpcDialogueLookAtDriver.NotifyDialogueClosed();

        SetDialogueVisible(false);
        RestoreHiddenCanvases();
        TogglePlayerControl(true);
    }

    void HideOtherUiCanvases()
    {
        _hiddenCanvases.Clear();
        var all = FindObjectsOfType<Canvas>(true);
        for (int i = 0; i < all.Length; i++)
        {
            Canvas c = all[i];
            if (c == null || c == _canvas)
                continue;

            var go = c.gameObject;
            if (go == null || !go.scene.IsValid() || !go.scene.isLoaded)
                continue;

            _hiddenCanvases.Add(new CanvasState
            {
                canvas = c,
                enabled = c.enabled,
                activeSelf = go.activeSelf
            });

            c.enabled = false;
            UiLifecycleSafe.SetActiveIfAlive(go, false);
        }
    }

    void RestoreHiddenCanvases()
    {
        for (int i = 0; i < _hiddenCanvases.Count; i++)
        {
            var state = _hiddenCanvases[i];
            if (state.canvas == null)
                continue;

            var go = state.canvas.gameObject;
            if (go == null || !go.scene.IsValid() || !go.scene.isLoaded)
                continue;

            UiLifecycleSafe.SetActiveIfAlive(go, state.activeSelf);
            state.canvas.enabled = state.enabled;
        }
        _hiddenCanvases.Clear();
    }

    void TogglePlayerControl(bool enable)
    {
        if (enable)
        {
            if (playerMovement != null)
            {
                playerMovement.enabled = _playerMovementWasEnabledBeforeDialogue;
                playerMovement.isMovementPaused = false;
            }

            Cursor.lockState = _cursorLockBeforeDialogue;
            Cursor.visible = _cursorVisibleBeforeDialogue;

            if (firstPersonCamera != null)
                firstPersonCamera.SetCursorLocked(_cursorLockBeforeDialogue == CursorLockMode.Locked);
        }
        else
        {
            if (playerMovement != null)
            {
                _playerMovementWasEnabledBeforeDialogue = playerMovement.enabled;
                playerMovement.enabled = false;
            }

            _cursorLockBeforeDialogue = Cursor.lockState;
            _cursorVisibleBeforeDialogue = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (firstPersonCamera != null)
                firstPersonCamera.SetCursorLocked(false);
        }
    }

    void SetDialogueVisible(bool visible)
    {
        if (_bottomPanel != null) UiLifecycleSafe.SetActiveIfAlive(_bottomPanel.gameObject, visible);
        if (_optionsPanel != null) UiLifecycleSafe.SetActiveIfAlive(_optionsPanel.gameObject, visible);
        if (_nearPromptText != null) UiLifecycleSafe.SetActiveIfAlive(_nearPromptText.gameObject, !visible && showNearPrompt);
    }

    RectTransform CreatePanel(string name, Transform parent, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        return go.GetComponent<RectTransform>();
    }

    TextMeshProUGUI CreateText(string name, Transform parent, string text, float size, FontStyles style, TextAlignmentOptions align)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        if (_resolvedFont != null)
            tmp.font = _resolvedFont;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.alignment = align;
        tmp.color = Color.white;
        tmp.enableWordWrapping = true;
        return tmp;
    }

    Button CreateOptionButton(string label)
    {
        var go = new GameObject("OptionButton");
        go.transform.SetParent(_optionsPanel, false);

        var img = go.AddComponent<Image>();
        img.color = optionButtonColor;

        var btn = go.AddComponent<Button>();
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0f, 58f);

        var txt = CreateText("Label", rt, label, 26, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        txt.color = optionTextColor;
        AnchorStretch(txt.rectTransform, 0f, 0f, 1f, 1f, 16f, 8f, -16f, -8f);

        return btn;
    }

    static void AnchorStretch(RectTransform rt, float minX, float minY, float maxX, float maxY, float left, float bottom, float right, float top)
    {
        rt.anchorMin = new Vector2(minX, minY);
        rt.anchorMax = new Vector2(maxX, maxY);
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(right, top);
    }
}

