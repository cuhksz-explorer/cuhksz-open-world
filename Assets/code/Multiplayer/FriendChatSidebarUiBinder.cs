using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 挂在场景里的 <b>FriendChatSidebar</b> 根物体上，由生成菜单或运行时构建写入引用，
/// 供 <see cref="MultiplayerFriendChatSidebarController"/> 在 Awake 时绑定（便于把面板放在场景里手动调 RectTransform）。
/// </summary>
[DisallowMultipleComponent]
public class FriendChatSidebarUiBinder : MonoBehaviour
{
    public TMP_FontAsset font;
    public TextMeshProUGUI statusText;
    public TextMeshProUGUI pendingFriendsText;
    public TextMeshProUGUI chatHeaderTitle;
    public TMP_InputField addFriendInput;
    public TMP_InputField friendRequestIdInput;
    public TMP_InputField chatInput;
    public Transform friendListContent;
    public Transform chatBubbleContent;
    public ScrollRect chatMessageScroll;

    [Header("可选：公告 / 全服喇叭（世界频道）")]
    public TextMeshProUGUI announcementBody;
    public TMP_InputField hornInput;
    public GameObject hornRow;
}
