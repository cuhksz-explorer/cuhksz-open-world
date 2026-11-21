using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameQuestSystem
{
    public class StoryDialogueUI : MonoBehaviour
    {
        const string DefaultAlibabaTmpResourcesPath = "Fonts & Materials/AlibabaPuHuiTi-3-75-SemiBold SDF";

        [Header("字体")]
        [SerializeField] private TMP_FontAsset dialogueFont;

        [Header("外观")]
        [SerializeField] private Color bottomPanelColor = new Color(0.03f, 0.025f, 0.02f, 0.42f);
        [SerializeField] private Color optionPanelColor = new Color(0f, 0f, 0f, 0f);
        [SerializeField] private Color optionButtonColor = new Color(0.34f, 0.29f, 0.24f, 0.58f);
        [SerializeField] private Color optionButtonBorderColor = new Color(1f, 0.82f, 0.58f, 0.36f);
        [SerializeField] private Color titleColor = new Color(1f, 0.84f, 0.54f, 0.95f);
        [SerializeField] private Color speakerColor = new Color(1f, 0.78f, 0.48f, 0.98f);
        [SerializeField] private int sortingOrder = 970;

        readonly List<Button> _optionButtons = new List<Button>(8);
        readonly List<CanvasState> _hiddenCanvases = new List<CanvasState>(64);

        StoryDialogueDatabase _database;
        StoryDialogueDocument _currentDocument;
        StoryQuestRuntime _runtime;
        StoryQuestNpc _sourceNpc;
        string _rootDocumentId;
        int _pageIndex;
        bool _isOpen;
        public bool IsOpen => _isOpen;
        public StoryQuestNpc ActiveSourceNpc => _isOpen ? _sourceNpc : null;
        public string CurrentDocumentId =>
            _isOpen && _currentDocument != null ? _currentDocument.documentId : null;

        Canvas _canvas;
        RectTransform _root;
        RectTransform _bottomPanel;
        RectTransform _optionsPanel;
        TextMeshProUGUI _taskTitleText;
        TextMeshProUGUI _nameText;
        TextMeshProUGUI _contentText;
        TextMeshProUGUI _hintText;
        TMP_FontAsset _resolvedFont;
        PlayerMovement _playerMovement;
        FirstPersonCamera _firstPersonCamera;
        bool _playerMovementWasEnabled;
        bool _firstPersonSuppressMouseLookBeforeDialogue;

        struct CanvasState
        {
            public Canvas canvas;
            public bool enabled;
            public bool activeSelf;
        }

        void Awake()
        {
            ResolveFont();
            BuildUi();
            SetVisible(false);
        }

        void OnEnable() => Loc.OnLanguageChanged += OnLanguageChanged;
        void OnDisable() => Loc.OnLanguageChanged -= OnLanguageChanged;

        void OnLanguageChanged()
        {
            if (_isOpen)
                ShowCurrentPage();
        }

        void Update()
        {
            if (!_isOpen)
                return;

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Close(false);
                return;
            }

            if (_optionButtons.Count == 0 &&
                (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Return)))
            {
                AdvancePage();
            }
        }

        public void Open(StoryDialogueDatabase database, string documentId, string rootDocumentId, StoryQuestRuntime runtime, StoryQuestNpc sourceNpc)
        {
            if (database == null)
                return;

            var doc = database.FindDocument(documentId);
            if (doc == null)
                return;

            Content1StoryDocxLoader.EnsureHydrated(database, doc);
            if (doc.pages == null || doc.pages.Count == 0)
                return;

            _database = database;
            _currentDocument = doc;
            _rootDocumentId = string.IsNullOrWhiteSpace(rootDocumentId) ? documentId : rootDocumentId;
            _runtime = runtime;
            _sourceNpc = sourceNpc;
            _pageIndex = 0;

            if (!_isOpen)
            {
                HideOtherUiCanvases();
                TogglePlayerControl(false);
                NpcDialogueLookAtDriver.NotifyDialogueOpened();
            }

            _isOpen = true;
            SetVisible(true);
            ShowCurrentPage();
            _runtime?.RefreshStoryNpcVisibility();
        }

        void ShowCurrentPage()
        {
            if (_currentDocument == null || _currentDocument.pages == null || _currentDocument.pages.Count == 0)
            {
                FinishCurrentDocument();
                return;
            }

            _pageIndex = Mathf.Clamp(_pageIndex, 0, _currentDocument.pages.Count - 1);
            var page = _currentDocument.pages[_pageIndex];
            string speaker = string.IsNullOrWhiteSpace(page.speakerName) ? ResolveDocumentSpeaker(_currentDocument) : page.speakerName;
            string body = page.text ?? "";
            Content1StoryEnglishOverlay.TryGetPageSpeaker(_currentDocument.documentId, page.pageNumber, speaker, out speaker);
            Content1StoryEnglishOverlay.TryGetPageText(_currentDocument.documentId, page.pageNumber, out var enBody);
            if (!string.IsNullOrWhiteSpace(enBody))
                body = enBody;
            speaker = StoryProtagonistNameUtility.ResolveSpeakerName(speaker);
            speaker = Loc.StoryTerm(speaker);
            if (_taskTitleText != null) _taskTitleText.text = StoryProtagonistNameUtility.PersonalizeStoryText(BuildTaskTitle(_currentDocument));
            if (_nameText != null) _nameText.text = speaker;
            if (_contentText != null) _contentText.text = StoryProtagonistNameUtility.PersonalizeStoryText(body);
            if (_hintText != null)
                _hintText.text = _pageIndex >= _currentDocument.pages.Count - 1
                    ? ""
                    : Loc.Choose("点击 / Space 继续", "Click / Space to Continue");

            RebuildOptionsIfNeeded();
            PlayPageVoice(page, speaker);
        }

        void PlayPageVoice(StoryDialoguePage page, string speaker)
        {
            StopAllPageVoice();

            if (_currentDocument == null || page == null)
                return;

            if (StoryNarrationVoice.IsNarrationSpeaker(page.speakerName)
                || StoryNarrationVoice.IsNarrationSpeaker(speaker))
            {
                StoryNarrationVoice.Play(_currentDocument.documentId, _pageIndex);
                return;
            }

            StoryDialogueVoice.Play(_currentDocument.documentId, _pageIndex);
        }

        static void StopAllPageVoice()
        {
            StoryNarrationVoice.Stop();
            StoryDialogueVoice.Stop();
        }

        void AdvancePage()
        {
            if (_currentDocument == null)
                return;

            StopAllPageVoice();

            if (_pageIndex < _currentDocument.pages.Count - 1)
            {
                _pageIndex++;
                ShowCurrentPage();
                return;
            }

            FinishCurrentDocument();
        }

        void RebuildOptionsIfNeeded()
        {
            ClearOptions();
            if (_currentDocument == null || _pageIndex < _currentDocument.pages.Count - 1)
            {
                if (_optionsPanel != null) _optionsPanel.gameObject.SetActive(false);
                return;
            }

            if (_currentDocument.choices == null || _currentDocument.choices.Count == 0)
            {
                if (_optionsPanel != null) _optionsPanel.gameObject.SetActive(false);
                return;
            }

            if (_optionsPanel != null) _optionsPanel.gameObject.SetActive(true);
            if (_hintText != null) _hintText.text = "";

            for (int i = 0; i < _currentDocument.choices.Count; i++)
            {
                int captured = i;
                var choice = _currentDocument.choices[i];
                string label = choice != null && !string.IsNullOrWhiteSpace(choice.label) ? choice.label : "继续";
                Content1StoryEnglishOverlay.TryGetChoiceLabel(_currentDocument.documentId, i, label, out label);
                label = StoryProtagonistNameUtility.PersonalizeStoryText(label);
                label = Loc.StoryTerm(label);
                Button btn = CreateOptionButton(label);
                btn.onClick.AddListener(() => Choose(captured));
                _optionButtons.Add(btn);
            }
        }

        void Choose(int choiceIndex)
        {
            if (_currentDocument == null || _currentDocument.choices == null ||
                choiceIndex < 0 || choiceIndex >= _currentDocument.choices.Count)
                return;

            StopAllPageVoice();

            var choice = _currentDocument.choices[choiceIndex];
            if (choice == null || string.IsNullOrWhiteSpace(choice.targetDocumentId))
            {
                FinishCurrentDocument();
                return;
            }

            var next = _database != null ? _database.FindDocument(choice.targetDocumentId) : null;
            if (next == null)
            {
                FinishCurrentDocument();
                return;
            }

            Content1StoryDocxLoader.EnsureHydrated(_database, next);
            if (next.pages == null || next.pages.Count == 0)
            {
                FinishCurrentDocument();
                return;
            }

            _currentDocument = next;
            _pageIndex = 0;
            ShowCurrentPage();
            _runtime?.RefreshStoryNpcVisibility();
        }

        void FinishCurrentDocument()
        {
            _runtime?.CompleteRootDocument(_rootDocumentId, _sourceNpc);
            Close(true);
        }

        void Close(bool completed)
        {
            if (!_isOpen)
                return;

            _isOpen = false;
            StopAllPageVoice();
            NpcDialogueLookAtDriver.NotifyDialogueClosed();
            _currentDocument = null;
            _rootDocumentId = "";
            ClearOptions();
            SetVisible(false);
            RestoreHiddenCanvases();
            TogglePlayerControl(true);
            if (completed && _sourceNpc != null)
                _sourceNpc.NotifyStoryCompleted();
            _sourceNpc = null;
            _runtime?.RefreshStoryNpcVisibility();
        }

        string ResolveDocumentSpeaker(StoryDialogueDocument doc)
        {
            if (doc == null)
                return Loc.StoryTerm("旁白");
            var npc = _database != null ? _database.FindNpc(doc.npcId) : null;
            if (npc != null && !string.IsNullOrWhiteSpace(npc.displayName))
                return Loc.StoryTerm(npc.displayName);
            return Loc.StoryTerm("旁白");
        }

        string BuildTaskTitle(StoryDialogueDocument doc)
        {
            if (doc == null)
                return "";
            string prefix = doc.category == QuestCategory.Main
                ? Loc.StoryTerm("主线剧情")
                : Loc.StoryTerm("支线剧情");
            return prefix + " · " + doc.title;
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
            _canvas.sortingOrder = sortingOrder;

            var scaler = gameObject.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            if (gameObject.GetComponent<GraphicRaycaster>() == null)
                gameObject.AddComponent<GraphicRaycaster>();

            _root = _canvas.GetComponent<RectTransform>();
            _bottomPanel = CreatePanel("StoryDialogueBottomPanel", _root, bottomPanelColor);
            AnchorStretch(_bottomPanel, 0.03f, 0.035f, 0.97f, 0.29f, 0f, 0f, 0f, 0f);
            AddOutline(_bottomPanel.gameObject, new Color(1f, 0.74f, 0.36f, 0.18f), new Vector2(1.4f, -1.4f));

            _taskTitleText = CreateText("TaskTitle", _bottomPanel, "", 22, FontStyles.Bold, TextAlignmentOptions.Center);
            _taskTitleText.color = titleColor;
            AnchorStretch(_taskTitleText.rectTransform, 0f, 0.74f, 1f, 1f, 48f, -8f, -48f, -4f);

            _nameText = CreateText("SpeakerName", _bottomPanel, "", 28, FontStyles.Bold, TextAlignmentOptions.Center);
            _nameText.color = speakerColor;
            AnchorStretch(_nameText.rectTransform, 0f, 0.51f, 1f, 0.76f, 48f, -8f, -48f, 0f);

            _contentText = CreateText("PageText", _bottomPanel, "", 30, FontStyles.Normal, TextAlignmentOptions.Top);
            AnchorStretch(_contentText.rectTransform, 0f, 0.13f, 1f, 0.54f, 82f, 10f, -82f, 0f);

            _hintText = CreateText("AdvanceHint", _bottomPanel, "", 18, FontStyles.Normal, TextAlignmentOptions.Right);
            _hintText.color = new Color(1f, 1f, 1f, 0.65f);
            AnchorStretch(_hintText.rectTransform, 0.62f, 0f, 1f, 0.13f, 0f, 4f, -48f, 0f);

            _optionsPanel = CreatePanel("StoryDialogueOptionsPanel", _root, optionPanelColor);
            AnchorStretch(_optionsPanel, 0.63f, 0.33f, 0.93f, 0.71f, 0f, 0f, 0f, 0f);
            var layout = _optionsPanel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 18f;
            layout.padding = new RectOffset(18, 18, 24, 24);
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            var fitter = _optionsPanel.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        RectTransform CreatePanel(string objectName, Transform parent, Color color)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            return go.GetComponent<RectTransform>();
        }

        TextMeshProUGUI CreateText(string objectName, Transform parent, string text, float size, FontStyles style, TextAlignmentOptions align)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            if (_resolvedFont != null) tmp.font = _resolvedFont;
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
            var go = new GameObject("StoryOptionButton");
            go.transform.SetParent(_optionsPanel, false);
            var img = go.AddComponent<Image>();
            img.color = optionButtonColor;
            AddOutline(go, optionButtonBorderColor, new Vector2(1.2f, -1.2f));
            var btn = go.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.88f, 0.68f, 1f);
            colors.pressedColor = new Color(0.86f, 0.62f, 0.38f, 1f);
            btn.colors = colors;
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0f, 64f);
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = 64f;
            le.preferredHeight = 64f;
            le.flexibleHeight = 0f;
            var txt = CreateText("Label", rt, "▣  " + label, 24, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            txt.color = new Color(1f, 0.96f, 0.88f, 0.96f);
            AnchorStretch(txt.rectTransform, 0f, 0f, 1f, 1f, 18f, 7f, -18f, -7f);
            return btn;
        }

        static void AddOutline(GameObject go, Color color, Vector2 distance)
        {
            if (go == null)
                return;
            var outline = go.GetComponent<Outline>();
            if (outline == null)
                outline = go.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = distance;
        }

        void ClearOptions()
        {
            for (int i = 0; i < _optionButtons.Count; i++)
                if (_optionButtons[i] != null)
                    Destroy(_optionButtons[i].gameObject);
            _optionButtons.Clear();
        }

        void SetVisible(bool visible)
        {
            if (_bottomPanel != null) _bottomPanel.gameObject.SetActive(visible);
            if (_optionsPanel != null) _optionsPanel.gameObject.SetActive(visible && _optionButtons.Count > 0);
        }

        void HideOtherUiCanvases()
        {
            _hiddenCanvases.Clear();
            var all = FindObjectsOfType<Canvas>(true);
            for (int i = 0; i < all.Length; i++)
            {
                Canvas c = all[i];
                if (c == null || c == _canvas) continue;
                var go = c.gameObject;
                if (go == null || !go.scene.IsValid() || !go.scene.isLoaded) continue;
                _hiddenCanvases.Add(new CanvasState { canvas = c, enabled = c.enabled, activeSelf = go.activeSelf });
                c.enabled = false;
                go.SetActive(false);
            }
        }

        void RestoreHiddenCanvases()
        {
            for (int i = 0; i < _hiddenCanvases.Count; i++)
            {
                var state = _hiddenCanvases[i];
                if (state.canvas == null) continue;
                var go = state.canvas.gameObject;
                if (go == null || !go.scene.IsValid() || !go.scene.isLoaded) continue;
                go.SetActive(state.activeSelf);
                state.canvas.enabled = state.enabled;
            }
            _hiddenCanvases.Clear();
        }

        void TogglePlayerControl(bool enable)
        {
            if (_playerMovement == null)
                _playerMovement = FindObjectOfType<PlayerMovement>(true);
            if (_firstPersonCamera == null)
                _firstPersonCamera = FindObjectOfType<FirstPersonCamera>(true);

            if (enable)
            {
                if (_playerMovement != null)
                {
                    _playerMovement.enabled = _playerMovementWasEnabled;
                    _playerMovement.isMovementPaused = false;
                }
                if (_firstPersonCamera != null)
                {
                    _firstPersonCamera.suppressMouseLook = _firstPersonSuppressMouseLookBeforeDialogue;
                    _firstPersonCamera.SetCursorLocked(true);
                }
                else
                {
                    GameplayCursorLock.LockForGameplayView();
                }
            }
            else
            {
                if (_playerMovement != null)
                {
                    _playerMovementWasEnabled = _playerMovement.enabled;
                    _playerMovement.isMovementPaused = true;
                    _playerMovement.enabled = false;
                }
                if (_firstPersonCamera != null)
                {
                    _firstPersonSuppressMouseLookBeforeDialogue = _firstPersonCamera.suppressMouseLook;
                    _firstPersonCamera.suppressMouseLook = true;
                }
                GameplayCursorLock.UnlockForModalUi();
            }
        }

        static void AnchorStretch(RectTransform rt, float minX, float minY, float maxX, float maxY, float left, float bottom, float right, float top)
        {
            rt.anchorMin = new Vector2(minX, minY);
            rt.anchorMax = new Vector2(maxX, maxY);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(right, top);
        }
    }
}
