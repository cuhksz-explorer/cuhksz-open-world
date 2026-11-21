using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 血条/体力条数值文本：自动查找 HPamount/STamount，并挂到 Slider 手柄上跟随移动。
/// </summary>
static class BarAmountLabelUtil
{
    public static TMP_Text FindLabel(Transform searchFrom, params string[] objectNames)
    {
        if (searchFrom == null || objectNames == null || objectNames.Length == 0)
            return null;

        var canvas = searchFrom.GetComponentInParent<Canvas>();
        Transform root = canvas != null ? canvas.transform : searchFrom.root;

        foreach (TMP_Text tmp in root.GetComponentsInChildren<TMP_Text>(true))
        {
            if (tmp == null)
                continue;

            string goName = tmp.gameObject.name;
            for (int i = 0; i < objectNames.Length; i++)
            {
                if (string.Equals(goName, objectNames[i], System.StringComparison.OrdinalIgnoreCase))
                    return tmp;
            }
        }

        return null;
    }

    /// <summary>将数值 TMP 挂到 Slider 的 handle 上，使其随条移动。</summary>
    public static void AttachLabelToHandle(TMP_Text label, Slider slider, Vector2 anchoredOffset)
    {
        if (label == null || slider == null || slider.handleRect == null)
            return;

        RectTransform rt = label.rectTransform;
        rt.SetParent(slider.handleRect, false);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(1f, 0.5f);
        rt.anchoredPosition = anchoredOffset;
        rt.localRotation = Quaternion.identity;
        rt.localScale = Vector3.one;
    }
}
