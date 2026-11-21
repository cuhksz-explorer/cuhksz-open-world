#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

namespace DormMall.Editor
{
    public static class DormMallEditor
    {
        [MenuItem("GameObject/DormMall/创建放置模式控制器", false, 12)]
        static void CreatePlacementModeController()
        {
            var go = new GameObject("PlacementModeController");
            go.AddComponent<DormMall.PlacementModeController>();
            Selection.activeGameObject = go;
            Undo.RegisterCreatedObjectUndo(go, "Create Placement Mode Controller");
        }

        [MenuItem("GameObject/DormMall/创建已放置家具编辑控制器", false, 13)]
        static void CreatePlacedFurnitureEditController()
        {
            var go = new GameObject("PlacedFurnitureEditController");
            go.AddComponent<DormMall.PlacedFurnitureEditController>();
            Selection.activeGameObject = go;
            Undo.RegisterCreatedObjectUndo(go, "Create Placed Furniture Edit Controller");
        }

        [MenuItem("GameObject/DormMall/创建背包 UI", false, 11)]
        static void CreateInventoryUI()
        {
            var root = new GameObject("InventoryCanvas");
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 100;
            root.AddComponent<GraphicRaycaster>();
            if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<UnityEngine.EventSystems.EventSystem>();
                es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            var panel = CreateInventoryOverlay(root.transform);
            var ui = root.AddComponent<DormMall.InventoryUI>();
            ui.inventoryPanel = panel.gameObject;
            ui.inventoryContent = panel.Find("Body/Scroll/Viewport/Content");

            root.SetActive(true);
            panel.gameObject.SetActive(false);
            Selection.activeGameObject = root;
            Undo.RegisterCreatedObjectUndo(root, "Create Inventory UI");
        }

        static RectTransform CreateInventoryOverlay(Transform root)
        {
            var go = new GameObject("Panel");
            go.transform.SetParent(root, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.color = new Color(0, 0, 0, 0.5f);
            img.raycastTarget = true;
            var overlayBtn = go.AddComponent<Button>();
            overlayBtn.transition = Selectable.Transition.None;

            var body = new GameObject("Body");
            body.transform.SetParent(go.transform, false);
            var bodyRect = body.AddComponent<RectTransform>();
            bodyRect.anchorMin = bodyRect.anchorMax = new Vector2(0.5f, 0.5f);
            bodyRect.anchoredPosition = Vector2.zero;
            bodyRect.sizeDelta = new Vector2(1920, 1080);
            body.AddComponent<Image>().color = Color.white;

            var title = NewTMPText(body.transform, "Title", "背包", 32, new Color(0.54f, 0.17f, 0.89f));
            title.rectTransform.anchorMin = new Vector2(0, 1);
            title.rectTransform.anchorMax = new Vector2(1, 1);
            title.rectTransform.pivot = new Vector2(0.5f, 1);
            title.rectTransform.anchoredPosition = Vector2.zero;
            title.rectTransform.offsetMin = new Vector2(20, -50);
            title.rectTransform.offsetMax = new Vector2(-20, -10);
            title.alignment = TextAlignmentOptions.Center;

            var closeBtn = NewTMPButton(body.transform, "CloseBtn", "关闭", new Color(0.8f, 0.8f, 0.8f));
            closeBtn.GetComponent<RectTransform>().anchorMin = new Vector2(1, 1);
            closeBtn.GetComponent<RectTransform>().anchorMax = new Vector2(1, 1);
            closeBtn.GetComponent<RectTransform>().pivot = new Vector2(1, 1);
            closeBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(-10, -10);
            closeBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(50, 50);

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
            cRect.anchoredPosition = Vector2.zero;
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

            return rect;
        }

        [MenuItem("GameObject/DormMall/创建寝室商城 UI", false, 10)]
        static void CreateDormMallUI()
        {
            var root = new GameObject("DormMallCanvas");
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 100;
            root.AddComponent<GraphicRaycaster>();
            if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<UnityEngine.EventSystems.EventSystem>();
                es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            var panel = CreatePanel(root.transform);
            var header = CreateHeader(panel);
            var categories = CreateCategoryBar(panel);
            var scrollView = CreateScrollView(panel);
            var modal = CreateModal(root.transform);
            var toast = CreateToast(root.transform);
            modal.gameObject.transform.SetAsLastSibling();
            toast.gameObject.transform.SetAsLastSibling();

            var ui = root.AddComponent<DormMall.DormMallUI>();
            ui.coinsText = header.Find("Coins/Value")?.GetComponent<TMP_Text>();
            ui.categoryContainer = categories.Find("Content");
            ui.productGridContent = scrollView.Find("Viewport/Content");
            ui.modalPanel = modal.gameObject;
            ui.modalIcon = modal.Find("Body/Icon")?.GetComponent<Image>();
            ui.modalName = modal.Find("Body/Info/Name")?.GetComponent<TMP_Text>();
            ui.modalCategory = modal.Find("Body/Info/Category")?.GetComponent<TMP_Text>();
            ui.modalPrice = modal.Find("Body/Info/PriceVal")?.GetComponent<TMP_Text>();
            ui.modalCoinsHint = modal.Find("Body/Info/Coins")?.GetComponent<TMP_Text>();
            ui.modalConfirm = modal.Find("Body/Buttons/Confirm")?.GetComponent<Button>();
            ui.modalCancel = modal.Find("Body/Buttons/Cancel")?.GetComponent<Button>();
            ui.toastPanel = toast.gameObject;
            ui.toastText = toast.Find("Text")?.GetComponent<TMP_Text>();

            Selection.activeGameObject = root;
            Undo.RegisterCreatedObjectUndo(root, "Create DormMall UI");
        }

        [MenuItem("GameObject/DormMall/创建食物商城 UI", false, 9)]
        static void CreateFoodMallUI()
        {
            var root = new GameObject("FoodMallCanvas");
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 100;
            root.AddComponent<GraphicRaycaster>();
            if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<UnityEngine.EventSystems.EventSystem>();
                es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            var panel = CreatePanel(root.transform);
            var header = CreateHeader(panel);
            var categories = CreateCategoryBar(panel);
            var scrollView = CreateScrollView(panel);
            var modal = CreateModal(root.transform);
            var toast = CreateToast(root.transform);
            modal.gameObject.transform.SetAsLastSibling();
            toast.gameObject.transform.SetAsLastSibling();

            var ui = root.AddComponent<DormMall.DormMallUI>();
            ui.isFoodShopPanel = true;
            ui.foodCategoryKeyword = "食物";
            ui.coinsText = header.Find("Coins/Value")?.GetComponent<TMP_Text>();
            ui.categoryContainer = categories.Find("Content");
            ui.productGridContent = scrollView.Find("Viewport/Content");
            ui.modalPanel = modal.gameObject;
            ui.modalIcon = modal.Find("Body/Icon")?.GetComponent<Image>();
            ui.modalName = modal.Find("Body/Info/Name")?.GetComponent<TMP_Text>();
            ui.modalCategory = modal.Find("Body/Info/Category")?.GetComponent<TMP_Text>();
            ui.modalPrice = modal.Find("Body/Info/PriceVal")?.GetComponent<TMP_Text>();
            ui.modalCoinsHint = modal.Find("Body/Info/Coins")?.GetComponent<TMP_Text>();
            ui.modalConfirm = modal.Find("Body/Buttons/Confirm")?.GetComponent<Button>();
            ui.modalCancel = modal.Find("Body/Buttons/Cancel")?.GetComponent<Button>();
            ui.toastPanel = toast.gameObject;
            ui.toastText = toast.Find("Text")?.GetComponent<TMP_Text>();

            var title = header.Find("Title")?.GetComponent<TMP_Text>();
            if (title != null)
                title.text = "食物商城";

            // 默认放右侧区域，方便与寝室商城并存；可在场景里自行拖动布局。
            var panelRt = panel.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.48f, 0f);
            panelRt.anchorMax = new Vector2(1f, 1f);
            panelRt.offsetMin = Vector2.zero;
            panelRt.offsetMax = Vector2.zero;

            Selection.activeGameObject = root;
            Undo.RegisterCreatedObjectUndo(root, "Create FoodMall UI");
        }

        static RectTransform CreatePanel(Transform parent)
        {
            var go = new GameObject("Panel");
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f);
            img.raycastTarget = true;
            return rect;
        }

        static RectTransform CreateHeader(Transform panel)
        {
            var go = new GameObject("Header");
            go.transform.SetParent(panel, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(0.5f, 1);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0, 100);

            var bg = go.AddComponent<Image>();
            bg.color = new Color(1f, 1f, 1f, 0.95f);

            var title = NewTMPText(go.transform, "Title", "寝室商城", 28, new Color(0.54f, 0.17f, 0.89f));
            title.rectTransform.anchorMin = new Vector2(0, 0.5f);
            title.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            title.rectTransform.pivot = new Vector2(0, 0.5f);
            title.rectTransform.anchoredPosition = new Vector2(30, 0);
            title.rectTransform.sizeDelta = new Vector2(400, 50);

            var coinsGo = new GameObject("Coins");
            coinsGo.transform.SetParent(go.transform, false);
            var coinsRect = coinsGo.AddComponent<RectTransform>();
            coinsRect.anchorMin = new Vector2(0.85f, 0.5f);
            coinsRect.anchorMax = new Vector2(1, 0.5f);
            coinsRect.pivot = new Vector2(1, 0.5f);
            coinsRect.anchoredPosition = new Vector2(-30, 0);
            coinsRect.sizeDelta = new Vector2(200, 55);
            var coinsBg = coinsGo.AddComponent<Image>();
            coinsBg.color = new Color(0.54f, 0.17f, 0.89f, 0.2f);
            var val = NewTMPText(coinsGo.transform, "Value", "1000", 28, new Color(0.54f, 0.17f, 0.89f));
            val.rectTransform.anchorMin = val.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            val.rectTransform.offsetMin = val.rectTransform.offsetMax = Vector2.zero;
            val.enableWordWrapping = false;
            val.overflowMode = TMPro.TextOverflowModes.Overflow;

            return rect;
        }

        static RectTransform CreateCategoryBar(Transform panel)
        {
            var go = new GameObject("Categories");
            go.transform.SetParent(panel, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(0.5f, 1);
            rect.anchoredPosition = new Vector2(0, -100);
            rect.sizeDelta = new Vector2(0, 110);

            var content = new GameObject("Content");
            content.transform.SetParent(go.transform, false);
            var contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 0.5f);
            contentRect.anchorMax = new Vector2(1, 0.5f);
            contentRect.offsetMin = new Vector2(30, 0);
            contentRect.offsetMax = new Vector2(-30, 0);
            contentRect.sizeDelta = new Vector2(0, 88);
            var hlg = content.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 24;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;

            return rect;
        }

        static RectTransform CreateScrollView(Transform panel)
        {
            var go = new GameObject("ProductScrollView");
            go.transform.SetParent(panel, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(1, 1);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = new Vector2(0, -230);

            var scroll = go.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 50;

            var viewport = new GameObject("Viewport");
            viewport.transform.SetParent(go.transform, false);
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
            cRect.anchoredPosition = Vector2.zero;
            cRect.sizeDelta = new Vector2(0, 850);
            var glg = content.AddComponent<GridLayoutGroup>();
            glg.cellSize = new Vector2(640, 850);
            glg.spacing = Vector2.zero;
            glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            glg.constraintCount = 3;
            glg.padding = new RectOffset(0, 0, 0, 0);
            glg.childAlignment = TextAnchor.UpperCenter;
            var csf = content.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.content = cRect;
            scroll.viewport = vpRect;

            return rect;
        }

        static RectTransform CreateModal(Transform root)
        {
            var go = new GameObject("Modal");
            go.transform.SetParent(root, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.color = new Color(0, 0, 0, 0.5f);
            img.raycastTarget = true;
            var overlayBtn = go.AddComponent<Button>();
            overlayBtn.transition = Selectable.Transition.None;

            var body = new GameObject("Body");
            body.transform.SetParent(go.transform, false);
            var bodyRect = body.AddComponent<RectTransform>();
            bodyRect.anchorMin = bodyRect.anchorMax = new Vector2(0.5f, 0.5f);
            bodyRect.anchoredPosition = Vector2.zero;
            bodyRect.sizeDelta = new Vector2(580, 480);
            body.AddComponent<Image>().color = Color.white;

            var icon = new GameObject("Icon").AddComponent<Image>();
            icon.transform.SetParent(body.transform, false);
            icon.rectTransform.anchorMin = new Vector2(0, 0.5f);
            icon.rectTransform.anchorMax = new Vector2(0, 0.5f);
            icon.rectTransform.pivot = new Vector2(0, 0.5f);
            icon.rectTransform.anchoredPosition = new Vector2(25, 0);
            icon.rectTransform.sizeDelta = new Vector2(220, 220);
            icon.color = new Color(0.9f, 0.9f, 0.9f);

            var info = new GameObject("Info");
            info.transform.SetParent(body.transform, false);
            var infoRect = info.AddComponent<RectTransform>();
            infoRect.anchorMin = new Vector2(0.5f, 0);
            infoRect.anchorMax = new Vector2(1, 1);
            infoRect.offsetMin = new Vector2(20, 20);
            infoRect.offsetMax = new Vector2(-20, -80);

            var nameT = NewTMPText(info.transform, "Name", "商品名称", 28, Color.black);
            nameT.rectTransform.anchorMin = new Vector2(0, 0.8f);
            nameT.rectTransform.anchorMax = new Vector2(1, 1);
            nameT.rectTransform.offsetMin = Vector2.zero;
            nameT.rectTransform.offsetMax = Vector2.zero;
            var catT = NewTMPText(info.transform, "Category", "分类", 22, Color.gray);
            catT.rectTransform.anchorMin = new Vector2(0, 0.6f);
            catT.rectTransform.anchorMax = new Vector2(1, 0.8f);
            catT.rectTransform.offsetMin = catT.rectTransform.offsetMax = Vector2.zero;
            var priceT = NewTMPText(info.transform, "PriceVal", "0", 32, new Color(1f, 0.84f, 0));
            priceT.rectTransform.anchorMin = new Vector2(0, 0.4f);
            priceT.rectTransform.anchorMax = new Vector2(1, 0.6f);
            priceT.rectTransform.offsetMin = priceT.rectTransform.offsetMax = Vector2.zero;
            var coinsT = NewTMPText(info.transform, "Coins", "当前金币：0", 22, Color.gray);
            coinsT.rectTransform.anchorMin = new Vector2(0, 0.2f);
            coinsT.rectTransform.anchorMax = new Vector2(1, 0.4f);
            coinsT.rectTransform.offsetMin = coinsT.rectTransform.offsetMax = Vector2.zero;

            var buttons = new GameObject("Buttons");
            buttons.transform.SetParent(body.transform, false);
            var btRect = buttons.AddComponent<RectTransform>();
            btRect.anchorMin = new Vector2(0, 0);
            btRect.anchorMax = new Vector2(1, 0);
            btRect.pivot = new Vector2(0.5f, 0);
            btRect.anchoredPosition = Vector2.zero;
            btRect.sizeDelta = new Vector2(0, 65);
            var bthg = buttons.AddComponent<HorizontalLayoutGroup>();
            bthg.spacing = 20;
            bthg.childAlignment = TextAnchor.MiddleCenter;

            var cancel = NewTMPButton(buttons.transform, "Cancel", "取消", new Color(0.8f, 0.8f, 0.8f));
            var confirm = NewTMPButton(buttons.transform, "Confirm", "确认购买", new Color(0.54f, 0.17f, 0.89f));

            go.SetActive(false);
            return rect;
        }

        static RectTransform CreateToast(Transform root)
        {
            var go = new GameObject("Toast");
            go.transform.SetParent(root, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(1, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(1, 0.5f);
            rect.anchoredPosition = new Vector2(-50, -100);
            rect.sizeDelta = new Vector2(250, 50);
            go.AddComponent<Image>().color = new Color(0.54f, 0.17f, 0.89f);
            var txt = NewTMPText(go.transform, "Text", "购买成功", 18, Color.white);
            txt.rectTransform.anchorMin = txt.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            txt.rectTransform.offsetMin = txt.rectTransform.offsetMax = Vector2.zero;
            go.SetActive(false);
            return rect;
        }

        static TMP_FontAsset GetFontForChinese()
        {
            var font = Resources.Load<TMP_FontAsset>("Fonts & Materials/AlibabaPuHuiTi-3-75-SemiBold SDF");
            if (font == null) font = Resources.Load<TMP_FontAsset>("AlibabaPuHuiTi");
            if (font == null) font = Resources.Load<TMP_FontAsset>("Fonts/AlibabaPuHuiTi");
            if (font == null) font = Resources.Load<TMP_FontAsset>("Alibaba Pu Hui Ti-3-75-Semi Bold SDF");
            if (font == null) font = Resources.Load<TMP_FontAsset>("Fonts & Materials/Alibaba Pu Hui Ti-3-75-Semi Bold SDF");
            if (font == null) font = Resources.Load<TMP_FontAsset>("NotoSansCJK-Regular SDF");
            if (font == null) font = Resources.Load<TMP_FontAsset>("SourceHanSansSC-Regular SDF");
            if (font == null) font = Resources.Load<TMP_FontAsset>("LiberationSans SDF");
            if (font == null) font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (font == null && TMP_Settings.instance != null) font = TMP_Settings.defaultFontAsset;
            return font;
        }

        static TMP_Text NewTMPText(Transform parent, string name, string content, int size, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var txt = go.AddComponent<TextMeshProUGUI>();
            txt.text = content;
            txt.fontSize = size;
            txt.color = color;
            var font = GetFontForChinese();
            if (font != null) txt.font = font;
            txt.alignment = TextAlignmentOptions.TopLeft;
            txt.raycastTarget = false;
            txt.enableWordWrapping = true;
            txt.overflowMode = TMPro.TextOverflowModes.Overflow;
            return txt;
        }

        static Button NewTMPButton(Transform parent, string name, string label, Color bgColor)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(180, 55);
            go.AddComponent<Image>().color = bgColor;
            var bt = go.AddComponent<Button>();
            var txt = NewTMPText(go.transform, "Text", label, 24, Color.white);
            txt.rectTransform.anchorMin = txt.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            txt.rectTransform.offsetMin = txt.rectTransform.offsetMax = Vector2.zero;
            txt.alignment = TextAlignmentOptions.Center;
            return bt;
        }

        [MenuItem("GameObject/DormMall/创建放置模式说明面板", false, 13)]
        static void CreatePlacementHelpPanelMenu()
        {
            if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<UnityEngine.EventSystems.EventSystem>();
                es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                Undo.RegisterCreatedObjectUndo(es, "Create EventSystem");
            }

            var root = new GameObject("PlacementHelpCanvas");
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;

            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 100;
            root.AddComponent<GraphicRaycaster>();

            var bar = new GameObject("PlacementHelpBar");
            bar.transform.SetParent(root.transform, false);
            var barRect = bar.AddComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0f, 1f);
            barRect.anchorMax = new Vector2(1f, 1f);
            barRect.pivot = new Vector2(0.5f, 1f);
            barRect.anchoredPosition = Vector2.zero;
            barRect.sizeDelta = new Vector2(0f, 152f);
            var bg = bar.AddComponent<Image>();
            bg.color = new Color(0.06f, 0.07f, 0.1f, 0.93f);
            bg.raycastTarget = false;

            var body = NewTMPText(bar.transform, "Body", "进入放置模式后，此处在屏幕顶端居中显示操作说明。", 17, new Color(0.88f, 0.9f, 0.97f));
            body.richText = true;
            body.lineSpacing = 2f;
            body.alignment = TextAlignmentOptions.Center;
            var bodyRt = body.rectTransform;
            bodyRt.anchorMin = Vector2.zero;
            bodyRt.anchorMax = Vector2.one;
            bodyRt.pivot = new Vector2(0.5f, 0.5f);
            bodyRt.offsetMin = new Vector2(32f, 10f);
            bodyRt.offsetMax = new Vector2(-32f, -10f);

            var help = root.AddComponent<DormMall.PlacementHelpPanel>();
            var so = new SerializedObject(help);
            so.FindProperty("panelRoot").objectReferenceValue = bar;
            so.FindProperty("bodyText").objectReferenceValue = body;
            so.ApplyModifiedProperties();

            bar.SetActive(false);

            var plc = Object.FindObjectOfType<DormMall.PlacementModeController>(true);
            if (plc != null)
            {
                Undo.RecordObject(plc, "Bind Placement Help Panel");
                var pso = new SerializedObject(plc);
                pso.FindProperty("placementHelpPanel").objectReferenceValue = help;
                pso.ApplyModifiedProperties();
                EditorUtility.SetDirty(plc);
            }

            Undo.RegisterCreatedObjectUndo(root, "Create Placement Help Panel");
            EditorUtility.SetDirty(help);
            Selection.activeGameObject = root;
        }
    }
}
#endif
