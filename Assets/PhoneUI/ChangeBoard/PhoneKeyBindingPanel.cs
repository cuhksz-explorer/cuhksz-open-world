using GameControls;
using UnityEngine;

namespace PhoneUI
{
    /// <summary>
    /// 兼容层：转发到 <see cref="PhoneKeyBindingPanelView"/> / <see cref="PhoneSessionController"/>。
    /// </summary>
    public class PhoneKeyBindingPanel : MonoBehaviour
    {
        public static PhoneKeyBindingPanel Instance { get; private set; }

        [Tooltip("可选；为空时由 Session.EnsureKeyBindingsPage 创建。")]
        public RectTransform contentRoot;

        public static bool IsOpen =>
            PhoneSessionController.Instance != null
            && PhoneKeyBindingPanelView.IsPageActive(PhoneSessionController.Instance);

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
            GameInput.SetKeyBindingsPanelOpen(false);
            GameInput.IsRebinding = false;
        }

        void Update()
        {
            var view = PhoneSessionController.Instance != null
                ? PhoneSessionController.Instance.keyBindingsPage?.GetComponent<PhoneKeyBindingPanelView>()
                : null;
            // Update handled on PanelView
        }

        public void Show()
        {
            PhoneSessionController.Instance?.OpenKeyBindingsPage();
        }

        public void Hide()
        {
            PhoneSessionController.Instance?.CloseKeyBindingsPage();
        }

        public static void ShowPanel()
        {
            if (PhoneSessionController.Instance != null)
            {
                PhoneSessionController.Instance.OpenKeyBindingsPage();
                return;
            }

            var session = PhoneSessionController.FindBestInScene();
            session?.OpenKeyBindingsPage();
        }

        public static void HidePanel()
        {
            PhoneSessionController.Instance?.CloseKeyBindingsPage();
        }
    }
}
