using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace PhoneUI
{
    /// <summary>
    /// 手机壳 Rect 布局：屏幕右侧手机 + 淡金色内屏居中九宫格。
    /// </summary>
    public static class PhoneUiLayout
    {
        const float HudRightMargin = 40f;
        const float ShellWidth = 520f;
        const float ShellHeight = 920f;
        const float FrameWidth = 480f;
        const float FrameHeight = 900f;
        const float ShellFitMarginX = 16f;
        const float ShellFitMarginY = 18f;
        const float GoldInsetMarginH = 0.1f;
        const float GoldInsetMarginV = 0.16f;
        const float ShellScaleMultiplier = 0.82f;
        const float GridFill = 0.70f;
        const float GridScale = 1f;

        public static void ApplyLauncherHudLayout(PhoneSessionController session)
        {
            if (session == null || session.phoneShell == null)
                return;

            var shellRt = session.phoneShell.transform as RectTransform;
            if (shellRt != null)
            {
                var canvas = session.phonePanel != null
                    ? session.phonePanel.GetComponentInParent<Canvas>()
                    : session.phoneShell.GetComponentInParent<Canvas>();
                if (canvas != null)
                {
                    var canvasRt = canvas.transform as RectTransform;
                    if (canvasRt != null && shellRt.parent != canvasRt)
                        shellRt.SetParent(canvasRt, false);
                }

                shellRt.SetAsLastSibling();
                shellRt.anchorMin = new Vector2(1f, 0.5f);
                shellRt.anchorMax = new Vector2(1f, 0.5f);
                shellRt.pivot = new Vector2(1f, 0.5f);
                float scale = CalculateShellScale(session);
                float rightMargin = Mathf.Min(HudRightMargin, Mathf.Max(ShellFitMarginX, HudRightMargin * scale));
                shellRt.anchoredPosition = new Vector2(-rightMargin, 0f);
                shellRt.sizeDelta = new Vector2(ShellWidth, ShellHeight);
                shellRt.localScale = Vector3.one * scale;
                ZeroLocalZ(shellRt);

                var shellImg = session.phoneShell.GetComponent<Image>();
                if (shellImg != null)
                {
                    shellImg.color = new Color(1f, 1f, 1f, 0f);
                    shellImg.raycastTarget = false;
                }
            }

            HideLegacyLauncherTitle(session.phoneShell.transform);

            var goldInset = FindRect(session.phoneShell.transform, "PhoneScreenInset");
            var frame = session.phoneShell.transform.Find("PhoneFrame") as RectTransform;
            if (frame != null)
            {
                frame.SetAsLastSibling();
                frame.anchorMin = new Vector2(0.5f, 0.5f);
                frame.anchorMax = new Vector2(0.5f, 0.5f);
                frame.pivot = new Vector2(0.5f, 0.5f);
                frame.anchoredPosition = Vector2.zero;
                frame.sizeDelta = new Vector2(FrameWidth, FrameHeight);
                frame.localScale = Vector3.one;
                ZeroLocalZ(frame);

                var frameImg = frame.GetComponent<Image>();
                if (frameImg != null)
                    frameImg.enabled = false;
            }

            if (goldInset == null && frame != null)
            {
                goldInset = frame;
            }

            var appGrid = FindRect(session.phoneShell.transform, "AppGrid");
            if (appGrid != null && goldInset != null)
                ApplyAppGridCentered(appGrid, goldInset);

            var closeBtn = FindRect(session.phoneShell.transform, "Btn_Close");
            var closeParent = goldInset != null ? goldInset : frame;
            if (closeBtn != null && closeParent != null)
            {
                closeBtn.SetParent(closeParent, false);
                closeBtn.anchorMin = new Vector2(0.5f, 0f);
                closeBtn.anchorMax = new Vector2(0.5f, 0f);
                closeBtn.pivot = new Vector2(0.5f, 0f);
                closeBtn.anchoredPosition = new Vector2(0f, 12f);
                closeBtn.sizeDelta = new Vector2(220f, 42f);
                closeBtn.localScale = Vector3.one;
                ZeroLocalZ(closeBtn);
            }

            var dim = FindRect(session.phoneShell.transform, "DimBackground");
            if (dim != null)
                dim.gameObject.SetActive(false);

            if (goldInset != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(goldInset);
        }

        public static void ApplyAppGridCentered(RectTransform appGrid, RectTransform goldInset)
        {
            if (appGrid == null || goldInset == null)
                return;

            appGrid.SetParent(goldInset, false);
            appGrid.anchorMin = new Vector2(0.5f, 0.5f);
            appGrid.anchorMax = new Vector2(0.5f, 0.5f);
            appGrid.pivot = new Vector2(0.5f, 0.5f);
            appGrid.anchoredPosition = new Vector2(0f, 34f);
            appGrid.localScale = Vector3.one * GridScale;
            appGrid.localRotation = Quaternion.identity;

            float insetW = goldInset.rect.width > 1f ? goldInset.rect.width : goldInset.sizeDelta.x;
            float insetH = goldInset.rect.height > 1f ? goldInset.rect.height : goldInset.sizeDelta.y;
            if (insetW < 1f)
                insetW = FrameWidth * (1f - GoldInsetMarginH * 2f);
            if (insetH < 1f)
                insetH = FrameHeight * (1f - GoldInsetMarginV * 2f);

            appGrid.sizeDelta = new Vector2(insetW * GridFill, insetH * GridFill);

            var appGridImg = appGrid.GetComponent<Image>();
            if (appGridImg != null)
            {
                appGridImg.color = Color.clear;
                appGridImg.raycastTarget = false;
            }

            var slotsLayout = appGrid.Find("SlotsLayout") as RectTransform;
            if (slotsLayout == null)
            {
                var grid = appGrid.GetComponentInChildren<GridLayoutGroup>(true);
                if (grid != null)
                    slotsLayout = grid.transform as RectTransform;
            }

            if (slotsLayout != null)
            {
                slotsLayout.anchorMin = Vector2.zero;
                slotsLayout.anchorMax = Vector2.one;
                slotsLayout.pivot = new Vector2(0.5f, 0.5f);
                slotsLayout.offsetMin = new Vector2(10f, 10f);
                slotsLayout.offsetMax = new Vector2(-10f, -10f);
                slotsLayout.anchoredPosition = Vector2.zero;
                slotsLayout.localScale = Vector3.one;

                var glg = slotsLayout.GetComponent<GridLayoutGroup>();
                if (glg != null)
                {
                    glg.childAlignment = TextAnchor.MiddleCenter;
                    float padW = glg.padding.left + glg.padding.right;
                    float padH = glg.padding.top + glg.padding.bottom;
                    float areaW = appGrid.sizeDelta.x - padW - 10f;
                    float areaH = appGrid.sizeDelta.y - padH - 10f;
                    float cellW = (areaW - glg.spacing.x * 2f) / 3f;
                    float cellH = (areaH - glg.spacing.y * 2f) / 3f;
                    float cell = Mathf.Clamp(Mathf.Min(cellW, cellH), 56f, 84f);
                    glg.cellSize = new Vector2(cell, cell);
                }
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(appGrid);
        }

        public static void SanitizeManualShell(PhoneSessionController session) => ApplyLauncherHudLayout(session);

        public static void ApplyStarRailShellLayout(PhoneSessionController session) => ApplyLauncherHudLayout(session);

        static RectTransform FindRect(Transform root, string name)
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

        static float CalculateShellScale(PhoneSessionController session)
        {
            float canvasW = Screen.width;
            float canvasH = Screen.height;
            var canvas = session.phonePanel != null
                ? session.phonePanel.GetComponentInParent<Canvas>()
                : session.phoneShell.GetComponentInParent<Canvas>();
            var canvasRt = canvas != null ? canvas.transform as RectTransform : null;
            if (canvasRt != null && canvasRt.rect.width > 1f && canvasRt.rect.height > 1f)
            {
                canvasW = canvasRt.rect.width;
                canvasH = canvasRt.rect.height;
            }

            float maxW = Mathf.Max(120f, canvasW - ShellFitMarginX - HudRightMargin);
            float maxH = Mathf.Max(120f, canvasH - ShellFitMarginY * 2f);
            float scale = Mathf.Min(1f, maxW / ShellWidth, maxH / ShellHeight) * ShellScaleMultiplier;
            return Mathf.Clamp(scale, 0.45f, 0.9f);
        }

        static void HideLegacyLauncherTitle(Transform root)
        {
            if (root == null)
                return;

            var labels = root.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                var label = labels[i];
                if (label != null && label.text == "手机导览")
                    label.gameObject.SetActive(false);
            }
        }

        static void ZeroLocalZ(RectTransform rt)
        {
            if (rt == null)
                return;
            var p = rt.localPosition;
            if (Mathf.Abs(p.z) > 0.01f)
                rt.localPosition = new Vector3(p.x, p.y, 0f);
        }
    }
}
