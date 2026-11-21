using UnityEngine;
using UnityEngine.UI;

namespace PhoneUI
{
    /// <summary>
    /// 手机九宫格子界面（设置 / 联机）打开时的 Rect 排版与层级修正。
    /// </summary>
    public static class PhoneUiOverlayLayout
    {
        const float HudRightMargin = 40f;
        const float SettingsPanelWidth = 460f;
        const float SettingsPanelHeight = 820f;

        const float MultiplayerInsetX = 0.08f;
        const float MultiplayerInsetY = 0.1f;

        public static void ApplySettingsPageLayout(PhoneSessionController session)
        {
            PhoneSettingsPanelView.Ensure(session)?.RefreshBindings();
        }

        public static void ApplyMultiplayerPanelForPhone(GameObject panelRoot, ref Transform defaultParent)
        {
            if (panelRoot == null)
                return;

            var session = PhoneSessionController.Instance;
            if (session == null || !PhoneSessionController.IsPhoneOpen || session.phonePanel == null)
                return;

            var panelRt = panelRoot.GetComponent<RectTransform>();
            if (panelRt == null)
                return;

            if (defaultParent == null)
                defaultParent = panelRoot.transform.parent;

            panelRoot.transform.SetParent(session.phonePanel.transform, false);
            panelRoot.transform.SetAsLastSibling();

            float ix = Mathf.Clamp(MultiplayerInsetX, 0f, 0.45f);
            float iy = Mathf.Clamp(MultiplayerInsetY, 0f, 0.45f);
            panelRt.anchorMin = new Vector2(ix, iy);
            panelRt.anchorMax = new Vector2(1f - ix, 1f - iy);
            panelRt.pivot = new Vector2(0.5f, 0.5f);
            panelRt.offsetMin = Vector2.zero;
            panelRt.offsetMax = Vector2.zero;
            panelRt.anchoredPosition = Vector2.zero;
            panelRt.sizeDelta = Vector2.zero;
            panelRt.localScale = Vector3.one;

            var canvas = session.phonePanel.GetComponentInParent<Canvas>();
            if (canvas != null)
                canvas.sortingOrder = Mathf.Max(canvas.sortingOrder, 3100);

            LayoutRebuilder.ForceRebuildLayoutImmediate(panelRt);
        }

        public static void RestoreMultiplayerPanelParent(GameObject panelRoot, Transform defaultParent)
        {
            if (panelRoot == null || defaultParent == null)
                return;
            if (panelRoot.transform.parent == defaultParent)
                return;

            panelRoot.transform.SetParent(defaultParent, false);
        }

        static void LayoutSettingsRow(RectTransform rt, float height)
        {
            if (rt == null)
                return;

            rt.localScale = Vector3.one;
            StretchInLayout(rt);
            var le = rt.GetComponent<LayoutElement>();
            if (le == null)
                le = rt.gameObject.AddComponent<LayoutElement>();
            le.minHeight = height;
            le.preferredHeight = height;
            le.flexibleHeight = 0f;
            le.flexibleWidth = 1f;
        }

        static void LayoutSettingsBlock(RectTransform rt, float height)
        {
            if (rt == null)
                return;

            rt.localScale = Vector3.one;
            StretchInLayout(rt);
            var le = rt.GetComponent<LayoutElement>();
            if (le == null)
                le = rt.gameObject.AddComponent<LayoutElement>();
            le.minHeight = height;
            le.preferredHeight = height;
            le.flexibleHeight = 0f;
            le.flexibleWidth = 1f;
        }

        static void StretchInLayout(RectTransform rt)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(0f, rt.sizeDelta.y > 1f ? rt.sizeDelta.y : 100f);
        }

        static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
            rt.localScale = Vector3.one;
        }

        static RectTransform FindChildRect(Transform root, string name)
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
