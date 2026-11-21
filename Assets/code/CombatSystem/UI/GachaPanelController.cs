using System.Collections.Generic;
using GameQuestSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CombatSystem
{
    public class GachaPanelController : MonoBehaviour
    {
        public static GachaPanelController Instance { get; private set; }
        public static bool IsOpen => Instance != null && Instance._panelRoot != null && Instance._panelRoot.activeSelf;

        const string TmpFontResource = "Fonts & Materials/AlibabaPuHuiTi-3-75-SemiBold SDF";

        GameObject _panelRoot;
        TextMeshProUGUI _goldText;
        TextMeshProUGUI _resultText;
        TextMeshProUGUI _hintText;
        TMP_FontAsset _font;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void Ensure()
        {
            if (Instance != null)
                return;
            var go = new GameObject("GachaPanel");
            go.AddComponent<GachaPanelController>();
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
            BuildUi();
        }

        void OnEnable()
        {
            var q = QuestSystemManager.Instance;
            if (q != null)
                q.OnGoldChanged += HandleGoldChanged;
        }

        void OnDestroy()
        {
            var q = QuestSystemManager.Instance;
            if (q != null)
                q.OnGoldChanged -= HandleGoldChanged;
            if (Instance == this)
                Instance = null;
        }

        void Update()
        {
            if (IsOpen)
            {
                if (CombatHostBridge.WasKeyDown(CombatHostBridge.OpenGachaKey))
                    Close();
                return;
            }

            if (!CanToggleHotkey())
                return;
            if (CombatHostBridge.WasKeyDown(CombatHostBridge.OpenGachaKey))
                Open();
        }

        static bool CanToggleHotkey()
        {
            if (!CombatMode.IsArmed)
                return false;
            if (DeathReviveUI.BlocksGameplayInput)
                return false;
            if (SceneManagerHelper.IsLoadingScene())
                return false;
            if (GameplayUiHotkeyGuard.ShouldBlockDontDestroyOnLoadHotkeys())
                return false;
            if (GameplayUiHotkeyGuard.IsAnyInputFieldFocused())
                return false;
            if (GameplayUiHotkeyGuard.ShouldBlockGameplayHotkeys())
                return false;
            return true;
        }

        void HandleGoldChanged(int _)
        {
            RefreshGold();
        }

        public static void Toggle()
        {
            Ensure();
            if (Instance == null)
                return;
            if (IsOpen)
                Instance.Close();
            else if (CombatMode.IsArmed)
                Instance.Open();
        }

        void Open()
        {
            if (!CombatMode.IsArmed)
                return;
            GameplayUiHotkeyGuard.CloseOtherExclusivePanels(GameplayUiHotkeyGuard.ExclusivePanelKind.None);
            _panelRoot.SetActive(true);
            RefreshGold();
            _resultText.text = "";
            _hintText.text = WeaponInventoryData.IsFull
                ? "武器背包已满，请先出售武器。"
                : "单抽 100 金币 | 十连 900 金币（9折）";
            GameplayCursorLock.UnlockForModalUi();
        }

        void Close()
        {
            if (_panelRoot != null)
                _panelRoot.SetActive(false);
            if (CombatHudController.ShouldShowCombatHud())
                GameplayCursorLock.TryApplyGameplayDefaultLock();
        }

        void BuildUi()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = CombatUiLayers.GachaPanel;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            gameObject.AddComponent<GraphicRaycaster>();

            _panelRoot = new GameObject("Panel");
            _panelRoot.transform.SetParent(transform, false);
            var panelRt = _panelRoot.AddComponent<RectTransform>();
            panelRt.anchorMin = Vector2.zero;
            panelRt.anchorMax = Vector2.one;
            panelRt.offsetMin = panelRt.offsetMax = Vector2.zero;
            var dim = _panelRoot.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.72f);

            var card = CreatePanel(_panelRoot.transform, new Vector2(900, 620), new Color(0.12f, 0.12f, 0.16f, 0.98f));

            var title = CreateTmp(card.transform, "Title", new Vector2(0, 260), new Vector2(800, 40), 30);
            title.text = "名笔库";
            title.alignment = TextAlignmentOptions.Center;

            _goldText = CreateTmp(card.transform, "Gold", new Vector2(0, 220), new Vector2(800, 28), 20);
            _goldText.alignment = TextAlignmentOptions.Center;

            var poolLabel = CreateTmp(card.transform, "Pool", new Vector2(0, 180), new Vector2(800, 24), 18);
            poolLabel.text = "卡池：各品牌写字笔 | 蓝60% 紫25% 橙12% 红3%";
            poolLabel.alignment = TextAlignmentOptions.Center;

            _resultText = CreateTmp(card.transform, "Result", new Vector2(0, 20), new Vector2(820, 220), 18);
            _resultText.alignment = TextAlignmentOptions.TopLeft;
            _resultText.enableWordWrapping = true;

            _hintText = CreateTmp(card.transform, "Hint", new Vector2(0, -170), new Vector2(820, 28), 16);
            _hintText.alignment = TextAlignmentOptions.Center;

            CreateButton(card.transform, "Close", new Vector2(-120, -250), new Vector2(160, 48), "关闭", Close);
            CreateButton(card.transform, "Single", new Vector2(120, -250), new Vector2(160, 48), "单抽", () => DoPull(1));
            CreateButton(card.transform, "Ten", new Vector2(300, -250), new Vector2(160, 48), "十连", () => DoPull(10));

            _panelRoot.SetActive(false);
        }

        void DoPull(int count)
        {
            if (WeaponInventoryData.IsFull)
            {
                _hintText.text = "武器背包已满（36/36），请先出售。";
                return;
            }

            int freeSlots = WeaponInventoryData.MaxWeapons - WeaponInventoryData.OwnedWeaponIds.Count;
            if (count > freeSlots)
            {
                _hintText.text = $"背包仅剩 {freeSlots} 格，无法十连。";
                return;
            }

            if (!GachaService.CanPull(count))
            {
                _hintText.text = "金币不足。";
                return;
            }

            var results = GachaService.Roll(count);
            RefreshGold();
            if (results.Count == 0)
            {
                _hintText.text = "抽选失败，请稍后重试。";
                return;
            }

            var lines = new List<string>();
            for (int i = 0; i < results.Count; i++)
            {
                var w = results[i];
                lines.Add($"[{WeaponDatabase.GetRarityLabel(w.rarity)}] {w.FullDisplayName}");
            }
            _resultText.text = string.Join("\n", lines);
            _hintText.text = WeaponInventoryData.IsFull ? "背包已满。" : "已获得新笔款，可在背包武器栏查看。";
        }

        void RefreshGold()
        {
            var q = QuestSystemManager.Instance;
            _goldText.text = q != null ? $"当前金币：{q.CurrentGold}" : "当前金币：--";
        }

        GameObject CreatePanel(Transform parent, Vector2 size, Color color)
        {
            var go = new GameObject("Card", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            go.GetComponent<Image>().color = color;
            return go;
        }

        void CreateButton(Transform parent, string name, Vector2 pos, Vector2 size, string label, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            go.GetComponent<Image>().color = new Color(0.35f, 0.55f, 0.95f, 0.95f);
            go.GetComponent<Button>().onClick.AddListener(onClick);
            var txt = CreateTmp(go.transform, "Text", Vector2.zero, size, 20);
            txt.text = label;
            txt.alignment = TextAlignmentOptions.Center;
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
            if (_font != null)
            {
                tmp.font = _font;
                tmp.fontSharedMaterial = _font.material;
            }
            return tmp;
        }
    }
}
