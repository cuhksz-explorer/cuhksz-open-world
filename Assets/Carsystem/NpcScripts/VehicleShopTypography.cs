using TMPro;
using UnityEngine;

/// <summary>
/// 选车界面统一使用阿里巴巴普惠体。
/// </summary>
public static class VehicleShopTypography
{
    const string FontResourcesPath = "Fonts & Materials/AlibabaPuHuiTi-3-75-SemiBold SDF";

    static TMP_FontAsset _font;

    public static TMP_FontAsset Font
    {
        get
        {
            if (_font == null)
                _font = Resources.Load<TMP_FontAsset>(FontResourcesPath);
            return _font;
        }
    }

    public static void Apply(TMP_Text text)
    {
        if (text == null || Font == null)
            return;
        text.font = Font;
    }

    public static void ApplyAllInChildren(Transform root)
    {
        if (root == null)
            return;
        var texts = root.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
            Apply(texts[i]);
    }
}
