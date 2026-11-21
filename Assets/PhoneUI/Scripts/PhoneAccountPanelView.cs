using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PhoneUI
{
    /// <summary>账户信息简页（黄/黑/白）。</summary>
    public static class PhoneAccountPanelView
    {
        const float PanelWidth = 420f;
        const float PanelHeight = 280f;

        static GameObject _root;
        static TextMeshProUGUI _titleText;
        static TextMeshProUGUI _bodyText;
        static TextMeshProUGUI _backButtonText;
        static bool _subscribed;

        public static void Show(PhoneSessionController session)
        {
            if (session == null || session.phonePanel == null)
                return;

            Hide();
            session.EnterOverlay(PhoneAppId.Account);

            _root = new GameObject("PhoneAccountPanel", typeof(RectTransform));
            var rt = _root.GetComponent<RectTransform>();
            rt.SetParent(session.phonePanel.transform, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(PanelWidth, PanelHeight);

            var bg = _root.AddComponent<Image>();
            bg.color = PhoneUiTheme.BgWhite;

            var border = new GameObject("Border", typeof(RectTransform));
            border.transform.SetParent(_root.transform, false);
            Stretch(border.GetComponent<RectTransform>());
            var borderImg = border.AddComponent<Image>();
            borderImg.color = PhoneUiTheme.BorderGold;
            borderImg.raycastTarget = false;

            _titleText = CreateTmp(_root.transform, Loc.Choose("账户", "Account"), 24, PhoneUiTheme.TextGold, FontStyles.Bold, new Vector2(0f, 90f));
            _titleText.alignment = TextAlignmentOptions.Center;

            _bodyText = CreateTmp(_root.transform, BuildBodyText(), 18, PhoneUiTheme.TextBlack, FontStyles.Normal, new Vector2(0f, 20f));
            _bodyText.alignment = TextAlignmentOptions.Center;

            CreateBackButton(_root.transform, session, new Vector2(0f, -90f));
            EnsureSubscribed();
        }

        public static void Hide()
        {
            if (_root != null)
                Object.Destroy(_root);
            _root = null;
            _titleText = null;
            _bodyText = null;
            _backButtonText = null;
            Unsubscribe();
        }

        static void EnsureSubscribed()
        {
            if (_subscribed)
                return;
            Loc.OnLanguageChanged += Refresh;
            _subscribed = true;
        }

        static void Unsubscribe()
        {
            if (!_subscribed)
                return;
            Loc.OnLanguageChanged -= Refresh;
            _subscribed = false;
        }

        static void Refresh()
        {
            if (_root == null)
                return;

            if (_titleText != null)
                _titleText.text = Loc.Choose("账户", "Account");
            if (_bodyText != null)
                _bodyText.text = BuildBodyText();
            if (_backButtonText != null)
                _backButtonText.text = Loc.Choose("返回", "Back");
        }

        static string BuildBodyText()
        {
            string user = GameServerSync.Instance != null ? GameServerSync.Instance.SavedUsername : "";
            if (string.IsNullOrEmpty(user))
                user = Loc.Choose("未登录", "Not logged in");
            return Loc.F("当前账号\n{0}", "Current account\n{0}", user);
        }

        static void CreateBackButton(Transform parent, PhoneSessionController session, Vector2 pos)
        {
            var go = new GameObject("BtnBack", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(140f, 40f);
            var img = go.AddComponent<Image>();
            img.color = PhoneUiTheme.BtnGoldBg;
            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(() =>
            {
                Hide();
                session.ShowLauncher();
            });
            _backButtonText = CreateTmp(go.transform, Loc.Choose("返回", "Back"), 16, PhoneUiTheme.TextBlack, FontStyles.Normal, Vector2.zero);
            _backButtonText.alignment = TextAlignmentOptions.Center;
            Stretch(_backButtonText.rectTransform);
        }

        static TextMeshProUGUI CreateTmp(Transform parent, string text, float size, Color color, FontStyles fs, Vector2 anchoredPos)
        {
            var go = new GameObject("Txt", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(360f, 80f);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = fs;
            tmp.color = color;
            PhoneUiAssetLoader.ApplyTmpFont(tmp);
            return tmp;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
