using System;
using GameQuestSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PhoneUI
{
    /// <summary>
    /// 1280×720 设计画布：左上画质、左下时间、右下登出。
    /// </summary>
    [DisallowMultipleComponent]
    public class PhoneSettingsPanelView : MonoBehaviour
    {
        const int LayoutVersion = 8;
        const float DesignWidth = 1280f;
        const float DesignHeight = 720f;
        const float EdgeMargin = 24f;
        const float BackButtonWidth = 120f;
        const float BackButtonHeight = 48f;
        const float QualityCardWidth = 600f;
        const float QualityCardHeight = 300f;
        const float TimeCardWidth = 600f;
        const float TimeCardHeight = 280f;
        const float LogoutButtonWidth = 180f;
        const float LogoutButtonHeight = 52f;
        const float QualityButtonHeight = 58f;
        const float ActionButtonHeight = 54f;
        const float SliderTrackHeight = 10f;
        const float SliderHandleSize = 16f;

        static string[] QualityLabels => new[]
        {
            Loc.Choose("极低", "Very Low"),
            Loc.Choose("低", "Low"),
            Loc.Choose("中", "Medium"),
            Loc.Choose("高", "High"),
            Loc.Choose("电影", "Cinematic")
        };

        [SerializeField] int _layoutVersionBuilt;
        RectTransform _panelRoot;
        Button _backButton;
        TMP_Text _titleText;
        TMP_Text _backButtonText;
        TMP_Text _qualityTitleText;
        TMP_Text _qualityHintText;
        TMP_Text _timeTitleText;
        TMP_Text _questButtonText;
        TMP_Text _logoutButtonText;
        TMP_Text _pauseButtonText;
        TMP_Text _resetButtonText;
        Button _langChineseButton;
        Button _langEnglishButton;
        Button _immersiveOnButton;
        Button _immersiveOffButton;
        TMP_Text _immersiveTitleText;
        TMP_Text _immersiveHintText;
        RectTransform _qualityGridRt;
        GridLayoutGroup _qualityGrid;
        Button[] _qualityButtons;
        Slider _daySlider;
        TMP_Text _dayValueText;
        TMP_Text _clockText;
        SunSimulation _sun;
        QualityController _quality;
        Canvas _rootCanvas;

        public static PhoneSettingsPanelView Ensure(PhoneSessionController session)
        {
            if (session == null || session.settingsPage == null)
                return null;

            var page = session.settingsPage;
            var view = page.GetComponent<PhoneSettingsPanelView>();
            if (view == null)
                view = page.AddComponent<PhoneSettingsPanelView>();
            view.BuildIfNeeded(session);
            return view;
        }

        void OnEnable()
        {
            Loc.OnLanguageChanged += OnLanguageChanged;
            ImmersiveModeSettings.Changed += OnImmersiveModeChanged;
        }

        void OnDisable()
        {
            Loc.OnLanguageChanged -= OnLanguageChanged;
            ImmersiveModeSettings.Changed -= OnImmersiveModeChanged;
        }

        void OnImmersiveModeChanged(bool _) => SyncImmersiveHighlight();

        void OnLanguageChanged()
        {
            if (_panelRoot == null)
                return;
            RefreshLocalizedLabels();
            RefreshBindings();
        }

        void Update()
        {
            RefreshClockLabel();
            UpdateQualityGridCellSize();
            UpdatePanelScale();
        }

        public void RefreshBindings()
        {
            _sun = FindSunSimulation();
            _quality = FindQualityController();
            SyncQualityHighlight();
            SyncDaySliderFromSun();
            SyncImmersiveHighlight();
            UpdateQualityGridCellSize();
            UpdatePanelScale();
            ApplyPanelFonts();
        }

        void BuildIfNeeded(PhoneSessionController session)
        {
            HideLegacySettingsUi(session.settingsPage.transform);

            if (_panelRoot != null && _layoutVersionBuilt == LayoutVersion)
            {
                ApplyFullScreenShell(session);
                HideLegacySettingsUi(session.settingsPage.transform);
                FixPanelInteraction();
                WireBackButton(session);
                RefreshBindings();
                return;
            }

            DestroyBuiltUi();

            _sun = FindSunSimulation();
            _quality = FindQualityController();

            var pageRt = session.settingsPage.transform as RectTransform;
            ApplyFullScreenShell(session);
            _rootCanvas = session.settingsPage.GetComponentInParent<Canvas>();

            var dim = CreateUiObject("SettingsDim", pageRt);
            StretchFull(dim.GetComponent<RectTransform>());
            var dimImg = dim.AddComponent<Image>();
            dimImg.color = PhoneUiTheme.SettingsOverlayDim;
            dimImg.raycastTarget = true;

            _panelRoot = CreateUiObject("PhoneSettingsPanelRoot", pageRt).GetComponent<RectTransform>();
            CenterFixedSize(_panelRoot, DesignWidth, DesignHeight);

            var panelBg = _panelRoot.gameObject.AddComponent<Image>();
            panelBg.color = PhoneUiTheme.SettingsPanelOpaque;
            panelBg.raycastTarget = true;

            var frame = CreateUiObject("GoldFrame", _panelRoot);
            StretchWithInset(frame.GetComponent<RectTransform>(), 3f);
            var frameImg = frame.AddComponent<Image>();
            frameImg.color = PhoneUiTheme.SettingsSectionBorder;
            frameImg.raycastTarget = false;

            float qualityTop = EdgeMargin + BackButtonHeight + 16f;
            BuildTitleLabel(_panelRoot);
            BuildQualitySection(_panelRoot, EdgeMargin, qualityTop);
            BuildTimeSection(_panelRoot, EdgeMargin, EdgeMargin);
            BuildLanguageSection(_panelRoot, EdgeMargin, qualityTop);
            BuildImmersiveSection(_panelRoot, EdgeMargin, qualityTop + 160f + 16f);
            BuildQuestButton(_panelRoot, session, EdgeMargin, EdgeMargin + LogoutButtonHeight + 12f);
            BuildLogoutButton(_panelRoot, EdgeMargin, EdgeMargin);
            BuildBackButton(_panelRoot, session, EdgeMargin, EdgeMargin);

            _layoutVersionBuilt = LayoutVersion;
            HideLegacySettingsUi(session.settingsPage.transform);
            FixPanelInteraction();
            WireBackButton(session);
            RefreshBindings();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_panelRoot);
        }

        void ApplyFullScreenShell(PhoneSessionController session)
        {
            var pageRt = session.settingsPage.transform as RectTransform;
            StretchFull(pageRt);
            session.settingsPage.transform.SetAsLastSibling();

            var pageBg = session.settingsPage.GetComponent<Image>();
            if (pageBg != null)
            {
                pageBg.enabled = false;
                pageBg.raycastTarget = false;
            }

            var canvas = session.settingsPage.GetComponentInParent<Canvas>();
            if (canvas != null)
                canvas.sortingOrder = Mathf.Max(canvas.sortingOrder, 3200);
        }

        void UpdatePanelScale()
        {
            if (_panelRoot == null)
                return;

            float w = Screen.width;
            float h = Screen.height;
            if (_rootCanvas != null)
            {
                var canvasRt = _rootCanvas.transform as RectTransform;
                if (canvasRt != null && canvasRt.rect.width > 1f && canvasRt.rect.height > 1f)
                {
                    w = canvasRt.rect.width;
                    h = canvasRt.rect.height;
                }
            }

            float scale = Mathf.Min(w / DesignWidth, h / DesignHeight);
            _panelRoot.localScale = Vector3.one * scale;
        }

        void DestroyBuiltUi()
        {
            if (_panelRoot != null)
            {
                Destroy(_panelRoot.gameObject);
                _panelRoot = null;
            }

            _qualityGrid = null;
            _qualityGridRt = null;
            _qualityButtons = null;
            _daySlider = null;
            _dayValueText = null;
            _clockText = null;
            _backButton = null;
            _titleText = null;
            _backButtonText = null;
            _qualityTitleText = null;
            _qualityHintText = null;
            _timeTitleText = null;
            _questButtonText = null;
            _logoutButtonText = null;
            _pauseButtonText = null;
            _resetButtonText = null;
            _langChineseButton = null;
            _langEnglishButton = null;
            _immersiveOnButton = null;
            _immersiveOffButton = null;
            _immersiveTitleText = null;
            _immersiveHintText = null;
            _layoutVersionBuilt = 0;
        }

        void BuildBackButton(RectTransform panel, PhoneSessionController session, float left, float top)
        {
            var go = CreateUiObject("Btn_Back", panel);
            var rt = go.GetComponent<RectTransform>();
            AnchorTopLeft(rt, left, top, BackButtonWidth, BackButtonHeight);

            var img = go.AddComponent<Image>();
            img.color = PhoneUiTheme.BtnPrimaryBg;
            img.raycastTarget = true;
            _backButton = go.AddComponent<Button>();
            _backButton.targetGraphic = img;
            WireBackButton(session);
            var cb = _backButton.colors;
            cb.highlightedColor = PhoneUiTheme.BgGoldTint;
            cb.pressedColor = PhoneUiTheme.BtnGoldBg;
            _backButton.colors = cb;

            var txtGo = CreateUiObject("Text", go.transform);
            StretchFull(txtGo.GetComponent<RectTransform>());
            var tmp = txtGo.AddComponent<TextMeshProUGUI>();
            _backButtonText = tmp;
            tmp.text = Loc.Choose("返回", "Back");
            tmp.fontSize = 20;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = PhoneUiTheme.TextGold;
            tmp.alignment = TextAlignmentOptions.Center;
            StyleTmp(tmp);

            rt.SetAsLastSibling();
        }

        void WireBackButton(PhoneSessionController session)
        {
            if (_backButton == null || session == null)
                return;

            _backButton.onClick.RemoveAllListeners();
            _backButton.onClick.AddListener(() => OnSettingsBackClicked(session));
        }

        static void OnSettingsBackClicked(PhoneSessionController session)
        {
            if (session == null)
                return;
            if (!session.TryCloseOverlayAndShowLauncher())
                session.CloseSettingsPage();
        }

        void BuildTitleLabel(RectTransform panel)
        {
            var go = CreateUiObject("Title", panel);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(BackButtonWidth + EdgeMargin * 2f, -EdgeMargin - 40f);
            rt.offsetMax = new Vector2(0f, -EdgeMargin);

            var tmp = go.AddComponent<TextMeshProUGUI>();
            _titleText = tmp;
            tmp.text = Loc.Choose("游戏设置", "Game Settings");
            tmp.fontSize = 30;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = PhoneUiTheme.TextBlack;
            tmp.alignment = TextAlignmentOptions.Center;
            StyleTmp(tmp);
        }

        void BuildQualitySection(RectTransform panel, float left, float top)
        {
            var cardGo = CreateUiObject("QualitySection", panel);
            var cardRt = cardGo.GetComponent<RectTransform>();
            AnchorTopLeft(cardRt, left, top, QualityCardWidth, QualityCardHeight);
            ApplySectionCardChrome(cardGo);

            var cardVlg = cardGo.AddComponent<VerticalLayoutGroup>();
            cardVlg.spacing = 12f;
            cardVlg.padding = new RectOffset(16, 16, 14, 14);
            cardVlg.childControlWidth = true;
            cardVlg.childForceExpandWidth = true;
            cardVlg.childControlHeight = true;
            cardVlg.childForceExpandHeight = false;

            _qualityTitleText = CreateSectionTitle(cardGo.transform, Loc.Choose("画质调整", "Graphics Quality"), 24);
            _qualityHintText = CreateBodyLabel(cardGo.transform, Loc.Choose("选择画面质量（电影为最高画质）", "Select visual quality (Cinematic is highest)"), 16);

            var gridGo = CreateUiObject("QualityGrid", cardGo.transform);
            _qualityGridRt = gridGo.GetComponent<RectTransform>();
            var gridLe = gridGo.AddComponent<LayoutElement>();
            gridLe.minHeight = QualityButtonHeight * 2f + 12f;
            gridLe.flexibleHeight = 1f;
            _qualityGrid = gridGo.AddComponent<GridLayoutGroup>();
            _qualityGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            _qualityGrid.constraintCount = 3;
            _qualityGrid.spacing = new Vector2(12f, 12f);
            _qualityGrid.childAlignment = TextAnchor.UpperCenter;

            _qualityButtons = new Button[QualityLabels.Length];
            for (int i = 0; i < QualityLabels.Length; i++)
            {
                int idx = i;
                _qualityButtons[i] = CreateQualityButton(gridGo.transform, QualityLabels[i], () => OnQualityClicked(idx));
            }
        }

        void BuildTimeSection(RectTransform panel, float left, float bottom)
        {
            var cardGo = CreateUiObject("TimeSection", panel);
            var cardRt = cardGo.GetComponent<RectTransform>();
            AnchorBottomLeft(cardRt, left, bottom, TimeCardWidth, TimeCardHeight);
            ApplySectionCardChrome(cardGo);

            var cardVlg = cardGo.AddComponent<VerticalLayoutGroup>();
            cardVlg.spacing = 12f;
            cardVlg.padding = new RectOffset(16, 16, 14, 14);
            cardVlg.childControlWidth = true;
            cardVlg.childForceExpandWidth = true;
            cardVlg.childControlHeight = true;
            cardVlg.childForceExpandHeight = false;

            _timeTitleText = CreateSectionTitle(cardGo.transform, Loc.Choose("时间调整", "Time Settings"), 24);

            _clockText = CreateBodyLabel(cardGo.transform, Loc.Choose("当前时间：--:--", "Current Time: --:--"), 20);
            _clockText.color = PhoneUiTheme.TextBlack;
            _clockText.fontStyle = FontStyles.Bold;

            _dayValueText = CreateBodyLabel(cardGo.transform, Loc.F("一天时长：{0} 秒", "Day Length: {0}s", 60), 20);
            _dayValueText.color = PhoneUiTheme.TextGold;
            _dayValueText.fontStyle = FontStyles.Bold;

            var sliderRow = CreateUiObject("DaySliderRow", cardGo.transform);
            var sliderLe = sliderRow.AddComponent<LayoutElement>();
            sliderLe.minHeight = 36f;
            sliderLe.preferredHeight = 36f;
            var sliderH = sliderRow.AddComponent<HorizontalLayoutGroup>();
            sliderH.spacing = 12f;
            sliderH.childAlignment = TextAnchor.MiddleCenter;
            sliderH.childControlWidth = true;
            sliderH.childForceExpandWidth = true;

            CreateMiniLabel(sliderRow.transform, "10s", 44f, 16);
            _daySlider = CreateThemedSlider(sliderRow.transform);
            CreateMiniLabel(sliderRow.transform, "300s", 48f, 16);

            var btnRow = CreateUiObject("TimeBtnRow", cardGo.transform);
            var btnRowLe = btnRow.AddComponent<LayoutElement>();
            btnRowLe.minHeight = ActionButtonHeight;
            btnRowLe.preferredHeight = ActionButtonHeight;
            var btnH = btnRow.AddComponent<HorizontalLayoutGroup>();
            btnH.spacing = 14f;
            btnH.childForceExpandWidth = true;
            btnH.childControlWidth = true;

            var pauseBtn = CreateTextButton(btnRow.transform, Loc.Choose("暂停 / 继续", "Pause / Resume"), OnToggleTime, 0f, ActionButtonHeight,
                PhoneUiTheme.BtnPrimaryBg, PhoneUiTheme.TextGold, 18);
            _pauseButtonText = pauseBtn.GetComponentInChildren<TextMeshProUGUI>();
            var resetBtn = CreateTextButton(btnRow.transform, Loc.Choose("重置到日出", "Reset to Sunrise"), OnResetTime, 0f, ActionButtonHeight,
                PhoneUiTheme.BtnGoldBg, PhoneUiTheme.BtnQualityOnText, 18);
            _resetButtonText = resetBtn.GetComponentInChildren<TextMeshProUGUI>();
        }

        void BuildLanguageSection(RectTransform panel, float right, float top)
        {
            const float cardWidth = 280f;
            const float cardHeight = 160f;

            var cardGo = CreateUiObject("LanguageSection", panel);
            var cardRt = cardGo.GetComponent<RectTransform>();
            AnchorTopRight(cardRt, right, top, cardWidth, cardHeight);
            ApplySectionCardChrome(cardGo);

            var cardVlg = cardGo.AddComponent<VerticalLayoutGroup>();
            cardVlg.spacing = 12f;
            cardVlg.padding = new RectOffset(16, 16, 14, 14);
            cardVlg.childControlWidth = true;
            cardVlg.childForceExpandWidth = true;
            cardVlg.childControlHeight = true;
            cardVlg.childForceExpandHeight = false;

            CreateSectionTitle(cardGo.transform, Loc.Choose("语言", "Language"), 24);

            var btnRow = CreateUiObject("LangBtnRow", cardGo.transform);
            var btnRowLe = btnRow.AddComponent<LayoutElement>();
            btnRowLe.minHeight = ActionButtonHeight;
            btnRowLe.preferredHeight = ActionButtonHeight;
            var btnH = btnRow.AddComponent<HorizontalLayoutGroup>();
            btnH.spacing = 14f;
            btnH.childForceExpandWidth = true;
            btnH.childControlWidth = true;

            _langChineseButton = CreateTextButton(btnRow.transform, Loc.Choose("中文", "中文"), () => OnLanguageSelected(GameLanguage.Chinese),
                0f, ActionButtonHeight, PhoneUiTheme.BtnGoldBg, PhoneUiTheme.BtnQualityOnText, 18);
            _langEnglishButton = CreateTextButton(btnRow.transform, "English", () => OnLanguageSelected(GameLanguage.English),
                0f, ActionButtonHeight, PhoneUiTheme.BtnPrimaryBg, PhoneUiTheme.TextGold, 18);

            SyncLanguageHighlight();
        }

        void BuildImmersiveSection(RectTransform panel, float right, float top)
        {
            const float cardWidth = 280f;
            const float cardHeight = 176f;

            var cardGo = CreateUiObject("ImmersiveSection", panel);
            var cardRt = cardGo.GetComponent<RectTransform>();
            AnchorTopRight(cardRt, right, top, cardWidth, cardHeight);
            ApplySectionCardChrome(cardGo);

            var cardVlg = cardGo.AddComponent<VerticalLayoutGroup>();
            cardVlg.spacing = 10f;
            cardVlg.padding = new RectOffset(16, 16, 14, 14);
            cardVlg.childControlWidth = true;
            cardVlg.childForceExpandWidth = true;
            cardVlg.childControlHeight = true;
            cardVlg.childForceExpandHeight = false;

            _immersiveTitleText = CreateSectionTitle(cardGo.transform, Loc.Choose("沉浸模式", "Immersive Mode"), 24);
            _immersiveHintText = CreateBodyLabel(
                cardGo.transform,
                Loc.Choose("隐藏小地图、任务栏、追踪光标与血条/体力", "Hide minimap, quest HUD, tracker, HP/stamina"),
                14);

            var btnRow = CreateUiObject("ImmersiveBtnRow", cardGo.transform);
            var btnRowLe = btnRow.AddComponent<LayoutElement>();
            btnRowLe.minHeight = ActionButtonHeight;
            btnRowLe.preferredHeight = ActionButtonHeight;
            var btnH = btnRow.AddComponent<HorizontalLayoutGroup>();
            btnH.spacing = 14f;
            btnH.childForceExpandWidth = true;
            btnH.childControlWidth = true;

            _immersiveOnButton = CreateTextButton(
                btnRow.transform,
                Loc.Choose("开启", "On"),
                () => OnImmersiveSelected(true),
                0f,
                ActionButtonHeight,
                PhoneUiTheme.BtnGoldBg,
                PhoneUiTheme.BtnQualityOnText,
                18);
            _immersiveOffButton = CreateTextButton(
                btnRow.transform,
                Loc.Choose("关闭", "Off"),
                () => OnImmersiveSelected(false),
                0f,
                ActionButtonHeight,
                PhoneUiTheme.BtnPrimaryBg,
                PhoneUiTheme.TextGold,
                18);

            SyncImmersiveHighlight();
        }

        static void OnImmersiveSelected(bool enabled) => ImmersiveModeSettings.SetEnabled(enabled);

        void SyncImmersiveHighlight()
        {
            bool enabled = ImmersiveModeSettings.IsEnabled;
            HighlightToggleButton(_immersiveOnButton, enabled);
            HighlightToggleButton(_immersiveOffButton, !enabled);
        }

        static void HighlightToggleButton(Button btn, bool selected)
        {
            if (btn == null)
                return;

            var img = btn.GetComponent<Image>();
            if (img != null)
                img.color = selected ? PhoneUiTheme.BtnQualityOn : PhoneUiTheme.BtnQualityOff;

            var tmp = btn.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null)
            {
                tmp.color = selected ? PhoneUiTheme.BtnQualityOnText : PhoneUiTheme.TextBlack;
                tmp.fontStyle = selected ? FontStyles.Bold : FontStyles.Normal;
            }
        }

        static void OnLanguageSelected(GameLanguage language) => LocalizationService.SetLanguage(language);

        void SyncLanguageHighlight()
        {
            bool isChinese = !LocalizationService.IsEnglish;
            HighlightLanguageButton(_langChineseButton, isChinese);
            HighlightLanguageButton(_langEnglishButton, !isChinese);
        }

        static void HighlightLanguageButton(Button btn, bool selected)
        {
            if (btn == null)
                return;

            var img = btn.GetComponent<Image>();
            if (img != null)
                img.color = selected ? PhoneUiTheme.BtnQualityOn : PhoneUiTheme.BtnQualityOff;

            var tmp = btn.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null)
            {
                tmp.color = selected ? PhoneUiTheme.BtnQualityOnText : PhoneUiTheme.TextBlack;
                tmp.fontStyle = selected ? FontStyles.Bold : FontStyles.Normal;
            }
        }

        void RefreshLocalizedLabels()
        {
            if (_titleText != null)
                _titleText.text = Loc.Choose("游戏设置", "Game Settings");
            if (_backButtonText != null)
                _backButtonText.text = Loc.Choose("返回", "Back");
            if (_qualityTitleText != null)
                _qualityTitleText.text = Loc.Choose("画质调整", "Graphics Quality");
            if (_qualityHintText != null)
                _qualityHintText.text = Loc.Choose("选择画面质量（电影为最高画质）", "Select visual quality (Cinematic is highest)");
            if (_timeTitleText != null)
                _timeTitleText.text = Loc.Choose("时间调整", "Time Settings");
            if (_questButtonText != null)
                _questButtonText.text = Loc.Choose("任务日志", "Quest Log");
            if (_logoutButtonText != null)
                _logoutButtonText.text = Loc.Choose("登出游戏", "Log Out");
            if (_pauseButtonText != null)
                _pauseButtonText.text = Loc.Choose("暂停 / 继续", "Pause / Resume");
            if (_resetButtonText != null)
                _resetButtonText.text = Loc.Choose("重置到日出", "Reset to Sunrise");
            if (_immersiveTitleText != null)
                _immersiveTitleText.text = Loc.Choose("沉浸模式", "Immersive Mode");
            if (_immersiveHintText != null)
                _immersiveHintText.text = Loc.Choose(
                    "隐藏小地图、任务栏、追踪光标与血条/体力",
                    "Hide minimap, quest HUD, tracker, HP/stamina");

            if (_qualityButtons != null)
            {
                var labels = QualityLabels;
                for (int i = 0; i < _qualityButtons.Length && i < labels.Length; i++)
                {
                    var tmp = _qualityButtons[i]?.GetComponentInChildren<TextMeshProUGUI>();
                    if (tmp != null)
                        tmp.text = labels[i];
                }
            }

            SyncLanguageHighlight();
            SyncImmersiveHighlight();
            SyncQualityHighlight();
            if (_daySlider != null)
                UpdateDayLengthLabel(_daySlider.value);
            RefreshClockLabel();
        }

        void BuildQuestButton(RectTransform panel, PhoneSessionController session, float right, float bottom)
        {
            var go = CreateUiObject("Btn_QuestJournal", panel);
            var rt = go.GetComponent<RectTransform>();
            AnchorBottomRight(rt, right, bottom, LogoutButtonWidth, LogoutButtonHeight);

            var img = go.AddComponent<Image>();
            img.color = PhoneUiTheme.BtnGoldBg;
            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(() => OnQuestButtonClicked(session));
            var cb = btn.colors;
            cb.highlightedColor = PhoneUiTheme.BgGoldTint;
            cb.pressedColor = PhoneUiTheme.BtnPrimaryBg;
            btn.colors = cb;

            var txtGo = CreateUiObject("Text", go.transform);
            StretchFull(txtGo.GetComponent<RectTransform>());
            var tmp = txtGo.AddComponent<TextMeshProUGUI>();
            _questButtonText = tmp;
            tmp.text = Loc.Choose("任务日志", "Quest Log");
            tmp.fontSize = 18;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = PhoneUiTheme.TextBlack;
            tmp.alignment = TextAlignmentOptions.Center;
            StyleTmp(tmp);
        }

        static void OnQuestButtonClicked(PhoneSessionController session)
        {
            if (session == null)
                return;

            var questUi = UnityEngine.Object.FindObjectOfType<QuestSystemUI>(true);
            if (questUi == null)
            {
                Debug.LogWarning("[PhoneUI] 未找到 QuestSystemUI");
                return;
            }

            questUi.OpenFullPanelFromSettings();
        }

        void BuildLogoutButton(RectTransform panel, float right, float bottom)
        {
            var go = CreateUiObject("Btn_Logout", panel);
            var rt = go.GetComponent<RectTransform>();
            AnchorBottomRight(rt, right, bottom, LogoutButtonWidth, LogoutButtonHeight);

            var img = go.AddComponent<Image>();
            img.color = PhoneUiTheme.BtnPrimaryBg;
            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(OnLogoutClicked);
            var cb = btn.colors;
            cb.highlightedColor = PhoneUiTheme.BgGoldTint;
            cb.pressedColor = PhoneUiTheme.BtnGoldBg;
            btn.colors = cb;

            var txtGo = CreateUiObject("Text", go.transform);
            StretchFull(txtGo.GetComponent<RectTransform>());
            var tmp = txtGo.AddComponent<TextMeshProUGUI>();
            _logoutButtonText = tmp;
            tmp.text = Loc.Choose("登出游戏", "Log Out");
            tmp.fontSize = 18;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = PhoneUiTheme.TextGold;
            tmp.alignment = TextAlignmentOptions.Center;
            StyleTmp(tmp);
        }

        static void OnLogoutClicked()
        {
            PhoneAppRouter.PerformLogout();
        }

        void UpdateQualityGridCellSize()
        {
            if (_qualityGrid == null || _qualityGridRt == null)
                return;

            float w = _qualityGridRt.rect.width;
            if (w < 80f)
                w = QualityCardWidth - 32f - 24f;
            if (w < 80f)
                return;

            float spacing = _qualityGrid.spacing.x;
            float cellW = (w - spacing * 2f) / 3f;
            cellW = Mathf.Max(96f, cellW);
            _qualityGrid.cellSize = new Vector2(cellW, QualityButtonHeight);
        }

        static void ApplySectionCardChrome(GameObject cardGo)
        {
            var img = cardGo.AddComponent<Image>();
            img.color = PhoneUiTheme.SettingsSectionBg;
            var outline = cardGo.AddComponent<Outline>();
            outline.effectColor = PhoneUiTheme.SettingsSectionBorder;
            outline.effectDistance = new Vector2(1.5f, -1.5f);
        }

        void OnQualityClicked(int index)
        {
            if (_quality == null)
                _quality = FindQualityController();
            if (_quality == null)
                return;

            if (index >= QualityLabels.Length - 1)
                _quality.SetCinematicQuality();
            else
                _quality.SetQuality(index);

            SyncQualityHighlight();
        }

        void SyncQualityHighlight()
        {
            if (_qualityButtons == null)
                return;

            int engineLevel = QualitySettings.GetQualityLevel();
            bool cinematic = PlayerPrefs.GetInt("SavedQualityIsCinematic_v1", 0) == 1;
            int selected = cinematic ? QualityLabels.Length - 1 : Mathf.Clamp(engineLevel, 0, QualityLabels.Length - 2);

            for (int i = 0; i < _qualityButtons.Length; i++)
            {
                var btn = _qualityButtons[i];
                if (btn == null) continue;
                bool on = i == selected;
                var img = btn.GetComponent<Image>();
                if (img != null)
                    img.color = on ? PhoneUiTheme.BtnQualityOn : PhoneUiTheme.BtnQualityOff;
                var tmp = btn.GetComponentInChildren<TextMeshProUGUI>();
                if (tmp != null)
                {
                    tmp.color = on ? PhoneUiTheme.BtnQualityOnText : PhoneUiTheme.TextBlack;
                    tmp.fontStyle = on ? FontStyles.Bold : FontStyles.Normal;
                }
            }
        }

        void SyncDaySliderFromSun()
        {
            if (_daySlider == null)
                return;
            if (_sun == null)
                _sun = FindSunSimulation();

            float val = _sun != null ? _sun.dayLengthInSeconds : 60f;
            val = Mathf.Clamp(val, 10f, 300f);
            _daySlider.SetValueWithoutNotify(val);
            UpdateDayLengthLabel(val);
        }

        void OnDaySliderChanged(float value)
        {
            if (_sun == null)
                _sun = FindSunSimulation();
            if (_sun != null)
                _sun.OnDayLengthChanged(value);
            UpdateDayLengthLabel(value);
        }

        void UpdateDayLengthLabel(float value)
        {
            if (_dayValueText != null)
                _dayValueText.text = Loc.F("一天时长：{0} 秒", "Day Length: {0}s", Mathf.RoundToInt(value));
        }

        void OnToggleTime()
        {
            if (_sun == null)
                _sun = FindSunSimulation();
            _sun?.ToggleSimulation();
        }

        void OnResetTime()
        {
            if (_sun == null)
                _sun = FindSunSimulation();
            _sun?.ResetTime();
        }

        void RefreshClockLabel()
        {
            if (_clockText == null)
                return;
            if (_sun == null)
                _sun = FindSunSimulation();
            if (_sun == null)
            {
                _clockText.text = Loc.Choose("当前时间：--:--", "Current Time: --:--");
                return;
            }

            int hours = (int)_sun.currentTimeOfDay;
            int minutes = (int)((_sun.currentTimeOfDay - hours) * 60f);
            _clockText.text = Loc.F("当前时间：{0:D2}:{1:D2}", "Current Time: {0:D2}:{1:D2}", hours, minutes);
        }

        static SunSimulation FindSunSimulation() => UnityEngine.Object.FindObjectOfType<SunSimulation>(true);

        static QualityController FindQualityController()
        {
            var session = PhoneSessionController.Instance;
            if (session != null && session.phonePanel != null)
            {
                var qc = session.phonePanel.GetComponent<QualityController>();
                if (qc != null)
                    return qc;
            }
            return UnityEngine.Object.FindObjectOfType<QualityController>(true);
        }

        static void HideLegacySettingsUi(Transform settingsPage)
        {
            if (settingsPage == null)
                return;

            for (int i = 0; i < settingsPage.childCount; i++)
            {
                var child = settingsPage.GetChild(i);
                if (child == null)
                    continue;
                var name = child.name;
                if (name == "SettingsDim" || name == "PhoneSettingsPanelRoot")
                    continue;
                child.gameObject.SetActive(false);
            }

            string[] legacyNames = { "closepanel", "Btn_SettingsBack" };
            for (int n = 0; n < legacyNames.Length; n++)
            {
                var legacy = settingsPage.Find(legacyNames[n]);
                if (legacy != null)
                    legacy.gameObject.SetActive(false);
            }
        }

        void ApplyPanelFonts()
        {
            if (_panelRoot == null)
                return;

            var tmps = _panelRoot.GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < tmps.Length; i++)
                StyleTmp(tmps[i]);
        }

        void FixPanelInteraction()
        {
            if (_panelRoot == null)
                return;

            if (_backButton == null)
                _backButton = _panelRoot.Find("Btn_Back")?.GetComponent<Button>();

            ApplyPanelFonts();

            if (_backButton != null)
            {
                var backImg = _backButton.GetComponent<Image>();
                if (backImg != null)
                {
                    backImg.raycastTarget = true;
                    _backButton.targetGraphic = backImg;
                }
                _backButton.transform.SetAsLastSibling();
            }
        }

        static void StyleTmp(TextMeshProUGUI tmp)
        {
            PhoneUiAssetLoader.ApplyTmpFont(tmp);
            ApplyNonInteractiveTmp(tmp);
        }

        static void ApplyNonInteractiveTmp(TMP_Text tmp)
        {
            if (tmp != null)
                tmp.raycastTarget = false;
        }

        TMP_Text CreateSectionTitle(Transform parent, string text, float fontSize)
        {
            var go = CreateUiObject("Section_" + text, parent);
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = fontSize + 10f;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = PhoneUiTheme.TextGold;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            StyleTmp(tmp);
            return tmp;
        }

        TMP_Text CreateBodyLabel(Transform parent, string text, float fontSize)
        {
            var go = CreateUiObject("Lbl", parent);
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = fontSize + 8f;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = PhoneUiTheme.TextBlack;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            StyleTmp(tmp);
            return tmp;
        }

        void CreateMiniLabel(Transform parent, string text, float width, float fontSize)
        {
            var go = CreateUiObject("Mini_" + text, parent);
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.minWidth = width;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = PhoneUiTheme.TextMuted;
            tmp.alignment = TextAlignmentOptions.Center;
            StyleTmp(tmp);
        }

        Button CreateQualityButton(Transform parent, string label, Action onClick)
        {
            var go = CreateUiObject("BtnQ_" + label, parent);
            var img = go.AddComponent<Image>();
            img.color = PhoneUiTheme.BtnQualityOff;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => onClick?.Invoke());
            var cb = btn.colors;
            cb.normalColor = PhoneUiTheme.BtnQualityOff;
            cb.highlightedColor = PhoneUiTheme.BgGoldTint;
            cb.pressedColor = PhoneUiTheme.BtnQualityOn;
            cb.selectedColor = PhoneUiTheme.BtnQualityOn;
            btn.colors = cb;

            var txtGo = CreateUiObject("Text", go.transform);
            StretchFull(txtGo.GetComponent<RectTransform>());
            var tmp = txtGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 20;
            tmp.color = PhoneUiTheme.TextBlack;
            tmp.alignment = TextAlignmentOptions.Center;
            StyleTmp(tmp);
            return btn;
        }

        Button CreateTextButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick, float width,
            float height, Color bg, Color textColor, float fontSize)
        {
            var go = CreateUiObject("Btn_" + label, parent);
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = height;
            le.preferredHeight = height;
            if (width > 0f)
            {
                le.preferredWidth = width;
                le.minWidth = width;
            }
            else
            {
                le.flexibleWidth = 1f;
            }

            var img = go.AddComponent<Image>();
            img.color = bg;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);
            var cb = btn.colors;
            cb.normalColor = bg;
            cb.highlightedColor = PhoneUiTheme.BgGoldTint;
            cb.pressedColor = PhoneUiTheme.BtnGoldBg;
            btn.colors = cb;

            var txtGo = CreateUiObject("Text", go.transform);
            StretchFull(txtGo.GetComponent<RectTransform>());
            var tmp = txtGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = fontSize;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = textColor;
            tmp.alignment = TextAlignmentOptions.Center;
            StyleTmp(tmp);
            return btn;
        }

        Slider CreateThemedSlider(Transform parent)
        {
            var go = CreateUiObject("DayLengthSlider", parent);
            var le = go.AddComponent<LayoutElement>();
            le.flexibleWidth = 1f;
            le.minHeight = 36f;
            le.preferredHeight = 36f;

            var bg = CreateUiObject("Background", go.transform);
            ApplyThinTrackRect(bg.GetComponent<RectTransform>());
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = PhoneUiTheme.SliderTrack;

            var fillArea = CreateUiObject("Fill Area", go.transform);
            ApplyThinTrackRect(fillArea.GetComponent<RectTransform>());

            var fill = CreateUiObject("Fill", fillArea.transform);
            StretchFull(fill.GetComponent<RectTransform>());
            var fillImg = fill.AddComponent<Image>();
            fillImg.color = PhoneUiTheme.SliderFill;

            var handleArea = CreateUiObject("Handle Slide Area", go.transform);
            StretchFull(handleArea.GetComponent<RectTransform>());

            var handle = CreateUiObject("Handle", handleArea.transform);
            var handleRt = handle.GetComponent<RectTransform>();
            handleRt.sizeDelta = new Vector2(SliderHandleSize, SliderHandleSize);
            var handleImg = handle.AddComponent<Image>();
            handleImg.color = PhoneUiTheme.BtnPrimaryBg;

            var slider = go.AddComponent<Slider>();
            slider.fillRect = fill.GetComponent<RectTransform>();
            slider.handleRect = handleRt;
            slider.targetGraphic = handleImg;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 10f;
            slider.maxValue = 300f;
            slider.wholeNumbers = true;
            slider.value = 60f;
            slider.onValueChanged.AddListener(OnDaySliderChanged);
            return slider;
        }

        static void ApplyThinTrackRect(RectTransform trackRt)
        {
            trackRt.anchorMin = new Vector2(0f, 0.5f);
            trackRt.anchorMax = new Vector2(1f, 0.5f);
            trackRt.pivot = new Vector2(0.5f, 0.5f);
            trackRt.anchoredPosition = Vector2.zero;
            trackRt.sizeDelta = new Vector2(0f, SliderTrackHeight);
        }

        static void CenterFixedSize(RectTransform rt, float width, float height)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(width, height);
            rt.anchoredPosition = Vector2.zero;
            rt.localScale = Vector3.one;
        }

        static void AnchorTopLeft(RectTransform rt, float left, float top, float width, float height)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(left, -top);
            rt.sizeDelta = new Vector2(width, height);
            rt.localScale = Vector3.one;
        }

        static void AnchorBottomLeft(RectTransform rt, float left, float bottom, float width, float height)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(left, bottom);
            rt.sizeDelta = new Vector2(width, height);
            rt.localScale = Vector3.one;
        }

        static void AnchorBottomRight(RectTransform rt, float right, float bottom, float width, float height)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-right, bottom);
            rt.sizeDelta = new Vector2(width, height);
            rt.localScale = Vector3.one;
        }

        static void AnchorTopRight(RectTransform rt, float right, float top, float width, float height)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-right, -top);
            rt.sizeDelta = new Vector2(width, height);
            rt.localScale = Vector3.one;
        }

        static GameObject CreateUiObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
        }

        static void StretchWithInset(RectTransform rt, float inset)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
            rt.anchoredPosition = Vector2.zero;
            rt.localScale = Vector3.one;
        }

        static RectTransform FindDeep(Transform root, string name)
        {
            if (root == null) return null;
            var all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == name)
                    return all[i] as RectTransform;
            }
            return null;
        }
    }
}
