using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using GameQuestSystem;

namespace DormMall
{
    /// <summary>寝室商城主界面逻辑</summary>
    public class DormMallUI : MonoBehaviour
    {
        const string DefaultChineseTmpResourcesPath = "Fonts & Materials/AlibabaPuHuiTi-3-75-SemiBold SDF";

        [Header("数据")]
        public ProductDatabase database;
        [Header("双商城面板")]
        [Tooltip("true=当前实例为食物商城面板；false=寝室商城面板。可在同一 Canvas 放两个 DormMallUI 分别配置。")]
        public bool isFoodShopPanel;
        [Tooltip("识别食物分类的关键字，默认“食物”。")]
        public string foodCategoryKeyword = "食物";
        [Tooltip("打开该商城时自动从 GameServer 拉取商品目录（/api/dorm_mall/catalog）。")]
        [SerializeField] bool autoLoadCatalogFromGameServer = true;
        [Tooltip("食物商城优先依赖远端目录（本地 ProductDatabase 常常没有食物项）。")]
        [SerializeField] bool preferRemoteCatalogForFoodPanel = true;
        [Tooltip("食物商城面板强制全屏（修复旧版一键创建只占半屏的问题）。")]
        [SerializeField] bool forceFoodPanelFullScreen = true;

        [Header("UI 引用")]
        public TMP_Text coinsText;
        public Transform categoryContainer;
        public GameObject categoryButtonPrefab;
        public Transform productGridContent;
        public GameObject productItemPrefab;
        public GameObject modalPanel;
        public Image modalIcon;
        public TMP_Text modalName;
        public TMP_Text modalCategory;
        public TMP_Text modalPrice;
        public TMP_Text modalCoinsHint;

        [Tooltip("弹窗中灰色小字前缀：该行实际显示的是「当前持有金币」(modalCoinsHint)，大字黄色为单价（modalPrice）。不要写「价格」除非你愿意让文案与数字含义不一致。")]
        [SerializeField] string modalWalletCoinsPrefix = "当前金币：";

        public Button modalConfirm;
        public Button modalCancel;
        public GameObject toastPanel;
        public TMP_Text toastText;

        [Header("字体（可拖入 TMP 中文字体；留空则尝试从 Resources 加载）")]
        public TMP_FontAsset fontAsset;

        [Header("经济")]
        public int initialCoins = 1000;
        [Range(0.1f, 3f)]
        public float priceMultiplier = 1f;
        public Sprite placeholderSprite;

        private int _coins;
        private string _currentCategory = "";
        private ProductEntry _selectedProduct;
        private List<GameObject> _categoryButtons = new List<GameObject>();
        private List<GameObject> _productItems = new List<GameObject>();
        private float _lastQuestionHideTime;
        private readonly List<string> _filteredCategories = new List<string>();
        bool _remoteCatalogLoading;
        bool _remoteCatalogTried;

        /// <summary>当前商城币余额（与任务金币同步时使用 Quest 金币）</summary>
        public int Coins => _coins;

        /// <summary>单笔购买完成 (productId, productName, category, price, coinsAfter)</summary>
        public event Action<int, string, string, int, int> OnPurchaseComplete;
        public static event Action<int, string, string, int, int> OnAnyPurchaseCompleted;

        void Update()
        {
            if (!gameObject.activeInHierarchy) return;
            if (Time.time - _lastQuestionHideTime > 1f)
            {
                _lastQuestionHideTime = Time.time;
                HideQuestionInputsInModal();
            }
        }

        void Awake()
        {
            if (EventSystem.current == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }
        }

        void OnEnable()
        {
            TryForceFoodPanelFullScreen();
            TryLoadCatalogFromServerIfNeeded();
        }

        void Start()
        {
            if (database == null) database = Resources.Load<ProductDatabase>("DormMall/ProductDatabase");
            TryForceFoodPanelFullScreen();
            EnsureFoodFallbackProductsIfNeeded();
            EnsureRefs();
            if (QuestSystemManager.Instance != null)
            {
                _coins = QuestSystemManager.Instance.CurrentGold;
                QuestSystemManager.Instance.OnGoldChanged -= OnQuestGoldChanged;
                QuestSystemManager.Instance.OnGoldChanged += OnQuestGoldChanged;
            }
            else
            {
                _coins = initialCoins;
            }
            UpdateCoinsDisplay();
            ApplyTmpChineseFont();
            BuildCategories();
            if (!string.IsNullOrEmpty(_currentCategory))
                RefreshProducts();
            if (modalPanel) modalPanel.SetActive(false);
            if (toastPanel) toastPanel.SetActive(false);
            _lastQuestionHideTime = Time.time;
            HideQuestionInputsInModal();
            var overlayBtn = modalPanel?.GetComponent<Button>();
            if (overlayBtn) { overlayBtn.onClick.RemoveAllListeners(); overlayBtn.onClick.AddListener(CloseModal); }
            if (modalConfirm) { modalConfirm.onClick.RemoveAllListeners(); modalConfirm.onClick.AddListener(OnConfirmPurchase); }
            if (modalCancel) { modalCancel.onClick.RemoveAllListeners(); modalCancel.onClick.AddListener(CloseModal); }

            TryLoadCatalogFromServerIfNeeded();
        }

        static Transform FindPanelTransform(Transform root)
        {
            if (root == null) return null;
            var direct = root.Find("Panel");
            if (direct != null) return direct;
            foreach (Transform c in root)
            {
                if (c.name == "Panel") return c;
                var deep = FindPanelTransform(c);
                if (deep != null) return deep;
            }
            return null;
        }

        void EnsureRefs()
        {
            var panel = FindPanelTransform(transform);
            if (panel != null)
            {
                if (categoryContainer == null) categoryContainer = panel.Find("Categories/Content");
                if (categoryContainer == null)
                {
                    var catBar = panel.Find("Categories");
                    if (catBar != null)
                        categoryContainer = catBar.Find("Content") ?? catBar;
                }
                if (productGridContent == null) productGridContent = panel.Find("ProductScrollView/Viewport/Content");
                if (productGridContent == null && panel != null)
                {
                    foreach (var sr in panel.GetComponentsInChildren<ScrollRect>(true))
                    {
                        if (sr.content != null) { productGridContent = sr.content; break; }
                    }
                }
                if (coinsText == null) coinsText = panel.Find("Header/Coins/Value")?.GetComponent<TMP_Text>();
            }
            if (modalPanel == null) modalPanel = transform.Find("Modal")?.gameObject;
            if (toastPanel == null) toastPanel = transform.Find("Toast")?.gameObject;
            if (modalPanel != null && modalIcon == null) modalIcon = modalPanel.transform.Find("Body/Icon")?.GetComponent<Image>();
            if (modalPanel != null && modalName == null) modalName = modalPanel.transform.Find("Body/Info/Name")?.GetComponent<TMP_Text>();
            if (modalPanel != null && modalCategory == null) modalCategory = modalPanel.transform.Find("Body/Info/Category")?.GetComponent<TMP_Text>();
            if (modalPanel != null && modalPrice == null) modalPrice = modalPanel.transform.Find("Body/Info/PriceVal")?.GetComponent<TMP_Text>();
            if (modalPanel != null && modalCoinsHint == null) modalCoinsHint = modalPanel.transform.Find("Body/Info/Coins")?.GetComponent<TMP_Text>();
            if (modalPanel != null && modalConfirm == null) modalConfirm = modalPanel.transform.Find("Body/Buttons/Confirm")?.GetComponent<Button>();
            if (modalPanel != null && modalCancel == null)
            {
                modalCancel = modalPanel.transform.Find("Body/Buttons/Cancel")?.GetComponent<Button>();
                if (modalCancel == null)
                {
                    foreach (var b in modalPanel.GetComponentsInChildren<Button>(true))
                        if (b.gameObject.name == "Cancel") { modalCancel = b; break; }
                }
            }
            if (toastPanel != null && toastText == null) toastText = toastPanel.transform.Find("Text")?.GetComponent<TMP_Text>();
        }

        void BuildCategories()
        {
            if (database == null || categoryContainer == null) return;

            var categories = database.GetAllCategories();
            _filteredCategories.Clear();
            for (int i = 0; i < categories.Count; i++)
            {
                string c = categories[i];
                bool isFood = IsFoodCategory(c);
                if (isFoodShopPanel && isFood)
                    _filteredCategories.Add(c);
                else if (!isFoodShopPanel && !isFood)
                    _filteredCategories.Add(c);
            }
            categories = _filteredCategories;
            if (categories.Count == 0) return;
            if (string.IsNullOrEmpty(_currentCategory) || !categories.Contains(_currentCategory))
                _currentCategory = categories[0];

            foreach (var go in _categoryButtons)
                if (go != null) Destroy(go);
            _categoryButtons.Clear();

            var prefab = categoryButtonPrefab;
            var destroyPrefab = false;
            if (prefab == null) { prefab = CreateDefaultCategoryButton(); destroyPrefab = true; }
            foreach (var cat in categories)
            {
                var btn = Instantiate(prefab, categoryContainer);
                _categoryButtons.Add(btn);
                var text = btn.GetComponentInChildren<Text>();
                if (text) text.text = Loc.Term(cat);
                var tmp = btn.GetComponentInChildren<TMP_Text>(true);
                if (tmp != null) tmp.text = Loc.Term(cat);
                var bt = btn.GetComponent<Button>();
                if (bt)
                {
                    var c = cat;
                    bt.onClick.AddListener(() => SelectCategory(c));
                }
                btn.SetActive(true);
            }
            if (destroyPrefab && prefab != null) Destroy(prefab);
        }

        GameObject CreateDefaultCategoryButton()
        {
            var go = new GameObject("CategoryBtn");
            var rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(260, 80);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.54f, 0.17f, 0.89f);
            img.raycastTarget = true;
            var bt = go.AddComponent<Button>();
            var child = new GameObject("Text");
            child.transform.SetParent(go.transform, false);
            var rt = child.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var txt = child.AddComponent<Text>();
            txt.text = Loc.Choose("分类", "Category");
            txt.fontSize = 28;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            txt.raycastTarget = false;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return go;
        }

        void SelectCategory(string category)
        {
            _currentCategory = category;
            RefreshProducts();
        }

        void RefreshProducts()
        {
            foreach (var go in _productItems)
                if (go != null) Destroy(go);
            _productItems.Clear();

            if (database == null || productGridContent == null) return;

            var list = database.GetByCategory(_currentCategory);
            var prefab = productItemPrefab;
            if (prefab == null) prefab = CreateDefaultProductItem();

            foreach (var entry in list)
            {
                bool isFood = entry != null && (entry.isConsumableFood || IsFoodCategory(entry.category));
                if (isFoodShopPanel && !isFood) continue;
                if (!isFoodShopPanel && isFood) continue;
                var go = Instantiate(prefab, productGridContent);
                _productItems.Add(go);
                var item = go.GetComponent<ProductItemUI>();
                if (item == null) item = go.AddComponent<ProductItemUI>();
                item.Setup(entry, database, placeholderSprite);
                item.OnBuyClick += OnProductBuyClick;
            }
        }

        GameObject CreateDefaultProductItem()
        {
            var go = new GameObject("ProductItem");
            var rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(640, 850);
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = 640;
            le.preferredHeight = 850;

            var bg = go.AddComponent<Image>();
            bg.color = new Color(1f, 1f, 1f, 0.95f);
            bg.raycastTarget = true;

            var icon = new GameObject("Icon").AddComponent<Image>();
            icon.transform.SetParent(go.transform, false);
            icon.rectTransform.anchorMin = new Vector2(0, 0.12f);
            icon.rectTransform.anchorMax = new Vector2(1, 1);
            icon.rectTransform.offsetMin = new Vector2(6, 6);
            icon.rectTransform.offsetMax = new Vector2(-6, -70);
            icon.color = Color.white;
            icon.raycastTarget = false;

            var nameObj = new GameObject("Name").AddComponent<Text>();
            nameObj.transform.SetParent(go.transform, false);
            nameObj.rectTransform.anchorMin = new Vector2(0, 0);
            nameObj.rectTransform.anchorMax = new Vector2(1, 0);
            nameObj.rectTransform.offsetMin = new Vector2(12, 0);
            nameObj.rectTransform.offsetMax = new Vector2(-12, 68);
            nameObj.fontSize = 26;
            nameObj.color = new Color(0.15f, 0.15f, 0.15f);
            nameObj.raycastTarget = false;
            nameObj.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var priceObj = new GameObject("Price").AddComponent<Text>();
            priceObj.transform.SetParent(go.transform, false);
            priceObj.rectTransform.anchorMin = new Vector2(0, 0);
            priceObj.rectTransform.anchorMax = new Vector2(0.72f, 0);
            priceObj.rectTransform.offsetMin = new Vector2(12, -30);
            priceObj.rectTransform.offsetMax = new Vector2(-12, 32);
            priceObj.fontSize = 26;
            priceObj.color = new Color(1f, 0.5f, 0f);
            priceObj.raycastTarget = false;
            priceObj.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var buyBtnGo = new GameObject("BuyBtn");
            buyBtnGo.AddComponent<RectTransform>();
            var buyBtn = buyBtnGo.AddComponent<Button>();
            buyBtn.transform.SetParent(go.transform, false);
            var br = buyBtn.GetComponent<RectTransform>();
            br.anchorMin = new Vector2(0.78f, 0);
            br.anchorMax = new Vector2(0.96f, 0.08f);
            br.offsetMin = new Vector2(4, 4);
            br.offsetMax = new Vector2(-4, 2);
            var bir = buyBtn.gameObject.AddComponent<Image>();
            bir.color = new Color(0.54f, 0.17f, 0.89f);
            var btx = new GameObject("Text").AddComponent<Text>();
            btx.transform.SetParent(buyBtn.transform, false);
            btx.rectTransform.anchorMin = Vector2.zero;
            btx.rectTransform.anchorMax = Vector2.one;
            btx.rectTransform.offsetMin = btx.rectTransform.offsetMax = Vector2.zero;
            btx.text = Loc.Choose("购买", "Buy");
            btx.fontSize = 20;
            btx.color = Color.white;
            btx.alignment = TextAnchor.MiddleCenter;
            btx.raycastTarget = false;
            btx.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var item = go.AddComponent<ProductItemUI>();
            item.iconImage = icon;
            item.nameText = nameObj;
            item.priceText = priceObj;
            item.buyButton = buyBtn;

            return go;
        }

        void OnProductBuyClick(ProductEntry entry)
        {
            _selectedProduct = entry;
            if (modalPanel == null) { TryPurchase(); return; }

            int price = Mathf.RoundToInt(entry.price * priceMultiplier);
            if (modalIcon) DormMallIconLoader.SetIcon(modalIcon, database, entry, placeholderSprite);
            if (modalName) modalName.text = Loc.Term(entry.productName);
            if (modalCategory) modalCategory.text = BuildProductEffectText(entry);
            if (modalPrice) modalPrice.text = price.ToString();
            if (modalCoinsHint) modalCoinsHint.text = Loc.Choose(modalWalletCoinsPrefix, "Current gold: ") + _coins;
            if (modalConfirm) { modalConfirm.onClick.RemoveAllListeners(); modalConfirm.onClick.AddListener(OnConfirmPurchase); }
            if (modalCancel) { modalCancel.onClick.RemoveAllListeners(); modalCancel.onClick.AddListener(CloseModal); }
            HideQuestionInputsInModal();
            modalPanel.SetActive(true);
        }

        void OnConfirmPurchase()
        {
            TryPurchase();
            CloseModal();
        }

        void TryPurchase()
        {
            if (CoopSessionState.BlockGuestCloudWrites)
            {
                ShowToast(Loc.Choose("联机客方无法购买，请由房主操作寝室商城", "Guests cannot purchase in co-op. Ask the host to buy."));
                return;
            }
            if (_selectedProduct == null) return;
            int price = Mathf.RoundToInt(_selectedProduct.price * priceMultiplier);
            if (_coins < price)
            {
                ShowToast(Loc.Choose("金币不足，无法购买", "Not enough gold to purchase"));
                return;
            }

            if (QuestSystemManager.Instance != null)
            {
                if (!QuestSystemManager.Instance.TryConsumeGold(price, "dorm_mall_purchase"))
                {
                    ShowToast(Loc.Choose("金币不足，无法购买", "Not enough gold to purchase"));
                    return;
                }
                _coins = QuestSystemManager.Instance.CurrentGold;
            }
            else
            {
                _coins -= price;
            }
            UpdateCoinsDisplay();
            InventoryData.AddItem(_selectedProduct);
            OnPurchaseComplete?.Invoke(_selectedProduct.id, _selectedProduct.productName, _selectedProduct.category, price, _coins);
            OnAnyPurchaseCompleted?.Invoke(_selectedProduct.id, _selectedProduct.productName, _selectedProduct.category, price, _coins);
            ShowToast(Loc.F("购买成功：{0}", "Purchase successful: {0}", Loc.Term(_selectedProduct.productName)));
            _selectedProduct = null;
        }

        void CloseModal()
        {
            if (modalPanel) modalPanel.SetActive(false);
            _selectedProduct = null;
            var panel = FindPanelTransform(transform);
            if (panel != null && !panel.gameObject.activeSelf)
                panel.gameObject.SetActive(true);
        }

        void HideQuestionInputsInModal()
        {
            var root = transform;
            foreach (var tmp in root.GetComponentsInChildren<TMP_InputField>(true))
            {
                var tmph = tmp.placeholder as TMP_Text;
                if (tmph != null && tmph.text != null &&
                    tmph.text.IndexOf("question", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    tmp.gameObject.SetActive(false);
            }
            foreach (var inp in root.GetComponentsInChildren<InputField>(true))
            {
                var ph = inp.placeholder as Text;
                if (ph != null && ph.text != null &&
                    ph.text.IndexOf("question", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    inp.gameObject.SetActive(false);
            }
        }

        void UpdateCoinsDisplay()
        {
            if (coinsText) { coinsText.text = _coins.ToString(); coinsText.enableWordWrapping = false; }
        }

        void ShowToast(string msg)
        {
            if (toastText) toastText.text = msg;
            if (toastPanel)
            {
                toastPanel.SetActive(true);
                CancelInvoke(nameof(HideToast));
                Invoke(nameof(HideToast), 2f);
            }
        }

        void HideToast()
        {
            if (toastPanel) toastPanel.SetActive(false);
        }

        /// <summary>
        /// 在跨场景情况下：当 database 引用在别的场景没有被正确配置时，
        /// 可以在打开面板时手动刷新一次分类/商品列表，避免出现“面板里空空的”。
        /// </summary>
        public void RebuildUI()
        {
            // 确保引用可用（运行时也可能为 null）
            EnsureRefs();
            if (database == null)
                database = Resources.Load<ProductDatabase>("DormMall/ProductDatabase");
            EnsureFoodFallbackProductsIfNeeded();

            // 重新应用金币来源
            if (QuestSystemManager.Instance != null)
            {
                _coins = QuestSystemManager.Instance.CurrentGold;
                QuestSystemManager.Instance.OnGoldChanged -= OnQuestGoldChanged;
                QuestSystemManager.Instance.OnGoldChanged += OnQuestGoldChanged;
            }
            else
            {
                _coins = initialCoins;
            }
            UpdateCoinsDisplay();
            ApplyTmpChineseFont();

            // 重建分类/商品
            BuildCategories();
            if (!string.IsNullOrEmpty(_currentCategory))
                RefreshProducts();

            // 重置弹窗/提示
            if (modalPanel) modalPanel.SetActive(false);
            if (toastPanel) toastPanel.SetActive(false);
            HideQuestionInputsInModal();

            // 确保模态框按钮监听与 Start 一致（RemoveAllListeners 避免重复）
            var overlayBtn = modalPanel?.GetComponent<Button>();
            if (overlayBtn)
            {
                overlayBtn.onClick.RemoveAllListeners();
                overlayBtn.onClick.AddListener(CloseModal);
            }
            if (modalConfirm)
            {
                modalConfirm.onClick.RemoveAllListeners();
                modalConfirm.onClick.AddListener(OnConfirmPurchase);
            }
            if (modalCancel)
            {
                modalCancel.onClick.RemoveAllListeners();
                modalCancel.onClick.AddListener(CloseModal);
            }
        }

        void ApplyTmpChineseFont()
        {
            TMP_FontAsset f = fontAsset;
            if (f == null)
                f = Resources.Load<TMP_FontAsset>(DefaultChineseTmpResourcesPath);
            if (f == null)
                return;
            foreach (var t in GetComponentsInChildren<TMPro.TMP_Text>(true))
            {
                t.font = f;
                if (f.material != null)
                    t.fontSharedMaterial = f.material;
            }
        }

        bool IsFoodCategory(string category)
        {
            string c = (category ?? "").Trim();
            string k = (foodCategoryKeyword ?? "食物").Trim();
            if (string.IsNullOrEmpty(k))
                k = "食物";
            return ProductDatabase.IsFoodCategory(c) || c.Contains(k);
        }

        void TryForceFoodPanelFullScreen()
        {
            if (!isFoodShopPanel || !forceFoodPanelFullScreen)
                return;
            var panel = FindPanelTransform(transform);
            if (panel == null)
                return;
            var rt = panel as RectTransform;
            if (rt == null)
                return;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
        }

        void TryLoadCatalogFromServerIfNeeded()
        {
            if (_remoteCatalogLoading || _remoteCatalogTried || !autoLoadCatalogFromGameServer)
                return;
            if (GameServerSync.Instance == null || string.IsNullOrEmpty(GameServerSync.Instance.BaseUrlTrimmed))
                return;
            if (!NeedsRemoteCatalog())
                return;

            _remoteCatalogLoading = true;
            _remoteCatalogTried = true;
            StartCoroutine(CoLoadRemoteCatalogAndRebuild());
        }

        bool NeedsRemoteCatalog()
        {
            if (database == null)
                return true;
            if (!isFoodShopPanel)
                return false;
            if (!preferRemoteCatalogForFoodPanel)
                return !HasAnyFoodProducts(database);
            return true;
        }

        bool HasAnyFoodProducts(ProductDatabase db)
        {
            if (db == null || db.products == null)
                return false;
            for (int i = 0; i < db.products.Count; i++)
            {
                var p = db.products[i];
                if (p == null) continue;
                if (p.isConsumableFood || IsFoodCategory(p.category))
                    return true;
            }
            return false;
        }

        IEnumerator CoLoadRemoteCatalogAndRebuild()
        {
            string baseUrl = GameServerSync.Instance != null ? GameServerSync.Instance.BaseUrlTrimmed : "";
            yield return DormMallRemoteCatalog.FetchCoroutine(baseUrl, db =>
            {
                if (db != null)
                    database = db;
            }, _ => { });

            _remoteCatalogLoading = false;
            if (DormMallRemoteCatalog.LastFetchOk && DormMallRemoteCatalog.CachedRuntimeDatabase != null)
            {
                database = DormMallRemoteCatalog.CachedRuntimeDatabase;
                RebuildUI();
            }
            else
            {
                // 远端目录失败时，食物商城至少有本地兜底可展示。
                EnsureFoodFallbackProductsIfNeeded();
                RebuildUI();
            }
        }

        void EnsureFoodFallbackProductsIfNeeded()
        {
            if (!isFoodShopPanel)
                return;
            if (database == null)
            {
                database = ScriptableObject.CreateInstance<ProductDatabase>();
                database.products = new List<ProductEntry>();
            }
            if (database.products == null)
                database.products = new List<ProductEntry>();
            if (HasAnyFoodProducts(database))
                return;

            AddFoodFallbackIfMissing(2001, "红苹果", 18f, 25f, 10f, 1f, 0f, "DormMall/Food/apple_real");
            AddFoodFallbackIfMissing(2002, "香蕉", 22f, 15f, 30f, 1.15f, 18f, "DormMall/Food/banana_real");
            AddFoodFallbackIfMissing(2003, "牛奶", 26f, 28f, 25f, 1f, 0f, "DormMall/Food/milk_real");
            AddFoodFallbackIfMissing(2004, "面包", 30f, 40f, 18f, 1.08f, 30f, "DormMall/Food/bread_real");
        }

        string BuildProductEffectText(ProductEntry entry)
        {
            if (entry == null)
                return string.Empty;
            if (!(entry.isConsumableFood || IsFoodCategory(entry.category)))
                return Loc.Term(entry.category);

            var parts = new List<string>();
            if (entry.healAmount > 0.01f)
                parts.Add(Loc.F("回血+{0}", "HP +{0}", Mathf.RoundToInt(entry.healAmount)));
            if (entry.staminaRecover > 0.01f)
                parts.Add(Loc.F("体力+{0}", "Stamina +{0}", Mathf.RoundToInt(entry.staminaRecover)));
            if (entry.speedBoostMultiplier > 1.001f && entry.speedBoostDuration > 0.01f)
            {
                int pct = Mathf.RoundToInt((entry.speedBoostMultiplier - 1f) * 100f);
                parts.Add(Loc.F("移速+{0}% 持续{1}秒", "Speed +{0}% for {1}s", pct, Mathf.RoundToInt(entry.speedBoostDuration)));
            }

            if (parts.Count == 0)
                return Loc.Term(entry.category);
            return Loc.Term(entry.category) + "\n" + string.Join(Loc.Choose("，", ", "), parts);
        }

        void AddFoodFallbackIfMissing(int id, string name, float price, float heal, float stamina, float speedMul, float speedSec, string iconUrl)
        {
            if (database == null || database.products == null)
                return;
            for (int i = 0; i < database.products.Count; i++)
            {
                var p = database.products[i];
                if (p != null && p.id == id)
                    return;
            }
            database.products.Add(new ProductEntry
            {
                id = id,
                productName = name,
                category = "食物",
                description = "食用后恢复状态并获得增益。",
                iconPath = iconUrl,
                price = price,
                placeablePrefabPath = "",
                isConsumableFood = true,
                healAmount = heal,
                staminaRecover = stamina,
                speedBoostMultiplier = speedMul,
                speedBoostDuration = speedSec
            });
        }

        /// <summary>直接设置商城币显示（与任务系统并存时请谨慎）</summary>
        public void SetCoins(int amount)
        {
            _coins = Mathf.Max(0, amount);
            UpdateCoinsDisplay();
            if (QuestSystemManager.Instance != null && QuestSystemManager.Instance.CurrentGold != _coins)
                QuestSystemManager.Instance.ForceSaveNow();
        }

        /// <summary>增加商城币并刷新显示</summary>
        public void AddCoins(int amount)
        {
            SetCoins(_coins + amount);
        }

        void OnDestroy()
        {
            if (QuestSystemManager.Instance != null)
                QuestSystemManager.Instance.OnGoldChanged -= OnQuestGoldChanged;
        }

        void OnQuestGoldChanged(int value)
        {
            _coins = Mathf.Max(0, value);
            UpdateCoinsDisplay();
        }

        /// <summary>
        /// 寝室商城是否处于「玩家可见的营业界面」：根 Canvas 实际在渲染、且主 Panel 或购买弹窗为开。
        /// 1）根物体常保持激活只关 Panel 时，要看 Panel 是否显示。
        /// 2）交互寝室面板（F 打开 / ESC 关）只将 <see cref="Canvas.enabled"/> 设为 false 时，子物体仍 activeInHierarchy，
        ///    必须用 rootCanvas.isActiveAndEnabled 排除这种「已关闭但仍激活」的情况。
        /// </summary>
        public bool IsMainShopPanelOpen()
        {
            if (!gameObject.activeInHierarchy)
                return false;

            var canvas = GetComponentInParent<Canvas>(true);
            if (canvas != null)
            {
                Canvas root = canvas.rootCanvas;
                if (root != null && !root.isActiveAndEnabled)
                    return false;
            }

            if (modalPanel != null && modalPanel.activeInHierarchy)
                return true;
            Transform panelTr = FindPanelTransform(transform);
            if (panelTr != null)
                return panelTr.gameObject.activeInHierarchy;
            return true;
        }

        /// <summary>任意实例的商城主界面是否打开（任务栏、教程判定等共用）。</summary>
        public static bool IsAnyMainShopPanelOpenInHierarchy()
        {
            var malls = UnityEngine.Object.FindObjectsOfType<DormMallUI>(true);
            for (int i = 0; i < malls.Length; i++)
            {
                if (malls[i] != null && malls[i].IsMainShopPanelOpen())
                    return true;
            }
            return false;
        }
    }
}
