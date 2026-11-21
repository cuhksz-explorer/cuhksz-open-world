using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PhoneUI
{
    /// <summary>运行时构建黑/白/紫/金手机外框与状态栏（保留原有九宫格与登出按钮）。</summary>
    [DisallowMultipleComponent]
    public class PhoneUiChromeView : MonoBehaviour
    {
        static readonly Color BgOuter = new Color(0.08f, 0.08f, 0.08f, 1f);
        static readonly Color BgScreen = PhoneUiTheme.BgWhite;
        static readonly Color AccentGold = PhoneUiTheme.TextGold;
        static readonly Color AccentPurple = new Color(0.79f, 0.66f, 0.28f, 0.18f);
        static readonly Color TextPrimary = PhoneUiTheme.TextBlack;
        static readonly Color TextMuted = PhoneUiTheme.TextMuted;

        RectTransform _screenInset;
        TMP_Text _timeText;
        TMP_Text _signalText;
        TMP_Text _batteryText;
        TMP_Text _carrierText;
        bool _built;

        public static PhoneUiChromeView Ensure(PhoneSessionController session)
        {
            if (session == null || session.phoneShell == null)
                return null;

            var view = session.phoneShell.GetComponent<PhoneUiChromeView>();
            if (view == null)
                view = session.phoneShell.AddComponent<PhoneUiChromeView>();
            view.BuildIfNeeded(session);
            return view;
        }

        void LateUpdate()
        {
            if (_timeText != null)
                _timeText.text = PhoneUiWorldClock.FormatGameTime();
        }

        void BuildIfNeeded(PhoneSessionController session)
        {
            if (_built)
            {
                if (_screenInset != null)
                    ReparentContentIntoScreen(session, _screenInset);
                return;
            }

            var shellRt = session.phoneShell.transform as RectTransform;
            if (shellRt == null)
                return;

            var legacyFrame = shellRt.Find("PhoneFrame") as RectTransform;
            if (legacyFrame != null)
            {
                var legacyImg = legacyFrame.GetComponent<Image>();
                if (legacyImg != null)
                    legacyImg.enabled = false;
            }

            var backdrop = EnsureChild(shellRt, "PhoneChromeBackdrop", out var backdropRt);
            SetupStretchImage(backdrop, BgOuter);
            backdropRt.SetAsFirstSibling();

            var glowSide = EnsureChild(backdropRt, "PhonePurpleGlow", out var glowRt);
            glowRt.anchorMin = new Vector2(0f, 0.2f);
            glowRt.anchorMax = new Vector2(0.35f, 0.95f);
            glowRt.offsetMin = Vector2.zero;
            glowRt.offsetMax = Vector2.zero;
            SetupStretchImage(glowSide, AccentPurple);

            var accentTop = EnsureChild(backdropRt, "PhoneGoldTopLine", out var accentRt);
            accentRt.anchorMin = new Vector2(0.05f, 1f);
            accentRt.anchorMax = new Vector2(0.95f, 1f);
            accentRt.pivot = new Vector2(0.5f, 1f);
            accentRt.sizeDelta = new Vector2(0f, 3f);
            accentRt.anchoredPosition = new Vector2(0f, -10f);
            SetupStretchImage(accentTop, AccentGold);

            var statusBar = EnsureChild(backdropRt, "PhoneStatusBar", out var statusRt);
            statusRt.anchorMin = new Vector2(0.04f, 1f);
            statusRt.anchorMax = new Vector2(0.96f, 1f);
            statusRt.pivot = new Vector2(0.5f, 1f);
            statusRt.sizeDelta = new Vector2(0f, 52f);
            statusRt.anchoredPosition = new Vector2(0f, -36f);

            _timeText = EnsureStatusText(statusRt, "StatusTime", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), TextAlignmentOptions.MidlineLeft);
            _carrierText = EnsureStatusText(statusRt, "StatusCarrier", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), TextAlignmentOptions.Center);
            _carrierText.text = "CEC LINK";
            _carrierText.fontSize = 16;
            _carrierText.color = AccentGold;
            _signalText = EnsureStatusText(statusRt, "StatusSignal", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), TextAlignmentOptions.MidlineRight);
            _signalText.rectTransform.anchoredPosition = new Vector2(-72f, 0f);
            _batteryText = EnsureStatusText(statusRt, "StatusBattery", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), TextAlignmentOptions.MidlineRight);
            _batteryText.rectTransform.anchoredPosition = new Vector2(0f, 0f);

            if (_timeText != null)
                _timeText.text = PhoneUiWorldClock.FormatGameTime();
            if (_signalText != null)
            {
                _signalText.text = PhoneUiWorldClock.FormatSignalBars();
                _signalText.color = TextMuted;
                _signalText.fontSize = 18;
            }
            if (_batteryText != null)
            {
                _batteryText.text = PhoneUiWorldClock.FormatBattery();
                _batteryText.color = TextPrimary;
                _batteryText.fontSize = 18;
            }

            EnsureChild(backdropRt, "PhoneScreenInset", out var insetRt);
            _screenInset = insetRt;
            insetRt.anchorMin = new Vector2(0.08f, 0.14f);
            insetRt.anchorMax = new Vector2(0.92f, 0.86f);
            insetRt.offsetMin = Vector2.zero;
            insetRt.offsetMax = Vector2.zero;
            SetupStretchImage(_screenInset.gameObject, BgScreen);

            var border = EnsureChild(insetRt, "PhoneScreenBorder", out var borderRt);
            SetupStretch(borderRt);
            var borderImg = border.GetComponent<Image>();
            if (borderImg == null)
                borderImg = border.AddComponent<Image>();
            borderImg.color = new Color(AccentGold.r, AccentGold.g, AccentGold.b, 0.35f);
            borderImg.raycastTarget = false;

            var notch = EnsureChild(backdropRt, "PhoneNotch", out var notchRt);
            notchRt.anchorMin = new Vector2(0.5f, 1f);
            notchRt.anchorMax = new Vector2(0.5f, 1f);
            notchRt.pivot = new Vector2(0.5f, 1f);
            notchRt.sizeDelta = new Vector2(120f, 18f);
            notchRt.anchoredPosition = new Vector2(0f, -8f);
            SetupStretchImage(notch, new Color(0.08f, 0.08f, 0.1f, 1f));

            ReparentContentIntoScreen(session, insetRt);
            insetRt.SetAsLastSibling();
            _built = true;
        }

        static void ReparentContentIntoScreen(PhoneSessionController session, RectTransform insetRt)
        {
            var closeBtn = FindDeep(session.phoneShell.transform, "Btn_Close");
            if (closeBtn != null)
            {
                closeBtn.SetParent(insetRt, false);
                closeBtn.anchorMin = new Vector2(0.5f, 0f);
                closeBtn.anchorMax = new Vector2(0.5f, 0f);
                closeBtn.pivot = new Vector2(0.5f, 0f);
                closeBtn.anchoredPosition = new Vector2(0f, 12f);
                closeBtn.sizeDelta = new Vector2(220f, 42f);
            }
        }

        static GameObject EnsureChild(RectTransform parent, string name, out RectTransform rt)
        {
            var existing = parent.Find(name) as RectTransform;
            if (existing != null)
            {
                rt = existing;
                return existing.gameObject;
            }

            var go = new GameObject(name, typeof(RectTransform));
            rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
            return go;
        }

        static void SetupStretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
        }

        static void SetupStretchImage(GameObject go, Color color)
        {
            var rt = go.GetComponent<RectTransform>();
            if (rt != null)
                SetupStretch(rt);
            var img = go.GetComponent<Image>();
            if (img == null)
                img = go.AddComponent<Image>();
            img.sprite = null;
            img.type = Image.Type.Simple;
            img.color = color;
            img.raycastTarget = false;
        }

        static TMP_Text EnsureStatusText(RectTransform bar, string name, Vector2 anchorMin, Vector2 anchorMax, TextAlignmentOptions align)
        {
            var existing = bar.Find(name);
            if (existing != null)
            {
                var tmp = existing.GetComponent<TMP_Text>();
                if (tmp != null)
                    return tmp;
            }

            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(bar, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = anchorMin;
            rt.sizeDelta = new Vector2(160f, 40f);
            rt.anchoredPosition = Vector2.zero;
            var text = go.AddComponent<TextMeshProUGUI>();
            text.fontSize = 22;
            text.color = TextPrimary;
            text.alignment = align;
            text.raycastTarget = false;
            PhoneUiAssetLoader.ApplyTmpFont(text);
            return text;
        }

        static RectTransform FindDeep(Transform root, string name)
        {
            if (root == null)
                return null;
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
