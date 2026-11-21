using UnityEngine;

/// <summary>
/// 联机房间号面板入口：UI 按钮 OnClick 绑定 <see cref="Toggle"/>；默认按 <b>K</b> 开关面板。
/// </summary>
public class MultiplayerEntryToggle : MonoBehaviour
{
    [SerializeField] MultiplayerRoomCodePanelController roomPanel;

    [Header("热键")]
    [SerializeField] bool enableHotkey = true;
    [SerializeField] KeyCode toggleKey = KeyCode.K;

    [Tooltip("关闭房间号面板后锁定并隐藏鼠标（若好友侧栏仍打开则保持解锁）")]
    [SerializeField] bool lockCursorWhenClosed = true;

    public void Toggle()
    {
        EnsureRoomPanel();
        if (roomPanel == null) return;
        roomPanel.TogglePanel();
        MultiplayerPanelHotkeyUtility.ApplyCursorForRoomPanelState(roomPanel, lockCursorWhenClosed);
    }

    void Update()
    {
        if (!enableHotkey) return;
        MultiplayerPanelHotkeyUtility.PollRoomCodePanelHotkey(toggleKey, lockCursorWhenClosed);
    }

    void OnDestroy()
    {
        if (lockCursorWhenClosed && roomPanel != null && roomPanel.IsPanelOpen)
            ApplyCursorForPanelClosed();
    }

    void EnsureRoomPanel()
    {
        if (roomPanel == null)
            roomPanel = MultiplayerPanelHotkeyUtility.FindOrBootstrapRoomPanel();
    }

    static void ApplyCursorForPanelClosed()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}
