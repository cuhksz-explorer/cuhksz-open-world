using System;
using System.Collections.Generic;
using GameControls;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using TMPro;
using Multiplayer;

namespace DormMall
{
    /// <summary>背包：按 B 打开；场景内 UI 与运行时生成的条目均使用阿里巴巴普惠体（TMP SDF）。</summary>
    public class InventoryUI : MonoBehaviour
    {
        public static InventoryUI Instance { get; private set; }
        public static bool IsAnyInventoryPanelOpen()
        {
            if (Instance == null || Instance.inventoryPanel == null)
                return false;
            return Instance.inventoryPanel.activeInHierarchy;
        }

        const string AlibabaTmpResourcesPath = "Fonts & Materials/AlibabaPuHuiTi-3-75-SemiBold SDF";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnsureInventoryAtRoot()
        {
            var existing = UnityEngine.Object.FindObjectOfType<InventoryUI>(true);
            if (existing != null)
            {
                Debug.Log("[InventoryUI] 找到已有实例，移到根节点");
                existing.transform.SetParent(null);
                UiLifecycleSafe.SetActiveIfAlive(existing.gameObject, true);
                return;
            }
            var go = new GameObject("InventoryCanvas");
            go.AddComponent<InventoryUI>();
            Debug.Log("[InventoryUI] 未找到实例，已创建 InventoryCanvas");
        }

        [Header("UI 引用（可在编辑器里拖，运行时也会自动查找）")]
        public GameObject inventoryPanel;
        public Transform inventoryContent;

        [Header("字体（留空则从 Resources 加载阿里巴巴普惠体 TMP）")]
        [Tooltip("背包 Canvas 下所有 TextMeshPro 使用该 SDF；留空则用 " + AlibabaTmpResourcesPath)]
        public TMP_FontAsset inventoryTmpFont;

        [Tooltip("若仍有旧版 Unity UI Text，可指定 TTF 字体；一般可留空")]
        public Font inventoryLegacyFont;

        [Header("输入")]
        [Tooltip("点击背包外层遮罩是否立即关闭。默认关闭此行为，避免点击物品时被父层误关。")]
        public bool closeOnOverlayClick = false;

        [Header("调试")]
        public bool debugLog = true;

        private List<GameObject> _itemUIs = new List<GameObject>();
        private ProductDatabase _database;
        private TMP_FontAsset _resolvedTmpFont;
        CursorLockMode _cursorLockBeforeOpen;
        bool _cursorVisibleBeforeOpen;
        bool _cursorStateCached;
        FirstPersonCamera _firstPersonCameraBeforeOpen;
        bool _firstPersonCursorLockedBeforeOpen;
        bool _firstPersonStateCached;

        void Awake()
        {
            if (debugLog) Debug.Log("[InventoryUI] Awake 执行");
            if (Instance != null && Instance != this)
            {
                if (debugLog) Debug.Log("[InventoryUI] 已有 Instance，销毁当前对象");
                Destroy(gameObject);
                return;
            }
            Instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
            DisableDuplicateAudioListeners();
            Invoke(nameof(DisableDuplicateAudioListenersDelayed), 0.2f);
            Invoke(nameof(DisableDuplicateAudioListenersDelayed), 1f);
            if (EventSystem.current == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }
            _database = Resources.Load<ProductDatabase>("DormMall/ProductDatabase");
            _resolvedTmpFont = inventoryTmpFont != null
                ? inventoryTmpFont
                : Resources.Load<TMP_FontAsset>(AlibabaTmpResourcesPath);

            EnsureRefs();
            if (inventoryPanel == null)
            {
                if (debugLog) Debug.Log("[InventoryUI] inventoryPanel 为空，调用 CreateRuntimeUI");
                CreateRuntimeUI();
            }
            else if (debugLog) Debug.Log("[InventoryUI] inventoryPanel 已赋值");

            BindCloseButtons();
            ApplyFontsToInventorySubtree(transform);

            if (inventoryPanel != null)
            {
                UiLifecycleSafe.SetActiveIfAlive(inventoryPanel, false);
                if (debugLog) Debug.Log($"[InventoryUI] Awake 完成，activeInHierarchy={gameObject.activeInHierarchy}，Panel 已关闭");
            }
            else if (debugLog) Debug.LogWarning("[InventoryUI] inventoryPanel 仍为空，B 键无响应");
        }

        void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            Loc.OnLanguageChanged += OnLanguageChanged;
        }

        void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Loc.OnLanguageChanged -= OnLanguageChanged;
        }

        void OnLanguageChanged()
        {
            RefreshInventoryTitle();
            if (inventoryPanel != null && inventoryPanel.activeSelf)
                RefreshDisplay();
        }

        void RefreshInventoryTitle()
        {
            if (inventoryPanel == null) return;
            var titleTmp = inventoryPanel.transform.Find("Body/Title")?.GetComponent<TextMeshProUGUI>();
            if (titleTmp != null)
                titleTmp.text = Loc.Choose("我的背包", "My Inventory");
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Additive)
                return;
            if (inventoryPanel != null && inventoryPanel.activeSelf)
                Close();
            ClearCachedInputState();
        }

        void Start()
        {
            if (debugLog && inventoryPanel != null)
                Debug.Log($"[InventoryUI] Start 完成，按 {GameInput.FormatChord(GameControlAction.OpenInventory)} 切换背包，activeInHierarchy={gameObject.activeInHierarchy}");
        }

        void EnsureRefs()
        {
            if (inventoryPanel == null) inventoryPanel = transform.Find("Panel")?.gameObject;
            if (inventoryContent == null) inventoryContent = transform.Find("Panel/Body/Scroll/Viewport/Content");
            if (debugLog) Debug.Log($"[InventoryUI] EnsureRefs: inventoryPanel={inventoryPanel != null}, inventoryContent={inventoryContent != null}");
        }

        /// <summary>统一背包 Canvas 下 TMP 字体与材质，并修正「字体是阿里巴巴但材质指向 Liberation」的情况。</summary>
        public void ApplyFontsToInventorySubtree(Transform root)
        {
            if (root == null) return;
            foreach (var tmp in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (_resolvedTmpFont != null)
                {
                    tmp.font = _resolvedTmpFont;
                    tmp.fontSharedMaterial = _resolvedTmpFont.material;
                }
                else if (tmp.font != null && tmp.font.material != null)
                    tmp.fontSharedMaterial = tmp.font.material;
            }

            if (inventoryLegacyFont == null)
                inventoryLegacyFont = Resources.Load<Font>("Fonts/AlibabaPuHuiTi-3-75-SemiBold");
            if (inventoryLegacyFont != null)
            {
                foreach (var leg in root.GetComponentsInChildren<Text>(true))
                    leg.font = inventoryLegacyFont;
            }
        }

        void CreateRuntimeUI()
        {
            if (GetComponent<Canvas>() == null)
            {
                var canvas = gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100;
                var scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;
                gameObject.AddComponent<GraphicRaycaster>();
            }

            var panelGo = new GameObject("Panel");
            panelGo.transform.SetParent(transform, false);
            var panelRect = panelGo.AddComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = panelRect.offsetMax = Vector2.zero;
            var panelImg = panelGo.AddComponent<Image>();
            panelImg.color = new Color(0, 0, 0, 0.5f);
            panelImg.raycastTarget = true;
            panelGo.AddComponent<Button>().transition = Selectable.Transition.None;

            var body = new GameObject("Body");
            body.transform.SetParent(panelGo.transform, false);
            var bodyRect = body.AddComponent<RectTransform>();
            bodyRect.anchorMin = bodyRect.anchorMax = new Vector2(0.5f, 0.5f);
            bodyRect.anchoredPosition = Vector2.zero;
            bodyRect.sizeDelta = new Vector2(1920, 1080);
            var bodyImg = body.AddComponent<Image>();
            bodyImg.color = Color.white;
            bodyImg.raycastTarget = true;

            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(body.transform, false);
            var titleRect = titleGo.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 1);
            titleRect.anchorMax = new Vector2(1, 1);
            titleRect.offsetMin = new Vector2(20, -50);
            titleRect.offsetMax = new Vector2(-20, -10);
            var titleTmp = titleGo.AddComponent<TextMeshProUGUI>();
            titleTmp.text = Loc.Choose("我的背包", "My Inventory");
            titleTmp.fontSize = 32;
            titleTmp.alignment = TextAlignmentOptions.Center;
            titleTmp.color = new Color(0.54f, 0.17f, 0.89f);
            titleTmp.raycastTarget = false;
            titleTmp.enableWordWrapping = true;
            titleTmp.overflowMode = TextOverflowModes.Overflow;
            BindTmpFont(titleTmp);

            var closeBtnGo = new GameObject("CloseBtn");
            closeBtnGo.transform.SetParent(body.transform, false);
            var closeRect = closeBtnGo.AddComponent<RectTransform>();
            closeRect.anchorMin = closeRect.anchorMax = new Vector2(1, 1);
            closeRect.pivot = new Vector2(1, 1);
            closeRect.anchoredPosition = new Vector2(-10, -10);
            closeRect.sizeDelta = new Vector2(50, 50);
            closeBtnGo.AddComponent<Image>().color = new Color(0.8f, 0.8f, 0.8f);
            closeBtnGo.AddComponent<Button>();
            var closeTxtGo = new GameObject("Text");
            closeTxtGo.transform.SetParent(closeBtnGo.transform, false);
            var closeTmp = closeTxtGo.AddComponent<TextMeshProUGUI>();
            closeTmp.rectTransform.anchorMin = closeTmp.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            closeTmp.rectTransform.offsetMin = closeTmp.rectTransform.offsetMax = Vector2.zero;
            closeTmp.text = "×";
            closeTmp.fontSize = 28;
            closeTmp.alignment = TextAlignmentOptions.Center;
            closeTmp.color = Color.white;
            closeTmp.raycastTarget = false;
            BindTmpFont(closeTmp);

            var scrollGo = new GameObject("Scroll");
            scrollGo.transform.SetParent(body.transform, false);
            var scrollRt = scrollGo.AddComponent<RectTransform>();
            scrollRt.anchorMin = new Vector2(0, 0);
            scrollRt.anchorMax = new Vector2(1, 1);
            scrollRt.offsetMin = new Vector2(20, 20);
            scrollRt.offsetMax = new Vector2(-20, -70);
            var scroll = scrollGo.AddComponent<ScrollRect>();

            var viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollGo.transform, false);
            var vpRect = viewport.AddComponent<RectTransform>();
            vpRect.anchorMin = Vector2.zero;
            vpRect.anchorMax = Vector2.one;
            vpRect.offsetMin = vpRect.offsetMax = Vector2.zero;
            viewport.AddComponent<RectMask2D>();

            var content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            var cRect = content.AddComponent<RectTransform>();
            cRect.anchorMin = new Vector2(0, 1);
            cRect.anchorMax = new Vector2(1, 1);
            cRect.pivot = new Vector2(0.5f, 1);
            cRect.sizeDelta = new Vector2(0, 400);
            var glg = content.AddComponent<GridLayoutGroup>();
            glg.cellSize = new Vector2(960, 800);
            glg.spacing = new Vector2(16, 16);
            glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            glg.constraintCount = 2;
            glg.padding = new RectOffset(10, 10, 10, 10);
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.content = cRect;
            scroll.viewport = vpRect;
            scroll.horizontal = false;
            scroll.vertical = true;

            inventoryPanel = panelGo;
            inventoryContent = content.transform;
        }

        void BindTmpFont(TextMeshProUGUI tmp)
        {
            if (_resolvedTmpFont == null) return;
            tmp.font = _resolvedTmpFont;
            tmp.fontSharedMaterial = _resolvedTmpFont.material;
        }

        void BindCloseButtons()
        {
            if (inventoryPanel == null) return;
            var overlayBtn = inventoryPanel.GetComponent<Button>();
            if (overlayBtn)
            {
                overlayBtn.onClick.RemoveAllListeners();
                overlayBtn.transition = Selectable.Transition.None;
                overlayBtn.interactable = closeOnOverlayClick;
                if (closeOnOverlayClick)
                    overlayBtn.onClick.AddListener(Close);
            }
            var closeBtn = inventoryPanel.transform.Find("Body/CloseBtn")?.GetComponent<Button>();
            if (closeBtn) { closeBtn.onClick.RemoveAllListeners(); closeBtn.onClick.AddListener(Close); }
        }

        static void DisableDuplicateAudioListeners()
        {
            if (TeleportSceneLoadOverlay.IsShowing)
                return;

            var switcher = UnityEngine.Object.FindObjectOfType<CameraSwitcher>();
            if (switcher != null)
            {
                switcher.RefreshGameplayAudioListeners();
                return;
            }

            var listeners = UnityEngine.Object.FindObjectsOfType<AudioListener>(true);
            if (listeners.Length <= 1) return;
            AudioListener keep = null;
            foreach (var al in listeners)
            {
                var cam = al.GetComponent<Camera>();
                if (cam != null && cam.CompareTag("MainCamera")) { keep = al; break; }
            }
            if (keep == null) keep = listeners[0];
            int disabled = 0;
            foreach (var al in listeners)
            {
                if (al != keep && al.enabled) { al.enabled = false; disabled++; }
            }
            if (disabled > 0 && Instance != null && Instance.debugLog)
                Debug.Log($"[InventoryUI] 已禁用 {disabled} 个重复 AudioListener");
        }

        void DisableDuplicateAudioListenersDelayed()
        {
            DisableDuplicateAudioListeners();
        }

        void OnDestroy()
        {
            if (_cursorStateCached || (inventoryPanel != null && inventoryPanel.activeSelf))
                RestoreInputStateAfterInventoryClose();
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (inventoryPanel == null) return;
            if (GameplayUiHotkeyGuard.ShouldBlockDontDestroyOnLoadHotkeys()) return;
            if (GameInput.GetKeyDown(GameControlAction.OpenInventory))
            {
                bool visible = inventoryPanel.activeSelf;
                if (!visible)
                    GameplayUiHotkeyGuard.CloseOtherExclusivePanels(GameplayUiHotkeyGuard.ExclusivePanelKind.Inventory);
                UiLifecycleSafe.SetActiveIfAlive(inventoryPanel, !visible);
                if (debugLog) Debug.Log($"[InventoryUI] 按下背包键，Panel 切换为 {(visible ? "隐藏" : "显示")}");
                if (!visible)
                {
                    RefreshDisplay();
                    ApplyInputStateForInventoryOpen();
                }
                else
                    RestoreInputStateAfterInventoryClose();
            }
        }

        public void AddPurchasedItem(int id, string productName, string category, string iconPath)
        {
            InventoryData.AddItem(id, productName, category, iconPath);
        }

        public void AddPurchasedItem(ProductEntry entry)
        {
            InventoryData.AddItem(entry);
        }

        public void Close()
        {
            if (inventoryPanel != null) UiLifecycleSafe.SetActiveIfAlive(inventoryPanel, false);
            RestoreInputStateAfterInventoryClose();
        }

        /// <summary>手机九宫格「背包」格：打开背包面板（与按切换键显示时相同）。</summary>
        public void OpenPanelExternal(bool preservePhoneSession = false)
        {
            if (inventoryPanel == null)
                return;
            if (inventoryPanel.activeSelf)
                return;

            GameplayUiHotkeyGuard.CloseOtherExclusivePanels(
                GameplayUiHotkeyGuard.ExclusivePanelKind.Inventory,
                preservePhoneSession);
            UiLifecycleSafe.SetActiveIfAlive(inventoryPanel, true);
            RefreshDisplay();
            ApplyInputStateForInventoryOpen();
        }

        void ApplyInputStateForInventoryOpen()
        {
            if (!_cursorStateCached)
            {
                _cursorLockBeforeOpen = Cursor.lockState;
                _cursorVisibleBeforeOpen = Cursor.visible;
                _cursorStateCached = true;
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (!_firstPersonStateCached)
            {
                _firstPersonCameraBeforeOpen = FindActiveFirstPersonCamera();
                if (_firstPersonCameraBeforeOpen != null)
                {
                    _firstPersonCursorLockedBeforeOpen = _firstPersonCameraBeforeOpen.IsCursorLocked;
                    _firstPersonStateCached = true;
                }
            }

            if (_firstPersonCameraBeforeOpen != null)
                _firstPersonCameraBeforeOpen.SetCursorLocked(false);
        }

        void RestoreInputStateAfterInventoryClose()
        {
            if (PhoneUI.PhoneSessionController.IsPhoneOpen)
            {
                PhoneUI.PhoneSessionController.Instance?.EnsurePhoneUiCursor();
                ClearCachedInputState();
                return;
            }

            if (_firstPersonStateCached && _firstPersonCameraBeforeOpen != null)
                _firstPersonCameraBeforeOpen.SetCursorLocked(_firstPersonCursorLockedBeforeOpen);
            else if (!DormSceneConfig.IsDormScene(SceneManager.GetActiveScene().name))
                RestoreWalkingCameraAfterInventoryClose();

            ClearCachedInputState();
        }

        static void RestoreWalkingCameraAfterInventoryClose()
        {
            var pm = UnityEngine.Object.FindObjectOfType<PlayerMovement>();
            if (pm != null)
            {
                pm.ApplyWalkingFirstPersonCameraBinding();
                return;
            }

            var fps = UnityEngine.Object.FindObjectOfType<FirstPersonCamera>(true);
            if (fps != null)
                fps.ReconcileWalkingControlState();
        }

        void ClearCachedInputState()
        {
            _firstPersonCameraBeforeOpen = null;
            _firstPersonStateCached = false;
            _cursorStateCached = false;
        }

        FirstPersonCamera FindActiveFirstPersonCamera()
        {
            var all = FindObjectsOfType<FirstPersonCamera>(true);
            for (int i = 0; i < all.Length; i++)
            {
                var fps = all[i];
                if (fps != null && fps.IsActive)
                    return fps;
            }
            return all.Length > 0 ? all[0] : null;
        }

        void RefreshDisplay()
        {
            if (inventoryContent == null) return;
            foreach (var go in _itemUIs)
                if (go != null) Destroy(go);
            _itemUIs.Clear();

            var items = InventoryData.GetItems();
            var dormItems = new List<(PurchasedItemData item, int index)>();
            var foodItems = new List<(PurchasedItemData item, int index)>();
            for (int i = 0; i < items.Count; i++)
            {
                var x = items[i];
                var dbEntry = _database != null ? _database.GetById(x.id) : null;
                bool isFood = (dbEntry != null && dbEntry.isConsumableFood) || ProductDatabase.IsFoodCategory(x.category);
                if (isFood) foodItems.Add((x, i));
                else dormItems.Add((x, i));
            }
            for (int i = 0; i < dormItems.Count; i++)
            {
                var x = dormItems[i];
                var go = CreateItemUI(x.item, x.index);
                if (go != null) { go.transform.SetParent(inventoryContent, false); _itemUIs.Add(go); }
            }
            for (int i = 0; i < foodItems.Count; i++)
            {
                var x = foodItems[i];
                var go = CreateItemUI(x.item, x.index);
                if (go != null) { go.transform.SetParent(inventoryContent, false); _itemUIs.Add(go); }
            }
        }

        void OnInventoryItemClick(PurchasedItemData item, int index)
        {
            if (_database == null) { Debug.LogWarning("[InventoryUI] 未找到 ProductDatabase"); return; }
            var entry = _database.GetById(item.id);
            if (entry != null && entry.isConsumableFood)
            {
                ConsumeFood(entry, index);
                return;
            }
            if (CoopSessionState.IsSessionGuest)
            {
                if (debugLog) Debug.Log("[InventoryUI] 联机客方背包只读，不可放置。");
                return;
            }
            string prefabPath = entry != null ? (entry.placeablePrefabPath ?? "") : "";
            if (debugLog)
            {
                Debug.Log($"[InventoryUI] 点击背包物品: id={item.id}, name={item.productName}, inventoryIndex={index}, productFound={(entry != null)}, prefabPath='{prefabPath}'");
            }
            if (string.IsNullOrEmpty(prefabPath))
            {
                if (debugLog) Debug.LogWarning($"[InventoryUI] 该商品未配置可放置预制体 (placeablePrefabPath)，无法放置。id={item.id}, name={item.productName}");
                return;
            }
            if (!DormPlacementZoneService.CanEnterPlacementMode(out string blockReason))
            {
                if (debugLog) Debug.LogWarning("[InventoryUI] " + blockReason);
                return;
            }
            Close();
            if (PlacementModeController.Instance != null)
                PlacementModeController.Instance.StartPlacement(item, index, prefabPath);
            else
                Debug.LogWarning("[InventoryUI] 场景中未找到 PlacementModeController，无法进入放置模式");
        }

        void ConsumeFood(ProductEntry entry, int index)
        {
            var pm = FindObjectOfType<PlayerMovement>();
            if (pm == null)
            {
                Debug.LogWarning("[InventoryUI] 未找到 PlayerMovement，无法食用。");
                return;
            }
            pm.ConsumeFood(entry);
            InventoryData.RemoveItemAt(index);
            RefreshDisplay();
            if (debugLog) Debug.Log($"[InventoryUI] 已食用: {entry.productName}");
        }

        public void RequestRefreshAfterHostSync()
        {
            if (inventoryPanel != null && inventoryPanel.activeSelf)
                RefreshDisplay();
        }

        GameObject CreateItemUI(PurchasedItemData item, int index)
        {
            var go = new GameObject("InventoryItem");
            var rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(960, 800);
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = 960;
            le.preferredHeight = 800;

            go.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.95f);

            var btn = go.AddComponent<Button>();
            PurchasedItemData capturedItem = item;
            int capturedIndex = index;
            btn.onClick.AddListener(() => OnInventoryItemClick(capturedItem, capturedIndex));

            var icon = new GameObject("Icon").AddComponent<Image>();
            icon.transform.SetParent(go.transform, false);
            icon.rectTransform.anchorMin = new Vector2(0, 0.12f);
            icon.rectTransform.anchorMax = new Vector2(1, 1);
            icon.rectTransform.offsetMin = new Vector2(8, 8);
            icon.rectTransform.offsetMax = new Vector2(-8, -48);
            icon.color = Color.white;
            icon.preserveAspect = true;
            if (_database != null)
            {
                var entry = _database.GetById(item.id);
                if (entry != null) DormMallIconLoader.SetIcon(icon, _database, entry, null);
                else if (!string.IsNullOrEmpty(item.iconPath)) icon.sprite = Resources.Load<Sprite>(ProductDatabase.NormalizeIconPath(item.iconPath));
            }
            else if (!string.IsNullOrEmpty(item.iconPath)) icon.sprite = Resources.Load<Sprite>(ProductDatabase.NormalizeIconPath(item.iconPath));
            icon.raycastTarget = false;

            bool isFood = false;
            if (_database != null)
            {
                var db = _database.GetById(item.id);
                isFood = db != null && db.isConsumableFood;
            }
            if (!isFood)
                isFood = ProductDatabase.IsFoodCategory(item.category);

            var badgeGo = new GameObject("TypeBadge");
            badgeGo.transform.SetParent(go.transform, false);
            var badgeImg = badgeGo.AddComponent<Image>();
            badgeImg.color = isFood ? new Color(0.82f, 0.47f, 0.16f, 0.95f) : new Color(0.45f, 0.34f, 0.68f, 0.95f);
            badgeImg.raycastTarget = false;
            var badgeRt = badgeGo.GetComponent<RectTransform>();
            badgeRt.anchorMin = new Vector2(0f, 1f);
            badgeRt.anchorMax = new Vector2(0f, 1f);
            badgeRt.pivot = new Vector2(0f, 1f);
            badgeRt.anchoredPosition = new Vector2(8f, -8f);
            badgeRt.sizeDelta = new Vector2(230f, 44f);
            var badgeTextGo = new GameObject("Text");
            badgeTextGo.transform.SetParent(badgeGo.transform, false);
            var badgeText = badgeTextGo.AddComponent<TextMeshProUGUI>();
            badgeText.text = isFood ? Loc.Choose("食物", "Food") : Loc.Choose("寝室商城物品", "Dorm Mall Item");
            badgeText.fontSize = 20;
            badgeText.alignment = TextAlignmentOptions.Center;
            badgeText.color = Color.white;
            badgeText.raycastTarget = false;
            var btr = badgeText.rectTransform;
            btr.anchorMin = Vector2.zero;
            btr.anchorMax = Vector2.one;
            btr.offsetMin = Vector2.zero;
            btr.offsetMax = Vector2.zero;
            BindTmpFont(badgeText);

            var nameObj = new GameObject("Name");
            nameObj.transform.SetParent(go.transform, false);
            var nameTmp = nameObj.AddComponent<TextMeshProUGUI>();
            nameTmp.rectTransform.anchorMin = new Vector2(0, 0);
            nameTmp.rectTransform.anchorMax = new Vector2(1, 0.12f);
            nameTmp.rectTransform.offsetMin = new Vector2(8, 4);
            nameTmp.rectTransform.offsetMax = new Vector2(-8, -4);
            nameTmp.text = item.productName ?? "";
            nameTmp.fontSize = 18;
            nameTmp.alignment = TextAlignmentOptions.Center;
            nameTmp.color = new Color(0.2f, 0.2f, 0.2f);
            nameTmp.raycastTarget = false;
            nameTmp.enableWordWrapping = true;
            nameTmp.overflowMode = TextOverflowModes.Ellipsis;
            BindTmpFont(nameTmp);

            return go;
        }
    }
}
