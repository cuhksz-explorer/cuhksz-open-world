using GameControls;
using UnityEngine;

/// <summary>
/// 竞拍面板热键：<b>O</b>。与 <see cref="MultiplayerPanelHotkeyUtility"/> 相同，自动挂到 <see cref="GameServerSync"/>。
/// </summary>
public static class FlagAuctionPanelHotkeyUtility
{
    static int _lastFrame = -1;
    static bool _loggedBootstrap;

    public static FlagAuctionPanelController FindOrBootstrapPanel()
    {
        var c = Object.FindObjectOfType<FlagAuctionPanelController>(true);
        if (c != null) return c;
        if (GameServerSync.Instance == null) return null;
        var root = GameServerSync.Instance.gameObject;
        c = root.GetComponent<FlagAuctionPanelController>();
        if (c == null)
            c = root.AddComponent<FlagAuctionPanelController>();
        if (!_loggedBootstrap)
        {
            _loggedBootstrap = true;
            Debug.Log("[旗帜竞拍] 已在 GameServerSync 上挂载 FlagAuctionPanelController（O 打开/关闭）。");
        }

        return c;
    }

    public static void PollFromBindings() => Poll(GameControlAction.OpenFlagAuction);

    public static void Poll(KeyCode key)
    {
        if (GameplayUiHotkeyGuard.ShouldBlockDontDestroyOnLoadHotkeys()) return;
        if (!Input.GetKeyDown(key)) return;
        PollCore();
    }

    static void Poll(GameControlAction action)
    {
        if (GameplayUiHotkeyGuard.ShouldBlockDontDestroyOnLoadHotkeys()) return;
        if (!GameInput.GetKeyDown(action)) return;
        PollCore();
    }

    static void PollCore()
    {
        int f = Time.frameCount;
        if (f == _lastFrame) return;

        var panel = FindOrBootstrapPanel();
        if (panel == null) return;
        if (!panel.IsPanelOpen && GameplayUiHotkeyGuard.ShouldBlockGameplayHotkeys()) return;

        _lastFrame = f;
        panel.TogglePanel();
    }
}
