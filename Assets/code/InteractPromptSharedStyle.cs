using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 统一各场景 <see cref="InteractPromptSharedUiLocator.PanelObjectName"/> 的尺寸为 480×320。
/// </summary>
public static class InteractPromptSharedStyle
{
    public const float PanelWidth = 480f;
    public const float PanelHeight = 320f;

    public const float NameFontSize = 34f;
    public const float TypeFontSize = 26f;
    public const float NameOutlineWidth = 0.25f;
    public const float TypeOutlineWidth = 0.2f;

    public const float NamePreferredHeight = 112f;
    public const float TypePreferredHeight = 88f;

    public const int LayoutPaddingHorizontal = 24;
    public const int LayoutPaddingVertical = 24;
    public const float LayoutSpacing = 8f;

    public const float ScreenPixelOffsetY = 24f;
    public const float ScreenEdgePadding = 40f;

    public static void ApplyIfNeeded(RectTransform panel)
    {
        if (panel == null || !InteractPromptSharedUiLocator.IsSharedPromptPanel(panel))
            return;

        panel.localScale = Vector3.one;
        panel.sizeDelta = new Vector2(PanelWidth, PanelHeight);

        var background = panel.Find("Background") as RectTransform;
        if (background != null)
        {
            background.anchorMin = Vector2.zero;
            background.anchorMax = Vector2.one;
            background.offsetMin = Vector2.zero;
            background.offsetMax = Vector2.zero;
            background.localScale = Vector3.one;
        }

        var content = panel.Find("Content");
        if (content != null)
        {
            var contentRt = content as RectTransform;
            if (contentRt != null)
            {
                contentRt.anchorMin = Vector2.zero;
                contentRt.anchorMax = Vector2.one;
                contentRt.offsetMin = Vector2.zero;
                contentRt.offsetMax = Vector2.zero;
                contentRt.localScale = Vector3.one;
            }

            var vlg = content.GetComponent<VerticalLayoutGroup>();
            if (vlg != null)
            {
                vlg.padding = new RectOffset(
                    LayoutPaddingHorizontal,
                    LayoutPaddingHorizontal,
                    LayoutPaddingVertical,
                    LayoutPaddingVertical);
                vlg.spacing = LayoutSpacing;
                vlg.childAlignment = TextAnchor.MiddleCenter;
            }
        }

        ApplyLineStyle(
            FindLineTmp(panel, "InteractNameText"),
            NameFontSize,
            NameOutlineWidth,
            NamePreferredHeight,
            FontStyles.Bold);
        ApplyLineStyle(
            FindLineTmp(panel, "InteractTypeText"),
            TypeFontSize,
            TypeOutlineWidth,
            TypePreferredHeight,
            FontStyles.Normal);

        if (panel.GetComponent<InteractPromptSharedStyleApplied>() == null)
            panel.gameObject.AddComponent<InteractPromptSharedStyleApplied>();

        ApplyFollowerSettings(panel);
    }

    static void ApplyFollowerSettings(RectTransform panel)
    {
        var follower = panel.GetComponent<InteractPromptSharedScreenFollower>();
        if (follower == null)
            return;

        follower.screenPixelOffset = new Vector2(0f, ScreenPixelOffsetY);
        follower.screenEdgePadding = ScreenEdgePadding;
    }

    static TextMeshProUGUI FindLineTmp(RectTransform panel, string objectName)
    {
        if (panel == null)
            return null;

        var transforms = panel.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform t = transforms[i];
            if (!string.Equals(t.name, objectName, System.StringComparison.Ordinal))
                continue;
            var tmp = t.GetComponent<TextMeshProUGUI>();
            if (tmp != null)
                return tmp;
        }

        return null;
    }

    static void ApplyLineStyle(TextMeshProUGUI tmp, float fontSize, float outlineWidth, float preferredHeight, FontStyles style)
    {
        if (tmp == null)
            return;

        if (tmp.font == null)
        {
            var fallback = Resources.Load<TMP_FontAsset>("Fonts & Materials/AlibabaPuHuiTi-3-75-SemiBold SDF");
            if (fallback != null)
                tmp.font = fallback;
        }

        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.outlineWidth = outlineWidth;

        var layout = tmp.GetComponent<LayoutElement>();
        if (layout != null)
            layout.preferredHeight = preferredHeight;
    }
}

[DisallowMultipleComponent]
sealed class InteractPromptSharedStyleApplied : MonoBehaviour
{
}
