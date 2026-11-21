using GameControls;
using UnityEngine;
using UnityEngine.UI;

namespace PhoneUI
{
    /// <summary>1280×720 自定义按键面板；运行时 Ensure，与 <see cref="PhoneSettingsPanelView"/> 同级。</summary>
    [DisallowMultipleComponent]
    public class PhoneKeyBindingPanelView : MonoBehaviour
    {
        const int LayoutVersion = 1;
        const float DesignWidth = 1280f;
        const float DesignHeight = 720f;

        [SerializeField] int _layoutVersionBuilt;
        PhoneKeyBindingUI _ui;
        Canvas _rootCanvas;

        public static bool IsPageActive(PhoneSessionController session) =>
            session != null && session.keyBindingsPage != null && session.keyBindingsPage.activeSelf;

        public static PhoneKeyBindingPanelView Ensure(PhoneSessionController session)
        {
            if (session == null)
                return null;

            session.EnsureKeyBindingsPage();
            if (session.keyBindingsPage == null)
                return null;

            var page = session.keyBindingsPage;
            var view = page.GetComponent<PhoneKeyBindingPanelView>();
            if (view == null)
                view = page.AddComponent<PhoneKeyBindingPanelView>();
            view.BuildIfNeeded(session);
            return view;
        }

        void OnEnable() => Loc.OnLanguageChanged += OnLanguageChanged;
        void OnDisable() => Loc.OnLanguageChanged -= OnLanguageChanged;

        void OnLanguageChanged() => _ui?.RefreshLocalizedLabels();

        void Update()
        {
            _ui?.Update();
            UpdatePanelScale();
        }

        void BuildIfNeeded(PhoneSessionController session)
        {
            ApplyFullScreenShell(session);

            if (_ui != null && _ui.IsBuilt && _layoutVersionBuilt == LayoutVersion)
            {
                UpdatePanelScale();
                return;
            }

            _layoutVersionBuilt = LayoutVersion;
            var pageRt = session.keyBindingsPage.transform as RectTransform;
            _rootCanvas = session.keyBindingsPage.GetComponentInParent<Canvas>();

            var dim = CreateUiObject("KeyBindingsDim", pageRt);
            StretchFull(dim.GetComponent<RectTransform>());
            var dimImg = dim.AddComponent<Image>();
            dimImg.color = PhoneUiTheme.SettingsOverlayDim;
            dimImg.raycastTarget = true;

            _ui = new PhoneKeyBindingUI();
            _ui.Build(pageRt, () =>
            {
                if (session != null)
                    session.CloseKeyBindingsPage();
            });
            UpdatePanelScale();
        }

        void ApplyFullScreenShell(PhoneSessionController session)
        {
            var pageRt = session.keyBindingsPage.transform as RectTransform;
            StretchFull(pageRt);
            session.keyBindingsPage.transform.SetAsLastSibling();

            var pageBg = session.keyBindingsPage.GetComponent<Image>();
            if (pageBg != null)
            {
                pageBg.enabled = false;
                pageBg.raycastTarget = false;
            }

            var canvas = session.keyBindingsPage.GetComponentInParent<Canvas>();
            if (canvas != null)
                canvas.sortingOrder = Mathf.Max(canvas.sortingOrder, 3200);
        }

        void UpdatePanelScale()
        {
            if (_ui == null || !_ui.IsBuilt)
                return;

            float w = Screen.width;
            float h = Screen.height;
            if (_rootCanvas != null)
            {
                var canvasRt = _rootCanvas.transform as RectTransform;
                if (canvasRt != null && canvasRt.rect.width > 1f && canvasRt.rect.height > 1f)
                {
                    w = canvasRt.rect.width;
                    h = canvasRt.rect.height;
                }
            }

            var panelRoot = sessionKeyBindingsPanelRoot();
            if (panelRoot == null)
                return;
            float scale = Mathf.Min(w / DesignWidth, h / DesignHeight);
            panelRoot.localScale = Vector3.one * scale;
        }

        RectTransform sessionKeyBindingsPanelRoot()
        {
            var page = GetComponent<RectTransform>();
            if (page == null)
                return null;
            var child = page.Find("KeyBindingPanelRoot");
            return child != null ? child as RectTransform : null;
        }

        public void Show()
        {
            _ui?.Show();
            GameInput.SetKeyBindingsPanelOpen(true);
            UpdatePanelScale();
        }

        public void Hide()
        {
            _ui?.Hide();
            GameInput.SetKeyBindingsPanelOpen(false);
        }

        static GameObject CreateUiObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
