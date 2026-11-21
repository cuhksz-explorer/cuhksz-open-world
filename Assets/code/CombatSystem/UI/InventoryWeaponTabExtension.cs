using System.Collections.Generic;
using GameQuestSystem;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CombatSystem
{
    /// <summary>扩展背包 UI：物品 / 武器 Tab、武器详情与出售。</summary>
    [DefaultExecutionOrder(150)]
    public class InventoryWeaponTabExtension : MonoBehaviour
    {
        const string TmpFontResource = "Fonts & Materials/AlibabaPuHuiTi-3-75-SemiBold SDF";

        DormMall.InventoryUI _inventory;
        Transform _body;
        Transform _itemScroll;
        GameObject _weaponPanel;
        Transform _weaponGrid;
        GameObject _detailPanel;
        TextMeshProUGUI _detailTitle;
        TextMeshProUGUI _detailBody;
        Button _sellButton;
        Button _equipButton;
        TMP_FontAsset _font;
        int _selectedWeaponId;
        float _lastClickTime;
        int _lastClickWeaponId;

        enum Tab { Items, Weapons }
        Tab _tab = Tab.Items;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Ensure()
        {
            InvokeEnsure();
        }

        public static void InvokeEnsure()
        {
            var inv = DormMall.InventoryUI.Instance;
            if (inv == null)
                return;
            if (inv.GetComponent<InventoryWeaponTabExtension>() == null)
                inv.gameObject.AddComponent<InventoryWeaponTabExtension>();
        }

        void Awake()
        {
            _inventory = GetComponent<DormMall.InventoryUI>();
            _font = Resources.Load<TMP_FontAsset>(TmpFontResource);
            WeaponInventoryData.OnChanged += OnWeaponChanged;
        }

        void OnDestroy()
        {
            WeaponInventoryData.OnChanged -= OnWeaponChanged;
        }

        void Start()
        {
            BuildIfNeeded();
        }

        void Update()
        {
            if (_inventory == null)
                _inventory = GetComponent<DormMall.InventoryUI>();
            if (_body == null)
                BuildIfNeeded();
        }

        void OnWeaponChanged()
        {
            if (_tab == Tab.Weapons && _weaponPanel != null && _weaponPanel.activeSelf)
                RefreshWeaponGrid();
        }

        void BuildIfNeeded()
        {
            if (_inventory == null || _inventory.inventoryPanel == null || _body != null)
                return;

            _body = _inventory.inventoryPanel.transform.Find("Body");
            if (_body == null)
                return;

            _itemScroll = _body.Find("Scroll");
            CreateTabBar();
            CreateWeaponPanel();
            CreateDetailPanel();
        }

        void CreateTabBar()
        {
            var tabBar = new GameObject("TabBar", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            tabBar.transform.SetParent(_body, false);
            tabBar.transform.SetAsFirstSibling();
            var rt = tabBar.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.anchoredPosition = new Vector2(0, -58);
            rt.sizeDelta = new Vector2(-40, 36);
            var h = tabBar.GetComponent<HorizontalLayoutGroup>();
            h.spacing = 12;
            h.childAlignment = TextAnchor.MiddleCenter;

            CreateTabButton(tabBar.transform, "物品", () => SetTab(Tab.Items));
            CreateTabButton(tabBar.transform, "武器", () => SetTab(Tab.Weapons));

            var scrollRt = _itemScroll as RectTransform;
            if (scrollRt != null)
                scrollRt.offsetMax = new Vector2(scrollRt.offsetMax.x, -110);
        }

        void CreateTabButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(140, 32);
            go.GetComponent<Image>().color = new Color(0.35f, 0.55f, 0.95f, 0.9f);
            go.GetComponent<Button>().onClick.AddListener(onClick);
            var txt = go.AddComponent<TextMeshProUGUI>();
            txt.text = label;
            txt.fontSize = 18;
            txt.alignment = TextAlignmentOptions.Center;
            txt.color = Color.white;
            if (_font != null) { txt.font = _font; txt.fontSharedMaterial = _font.material; }
        }

        void CreateWeaponPanel()
        {
            _weaponPanel = new GameObject("WeaponScroll");
            _weaponPanel.transform.SetParent(_body, false);
            var scrollRt = _weaponPanel.AddComponent<RectTransform>();
            scrollRt.anchorMin = new Vector2(0, 0);
            scrollRt.anchorMax = new Vector2(0.62f, 1);
            scrollRt.offsetMin = new Vector2(20, 20);
            scrollRt.offsetMax = new Vector2(-10, -110);

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewport.transform.SetParent(_weaponPanel.transform, false);
            var vpRt = viewport.GetComponent<RectTransform>();
            vpRt.anchorMin = Vector2.zero;
            vpRt.anchorMax = Vector2.one;
            vpRt.offsetMin = vpRt.offsetMax = Vector2.zero;

            var content = new GameObject("Content", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            _weaponGrid = content.transform;
            var glg = content.GetComponent<GridLayoutGroup>();
            glg.cellSize = new Vector2(110, 110);
            glg.spacing = new Vector2(10, 10);
            glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            glg.constraintCount = 6;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _weaponPanel.SetActive(false);
        }

        void CreateDetailPanel()
        {
            _detailPanel = new GameObject("WeaponDetail", typeof(RectTransform), typeof(Image));
            _detailPanel.transform.SetParent(_body, false);
            var rt = _detailPanel.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.62f, 0);
            rt.anchorMax = new Vector2(1, 1);
            rt.offsetMin = new Vector2(10, 20);
            rt.offsetMax = new Vector2(-20, -110);
            _detailPanel.GetComponent<Image>().color = new Color(0.95f, 0.95f, 0.98f, 0.95f);

            _detailTitle = CreateDetailText("Title", new Vector2(0, 1), new Vector2(1, 1), new Vector2(12, -12), new Vector2(-12, -50), 22);
            _detailBody = CreateDetailText("Body", new Vector2(0, 0.25f), new Vector2(1, 0.85f), new Vector2(12, 0), new Vector2(-12, 0), 16);
            _detailBody.alignment = TextAlignmentOptions.TopLeft;
            _detailBody.enableWordWrapping = true;

            _equipButton = CreateDetailButton("Equip", new Vector2(-90, 40), "装备", OnEquipSelected);
            _sellButton = CreateDetailButton("Sell", new Vector2(90, 40), "出售", OnSellSelected);

            _detailPanel.SetActive(false);
        }

        TextMeshProUGUI CreateDetailText(string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offMin, Vector2 offMax, float size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(_detailPanel.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offMin;
            rt.offsetMax = offMax;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = size;
            tmp.color = new Color(0.15f, 0.15f, 0.2f);
            if (_font != null) { tmp.font = _font; tmp.fontSharedMaterial = _font.material; }
            return tmp;
        }

        Button CreateDetailButton(string name, Vector2 pos, string label, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(_detailPanel.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(140, 40);
            go.GetComponent<Image>().color = new Color(0.35f, 0.55f, 0.95f, 0.95f);
            var btn = go.GetComponent<Button>();
            btn.onClick.AddListener(onClick);
            var txt = go.AddComponent<TextMeshProUGUI>();
            txt.text = label;
            txt.fontSize = 18;
            txt.alignment = TextAlignmentOptions.Center;
            txt.color = Color.white;
            if (_font != null) { txt.font = _font; txt.fontSharedMaterial = _font.material; }
            return btn;
        }

        void SetTab(Tab tab)
        {
            _tab = tab;
            bool weapons = tab == Tab.Weapons;
            if (_itemScroll != null)
                _itemScroll.gameObject.SetActive(!weapons);
            if (_weaponPanel != null)
                _weaponPanel.SetActive(weapons);
            if (_detailPanel != null)
                _detailPanel.SetActive(weapons);
            if (weapons)
            {
                RefreshWeaponGrid();
                ShowWeaponDetail(_selectedWeaponId > 0 ? _selectedWeaponId : WeaponInventoryData.EquippedWeaponId);
            }
        }

        public void OnInventoryOpened()
        {
            BuildIfNeeded();
            SetTab(_tab);
        }

        void RefreshWeaponGrid()
        {
            if (_weaponGrid == null)
                return;
            for (int i = _weaponGrid.childCount - 1; i >= 0; i--)
                Destroy(_weaponGrid.GetChild(i).gameObject);

            var db = WeaponDatabase.Load();
            if (db == null)
                return;

            var owned = WeaponInventoryData.OwnedWeaponIds;
            for (int i = 0; i < owned.Count; i++)
            {
                int weaponId = owned[i];
                var entry = db.GetById(weaponId);
                if (entry == null)
                    continue;
                CreateWeaponCell(entry);
            }
        }

        void CreateWeaponCell(WeaponEntry entry)
        {
            var go = new GameObject("WeaponCell", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_weaponGrid, false);
            var img = go.GetComponent<Image>();
            img.color = entry.RarityColor * 0.35f + Color.white * 0.65f;
            img.raycastTarget = true;

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(go.transform, false);
            var iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.anchorMin = Vector2.zero;
            iconRt.anchorMax = Vector2.one;
            iconRt.offsetMin = new Vector2(8, 8);
            iconRt.offsetMax = new Vector2(-8, -8);
            var icon = iconGo.GetComponent<Image>();
            icon.sprite = WeaponDatabase.Load()?.ResolveIcon(entry);
            icon.preserveAspect = true;

            int capturedId = entry.id;
            var trigger = go.AddComponent<WeaponCellClickHandler>();
            trigger.Init(capturedId, OnWeaponCellClick);
        }

        void OnWeaponCellClick(int weaponId)
        {
            float now = Time.unscaledTime;
            if (_lastClickWeaponId == weaponId && now - _lastClickTime < 0.35f)
            {
                WeaponController.TryEquipWeapon(weaponId);
                ShowWeaponDetail(weaponId);
            }
            else
            {
                _selectedWeaponId = weaponId;
                ShowWeaponDetail(weaponId);
            }
            _lastClickWeaponId = weaponId;
            _lastClickTime = now;
        }

        void ShowWeaponDetail(int weaponId)
        {
            var db = WeaponDatabase.Load();
            var entry = db != null ? db.GetById(weaponId) : null;
            if (entry == null)
            {
                _detailTitle.text = "选择一支笔";
                _detailBody.text = "单击查看详情，双击装备。";
                return;
            }

            _detailTitle.text = entry.FullDisplayName;
            _detailBody.text =
                $"品牌：{entry.brand}\n" +
                $"笔型：{WeaponDatabase.GetPenStyleLabel(entry.penStyle)}\n" +
                $"稀有度：{WeaponDatabase.GetRarityLabel(entry.rarity)}\n" +
                $"伤害：{entry.damage:0}\n" +
                $"出售价：{entry.sellPrice}\n\n" +
                entry.description;
        }

        void OnEquipSelected()
        {
            if (_selectedWeaponId > 0)
                WeaponController.TryEquipWeapon(_selectedWeaponId);
        }

        void OnSellSelected()
        {
            if (_selectedWeaponId <= 0)
                return;

            var db = WeaponDatabase.Load();
            var entry = db != null ? db.GetById(_selectedWeaponId) : null;
            if (entry == null)
                return;

            var owned = WeaponInventoryData.OwnedWeaponIds;
            int index = -1;
            for (int i = 0; i < owned.Count; i++)
            {
                if (owned[i] == _selectedWeaponId)
                {
                    index = i;
                    break;
                }
            }
            if (index < 0)
                return;

            if (owned.Count <= 1)
            {
                _detailBody.text = "至少保留一支笔。";
                return;
            }

            WeaponInventoryData.TryRemoveAt(index);
            var q = QuestSystemManager.Instance;
            if (q != null)
                q.AddGold(entry.sellPrice, "weapon_sell");
            _selectedWeaponId = WeaponInventoryData.EquippedWeaponId;
            RefreshWeaponGrid();
            ShowWeaponDetail(_selectedWeaponId);
        }
    }

    public class WeaponCellClickHandler : MonoBehaviour, IPointerClickHandler
    {
        int _weaponId;
        System.Action<int> _onClick;

        public void Init(int weaponId, System.Action<int> onClick)
        {
            _weaponId = weaponId;
            _onClick = onClick;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            _onClick?.Invoke(_weaponId);
        }
    }
}
