using System.Collections;
using System.Collections.Generic;
using GameControls;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using DormMall;
using PhoneUI;

namespace GameQuestSystem
{
    public partial class QuestSystemUI : MonoBehaviour
    {
        [Header("依赖")]
        [SerializeField] private QuestSystemManager questManager;
        [SerializeField] private Font questUIFont;
        [SerializeField] private string preferredFontName = "Alibaba Pu Hui Ti-3-75-Semi Bold";
        [SerializeField] private bool dontDestroyOnLoad = true;
        [SerializeField] private bool showBriefPanelWhenNoTask = true;

        [Header("简要任务栏")]
        [SerializeField] private GameObject briefPanel;
        [SerializeField] private Text briefDistanceText;
        [Tooltip("简易任务栏不再显示距离（距离由画面内 TargetGuideUI 等展示）。若为 true 则保留原距离行。")]
        [SerializeField] private bool showBriefDistanceText = false;
        [SerializeField] private Text briefTitleText;
        [SerializeField] private Text briefProgressText;
        [SerializeField] private Text briefSceneText;
        [SerializeField] private Text briefIntroText;
        [SerializeField] private Button briefOpenButton;
        [Header("简要任务栏 · 与其它界面叠层")]
        [Tooltip("左上角「设置」打开的 Curve 面板根物体。留空则自动查找 CurvePanelController.curvePanel（若场景使用 QualityController.ToggleQualityPanel 则无需填写，会自动检测 qualityPanel）。")]
        [SerializeField] private GameObject settingsCurvePanelOverride;

        [Header("简要任务栏 · 逐行字号")]
        [Tooltip("开启后由下方四个整数固定每行字号，并关闭 Best Fit，避免「1/1」等短行被撑得过大。")]
        [SerializeField] private bool useManualBriefLineFontSizes = true;
        [Tooltip("距离行字号（仅当显示距离行时生效）。")]
        [SerializeField] private int briefDistanceLineFontSize = 35;
        [Tooltip("第一行：任务标题（含 [主线] 等）。")]
        [SerializeField] private int briefTitleLineFontSize = 35;
        [Tooltip("第二行：进度（如 1/1）。")]
        [SerializeField] private int briefProgressLineFontSize = 35;
        [Tooltip("第三行：任务场景。")]
        [SerializeField] private int briefSceneLineFontSize = 28;
        [Tooltip("第四行：简述/目标文案。")]
        [SerializeField] private int briefIntroLineFontSize = 35;

        [Header("完整任务栏")]
        [SerializeField] private GameObject fullPanel;
        [SerializeField] private Text fullDistanceText;
        [SerializeField] private Text fullTitleText;
        [SerializeField] private Text fullProgressText;
        [SerializeField] private Text fullSceneText;
        [SerializeField] private Text fullDetailText;
        [SerializeField] private Text fullRewardsText;
        [SerializeField] private Button fullCloseButton;

        [Header("任务日志（J 键复杂任务栏）")]
        [SerializeField] private RectTransform journalListRoot;
        [SerializeField] private Button journalTrackButton;
        [SerializeField] private Text journalHintText;

        [Header("按键")]
        [SerializeField] private KeyCode closeDetailKey = KeyCode.Escape;
        [Header("全屏任务栏联动隐藏")]
        [Tooltip("当完整任务栏展开时隐藏这些 UI（如小地图、血条/体力、右上角设置），关闭后自动恢复。")]
        [SerializeField] private List<GameObject> hideWhenFullPanelOpen = new List<GameObject>();
        [Tooltip("为 true 时，按 J 展开主任务栏会隐藏所有「根 Canvas」分支，只要该 Canvas 下不包含本任务栏（含简要/完整面板）。适用于小地图、血条等与任务分属不同 Canvas 的场景。")]
        [SerializeField] private bool autoHideOtherRootCanvasesWhenFullOpen = true;
        [Tooltip("自动隐藏时跳过 World Space 画布（如场景内 3D UI），避免误关。")]
        [SerializeField] private bool skipWorldSpaceCanvasWhenAutoHiding = true;
        [Tooltip("为 true 时，展开完整任务栏（Quest Full Panel）会隐藏场景中的 openpanel（常见 HUD 条）。关闭后恢复原激活状态。")]
        [SerializeField] private bool hideOpenPanelWhenQuestFull = true;
        [Tooltip("要隐藏的物体名称（与层级里 GameObject 名一致，默认 openpanel）。")]
        [SerializeField] private string openPanelObjectName = "openpanel";
        [Tooltip("若指定则优先使用，不再按名称查找（多场景或重名时推荐）。")]
        [SerializeField] private GameObject openPanelRootOverride;
        [Header("全屏任务栏鼠标控制")]
        [SerializeField] private bool showCursorWhenFullPanelOpen = true;
        [Header("字体自适应")]
        [SerializeField] private bool autoFitByPanelSize = false;
        [SerializeField] private bool useInspectorFontSizeAsBase = false;
        [SerializeField] private int briefMinFontSize = 9;
        [SerializeField] private int briefMaxFontSize = 64;
        [SerializeField] private int fullMinFontSize = 16;
        [SerializeField] private int fullMaxFontSize = 72;

        private bool _isFullOpen;
        private bool _briefHiddenByOverlay;
        private GameObject _cachedSettingsCurveRoot;
        private QualityController[] _cachedQualityControllers;
        private bool _settingsBlockingCacheValid;
        private GameObject _cachedOpenPanelRef;
        private bool _openPanelQuestFullSuppressActive;
        private bool _openPanelPrevActiveSelf;
        private bool _fullPanelReparentedForOpenPanelSuppress;
        private Transform _fullPanelRestoreParent;
        private int _fullPanelRestoreSiblingIndex;
        private readonly Dictionary<GameObject, bool> _cachedUiActiveStates = new Dictionary<GameObject, bool>();
        private readonly Dictionary<GameObject, bool> _autoHiddenCanvasPrevActive = new Dictionary<GameObject, bool>();
        private bool _cachedCursorStateValid;
        private bool _cachedCursorVisible;
        private CursorLockMode _cachedCursorLockMode;
        private bool _cachedGameplayCursorLocked;
        private string _lastDistanceText = "";
        string _journalSelectedQuestId = "";
        readonly List<GameObject> _journalListItemInstances = new List<GameObject>();

        void Awake()
        {
            if (dontDestroyOnLoad && IsUiHierarchySafeToPersist())
                DontDestroyOnLoad(gameObject);

            // 兼容旧场景序列化值：历史版本把 briefMaxFontSize 卡在 18
            if (briefMaxFontSize <= 18)
                briefMaxFontSize = 64;
            if (fullMaxFontSize <= 36)
                fullMaxFontSize = 72;

            if (questManager == null)
                questManager = QuestSystemManager.Instance;
            if (questManager == null)
                questManager = FindObjectOfType<QuestSystemManager>(true);
        }

        bool IsUiHierarchySafeToPersist()
        {
            // 只有当面板都在 QuestSystemUI 自身层级下时，才允许 DontDestroyOnLoad。
            // 现有 undergarden/uppergarden 的任务面板挂在场景 openpanel 下；若只保留脚本物体，
            // 跨场景后会留下已失效面板引用，按 J 时旧实例会隐藏其它 Canvas 却没有完整面板可显示。
            return IsReferenceOwnedByThisHierarchy(briefPanel) && IsReferenceOwnedByThisHierarchy(fullPanel);
        }

        bool IsReferenceOwnedByThisHierarchy(GameObject go)
        {
            return go == null || go.transform == transform || go.transform.IsChildOf(transform);
        }

        void Start()
        {
            EnsureQuestFont();
            ApplyFontToAllTexts();
            ApplyUndergardenBriefPanelStyleIfNeeded();
            ApplyAllAdaptiveLayout();
            ApplyFullPanelJournalStyle();

            if (briefOpenButton != null)
            {
                briefOpenButton.onClick.RemoveListener(ToggleFullPanelFromBriefUi);
                briefOpenButton.onClick.AddListener(ToggleFullPanelFromBriefUi);
            }
            if (fullCloseButton != null)
            {
                fullCloseButton.onClick.RemoveListener(CloseFullPanelWithReturnNavigation);
                fullCloseButton.onClick.AddListener(CloseFullPanelWithReturnNavigation);
            }

            if (journalTrackButton != null)
            {
                journalTrackButton.onClick.RemoveListener(OnJournalTrackButtonClicked);
                journalTrackButton.onClick.AddListener(OnJournalTrackButtonClicked);
            }

            if (questManager != null)
            {
                questManager.OnQuestChanged += RefreshAll;
                questManager.OnTrackedQuestChanged += OnTrackedQuestChangedFromManager;
                questManager.OnPlayerStateLoaded += OnPlayerStateLoaded;
            }

            if (fullPanel != null) fullPanel.SetActive(false);
            if (briefPanel != null) briefPanel.SetActive(true);
            ApplyGameplayUiSuppressionForFullPanel(false);
            ApplyBriefDistanceRowVisibility();
            StartCoroutine(CoRefreshAfterOtherSceneLoadHandlers());

            SceneManager.sceneLoaded += OnSceneLoaded;
            Loc.OnLanguageChanged += OnLanguageChanged;
            RefreshSettingsUiBlockingCache();
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Loc.OnLanguageChanged -= OnLanguageChanged;
            ApplyGameplayUiSuppressionForFullPanel(false);
            if (questManager != null)
            {
                questManager.OnQuestChanged -= RefreshAll;
                questManager.OnTrackedQuestChanged -= OnTrackedQuestChangedFromManager;
                questManager.OnPlayerStateLoaded -= OnPlayerStateLoaded;
            }
        }

        void OnPlayerStateLoaded()
        {
            RefreshAll();
        }

        void OnLanguageChanged()
        {
            RefreshAll();
            if (_isFullOpen)
            {
                ApplyFullPanelJournalStyle();
                RefreshJournalLocalizedText();
                RebuildJournalList();
            }
        }

        static string CategoryPrefix(QuestCategory category)
        {
            return category == QuestCategory.Main
                ? Loc.Choose("[主线]", "[Main]")
                : Loc.Choose("[支线]", "[Side]");
        }

        static string FormatDistanceText(float dist)
        {
            return dist < 0f
                ? Loc.Choose("距离目标: --", "Distance: --")
                : Loc.F("距离目标: {0}m", "Distance: {0}m", Mathf.RoundToInt(dist));
        }

        string FormatQuestSceneText(QuestSubTask sub)
        {
            if (questManager == null || sub == null)
                return QuestSceneDisplayUtil.FormatQuestSceneLine(null, false);

            questManager.TryGetSubTaskTargetSceneName(sub, out string sceneName);
            bool inCurrentScene = questManager.IsSubTaskTargetInCurrentScene(sub);
            return QuestSceneDisplayUtil.FormatQuestSceneLine(sceneName, inCurrentScene);
        }

        static string FormatRewardsText(int stepReward, int finalReward)
        {
            return Loc.F(
                "本步奖励: {0} 金币\n全部完成额外奖励: {1} 金币",
                "Step reward: {0} gold\nFull completion bonus: {1} gold",
                stepReward,
                finalReward);
        }

        void OnTrackedQuestChangedFromManager(string questId)
        {
            RefreshAll();
            if (_isFullOpen)
                RebuildJournalList();
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _cachedOpenPanelRef = null;
            _settingsBlockingCacheValid = false;
            _isFullOpen = false;
            _briefHiddenByOverlay = false;
            TryRebindSceneUiReferences();
            if (fullPanel != null) fullPanel.SetActive(false);
            if (briefPanel != null) briefPanel.SetActive(true);
            ApplyGameplayUiSuppressionForFullPanel(false);
            ApplyUndergardenBriefPanelStyleIfNeeded();
            ApplyFullPanelJournalStyle();

            questManager = QuestSystemManager.Instance;
            if (questManager == null)
                questManager = FindObjectOfType<QuestSystemManager>(true);
            if (questManager != null)
            {
                questManager.OnQuestChanged -= RefreshAll;
                questManager.OnTrackedQuestChanged -= OnTrackedQuestChangedFromManager;
                questManager.OnPlayerStateLoaded -= OnPlayerStateLoaded;
            }
            if (questManager != null)
            {
                questManager.OnQuestChanged += RefreshAll;
                questManager.OnTrackedQuestChanged += OnTrackedQuestChangedFromManager;
                questManager.OnPlayerStateLoaded += OnPlayerStateLoaded;
            }

            StartCoroutine(CoRefreshAfterOtherSceneLoadHandlers());
            RefreshSettingsUiBlockingCache();
        }

        /// <summary>
        /// 延后一帧再刷新，确保其它 sceneLoaded 订阅者（如寝室商城强制关闭）已执行完毕；
        /// 若任务进度仍在从服务器/本地加载，则等待加载完成后再刷新 UI。
        /// </summary>
        IEnumerator CoRefreshAfterOtherSceneLoadHandlers()
        {
            yield return null;

            if (questManager == null)
                questManager = QuestSystemManager.Instance != null
                    ? QuestSystemManager.Instance
                    : FindObjectOfType<QuestSystemManager>(true);

            if (questManager != null)
            {
                float deadline = Time.unscaledTime + 10f;
                while (!questManager.IsPlayerStateReady &&
                       questManager.IsPlayerStateLoadInProgress &&
                       Time.unscaledTime < deadline)
                    yield return null;
            }

            ApplyUndergardenBriefPanelStyleIfNeeded();
            TryRebindSceneUiReferences();
            RefreshAll();
        }

        void Update()
        {
            if (GameplayUiHotkeyGuard.ShouldBlockGameplayHotkeys())
                return;

            if (questManager == null)
                questManager = QuestSystemManager.Instance != null ? QuestSystemManager.Instance : FindObjectOfType<QuestSystemManager>(true);

            if (GameInput.GetKeyDown(GameControlAction.OpenQuest))
            {
                if (_isFullOpen)
                    CloseFullPanelWithReturnNavigation();
                else if (HasUsableFullPanel())
                {
                    GameplayUiHotkeyGuard.CloseOtherExclusivePanels(GameplayUiHotkeyGuard.ExclusivePanelKind.Quest);
                    OpenFullPanelWithContextDetect();
                }
            }

            if (_isFullOpen && Input.GetKeyDown(closeDetailKey) && !PhoneSessionController.IsPhoneOpen)
                CloseFullPanelWithReturnNavigation();

            RefreshDistanceOnly();
        }

        void LateUpdate()
        {
            // 放在 LateUpdate：确保在 Button.onClick（如 QualityController.ToggleQualityPanel）之后再同步遮挡状态。
            SyncBriefPanelWithDormMallOverlay();
        }

        /// <summary>
        /// 寝室商城、左上角设置面板等打开时隐藏简要任务栏；关闭后恢复。
        /// </summary>
        void SyncBriefPanelWithDormMallOverlay()
        {
            if (briefPanel == null)
                return;

            bool block = ShouldHideBriefDueToOverlay();
            if (block)
            {
                if (briefPanel.activeSelf)
                    briefPanel.SetActive(false);
                _briefHiddenByOverlay = true;
                return;
            }

            if (_briefHiddenByOverlay)
            {
                _briefHiddenByOverlay = false;
                RefreshAll();
            }
        }

        void RefreshSettingsUiBlockingCache()
        {
            if (settingsCurvePanelOverride != null)
                _cachedSettingsCurveRoot = settingsCurvePanelOverride;
            else
            {
                _cachedSettingsCurveRoot = null;
                var curve = FindObjectOfType<CurvePanelController>(true);
                if (curve != null && curve.curvePanel != null)
                    _cachedSettingsCurveRoot = curve.curvePanel;
            }

            _cachedQualityControllers = FindObjectsOfType<QualityController>(true);
            _settingsBlockingCacheValid = true;
        }

        bool IsTopLeftSettingsCurvePanelOpen()
        {
            if (!_settingsBlockingCacheValid)
                RefreshSettingsUiBlockingCache();
            return _cachedSettingsCurveRoot != null && _cachedSettingsCurveRoot.activeInHierarchy;
        }

        bool IsAnyQualitySettingsPanelOpen()
        {
            if (!_settingsBlockingCacheValid)
                RefreshSettingsUiBlockingCache();

            if (_cachedQualityControllers == null || _cachedQualityControllers.Length == 0)
                return false;

            for (int i = 0; i < _cachedQualityControllers.Length; i++)
            {
                QualityController q = _cachedQualityControllers[i];
                if (q == null) continue;
                if (q.qualityPanel != null && q.qualityPanel.activeInHierarchy)
                    return true;
            }

            return false;
        }

        bool ShouldHideBriefDueToOverlay()
        {
            return ImmersiveModeSettings.IsEnabled
                   || PhoneSessionController.IsPhoneOpen
                   || IsAnyDormMallUiOpen()
                   || IsTopLeftSettingsCurvePanelOpen()
                   || IsAnyQualitySettingsPanelOpen();
        }

        static bool IsAnyDormMallUiOpen()
        {
            return DormMallUI.IsAnyMainShopPanelOpenInHierarchy();
        }

        /// <summary>与按 J 相同：展开时点击简要栏可关闭完整栏。</summary>
        void ToggleFullPanelFromBriefUi()
        {
            if (_isFullOpen)
                CloseFullPanelWithReturnNavigation();
            else
                OpenFullPanelWithContextDetect();
        }

        void OpenFullPanelCore()
        {
            if (_isFullOpen)
                return;
            if (!HasUsableFullPanel())
                return;
            _isFullOpen = true;
            ApplyGameplayUiSuppressionForFullPanel(true);
            ApplyCursorForFullPanel(true);
            if (fullPanel != null)
                fullPanel.SetActive(true);
            EnsureJournalUiBuilt();
            ApplyFullPanelJournalStyle();
            RebuildJournalList();
            RefreshAll();
        }

        bool HasUsableFullPanel()
        {
            return fullPanel != null;
        }

        void CloseFullPanelCore()
        {
            if (!_isFullOpen)
                return;
            _isFullOpen = false;
            if (fullPanel != null)
                fullPanel.SetActive(false);
            ApplyGameplayUiSuppressionForFullPanel(false);
            ApplyCursorForFullPanel(false);
            // 打开完整栏时 RefreshAll 会关掉简要栏；关闭后必须再刷新一次才能恢复简要栏
            RefreshAll();
        }

        public void DismissForLoginFlow()
        {
            _fullPanelReturnTarget = QuestFullPanelReturnTarget.Gameplay;
            CloseFullPanelCore();
        }

        public bool IsFullPanelOpen => _isFullOpen;

        /// <summary>手机九宫格「任务」格：与按 J 打开完整任务栏相同。</summary>
        public void OpenFullPanelFromPhone() => OpenFullPanelFromPhoneLauncher();

        /// <summary>手机 ESC 返回上一级（设置 / 九宫格 / 游戏）。</summary>
        public void CloseFullPanelFromPhone() => TryHandleEscapeClose();

        /// <summary>
        /// 展开主任务栏时：先按 Inspector 列表隐藏，再自动隐藏其它根 Canvas；关闭时顺序相反以正确恢复层级。
        /// </summary>
        void ApplyGameplayUiSuppressionForFullPanel(bool hide)
        {
            if (hide)
            {
                ApplyOpenPanelSuppressionForQuestFull(true);
                ApplyExternalUiVisibility(true);
                ApplyAutoHideOtherRootCanvases(true);
            }
            else
            {
                ApplyAutoHideOtherRootCanvases(false);
                ApplyExternalUiVisibility(false);
                ApplyOpenPanelSuppressionForQuestFull(false);
            }
        }

        void ApplyOpenPanelSuppressionForQuestFull(bool hide)
        {
            if (!hideOpenPanelWhenQuestFull)
            {
                if (!hide && _openPanelQuestFullSuppressActive)
                {
                    GameObject p = ResolveOpenPanelRoot();
                    if (p != null)
                        p.SetActive(_openPanelPrevActiveSelf);
                    _openPanelQuestFullSuppressActive = false;
                }
                return;
            }

            GameObject panel = ResolveOpenPanelRoot();
            if (panel == null)
            {
                if (!hide && _openPanelQuestFullSuppressActive)
                {
                    RestoreFullPanelParentAfterOpenPanelSuppress();
                    _openPanelQuestFullSuppressActive = false;
                }
                return;
            }

            if (hide)
            {
                TryReparentFullPanelOutOfOpenPanelBeforeHide(panel);

                if (!_openPanelQuestFullSuppressActive)
                {
                    _openPanelPrevActiveSelf = panel.activeSelf;
                    _openPanelQuestFullSuppressActive = true;
                }
                panel.SetActive(false);
            }
            else if (_openPanelQuestFullSuppressActive)
            {
                panel.SetActive(_openPanelPrevActiveSelf);
                RestoreFullPanelParentAfterOpenPanelSuppress();
                _openPanelQuestFullSuppressActive = false;
            }
        }

        /// <summary>
        /// 完整栏挂在 openpanel 子级时，整关 openpanel 会一并关掉完整栏。先挂到 openpanel 的父节点下再关 openpanel，关闭完整栏后再挂回。
        /// </summary>
        void TryReparentFullPanelOutOfOpenPanelBeforeHide(GameObject openPanelRoot)
        {
            if (fullPanel == null || openPanelRoot == null || _fullPanelReparentedForOpenPanelSuppress)
                return;
            if (!fullPanel.transform.IsChildOf(openPanelRoot.transform))
                return;

            Transform anchor = openPanelRoot.transform.parent;
            if (anchor == null)
            {
                Canvas cv = openPanelRoot.GetComponentInParent<Canvas>(true);
                if (cv != null && cv.transform != openPanelRoot.transform)
                    anchor = cv.transform;
            }

            if (anchor == null)
                return;

            _fullPanelRestoreParent = fullPanel.transform.parent;
            _fullPanelRestoreSiblingIndex = fullPanel.transform.GetSiblingIndex();
            fullPanel.transform.SetParent(anchor, false);
            fullPanel.transform.SetAsLastSibling();
            _fullPanelReparentedForOpenPanelSuppress = true;
        }

        void RestoreFullPanelParentAfterOpenPanelSuppress()
        {
            if (!_fullPanelReparentedForOpenPanelSuppress || fullPanel == null)
                return;
            if (_fullPanelRestoreParent != null)
            {
                fullPanel.transform.SetParent(_fullPanelRestoreParent, false);
                int n = _fullPanelRestoreParent.childCount;
                int idx = Mathf.Clamp(_fullPanelRestoreSiblingIndex, 0, Mathf.Max(0, n - 1));
                fullPanel.transform.SetSiblingIndex(idx);
            }
            _fullPanelReparentedForOpenPanelSuppress = false;
            _fullPanelRestoreParent = null;
            _fullPanelRestoreSiblingIndex = 0;
        }

        GameObject ResolveOpenPanelRoot()
        {
            if (openPanelRootOverride != null)
                return openPanelRootOverride;
            if (_cachedOpenPanelRef != null)
                return _cachedOpenPanelRef;
            if (string.IsNullOrEmpty(openPanelObjectName))
                return null;
            _cachedOpenPanelRef = FindInLoadedScenesByName(openPanelObjectName);
            return _cachedOpenPanelRef;
        }

        static GameObject FindInLoadedScenesByName(string objectName)
        {
            for (int si = 0; si < SceneManager.sceneCount; si++)
            {
                Scene s = SceneManager.GetSceneAt(si);
                if (!s.isLoaded)
                    continue;
                GameObject[] roots = s.GetRootGameObjects();
                for (int ri = 0; ri < roots.Length; ri++)
                {
                    Transform found = FindChildTransformByName(roots[ri].transform, objectName);
                    if (found != null)
                        return found.gameObject;
                }
            }
            return null;
        }

        static Transform FindChildTransformByName(Transform root, string objectName)
        {
            if (root == null)
                return null;
            if (string.Equals(root.name, objectName, System.StringComparison.Ordinal))
                return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform t = FindChildTransformByName(root.GetChild(i), objectName);
                if (t != null)
                    return t;
            }
            return null;
        }

        void ApplyAutoHideOtherRootCanvases(bool hide)
        {
            if (!hide)
            {
                if (_autoHiddenCanvasPrevActive.Count == 0)
                    return;
                var snapshot = new List<KeyValuePair<GameObject, bool>>(_autoHiddenCanvasPrevActive);
                _autoHiddenCanvasPrevActive.Clear();
                for (int i = 0; i < snapshot.Count; i++)
                {
                    var go = snapshot[i].Key;
                    if (go != null)
                        go.SetActive(snapshot[i].Value);
                }
                return;
            }

            if (!autoHideOtherRootCanvasesWhenFullOpen)
                return;

            var canvases = FindObjectsOfType<Canvas>(true);
            var processedRoots = new HashSet<GameObject>();
            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas c = canvases[i];
                if (c == null) continue;
                if (skipWorldSpaceCanvasWhenAutoHiding && c.renderMode == RenderMode.WorldSpace)
                    continue;
                if (!IsOutermostCanvasInHierarchy(c))
                    continue;
                if (!c.gameObject.activeInHierarchy)
                    continue;
                if (IsCanvasBranchHostingQuestUi(c))
                    continue;

                GameObject root = c.gameObject;
                if (processedRoots.Contains(root))
                    continue;
                processedRoots.Add(root);

                if (!_autoHiddenCanvasPrevActive.ContainsKey(root))
                    _autoHiddenCanvasPrevActive[root] = root.activeSelf;
                root.SetActive(false);
            }
        }

        static bool IsOutermostCanvasInHierarchy(Canvas c)
        {
            Transform p = c.transform.parent;
            while (p != null)
            {
                if (p.GetComponent<Canvas>() != null)
                    return false;
                p = p.parent;
            }
            return true;
        }

        bool IsCanvasBranchHostingQuestUi(Canvas c)
        {
            if (c == null) return false;
            // QuestSystemUI 常挂在 Canvas 根物体上：IsChildOf(自身) 在 Unity 中为 false，会导致整块任务 Canvas 被 J 键「全屏隐藏」后无法点任何按钮恢复。
            if (c.transform == transform)
                return true;
            if (fullPanel != null && fullPanel.transform.IsChildOf(c.transform))
                return true;
            if (briefPanel != null && briefPanel.transform.IsChildOf(c.transform))
                return true;
            return transform.IsChildOf(c.transform);
        }

        void ApplyExternalUiVisibility(bool hide)
        {
            if (hideWhenFullPanelOpen == null || hideWhenFullPanelOpen.Count == 0)
                return;

            for (int i = 0; i < hideWhenFullPanelOpen.Count; i++)
            {
                var go = hideWhenFullPanelOpen[i];
                if (go == null) continue;

                if (hide)
                {
                    if (!_cachedUiActiveStates.ContainsKey(go))
                        _cachedUiActiveStates[go] = go.activeSelf;
                    go.SetActive(false);
                }
                else
                {
                    bool wasActive;
                    if (_cachedUiActiveStates.TryGetValue(go, out wasActive))
                    {
                        go.SetActive(wasActive);
                    }
                    else if (!go.activeSelf)
                    {
                        go.SetActive(true);
                    }
                }
            }

            if (!hide)
                _cachedUiActiveStates.Clear();
        }

        void ApplyCursorForFullPanel(bool isOpen)
        {
            if (!showCursorWhenFullPanelOpen)
                return;

            if (isOpen)
            {
                if (!_cachedCursorStateValid)
                {
                    _cachedCursorVisible = Cursor.visible;
                    _cachedCursorLockMode = Cursor.lockState;
                    _cachedGameplayCursorLocked = GameplayCursorLock.IsLocked;
                    _cachedCursorStateValid = true;
                }

                GameplayCursorLock.SetLocked(false);
            }
            else if (_cachedCursorStateValid)
            {
                if (!PhoneUI.PhoneSessionController.IsPhoneOpen)
                {
                    GameplayCursorLock.SetLocked(_cachedGameplayCursorLocked);
                    if (Cursor.lockState != _cachedCursorLockMode || Cursor.visible != _cachedCursorVisible)
                    {
                        Cursor.lockState = _cachedCursorLockMode;
                        Cursor.visible = _cachedCursorVisible;
                    }
                }

                _cachedCursorStateValid = false;
            }
        }

        void RefreshDistanceOnly()
        {
            if (questManager == null) return;
            var quest = questManager.GetTrackedQuest();
            var sub = questManager.GetTrackedSubTask();
            if (quest == null || sub == null) return;

            float dist = questManager.GetDistanceToActiveTarget();
            string distance = FormatDistanceText(dist);
            bool briefNeedsUpdate = showBriefDistanceText && briefDistanceText != null && briefDistanceText.text != distance;
            bool fullNeedsUpdate = _isFullOpen && fullDistanceText != null && fullDistanceText.text != distance;
            if (distance == _lastDistanceText && !briefNeedsUpdate && !fullNeedsUpdate)
                return;

            _lastDistanceText = distance;
            if (briefNeedsUpdate) briefDistanceText.text = distance;
            if (fullNeedsUpdate) fullDistanceText.text = distance;
        }

        void ApplyBriefDistanceRowVisibility()
        {
            if (briefDistanceText == null) return;
            briefDistanceText.gameObject.SetActive(showBriefDistanceText);
        }

        public void RefreshAll()
        {
            if (questManager == null)
                questManager = QuestSystemManager.Instance;
            if (questManager == null)
                questManager = FindObjectOfType<QuestSystemManager>(true);

            if (questManager == null)
            {
                ShowNoTaskState();
                return;
            }

            var quest = questManager.GetTrackedQuest();
            var sub = questManager.GetTrackedSubTask();
            if (quest == null || sub == null)
            {
                ShowNoTaskState();
                if (_isFullOpen)
                    RefreshJournalDetailPanel(null, null);
                return;
            }

            if (briefPanel != null) briefPanel.SetActive(!_isFullOpen);
            if (_isFullOpen && fullPanel != null) fullPanel.SetActive(true);

            string categoryPrefix = CategoryPrefix(quest.category);
            string title = categoryPrefix + " " + sub.subTaskName + " - " + sub.finishLocationName;
            title = StoryProtagonistNameUtility.PersonalizeStoryText(title);
            int step = questManager.GetDisplaySubTaskIndex();
            int total = Mathf.Max(1, questManager.GetTotalSubTaskCount());
            string progress = step + "/" + total;
            float dist = questManager.GetDistanceToActiveTarget();
            string distance = FormatDistanceText(dist);
            string shortDesc = StoryProtagonistNameUtility.PersonalizeStoryText(sub.shortDescription);
            string longDesc = StoryProtagonistNameUtility.PersonalizeStoryText(sub.longDescription);
            if (sub.triggerType == QuestTriggerType.Counter)
            {
                int cur = questManager.GetActiveCounterCurrent();
                int need = Mathf.Max(1, sub.targetCount);
                shortDesc += "\n" + Loc.F("进度: {0}/{1}", "Progress: {0}/{1}", cur, need);
                longDesc += "\n\n" + Loc.F("当前计数进度: {0}/{1}", "Counter progress: {0}/{1}", cur, need);
            }

            if (showBriefDistanceText && briefDistanceText != null) briefDistanceText.text = distance;
            if (briefTitleText != null) briefTitleText.text = title;
            if (briefProgressText != null) briefProgressText.text = progress;
            if (briefSceneText != null) briefSceneText.text = FormatQuestSceneText(sub);
            if (briefIntroText != null) briefIntroText.text = NormalizeBriefIntroText(shortDesc);

            if (fullDistanceText != null) fullDistanceText.text = distance;
            if (fullTitleText != null) fullTitleText.text = title;
            if (fullProgressText != null) fullProgressText.text = progress;
            if (fullSceneText != null) fullSceneText.text = FormatQuestSceneText(sub);
            if (fullDetailText != null) fullDetailText.text = longDesc;
            if (fullRewardsText != null)
                fullRewardsText.text = FormatRewardsText(sub.rewardGold, quest.finalRewardGold);

            if (ShouldHideBriefDueToOverlay() && briefPanel != null)
                briefPanel.SetActive(false);

            if (_isFullOpen)
                RefreshJournalDetailPanel(quest, sub);
        }

        void ShowNoTaskState()
        {
            if (briefPanel != null)
                briefPanel.SetActive(showBriefPanelWhenNoTask && !_isFullOpen);

            // 无任务时 RefreshAll 仍会调用本方法；若玩家已按 J 展开完整栏，不得关掉 fullPanel，否则会立刻回到「隐藏」状态。
            if (fullPanel != null && _isFullOpen)
                fullPanel.SetActive(true);

            if (!showBriefPanelWhenNoTask && !_isFullOpen)
                return;

            if (showBriefDistanceText && briefDistanceText != null) briefDistanceText.text = Loc.Choose("任务状态", "Quest Status");
            if (briefTitleText != null) briefTitleText.text = Loc.Choose("暂无可追踪任务", "No tracked quest");
            if (briefProgressText != null) briefProgressText.text = "--/--";
            if (briefSceneText != null) briefSceneText.text = QuestSceneDisplayUtil.FormatQuestSceneLine(null, false);
            if (briefIntroText != null)
                briefIntroText.text = Loc.Choose(
                    "请在 QuestSystemManager 的 Quest Chains 中配置至少一个大任务和小任务。",
                    "Configure at least one quest chain and sub-task in QuestSystemManager.");

            if (_isFullOpen)
            {
                if (fullDistanceText != null) fullDistanceText.text = Loc.Choose("距离目标: --", "Distance: --");
                if (fullTitleText != null) fullTitleText.text = Loc.Choose("暂无可追踪任务", "No tracked quest");
                if (fullProgressText != null) fullProgressText.text = "--/--";
                if (fullSceneText != null) fullSceneText.text = QuestSceneDisplayUtil.FormatQuestSceneLine(null, false);
                if (fullDetailText != null)
                    fullDetailText.text = Loc.Choose(
                        "请在 QuestSystemManager 的 Quest Chains 中配置至少一个大任务和小任务。",
                        "Configure at least one quest chain and sub-task in QuestSystemManager.");
                if (fullRewardsText != null) fullRewardsText.text = "";
            }

            if (ShouldHideBriefDueToOverlay() && briefPanel != null)
                briefPanel.SetActive(false);
        }

        void EnsureQuestFont()
        {
            if (questUIFont != null) return;
            var all = Resources.FindObjectsOfTypeAll<Font>();
            if (all == null || all.Length == 0) return;
            for (int i = 0; i < all.Length; i++)
            {
                var f = all[i];
                if (f == null) continue;
                if (string.Equals(f.name, preferredFontName, System.StringComparison.OrdinalIgnoreCase))
                {
                    questUIFont = f;
                    break;
                }
            }
            if (questUIFont == null)
            {
                for (int i = 0; i < all.Length; i++)
                {
                    var f = all[i];
                    if (f == null) continue;
                    if (f.name != null && f.name.IndexOf("Alibaba", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        questUIFont = f;
                        break;
                    }
                }
            }
        }

        void ApplyFontToAllTexts()
        {
            if (questUIFont == null) return;
            ApplyFont(briefDistanceText, questUIFont);
            ApplyFont(briefTitleText, questUIFont);
            ApplyFont(briefProgressText, questUIFont);
            ApplyFont(briefIntroText, questUIFont);
            ApplyFont(fullDistanceText, questUIFont);
            ApplyFont(fullTitleText, questUIFont);
            ApplyFont(fullProgressText, questUIFont);
            ApplyFont(fullDetailText, questUIFont);
            ApplyFont(fullRewardsText, questUIFont);
        }

        static void ApplyFont(Text t, Font font)
        {
            if (t == null) return;
            t.font = font;
        }

        void ApplyAllAdaptiveLayout()
        {
            ApplyBriefTextLayout();
            if (autoFitByPanelSize)
                ApplyFullAdaptiveLayout();
        }

        /// <summary>简要栏：手动逐行字号，或按面板自适应（旧逻辑）。</summary>
        void ApplyBriefTextLayout()
        {
            if (useManualBriefLineFontSizes)
            {
                ApplyManualBriefLineSizes();
                return;
            }

            if (!autoFitByPanelSize)
                return;

            ApplyBriefAdaptiveLayoutScaled();
        }

        void ApplyManualBriefLineSizes()
        {
            if (showBriefDistanceText)
                SetBriefFixedFontSize(briefDistanceText, briefDistanceLineFontSize);
            SetBriefFixedFontSize(briefTitleText, briefTitleLineFontSize);
            SetBriefFixedFontSize(briefProgressText, briefProgressLineFontSize);
            SetBriefFixedFontSize(briefSceneText, briefSceneLineFontSize);
            SetBriefFixedFontSize(briefIntroText, briefIntroLineFontSize);
        }

        static void SetBriefFixedFontSize(Text t, int size)
        {
            if (t == null) return;
            int s = Mathf.Max(1, size);
            t.fontSize = s;
            t.resizeTextForBestFit = false;
            t.resizeTextMinSize = s;
            t.resizeTextMaxSize = s;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
        }

        void ApplyBriefAdaptiveLayoutScaled()
        {
            var rt = briefPanel != null ? briefPanel.GetComponent<RectTransform>() : null;
            if (rt == null) return;

            float w = Mathf.Max(1f, rt.rect.width);
            float h = Mathf.Max(1f, rt.rect.height);
            float scale = Mathf.Min(w / 430f, h / 220f);
            scale = Mathf.Clamp(scale, 0.35f, 1.25f);

            if (showBriefDistanceText)
                SetTextSize(briefDistanceText, GetBaseSize(briefDistanceText, 20, useInspectorFontSizeAsBase), scale, briefMinFontSize, briefMaxFontSize);
            SetTextSize(briefTitleText, GetBaseSize(briefTitleText, 22, useInspectorFontSizeAsBase), scale, briefMinFontSize, briefMaxFontSize);
            SetTextSize(briefProgressText, GetBaseSize(briefProgressText, 20, useInspectorFontSizeAsBase), scale, briefMinFontSize, briefMaxFontSize);
            SetTextSize(briefIntroText, GetBaseSize(briefIntroText, 19, useInspectorFontSizeAsBase), scale, briefMinFontSize, briefMaxFontSize);
        }

        void ApplyFullAdaptiveLayout()
        {
            var rt = fullPanel != null ? fullPanel.GetComponent<RectTransform>() : null;
            if (rt == null) return;

            float w = Mathf.Max(1f, rt.rect.width);
            float h = Mathf.Max(1f, rt.rect.height);
            float scale = Mathf.Min(w / 1920f, h / 1080f);
            scale = Mathf.Clamp(scale, 0.65f, 1.3f);

            SetTextSize(fullDistanceText, GetBaseSize(fullDistanceText, 28, useInspectorFontSizeAsBase), scale, fullMinFontSize, fullMaxFontSize);
            SetTextSize(fullTitleText, GetBaseSize(fullTitleText, 34, useInspectorFontSizeAsBase), scale, fullMinFontSize, fullMaxFontSize);
            SetTextSize(fullProgressText, GetBaseSize(fullProgressText, 28, useInspectorFontSizeAsBase), scale, fullMinFontSize, fullMaxFontSize);
            SetTextSize(fullDetailText, GetBaseSize(fullDetailText, 26, useInspectorFontSizeAsBase), scale, fullMinFontSize, fullMaxFontSize);
            SetTextSize(fullRewardsText, GetBaseSize(fullRewardsText, 26, useInspectorFontSizeAsBase), scale, fullMinFontSize, fullMaxFontSize);
        }

        static int GetBaseSize(Text t, int fallback, bool useInspector)
        {
            if (!useInspector || t == null) return fallback;
            return Mathf.Max(1, t.fontSize);
        }

        static void SetTextSize(Text t, int baseSize, float scale, int minSize, int maxSize)
        {
            if (t == null) return;
            int size = Mathf.Clamp(Mathf.RoundToInt(baseSize * scale), minSize, maxSize);
            t.fontSize = size;
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = minSize;
            t.resizeTextMaxSize = maxSize;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
        }
    }
}
