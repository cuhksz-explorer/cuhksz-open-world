using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CombatSystem
{
    public class CombatHudController : MonoBehaviour
    {
        public static CombatHudController Instance { get; private set; }

        const string TmpFontResource = "Fonts & Materials/AlibabaPuHuiTi-3-75-SemiBold SDF";

        Canvas _canvas;
        GameObject _crosshairRoot;
        GameObject _equipBarRoot;
        GameObject _ammoRoot;
        Image _currentIcon;
        Image _previousIcon;
        TextMeshProUGUI _currentName;
        TextMeshProUGUI _previousName;
        TextMeshProUGUI _ammoText;
        TextMeshProUGUI _reloadText;
        Button _gachaButton;
        TMP_FontAsset _font;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void Ensure()
        {
            if (Instance != null)
                return;
            var go = new GameObject("CombatHud");
            go.AddComponent<CombatHudController>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            _font = Resources.Load<TMP_FontAsset>(TmpFontResource);
            EnsureEventSystem();
            BuildUi();
            WeaponInventoryData.EnsureInitialized();
            WeaponInventoryData.OnChanged += RefreshEquipBar;
            WeaponAmmoState.OnAmmoChanged += RefreshAmmoDisplay;
            RefreshEquipBar();
            RefreshAmmoDisplay();
            SetHudElementsVisible(false);
        }

        void OnDestroy()
        {
            WeaponInventoryData.OnChanged -= RefreshEquipBar;
            WeaponAmmoState.OnAmmoChanged -= RefreshAmmoDisplay;
            if (Instance == this)
                Instance = null;
        }

        void Update()
        {
            CombatMode.TickHotkey();
            SetHudElementsVisible(ShouldShowCombatHud());
            RefreshAmmoDisplay();
        }

        void SetHudElementsVisible(bool show)
        {
            if (_crosshairRoot != null)
                _crosshairRoot.SetActive(show);
            if (_equipBarRoot != null)
                _equipBarRoot.SetActive(show);
            if (_ammoRoot != null)
                _ammoRoot.SetActive(show);
            if (_gachaButton != null)
                _gachaButton.gameObject.SetActive(show && !GachaPanelController.IsOpen);
        }

        static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
                return;
            var esGo = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Object.DontDestroyOnLoad(esGo);
        }

        public static bool ShouldShowCombatHud()
        {
            if (!CombatMode.IsArmed)
                return false;
            if (DeathReviveUI.BlocksGameplayInput)
                return false;
            if (GachaPanelController.IsOpen)
                return false;
            if (GameplayUiHotkeyGuard.ShouldBlockGameplayHotkeys())
                return false;
            if (GameplayUiHotkeyGuard.BlocksCameraMouseLook())
                return false;

            var switcher = Object.FindObjectOfType<CameraSwitcher>();
            if (switcher == null || !switcher.IsFirstPersonActive)
                return false;

            var pm = Object.FindObjectOfType<PlayerMovement>();
            if (pm != null && (!pm.enabled || pm.IsDeathReviveFlowActive))
                return false;

            if (SceneManagerHelper.IsLoadingScene())
                return false;

            return true;
        }

        void BuildUi()
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = CombatUiLayers.CombatHud;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.55f;
            gameObject.AddComponent<GraphicRaycaster>();

            BuildCrosshair();
            BuildEquipBar();
            BuildAmmoHud();
            BuildGachaButton();
        }

        void BuildCrosshair()
        {
            _crosshairRoot = new GameObject("Crosshair");
            _crosshairRoot.transform.SetParent(transform, false);
            var rt = _crosshairRoot.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(24, 24);

            CreateCrossLine(_crosshairRoot.transform, new Vector2(0, 6), new Vector2(2, 8));
            CreateCrossLine(_crosshairRoot.transform, new Vector2(0, -6), new Vector2(2, 8));
            CreateCrossLine(_crosshairRoot.transform, new Vector2(-6, 0), new Vector2(8, 2));
            CreateCrossLine(_crosshairRoot.transform, new Vector2(6, 0), new Vector2(8, 2));
        }

        void CreateCrossLine(Transform parent, Vector2 pos, Vector2 size)
        {
            var go = new GameObject("Line", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            go.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.85f);
        }

        void BuildEquipBar()
        {
            _equipBarRoot = new GameObject("EquipBar", typeof(RectTransform), typeof(Image));
            _equipBarRoot.transform.SetParent(transform, false);
            var barRt = _equipBarRoot.GetComponent<RectTransform>();
            barRt.anchorMin = new Vector2(1, 0.5f);
            barRt.anchorMax = new Vector2(1, 0.5f);
            barRt.pivot = new Vector2(1, 0.5f);
            barRt.anchoredPosition = new Vector2(-24, 0);
            barRt.sizeDelta = new Vector2(160, 220);
            _equipBarRoot.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.45f);

            var title = CreateTmp(_equipBarRoot.transform, "Title", new Vector2(0, 90), new Vector2(150, 28), 16);
            title.text = "装备";
            title.alignment = TextAlignmentOptions.Center;

            _currentIcon = CreateSlot(_equipBarRoot.transform, "Current", new Vector2(0, 30), out _currentName);
            var prevLabel = CreateTmp(_equipBarRoot.transform, "PrevLabel", new Vector2(0, -20), new Vector2(150, 20), 14);
            prevLabel.text = "上一把 (Q / 滚轮↑)";
            prevLabel.alignment = TextAlignmentOptions.Center;
            _previousIcon = CreateSlot(_equipBarRoot.transform, "Previous", new Vector2(0, -70), out _previousName);
        }

        void BuildAmmoHud()
        {
            _ammoRoot = new GameObject("AmmoHud", typeof(RectTransform), typeof(Image));
            _ammoRoot.transform.SetParent(transform, false);
            var rt = _ammoRoot.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1, 0);
            rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(1, 0);
            rt.anchoredPosition = new Vector2(-32, 32);
            rt.sizeDelta = new Vector2(220, 72);
            _ammoRoot.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.4f);

            _ammoText = CreateTmp(_ammoRoot.transform, "Ammo", new Vector2(0, 12), new Vector2(200, 36), 28);
            _ammoText.alignment = TextAlignmentOptions.Right;
            _ammoText.fontStyle = FontStyles.Bold;

            _reloadText = CreateTmp(_ammoRoot.transform, "Reload", new Vector2(0, -18), new Vector2(200, 22), 16);
            _reloadText.alignment = TextAlignmentOptions.Right;
            _reloadText.color = new Color(1f, 0.85f, 0.35f, 1f);
        }

        Image CreateSlot(Transform parent, string name, Vector2 pos, out TextMeshProUGUI label)
        {
            var slot = new GameObject(name, typeof(RectTransform), typeof(Image));
            slot.transform.SetParent(parent, false);
            var rt = slot.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(72, 72);
            slot.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.12f);

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(slot.transform, false);
            var iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.anchorMin = Vector2.zero;
            iconRt.anchorMax = Vector2.one;
            iconRt.offsetMin = new Vector2(6, 6);
            iconRt.offsetMax = new Vector2(-6, -6);
            var icon = iconGo.GetComponent<Image>();
            icon.preserveAspect = true;

            label = CreateTmp(slot.transform, "Label", new Vector2(0, -48), new Vector2(140, 22), 12);
            label.alignment = TextAlignmentOptions.Center;
            return icon;
        }

        void BuildGachaButton()
        {
            var go = new GameObject("GachaButton", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(1, 1);
            rt.anchoredPosition = new Vector2(-80, -80);
            rt.sizeDelta = new Vector2(120, 72);

            var img = go.GetComponent<Image>();
            var icon = Resources.Load<Sprite>("CombatSystem/GachaIcon");
            if (icon != null)
            {
                img.sprite = icon;
                img.color = Color.white;
            }
            else
            {
                img.color = new Color(0.95f, 0.75f, 0.2f, 0.9f);
            }

            _gachaButton = go.GetComponent<Button>();
            _gachaButton.onClick.AddListener(() => GachaPanelController.Toggle());

            var hint = CreateTmp(go.transform, "Hint", new Vector2(0, -8), new Vector2(120, 40), 13);
            hint.text = icon == null ? "名笔库\n[G]" : "[G] 名笔库";
            hint.alignment = TextAlignmentOptions.Center;
            hint.raycastTarget = false;
        }

        TextMeshProUGUI CreateTmp(Transform parent, string name, Vector2 pos, Vector2 size, float fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = fontSize;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            if (_font != null)
            {
                tmp.font = _font;
                tmp.fontSharedMaterial = _font.material;
            }
            return tmp;
        }

        void RefreshEquipBar()
        {
            var db = WeaponDatabase.Load();
            if (db == null)
                return;

            ApplyWeaponSlot(db.GetById(WeaponInventoryData.EquippedWeaponId), _currentIcon, _currentName);
            int prevId = WeaponInventoryData.PreviousWeaponId;
            ApplyWeaponSlot(prevId > 0 ? db.GetById(prevId) : null, _previousIcon, _previousName);
        }

        void RefreshAmmoDisplay()
        {
            if (_ammoText == null)
                return;

            int weaponId = WeaponInventoryData.EquippedWeaponId;
            var db = WeaponDatabase.Load();
            var entry = db != null ? db.GetById(weaponId) : null;
            if (entry != null)
                WeaponAmmoState.EnsureWeapon(weaponId, entry);

            int mag = WeaponAmmoState.GetMagazine(weaponId);
            int reserve = WeaponAmmoState.GetReserve(weaponId);
            int magSize = entry != null ? WeaponStatsHelper.GetMagazineSize(entry) : mag;

            _ammoText.text = $"{mag} / {reserve}";
            _ammoText.color = mag <= 0 ? new Color(1f, 0.45f, 0.35f, 1f) : Color.white;

            if (_reloadText != null)
            {
                if (WeaponController.IsReloading)
                    _reloadText.text = "换弹中...";
                else if (mag <= 0 && reserve > 0)
                    _reloadText.text = "按 [R] 换弹";
                else if (mag < magSize && reserve > 0)
                    _reloadText.text = "[R] 换弹";
                else if (reserve <= 0 && mag <= 0)
                    _reloadText.text = "弹药耗尽";
                else
                    _reloadText.text = "";
            }
        }

        void ApplyWeaponSlot(WeaponEntry entry, Image icon, TextMeshProUGUI label)
        {
            if (label != null)
                label.text = entry != null ? entry.FullDisplayName : "-";
            if (icon == null)
                return;
            if (entry == null)
            {
                icon.sprite = null;
                icon.color = new Color(1f, 1f, 1f, 0.15f);
                return;
            }
            var db = WeaponDatabase.Load();
            icon.sprite = db != null ? db.ResolveIcon(entry) : entry.icon;
            icon.color = Color.white;
        }
    }

    static class SceneManagerHelper
    {
        public static bool IsLoadingScene()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            return string.Equals(scene, "Loadingscene", System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
