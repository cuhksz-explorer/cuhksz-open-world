using UnityEngine;

/// <summary>
/// 兼容旧场景与引用：实际 UI 已拆至 <see cref="MultiplayerRoomCodePanelController"/>（K）与
/// <see cref="MultiplayerFriendChatSidebarController"/>（C）。本类方法均转发到房间号面板。
/// </summary>
[DisallowMultipleComponent]
public class MultiplayerHubController : MonoBehaviour
{
    MultiplayerRoomCodePanelController ResolveRoom()
    {
        var r = GetComponent<MultiplayerRoomCodePanelController>();
        if (r != null) return r;
        return GetComponentInParent<MultiplayerRoomCodePanelController>()
               ?? FindObjectOfType<MultiplayerRoomCodePanelController>(true);
    }

    public bool IsPanelOpen
    {
        get
        {
            var r = ResolveRoom();
            return r != null && r.IsPanelOpen;
        }
    }

    public void TogglePanel()
    {
        var r = ResolveRoom() ?? MultiplayerPanelHotkeyUtility.FindOrBootstrapRoomPanel();
        r?.TogglePanel();
    }

    public void OpenPanel()
    {
        var r = ResolveRoom() ?? MultiplayerPanelHotkeyUtility.FindOrBootstrapRoomPanel();
        r?.OpenPanel();
    }

    public void ClosePanel()
    {
        ResolveRoom()?.ClosePanel();
    }
}
