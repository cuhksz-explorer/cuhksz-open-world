using UnityEngine;

/// <summary>
/// 联机 UI 统一配色：白底、黑/金字；联机成功提示黑红交替。
/// </summary>
public static class MultiplayerUiPalette
{
    public static readonly Color BgWhite = Color.white;
    public static readonly Color BgSubtle = new Color(0.97f, 0.97f, 0.97f, 1f);
    public static readonly Color BgGoldTint = new Color(0.98f, 0.96f, 0.90f, 1f);
    public static readonly Color TextBlack = new Color(0.12f, 0.12f, 0.12f, 1f);
    public static readonly Color TextGold = new Color(0.79f, 0.66f, 0.28f, 1f);
    public static readonly Color TextMuted = new Color(0.45f, 0.45f, 0.45f, 1f);
    public static readonly Color TextRed = new Color(0.75f, 0.2f, 0.2f, 1f);
    public static readonly Color InputBg = new Color(0.97f, 0.97f, 0.97f, 1f);
    public static readonly Color SepLine = new Color(0.85f, 0.85f, 0.85f, 0.85f);
    public static readonly Color BtnPrimaryBg = new Color(0.18f, 0.18f, 0.18f, 1f);
    public static readonly Color BtnSecondaryBg = new Color(0.94f, 0.92f, 0.86f, 1f);
    public static readonly Color CloseBtnBg = new Color(0.92f, 0.92f, 0.92f, 1f);

    public static readonly Color ToggleGenerateBg = TextGold;
    public static readonly Color ToggleDisableBg = new Color(0.75f, 0.2f, 0.35f, 1f);
    public static readonly Color JoinLeaveJoinBg = BtnPrimaryBg;
    public static readonly Color JoinLeaveLeaveBg = new Color(0.55f, 0.25f, 0.25f, 1f);

    public static bool IsCoopSuccessStatus(string msg)
    {
        if (string.IsNullOrEmpty(msg)) return false;
        return msg.Contains("联机已就绪") || msg.Contains("已加入房间");
    }

    public static Color GetSuccessBlinkColor(float unscaledTime, float hz = 2f)
    {
        bool redPhase = (Mathf.FloorToInt(unscaledTime * hz) % 2) == 0;
        return redPhase ? TextRed : TextBlack;
    }
}
