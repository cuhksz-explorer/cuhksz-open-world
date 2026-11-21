using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class VehicleShopUI : MonoBehaviour
{
    static readonly Color Gold = new Color(0.79f, 0.66f, 0.28f, 1f);
    static readonly Color TextBlack = new Color(0.12f, 0.12f, 0.12f, 1f);
    static readonly Color PanelWhite = Color.white;
    static readonly Color OverlayBlack = new Color(0f, 0f, 0f, 0.82f);
    static readonly Color BtnDark = new Color(0.12f, 0.12f, 0.12f, 1f);
    static Color PreviewBg => VehicleShopPreview3D.PreviewBackground;
    static readonly Color StatsBg = new Color(0.97f, 0.97f, 0.97f, 1f);

    const float StatsPanelHeight = 260f;
    const float PreviewTopInset = 52f;
    const int ShopCanvasSortOrder = 400;

    [Header("车辆列表（在 Inspector 中配置）")]
    public List<VehicleData> vehicles;

    [Header("UI 组件")]
    public GameObject vehicleCardPrefab;
    public Transform vehicleListContent;
    public Image detailImage;
    public TMP_Text detailNameText;
    public TMP_Text detailDescText;
    public TMP_Text detailSpeedText;
    public Button selectButton;
    public TMP_Text spawnMessageText;
    public Transform spawnPoint;
    public Button closeButton;
    public VehicleShopPreview3D preview3D;
    public RawImage previewRawImage;

    InteractableNPC currentNPC;
    VehicleData currentSelectedVehicle;
    Canvas hostCanvas;
    bool wasCanvasVisible;
    int canvasSortOrderBefore;
    readonly List<VehicleCard> activeCards = new List<VehicleCard>();

    RectTransform _leftPanel;
    RectTransform _rightPanel;
    RectTransform _previewViewport;
    RectTransform _statsPanel;

    void Awake()
    {
        if (currentNPC == null)
            currentNPC = GetComponentInParent<InteractableNPC>();
        hostCanvas = GetComponentInParent<Canvas>();

        EnsurePreviewComponent();
        CachePanelRefs();
        ApplyLayout();

        if (hostCanvas != null)
            hostCanvas.enabled = false;

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(CloseShop);
            closeButton.onClick.AddListener(CloseShop);
        }

        if (selectButton != null)
        {
            selectButton.onClick.RemoveListener(OnSelectButtonClicked);
            selectButton.onClick.AddListener(OnSelectButtonClicked);
        }
    }

    void Update()
    {
        bool visible = hostCanvas == null
            ? gameObject.activeInHierarchy
            : gameObject.activeInHierarchy && hostCanvas.enabled;

        if (visible && Input.GetKeyDown(KeyCode.Escape))
            CloseShop();

        if (visible && !wasCanvasVisible)
            HandleOpened();
        else if (!visible && wasCanvasVisible)
            HandleClosed();

        wasCanvasVisible = visible;
    }

    void EnsurePreviewComponent()
    {
        if (preview3D == null)
            preview3D = GetComponent<VehicleShopPreview3D>();
        if (preview3D == null)
            preview3D = gameObject.AddComponent<VehicleShopPreview3D>();

        if (previewRawImage != null)
            preview3D.BindPreviewImage(previewRawImage);
    }

    void CachePanelRefs()
    {
        Transform mainPanel = transform.Find("BackgroundPanel/MainPanel");
        if (mainPanel == null)
            mainPanel = transform.Find("MainPanel");
        if (mainPanel == null)
            return;

        _leftPanel = mainPanel.Find("LeftPanel") as RectTransform;
        _rightPanel = mainPanel.Find("RightPanel") as RectTransform;
    }

    public void SetNPC(InteractableNPC npc)
    {
        currentNPC = npc;
    }

    public bool IsShopOpen =>
        hostCanvas != null
            ? hostCanvas.enabled
            : gameObject.activeInHierarchy;

    public void Show()
    {
        ApplyLayout();
        UIInteractionState.SetNpcShopOpen(true);
        VehicleShopUiSession.Enter();
        EnsureHostCanvasVisible();

        if (hostCanvas != null)
        {
            canvasSortOrderBefore = hostCanvas.sortingOrder;
            hostCanvas.sortingOrder = ShopCanvasSortOrder;
            hostCanvas.enabled = true;
        }
        else if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }
    }

    void EnsureHostCanvasVisible()
    {
        var root = hostCanvas != null ? hostCanvas.gameObject : gameObject;
        if (!root.activeSelf)
            root.SetActive(true);

        var rt = root.GetComponent<RectTransform>();
        if (rt != null && rt.localScale.sqrMagnitude < 0.001f)
            rt.localScale = Vector3.one;
    }

    public void CloseShop()
    {
        if (hostCanvas != null)
        {
            hostCanvas.enabled = false;
            hostCanvas.sortingOrder = canvasSortOrderBefore;
        }
        else if (gameObject.activeSelf)
        {
            gameObject.SetActive(false);
        }

        UIInteractionState.SetNpcShopOpen(false);
        VehicleShopUiSession.Exit();
    }

    void HandleOpened()
    {
        if (currentNPC == null)
            currentNPC = GetComponentInParent<InteractableNPC>();

        ApplyLayout();
        RefreshVehicleList();

        if (vehicles != null && vehicles.Count > 0 && vehicles[0] != null)
            OnVehicleSelected(vehicles[0]);
        else
        {
            currentSelectedVehicle = null;
            ClearDetailPanel();
        }

        if (spawnMessageText != null)
            spawnMessageText.text = "";
    }

    void HandleClosed()
    {
        if (VehicleShopUiSession.IsActive)
        {
            UIInteractionState.SetNpcShopOpen(false);
            VehicleShopUiSession.Exit();
        }

        if (preview3D != null)
            preview3D.ReleaseWhenShopClosed();
    }

    void RefreshVehicleList()
    {
        activeCards.Clear();

        if (vehicleListContent == null || vehicleCardPrefab == null)
        {
            Debug.LogWarning("[VehicleShopUI] 未配置 vehicleListContent 或 vehicleCardPrefab。");
            return;
        }

        if (vehicles == null || vehicles.Count == 0)
        {
            Debug.LogWarning("[VehicleShopUI] 车辆列表为空，请检查 InteractableNPC.availableVehicles。");
            return;
        }

        foreach (Transform child in vehicleListContent)
            Destroy(child.gameObject);

        foreach (var vehicle in vehicles)
        {
            if (vehicle == null) continue;
            GameObject card = Instantiate(vehicleCardPrefab, vehicleListContent);
            card.transform.localScale = Vector3.one;

            var layoutElement = card.GetComponent<LayoutElement>();
            if (layoutElement == null)
                layoutElement = card.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 88f;
            layoutElement.minHeight = 88f;

            VehicleCard cardScript = card.GetComponent<VehicleCard>();
            if (cardScript != null)
            {
                cardScript.Setup(vehicle, this);
                activeCards.Add(cardScript);
                NormalizeCardTextLayout(card.transform);
            }
            else if (card.TryGetComponent<Button>(out Button btn))
            {
                VehicleData captured = vehicle;
                btn.onClick.AddListener(() => OnVehicleSelected(captured));

                Transform imgTransform = card.transform.Find("ThumbnailImage");
                if (imgTransform != null) imgTransform.GetComponent<Image>().sprite = vehicle.thumbnail;
                Transform nameTransform = card.transform.Find("NameText");
                if (nameTransform != null)
                {
                    TMP_Text nameText = nameTransform.GetComponent<TMP_Text>();
                    if (nameText != null)
                    {
                        nameText.text = vehicle.vehicleName;
                        NormalizeSingleLineText(nameText);
                    }
                }
            }
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(vehicleListContent as RectTransform);
    }

    static void NormalizeCardTextLayout(Transform cardRoot)
    {
        if (cardRoot == null) return;
        Transform nameTransform = cardRoot.Find("NameText");
        if (nameTransform == null) return;
        TMP_Text nameText = nameTransform.GetComponent<TMP_Text>();
        if (nameText == null) return;
        NormalizeSingleLineText(nameText);
    }

    static void NormalizeSingleLineText(TMP_Text text)
    {
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
    }

    public void OnVehicleSelected(VehicleData vehicle)
    {
        currentSelectedVehicle = vehicle;
        UpdateDetailPanel(vehicle);
        UpdateCardSelectionHighlight(vehicle);
    }

    void UpdateCardSelectionHighlight(VehicleData vehicle)
    {
        for (int i = 0; i < activeCards.Count; i++)
        {
            var card = activeCards[i];
            if (card == null) continue;
            card.SetSelected(card.VehicleData == vehicle);
        }
    }

    void UpdateDetailPanel(VehicleData vehicle)
    {
        if (detailImage != null)
            detailImage.gameObject.SetActive(false);

        if (detailNameText != null) detailNameText.text = vehicle.vehicleName;
        if (detailDescText != null) detailDescText.text = vehicle.description;
        if (detailSpeedText != null)
        {
            detailSpeedText.text =
                Loc.F("极速: {0} km/h\n", "Top speed: {0} km/h\n", vehicle.displayMaxSpeed) +
                Loc.Choose("加速: ", "Acceleration: ") + new string('★', vehicle.accelerationRating) + new string('☆', 5 - vehicle.accelerationRating) + "\n" +
                Loc.Choose("操控: ", "Handling: ") + new string('★', vehicle.handlingRating) + new string('☆', 5 - vehicle.handlingRating);
        }

        if (preview3D != null)
            preview3D.ShowVehicle(vehicle);
    }

    void ClearDetailPanel()
    {
        if (detailNameText != null) detailNameText.text = "";
        if (detailDescText != null) detailDescText.text = "";
        if (detailSpeedText != null) detailSpeedText.text = "";
        if (preview3D != null)
            preview3D.ClearPreview();
        UpdateCardSelectionHighlight(null);
    }

    void OnSelectButtonClicked()
    {
        if (currentSelectedVehicle == null)
        {
            if (spawnMessageText != null) spawnMessageText.text = Loc.Choose("请先选择一辆车！", "Please select a vehicle first!");
            return;
        }

        if (currentNPC != null)
            currentNPC.OnVehicleSelected(currentSelectedVehicle);
        else
            Debug.LogError("[VehicleShopUI] 未绑定 InteractableNPC，无法生成车辆。");

        if (spawnMessageText != null)
        {
            spawnMessageText.text = Loc.F("已生成 {0}！", "Spawned {0}!", currentSelectedVehicle.vehicleName);
            CancelInvoke(nameof(ClearMessage));
            Invoke(nameof(ClearMessage), 2f);
        }

        CloseShop();
    }

    void ClearMessage()
    {
        if (spawnMessageText != null) spawnMessageText.text = "";
    }

    void ApplyLayout()
    {
        var scaler = GetComponentInParent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        Transform bg = transform.Find("BackgroundPanel");
        if (bg != null)
        {
            var bgImg = bg.GetComponent<Image>();
            if (bgImg != null) bgImg.color = OverlayBlack;
        }

        Transform mainPanel = transform.Find("BackgroundPanel/MainPanel");
        if (mainPanel == null)
            mainPanel = transform.Find("MainPanel");
        if (mainPanel != null)
        {
            var mainImg = mainPanel.GetComponent<Image>();
            if (mainImg != null) mainImg.color = PanelWhite;
            StretchRect(mainPanel as RectTransform, 40f, 40f, 40f, 40f);
        }

        CachePanelRefs();
        LayoutLeftPanel();
        LayoutRightPanel();
        StyleTitleAndButtons();
        EnsurePreviewViewport();
        VehicleShopTypography.ApplyAllInChildren(transform);
    }

    void LayoutLeftPanel()
    {
        if (_leftPanel == null) return;

        var leftVlg = _leftPanel.GetComponent<VerticalLayoutGroup>();
        if (leftVlg != null)
            leftVlg.enabled = false;

        _leftPanel.anchorMin = new Vector2(0f, 0f);
        _leftPanel.anchorMax = new Vector2(0f, 1f);
        _leftPanel.pivot = new Vector2(0f, 0.5f);
        _leftPanel.anchoredPosition = new Vector2(20f, 0f);
        _leftPanel.sizeDelta = new Vector2(248f, -32f);

        var leftImg = _leftPanel.GetComponent<Image>();
        if (leftImg != null)
        {
            leftImg.color = new Color(0.96f, 0.96f, 0.96f, 1f);
            leftImg.raycastTarget = false;
        }

        Transform title = _leftPanel.Find("TitleText");
        if (title != null)
        {
            title.gameObject.SetActive(false);
        }

        Transform scroll = _leftPanel.Find("Scroll View");
        if (scroll != null)
        {
            var scrollRt = scroll as RectTransform;
            scrollRt.anchorMin = Vector2.zero;
            scrollRt.anchorMax = Vector2.one;
            scrollRt.offsetMin = new Vector2(4f, 4f);
            scrollRt.offsetMax = new Vector2(-4f, -4f);

            var scrollRect = scroll.GetComponent<ScrollRect>();
            if (scrollRect != null)
            {
                scrollRect.horizontal = false;
                scrollRect.vertical = true;
                scrollRect.movementType = ScrollRect.MovementType.Clamped;
            }
        }

        if (vehicleListContent != null)
        {
            var contentRt = vehicleListContent as RectTransform;
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.anchoredPosition = Vector2.zero;
            contentRt.sizeDelta = new Vector2(0f, 0f);

            var vlg = vehicleListContent.GetComponent<VerticalLayoutGroup>();
            if (vlg == null)
                vlg = vehicleListContent.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 10f;
            vlg.padding = new RectOffset(4, 4, 4, 4);
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var fitter = vehicleListContent.GetComponent<ContentSizeFitter>();
            if (fitter == null)
                fitter = vehicleListContent.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }
    }

    void LayoutRightPanel()
    {
        if (_rightPanel == null) return;

        _rightPanel.anchorMin = new Vector2(0f, 0f);
        _rightPanel.anchorMax = new Vector2(1f, 1f);
        _rightPanel.offsetMin = new Vector2(276f, 16f);
        _rightPanel.offsetMax = new Vector2(-16f, -16f);

        var rightImg = _rightPanel.GetComponent<Image>();
        if (rightImg != null)
            rightImg.color = new Color(1f, 1f, 1f, 0f);

        EnsureStatsPanel();

        if (_statsPanel != null)
        {
            _statsPanel.anchorMin = new Vector2(0f, 0f);
            _statsPanel.anchorMax = new Vector2(1f, 0f);
            _statsPanel.pivot = new Vector2(0.5f, 0f);
            _statsPanel.anchoredPosition = Vector2.zero;
            _statsPanel.sizeDelta = new Vector2(0f, StatsPanelHeight);

            var statsImg = _statsPanel.GetComponent<Image>();
            if (statsImg != null)
                statsImg.color = StatsBg;
        }

        if (detailNameText != null)
        {
            var rt = detailNameText.rectTransform;
            SetParentIfNeeded(rt, _statsPanel);
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(16f, -12f);
            rt.sizeDelta = new Vector2(-32f, 36f);
            detailNameText.fontSize = 28f;
            detailNameText.color = TextBlack;
            detailNameText.fontStyle = FontStyles.Bold;
            detailNameText.gameObject.SetActive(true);
        }

        if (detailDescText != null)
        {
            var rt = detailDescText.rectTransform;
            SetParentIfNeeded(rt, _statsPanel);
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(16f, -52f);
            rt.sizeDelta = new Vector2(-200f, 64f);
            detailDescText.fontSize = 18f;
            detailDescText.color = new Color(0.35f, 0.35f, 0.35f, 1f);
            detailDescText.gameObject.SetActive(true);
        }

        if (detailSpeedText != null)
        {
            var rt = detailSpeedText.rectTransform;
            SetParentIfNeeded(rt, _statsPanel);
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(16f, 16f);
            rt.sizeDelta = new Vector2(360f, 96f);
            detailSpeedText.fontSize = 20f;
            detailSpeedText.color = TextBlack;
            detailSpeedText.gameObject.SetActive(true);
        }

        if (selectButton != null)
        {
            var rt = selectButton.GetComponent<RectTransform>();
            SetParentIfNeeded(rt, _statsPanel);
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-16f, 16f);
            rt.sizeDelta = new Vector2(148f, 48f);
            selectButton.gameObject.SetActive(true);
        }

        if (detailImage != null)
            detailImage.gameObject.SetActive(false);

        LayoutPreviewViewport();
    }

    void EnsureStatsPanel()
    {
        if (_rightPanel == null) return;

        Transform existing = _rightPanel.Find("StatsPanel");
        if (existing != null)
        {
            _statsPanel = existing as RectTransform;
            return;
        }

        var statsGo = new GameObject("StatsPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        statsGo.transform.SetParent(_rightPanel, false);
        _statsPanel = statsGo.GetComponent<RectTransform>();
        var img = statsGo.GetComponent<Image>();
        img.color = StatsBg;
        img.raycastTarget = false;
    }

    void EnsurePreviewViewport()
    {
        if (_rightPanel == null) return;

        Transform existing = _rightPanel.Find("PreviewViewport");
        if (existing == null)
        {
            var viewportGo = new GameObject("PreviewViewport", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            viewportGo.transform.SetParent(_rightPanel, false);
            viewportGo.transform.SetAsFirstSibling();
            _previewViewport = viewportGo.GetComponent<RectTransform>();

            var viewportImg = viewportGo.GetComponent<Image>();
            viewportImg.color = PreviewBg;
            viewportImg.raycastTarget = true;

            var input = viewportGo.AddComponent<VehiclePreviewInput>();
            if (preview3D == null)
                EnsurePreviewComponent();
            input.Initialize(preview3D);

            var rawGo = new GameObject("PreviewRawImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            rawGo.transform.SetParent(viewportGo.transform, false);
            StretchFill(rawGo.GetComponent<RectTransform>());

        previewRawImage = rawGo.GetComponent<RawImage>();
        previewRawImage.raycastTarget = false;
        previewRawImage.color = Color.white;

        var fitter = rawGo.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = 960f / 540f;

            var hintGo = new GameObject("PreviewHint", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            hintGo.transform.SetParent(viewportGo.transform, false);
            var hintRt = hintGo.GetComponent<RectTransform>();
            hintRt.anchorMin = new Vector2(0.5f, 0f);
            hintRt.anchorMax = new Vector2(0.5f, 0f);
            hintRt.pivot = new Vector2(0.5f, 0f);
            hintRt.anchoredPosition = new Vector2(0f, 10f);
            hintRt.sizeDelta = new Vector2(420f, 22f);
            var hintText = hintGo.GetComponent<Text>();
            hintText.text = Loc.Choose("拖拽左右旋转 · 滚轮缩放", "Drag to rotate · scroll to zoom");
            hintText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            hintText.fontSize = 14;
            hintText.alignment = TextAnchor.MiddleCenter;
            hintText.color = Gold;
            hintText.raycastTarget = false;

            preview3D.BindPreviewImage(previewRawImage);
        }
        else
        {
            _previewViewport = existing as RectTransform;
            previewRawImage = existing.Find("PreviewRawImage")?.GetComponent<RawImage>();
            if (preview3D != null && previewRawImage != null)
                preview3D.BindPreviewImage(previewRawImage);
        }

        LayoutPreviewViewport();
    }

    void LayoutPreviewViewport()
    {
        if (_previewViewport == null) return;

        _previewViewport.anchorMin = new Vector2(0f, 0f);
        _previewViewport.anchorMax = new Vector2(1f, 1f);
        _previewViewport.offsetMin = new Vector2(0f, StatsPanelHeight + 8f);
        _previewViewport.offsetMax = new Vector2(0f, -PreviewTopInset);

        var img = _previewViewport.GetComponent<Image>();
        if (img != null)
            img.color = PreviewBg;
    }

    void StyleTitleAndButtons()
    {
        Transform mainPanel = transform.Find("BackgroundPanel/MainPanel");
        if (mainPanel == null)
            mainPanel = transform.Find("MainPanel");

        HideTitleText(mainPanel);
        HideTitleText(transform.Find("BackgroundPanel/MainPanel/LeftPanel"));

        if (closeButton != null)
        {
            if (mainPanel != null && closeButton.transform.parent != mainPanel)
                closeButton.transform.SetParent(mainPanel, false);

            StyleButton(closeButton, PanelWhite, TextBlack, Gold);
            var rt = closeButton.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-8f, -8f);
            rt.sizeDelta = new Vector2(112f, 40f);

            var label = closeButton.GetComponentInChildren<TMP_Text>();
            if (label != null)
            {
                label.text = Loc.Choose("关闭", "Close");
                label.color = TextBlack;
                VehicleShopTypography.Apply(label);
            }
            closeButton.gameObject.SetActive(true);
        }

        if (selectButton != null)
        {
            StyleButton(selectButton, BtnDark, Color.white, Gold);
            var label = selectButton.GetComponentInChildren<TMP_Text>();
            if (label != null)
            {
                label.text = Loc.Choose("选择", "Select");
                label.color = Color.white;
                VehicleShopTypography.Apply(label);
            }
        }

        if (spawnMessageText != null)
        {
            spawnMessageText.color = Gold;
            VehicleShopTypography.Apply(spawnMessageText);
        }
    }

    static void HideTitleText(Transform parent)
    {
        if (parent == null)
            return;

        Transform title = parent.Find("TitleText");
        if (title != null)
            title.gameObject.SetActive(false);
    }

    static void SetParentIfNeeded(RectTransform rt, RectTransform newParent)
    {
        if (rt == null || newParent == null || rt.parent == newParent)
            return;
        rt.SetParent(newParent, false);
    }

    static void StyleButton(Button button, Color bg, Color textColor, Color borderTint)
    {
        if (button == null) return;
        var img = button.GetComponent<Image>();
        if (img != null)
        {
            img.color = bg;
            var outline = button.GetComponent<Outline>();
            if (outline == null)
                outline = button.gameObject.AddComponent<Outline>();
            outline.effectColor = borderTint;
            outline.effectDistance = new Vector2(2f, -2f);
        }

        var colors = button.colors;
        colors.normalColor = bg;
        colors.highlightedColor = Color.Lerp(bg, borderTint, 0.25f);
        colors.pressedColor = Color.Lerp(bg, borderTint, 0.4f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;
    }

    static void StretchRect(RectTransform rt, float left, float right, float top, float bottom)
    {
        if (rt == null) return;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
    }

    static void StretchFill(RectTransform rt)
    {
        if (rt == null) return;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
