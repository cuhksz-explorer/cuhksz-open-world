using GameControls;
using UnityEngine;

/// <summary>
/// 联机 UI 热键：<b>K</b> 房间代码面板，<b>C</b> 好友/聊天侧栏。
/// 供 <see cref="GameServerSync"/> 与 <see cref="MultiplayerEntryToggle"/> 共用。
/// </summary>
public static class MultiplayerPanelHotkeyUtility
{
    static int _lastRoomFrame = -1;
    static int _lastChatFrame = -1;
    static bool _loggedBootstrapRoom;
    static bool _loggedBootstrapChat;

    public static MultiplayerRoomCodePanelController FindOrBootstrapRoomPanel()
    {
        var c = Object.FindObjectOfType<MultiplayerRoomCodePanelController>(true);
        if (c != null) return c;
        if (GameServerSync.Instance == null) return null;
        var root = GameServerSync.Instance.gameObject;
        c = root.GetComponent<MultiplayerRoomCodePanelController>();
        if (c == null)
            c = root.AddComponent<MultiplayerRoomCodePanelController>();
        if (!_loggedBootstrapRoom)
        {
            _loggedBootstrapRoom = true;
            Debug.Log("[联机] 已在 GameServerSync 上挂载 MultiplayerRoomCodePanelController（K 房间号面板）。");
        }
        return c;
    }

    public static MultiplayerFriendChatSidebarController FindOrBootstrapFriendChatSidebar()
    {
        var c = Object.FindObjectOfType<MultiplayerFriendChatSidebarController>(true);
        if (c != null) return c;
        if (GameServerSync.Instance == null) return null;
        var root = GameServerSync.Instance.gameObject;
        c = root.GetComponent<MultiplayerFriendChatSidebarController>();
        if (c == null)
            c = root.AddComponent<MultiplayerFriendChatSidebarController>();
        if (!_loggedBootstrapChat)
        {
            _loggedBootstrapChat = true;
            Debug.Log("[联机] 已在 GameServerSync 上挂载 MultiplayerFriendChatSidebarController（C 侧栏）。");
        }
        return c;
    }

    /// <summary>兼容旧代码：等同 <see cref="FindOrBootstrapRoomPanel"/>。</summary>
    public static MultiplayerHubController FindOrBootstrapHub()
    {
        return Object.FindObjectOfType<MultiplayerHubController>(true);
    }

    public static void PollRoomCodePanelHotkey(bool lockCursorWhenClosed) =>
        PollRoomCodePanelHotkey(GameControlAction.OpenMultiplayer, lockCursorWhenClosed);

    public static void PollRoomCodePanelHotkey(KeyCode key, bool lockCursorWhenClosed)
    {
        if (GameplayUiHotkeyGuard.ShouldBlockDontDestroyOnLoadHotkeys()) return;
        if (!Input.GetKeyDown(key)) return;
        PollRoomCodePanelHotkeyCore(lockCursorWhenClosed);
    }

    static void PollRoomCodePanelHotkey(GameControlAction action, bool lockCursorWhenClosed)
    {
        if (GameplayUiHotkeyGuard.ShouldBlockDontDestroyOnLoadHotkeys()) return;
        if (!GameInput.GetKeyDown(action)) return;
        PollRoomCodePanelHotkeyCore(lockCursorWhenClosed);
    }

    static void PollRoomCodePanelHotkeyCore(bool lockCursorWhenClosed)
    {
        int f = Time.frameCount;
        if (f == _lastRoomFrame) return;

        var panel = FindOrBootstrapRoomPanel();
        if (panel == null) return;
        // 保留“同键可关”体验：面板已开时，允许按原热键关闭；仅在尝试打开时受全局 UI 独占锁限制。
        if (!panel.IsPanelOpen && GameplayUiHotkeyGuard.ShouldBlockGameplayHotkeys()) return;

        _lastRoomFrame = f;
        panel.TogglePanel();
        ApplyCursorForRoomPanelState(panel, lockCursorWhenClosed);
    }

    public static void PollFriendChatSidebarHotkey(bool lockCursorWhenClosed) =>
        PollFriendChatSidebarHotkey(GameControlAction.OpenFriends, lockCursorWhenClosed);

    public static void PollFriendChatSidebarHotkey(KeyCode key, bool lockCursorWhenClosed)
    {
        if (GameplayUiHotkeyGuard.ShouldBlockDontDestroyOnLoadHotkeys()) return;
        if (!Input.GetKeyDown(key)) return;
        PollFriendChatSidebarHotkeyCore(lockCursorWhenClosed);
    }

    static void PollFriendChatSidebarHotkey(GameControlAction action, bool lockCursorWhenClosed)
    {
        if (GameplayUiHotkeyGuard.ShouldBlockDontDestroyOnLoadHotkeys()) return;
        if (!GameInput.GetKeyDown(action)) return;
        PollFriendChatSidebarHotkeyCore(lockCursorWhenClosed);
    }

    static void PollFriendChatSidebarHotkeyCore(bool lockCursorWhenClosed)
    {
        int f = Time.frameCount;
        if (f == _lastChatFrame) return;

        var bar = FindOrBootstrapFriendChatSidebar();
        if (bar == null) return;
        bool sidebarOpen = bar.IsOpen;
        // 同键关闭不被拦截；仅在“打开侧栏”时应用 UI 独占锁。
        if (!sidebarOpen && GameplayUiHotkeyGuard.ShouldBlockGameplayHotkeys()) return;

        var roomPanel = Object.FindObjectOfType<MultiplayerRoomCodePanelController>(true);
        if (!sidebarOpen && roomPanel != null && roomPanel.IsPanelOpen)
            return;

        _lastChatFrame = f;
        bar.ToggleSidebar();
        ApplyCursorForSidebarState(bar, lockCursorWhenClosed);
    }

    /// <summary>旧版 K 打开整页 Hub；已废弃，请用 <see cref="PollRoomCodePanelHotkey"/>。</summary>
    public static void PollHotkey(KeyCode key, bool lockCursorWhenClosed)
    {
        PollRoomCodePanelHotkey(key, lockCursorWhenClosed);
    }

    public static void ApplyCursorForRoomPanelState(MultiplayerRoomCodePanelController panel, bool lockCursorWhenClosed)
    {
        if (panel == null) return;
        bool sideOpen = Object.FindObjectOfType<MultiplayerFriendChatSidebarController>(true) is { } s && s.IsOpen;
        if (panel.IsPanelOpen || sideOpen)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else if (lockCursorWhenClosed)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    public static void ApplyCursorForSidebarState(MultiplayerFriendChatSidebarController sidebar, bool lockCursorWhenClosed)
    {
        if (sidebar == null) return;
        bool roomOpen = Object.FindObjectOfType<MultiplayerRoomCodePanelController>(true) is { } r && r.IsPanelOpen;
        if (sidebar.IsOpen || roomOpen)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else if (lockCursorWhenClosed)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}
