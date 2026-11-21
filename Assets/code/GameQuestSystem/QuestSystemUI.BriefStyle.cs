using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

namespace GameQuestSystem
{
    public partial class QuestSystemUI
    {
        [Header("简要栏样式")]
        [Tooltip("进入任意场景时，将简要任务栏布局/字号统一为 undergarden（下园）标准。")]
        [SerializeField] bool enforceUndergardenBriefStyle = true;

        const float UndergardenBriefPanelScale = 0.3125f;
        static readonly Vector2 UndergardenBriefPanelSize = new Vector2(400f, 400f);
        static readonly Vector2 UndergardenBriefPanelAnchoredPos = new Vector2(1.40625f, 12.3f);
        const int UndergardenBriefLineFontSize = 35;
        const float BriefBandHorizontalPadding = 8f;

        void ApplyUndergardenBriefPanelStyleIfNeeded()
        {
            if (!enforceUndergardenBriefStyle || briefPanel == null)
                return;

            showBriefDistanceText = false;
            useManualBriefLineFontSizes = true;
            autoFitByPanelSize = false;
            useInspectorFontSizeAsBase = false;
            briefDistanceLineFontSize = UndergardenBriefLineFontSize;
            briefTitleLineFontSize = UndergardenBriefLineFontSize;
            briefProgressLineFontSize = UndergardenBriefLineFontSize;
            briefSceneLineFontSize = 28;
            briefIntroLineFontSize = UndergardenBriefLineFontSize;

            var panelRt = briefPanel.GetComponent<RectTransform>();
            if (panelRt != null)
            {
                panelRt.anchorMin = new Vector2(0f, 0.5f);
                panelRt.anchorMax = new Vector2(0f, 0.5f);
                panelRt.pivot = new Vector2(0f, 0.5f);
                panelRt.anchoredPosition = UndergardenBriefPanelAnchoredPos;
                panelRt.sizeDelta = UndergardenBriefPanelSize;
                panelRt.localScale = Vector3.one * UndergardenBriefPanelScale;
            }

            var panelImage = briefPanel.GetComponent<Image>();
            if (panelImage != null)
                panelImage.color = new Color(0f, 0f, 0f, 0.5f);

            ApplyUndergardenBriefTextBand(briefDistanceText, -35f, 70f, visible: false);
            ApplyUndergardenBriefTextBand(briefTitleText, -52f, 88f, visible: true);
            ApplyUndergardenBriefTextBand(briefProgressText, -122f, 44f, visible: true);
            EnsureBriefSceneText();
            if (briefSceneText != null && briefIntroText != null)
                briefSceneText.transform.SetSiblingIndex(briefIntroText.transform.GetSiblingIndex());
            ApplyUndergardenBriefTextBand(briefSceneText, -172f, 36f, visible: true);
            if (briefSceneText != null)
            {
                briefSceneText.fontSize = briefSceneLineFontSize;
                briefSceneText.resizeTextMinSize = briefSceneLineFontSize;
                briefSceneText.resizeTextMaxSize = briefSceneLineFontSize;
                briefSceneText.color = new Color(0.82f, 0.80f, 0.74f, 1f);
            }
            ApplyUndergardenBriefTextBand(briefIntroText, -300f, 180f, visible: true);

            ApplyBriefDistanceRowVisibility();
            ApplyBriefTextLayout();
        }

        void EnsureBriefSceneText()
        {
            if (briefPanel == null)
                return;

            if (briefSceneText != null && briefSceneText.transform.IsChildOf(briefPanel.transform))
                return;

            var existing = briefPanel.transform.Find("SceneText");
            if (existing != null)
            {
                briefSceneText = existing.GetComponent<Text>();
                if (briefIntroText != null)
                    existing.SetSiblingIndex(briefIntroText.transform.GetSiblingIndex());
                if (briefSceneText != null)
                    briefSceneText.supportRichText = false;
                return;
            }

            var go = new GameObject("SceneText", typeof(Text));
            go.transform.SetParent(briefPanel.transform, false);
            if (briefIntroText != null)
                go.transform.SetSiblingIndex(briefIntroText.transform.GetSiblingIndex());
            briefSceneText = go.GetComponent<Text>();
            briefSceneText.font = questUIFont != null
                ? questUIFont
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            briefSceneText.supportRichText = false;
        }

        static string NormalizeBriefIntroText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            text = text.Trim();
            text = text.Replace("\r\n", "\n").Replace('\r', '\n');
            while (text.Contains("\n\n"))
                text = text.Replace("\n\n", "\n");

            // 避免「按 V」在窄栏里把空格单独折到下一行，视觉上像多了一行空行。
            return Regex.Replace(text, @"(?<=[\u4e00-\u9fff\u3001\u3002\uff0c\uFF01\uFF1f按])\s+(?=[A-Za-z0-9])", "\u00A0");
        }

#if UNITY_EDITOR
        /// <summary>编辑器批量同步场景时调用。</summary>
        public void EditorApplyUndergardenBriefPanelStyle()
        {
            enforceUndergardenBriefStyle = true;
            ApplyUndergardenBriefPanelStyleIfNeeded();
        }
#endif

        public static void ApplyUndergardenBriefStyleToAllInLoadedScenes()
        {
            var all = Object.FindObjectsOfType<QuestSystemUI>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null)
                    all[i].ApplyUndergardenBriefPanelStyleIfNeeded();
            }
        }

        static void ApplyUndergardenBriefTextBand(Text text, float topOffsetY, float height, bool visible)
        {
            if (text == null)
                return;

            text.gameObject.SetActive(visible);
            var rt = text.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, topOffsetY);
            rt.sizeDelta = new Vector2(0f, height);
            rt.offsetMin = new Vector2(BriefBandHorizontalPadding, rt.offsetMin.y);
            rt.offsetMax = new Vector2(-BriefBandHorizontalPadding, rt.offsetMax.y);
            rt.localScale = Vector3.one;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = false;
            text.resizeTextMinSize = UndergardenBriefLineFontSize;
            text.resizeTextMaxSize = UndergardenBriefLineFontSize;
            text.fontSize = UndergardenBriefLineFontSize;
            text.supportRichText = false;
        }
    }
}
