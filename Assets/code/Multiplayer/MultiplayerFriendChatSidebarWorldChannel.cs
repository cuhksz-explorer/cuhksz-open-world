using System;
using System.Collections.Generic;
using GameQuestSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class MultiplayerFriendChatSidebarController
{
    public enum ChatChannelKind
    {
        Friend,
        World
    }

    [SerializeField] string[] gameAnnouncements =
    {
        "欢迎来到游戏。",
        "【世界频道】每次发言消耗 5 金币，10 秒内只能发送一次，最多 50 字。",
        "【全服喇叭】消耗 100 金币，当前场景在线玩家屏幕中上方会飘过一行字（最多 40 字）。"
    };

    ChatChannelKind _channelKind = ChatChannelKind.Friend;

    struct WorldLine
    {
        public int UserId;
        public string Speaker;
        public string Body;
    }

    readonly List<WorldLine> _worldLines = new List<WorldLine>();
    const int MaxWorldLines = 250;

    TextMeshProUGUI _announcementBody;
    GameObject _hornRow;
    TMP_InputField _hornInput;
    float _nextWorldChatClientOkAt;

    void WorldChannel_ApplyBinder(FriendChatSidebarUiBinder b)
    {
        if (b == null) return;
        if (b.announcementBody != null)
            _announcementBody = b.announcementBody;
        if (b.hornInput != null)
            _hornInput = b.hornInput;
        if (b.hornRow != null)
            _hornRow = b.hornRow;
    }

    void WorldChannel_CompleteSetup()
    {
        WorldChannel_RefreshAnnouncementText();
        SetChatChannel(ChatChannelKind.Friend);
    }

    void WorldChannel_RegisterNetEvents()
    {
        CoopNetworkPlayer.OnWorldChatMessage += WorldChannel_OnWorldLine;
    }

    void WorldChannel_UnregisterNetEvents()
    {
        CoopNetworkPlayer.OnWorldChatMessage -= WorldChannel_OnWorldLine;
    }

    void WorldChannel_OnWorldLine(int uid, string speaker, string body)
    {
        _worldLines.Add(new WorldLine
        {
            UserId = uid,
            Speaker = speaker ?? "",
            Body = body ?? ""
        });
        while (_worldLines.Count > MaxWorldLines)
            _worldLines.RemoveAt(0);
        if (_channelKind == ChatChannelKind.World && sidebarRoot != null && sidebarRoot.activeSelf)
            WorldChannel_RefreshWorldBubbles();
    }

    bool WorldChannel_IsWorldMode() => _channelKind == ChatChannelKind.World;

    public void SetChatChannel(ChatChannelKind k)
    {
        _channelKind = k;
        if (_hornRow != null)
            _hornRow.SetActive(k == ChatChannelKind.World);
        if (_chatInput != null)
        {
            _chatInput.characterLimit = k == ChatChannelKind.World ? CoopNetworkPlayer.WorldChatMaxChars : 0;
            var ph = _chatInput.placeholder as TextMeshProUGUI;
            if (ph != null)
            {
                ph.text = k == ChatChannelKind.World
                    ? Loc.Choose("世界频道（5金币，10秒冷却，50字内）", "World chat (5 gold, 10s cooldown, 50 chars)")
                    : Loc.Choose("发消息…", "Type a message…");
            }
        }
        if (_chatHeaderTitle != null)
        {
            if (k == ChatChannelKind.World)
                _chatHeaderTitle.text = Loc.F(
                    "世界频道 · {0}金币/条 · 10秒间隔",
                    "World chat · {0} gold/msg · 10s cooldown",
                    CoopNetworkPlayer.WorldChatGoldCost);
            else
                RefreshChatHeader();
        }
        if (k == ChatChannelKind.World)
            WorldChannel_RefreshWorldBubbles();
        else
            RefreshChatBubbles();
    }

    void WorldChannel_RefreshAnnouncementText()
    {
        if (_announcementBody == null) return;
        var arr = gameAnnouncements != null && gameAnnouncements.Length > 0
            ? gameAnnouncements
            : Array.Empty<string>();
        if (LocalizationService.IsEnglish)
        {
            _announcementBody.text = Loc.Choose(
                "欢迎来到游戏。\n【世界频道】每次发言消耗 5 金币，10 秒内只能发送一次，最多 50 字。\n【全服喇叭】消耗 100 金币，当前场景在线玩家屏幕中上方会飘过一行字（最多 40 字）。",
                "Welcome to the game.\n[World chat] Each message costs 5 gold, 10s cooldown, max 50 chars.\n[Server horn] Costs 100 gold; a banner scrolls at the top for players in this scene (max 40 chars).");
            return;
        }
        _announcementBody.text = string.Join("\n", arr);
    }

    void WorldChannel_RefreshWorldBubbles()
    {
        if (_chatBubbleContent == null) return;
        ClearBubbleChildren();
        if (_runtimeFont == null) return;
        for (int i = 0; i < _worldLines.Count; i++)
        {
            var L = _worldLines[i];
            HubStyle.AddWorldChannelLine(_chatBubbleContent, L.Speaker, L.Body, _runtimeFont);
        }
        Canvas.ForceUpdateCanvases();
        ScrollChatToBottom();
    }

    void WorldChannel_UiSendWorld()
    {
        if (_chatInput == null) return;
        string t = _chatInput.text != null ? _chatInput.text.Trim() : "";
        if (string.IsNullOrEmpty(t)) return;
        if (t.Length > CoopNetworkPlayer.WorldChatMaxChars)
            t = t.Substring(0, CoopNetworkPlayer.WorldChatMaxChars);

        var q = QuestSystemManager.Instance;
        if (q != null && q.CurrentGold < CoopNetworkPlayer.WorldChatGoldCost)
        {
            SetStatus(Loc.F("金币不足（需要 {0}）", "Not enough gold (need {0})", CoopNetworkPlayer.WorldChatGoldCost));
            return;
        }
        if (Time.unscaledTime < _nextWorldChatClientOkAt)
        {
            SetStatus(Loc.F("请等待 {0} 秒后再发", "Please wait {0}s before sending again", Mathf.CeilToInt(_nextWorldChatClientOkAt - Time.unscaledTime)));
            return;
        }

        CoopNetworkPlayer.SendWorldChatFromUi(t);
        _nextWorldChatClientOkAt = Time.unscaledTime + CoopNetworkPlayer.WorldChatCooldownSeconds;
        _chatInput.text = "";
        SetStatus("");
    }

    public void UiSendServerHorn()
    {
        if (_hornInput == null) return;
        string t = _hornInput.text != null ? _hornInput.text.Trim() : "";
        if (string.IsNullOrEmpty(t)) return;
        if (t.Length > CoopNetworkPlayer.ServerHornMaxChars)
            t = t.Substring(0, CoopNetworkPlayer.ServerHornMaxChars);

        var q = QuestSystemManager.Instance;
        if (q != null && q.CurrentGold < CoopNetworkPlayer.ServerHornGoldCost)
        {
            SetStatus($"金币不足（需要 {CoopNetworkPlayer.ServerHornGoldCost}）");
            return;
        }

        CoopNetworkPlayer.SendServerHornFromUi(t);
        _hornInput.text = "";
        SetStatus("");
    }
}
