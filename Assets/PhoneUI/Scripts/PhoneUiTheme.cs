using UnityEngine;

namespace PhoneUI
{
    /// <summary>手机 / 设置 UI：黄、黑、白配色。</summary>
    public static class PhoneUiTheme
    {
        public static readonly Color BgWhite = Color.white;
        public static readonly Color BgSoft = new Color(0.97f, 0.97f, 0.97f, 1f);
        public static readonly Color BgGoldTint = new Color(0.98f, 0.96f, 0.90f, 1f);
        public static readonly Color TextBlack = new Color(0.10f, 0.10f, 0.10f, 1f);
        public static readonly Color TextGold = new Color(0.79f, 0.66f, 0.28f, 1f);
        public static readonly Color TextMuted = new Color(0.45f, 0.45f, 0.45f, 1f);
        public static readonly Color BtnPrimaryBg = new Color(0.14f, 0.14f, 0.14f, 1f);
        public static readonly Color BtnGoldBg = new Color(0.79f, 0.66f, 0.28f, 1f);
        public static readonly Color BtnIdleBg = new Color(0.94f, 0.92f, 0.86f, 1f);
        public static readonly Color BorderGold = new Color(0.79f, 0.66f, 0.28f, 0.55f);
        public static readonly Color DimOverlay = new Color(0f, 0f, 0f, 0.42f);
        public static readonly Color SliderFill = TextGold;
        public static readonly Color SliderTrack = new Color(0.88f, 0.88f, 0.88f, 1f);

        public static readonly Color SettingsOverlayDim = new Color(0f, 0f, 0f, 0.55f);
        public static readonly Color SettingsPanelOpaque = new Color(1f, 1f, 1f, 1f);
        public static readonly Color SettingsSectionBg = new Color(0.96f, 0.95f, 0.93f, 1f);
        public static readonly Color SettingsSectionBorder = new Color(0.79f, 0.66f, 0.28f, 0.35f);
        public static readonly Color BtnQualityOff = new Color(0.92f, 0.90f, 0.84f, 1f);
        public static readonly Color BtnQualityOn = new Color(0.79f, 0.66f, 0.28f, 1f);
        public static readonly Color BtnQualityOnText = new Color(0.12f, 0.10f, 0.06f, 1f);
    }
}
