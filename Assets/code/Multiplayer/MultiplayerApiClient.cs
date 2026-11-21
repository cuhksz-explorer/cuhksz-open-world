using System;
using System.Collections;
using System.Text;
using Multiplayer;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// 调用 GameServer 联机相关 REST（好友 / 聊天 / 邀请信令）。需已登录 <see cref="GameServerSync"/>。
/// </summary>
[DisallowMultipleComponent]
public class MultiplayerApiClient : MonoBehaviour
{
    string BaseUrl => GameServerSync.Instance != null ? GameServerSync.Instance.BaseUrlTrimmed : "";
    string Token => GameServerSync.Instance != null ? GameServerSync.Instance.AuthTokenForExtensions : "";

    public bool IsReady => GameServerSync.Instance != null && GameServerSync.Instance.IsLoggedIn;

    public IEnumerator CoMe(Action<MeResponseDto> onOk, Action<string> onErr)
    {
        if (!IsReady) { onErr?.Invoke("未登录"); yield break; }
        string url = $"{BaseUrl}/api/me?token={UnityWebRequest.EscapeURL(Token)}";
        using (var req = UnityWebRequest.Get(url))
        {
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                onErr?.Invoke(req.error + " " + req.downloadHandler.text);
                yield break;
            }
            var r = JsonUtility.FromJson<MeResponseDto>(req.downloadHandler.text);
            onOk?.Invoke(r ?? new MeResponseDto());
        }
    }

    public IEnumerator CoSetMyNickname(string nickname, Action onOk, Action<string> onErr)
    {
        if (!IsReady) { onErr?.Invoke("未登录"); yield break; }
        string url = $"{BaseUrl}/api/me/nickname";
        string nick = (nickname ?? "").Trim();
        string json = JsonUtility.ToJson(new SetNicknameWire { token = Token, nickname = nick });
        using (var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
        {
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                onErr?.Invoke(req.error + " " + req.downloadHandler.text);
                yield break;
            }
        }
        onOk?.Invoke();
    }

    public IEnumerator CoFriendsRequest(string targetUsername, Action onOk, Action<string> onErr)
    {
        if (!IsReady) { onErr?.Invoke("未登录"); yield break; }
        string url = $"{BaseUrl}/api/friends/request?token={UnityWebRequest.EscapeURL(Token)}";
        string json = JsonUtility.ToJson(new FriendRequestWire { target_username = targetUsername.Trim() });
        using (var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
        {
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                onErr?.Invoke(req.error + " " + req.downloadHandler.text);
                yield break;
            }
        }
        onOk?.Invoke();
    }

    public IEnumerator CoFriendsPending(Action<FriendsPendingResponse> onOk, Action<string> onErr)
    {
        if (!IsReady) { onErr?.Invoke("未登录"); yield break; }
        string url = $"{BaseUrl}/api/friends/pending?token={UnityWebRequest.EscapeURL(Token)}";
        using (var req = UnityWebRequest.Get(url))
        {
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                onErr?.Invoke(req.error);
                yield break;
            }
            var r = JsonUtility.FromJson<FriendsPendingResponse>(req.downloadHandler.text);
            onOk?.Invoke(r ?? new FriendsPendingResponse());
        }
    }

    public IEnumerator CoFriendsRespond(int requestId, bool accept, Action onOk, Action<string> onErr)
    {
        if (!IsReady) { onErr?.Invoke("未登录"); yield break; }
        string url = $"{BaseUrl}/api/friends/respond?token={UnityWebRequest.EscapeURL(Token)}";
        string json = JsonUtility.ToJson(new FriendRespondWire { request_id = requestId, accept = accept });
        using (var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
        {
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                onErr?.Invoke(req.error + " " + req.downloadHandler.text);
                yield break;
            }
        }
        onOk?.Invoke();
    }

    public IEnumerator CoFriendsList(Action<FriendsListResponse> onOk, Action<string> onErr)
    {
        if (!IsReady) { onErr?.Invoke("未登录"); yield break; }
        string url = $"{BaseUrl}/api/friends/list?token={UnityWebRequest.EscapeURL(Token)}";
        using (var req = UnityWebRequest.Get(url))
        {
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                onErr?.Invoke(req.error);
                yield break;
            }
            var r = JsonUtility.FromJson<FriendsListResponse>(req.downloadHandler.text);
            onOk?.Invoke(r ?? new FriendsListResponse());
        }
    }

    public IEnumerator CoChatSend(int toUserId, string text, Action onOk, Action<string> onErr)
    {
        if (!IsReady) { onErr?.Invoke("未登录"); yield break; }
        string url = $"{BaseUrl}/api/chat/send";
        string json = JsonUtility.ToJson(new ChatSendWire { token = Token, to_user_id = toUserId, text = text });
        using (var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
        {
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                onErr?.Invoke(req.error + " " + req.downloadHandler.text);
                yield break;
            }
        }
        onOk?.Invoke();
    }

    public IEnumerator CoChatInbox(int sinceId, Action<ChatInboxResponse> onOk, Action<string> onErr)
    {
        if (!IsReady) { onErr?.Invoke("未登录"); yield break; }
        string url = $"{BaseUrl}/api/chat/inbox?token={UnityWebRequest.EscapeURL(Token)}&since_id={sinceId}";
        using (var req = UnityWebRequest.Get(url))
        {
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                onErr?.Invoke(req.error);
                yield break;
            }
            var r = JsonUtility.FromJson<ChatInboxResponse>(req.downloadHandler.text);
            onOk?.Invoke(r ?? new ChatInboxResponse());
        }
    }

    public IEnumerator CoCoopInvite(int toUserId, Action<int> onOk, Action<string> onErr)
    {
        if (!IsReady) { onErr?.Invoke("未登录"); yield break; }
        string url = $"{BaseUrl}/api/coop/invite";
        string json = JsonUtility.ToJson(new CoopInviteCreateWire { token = Token, to_user_id = toUserId });
        using (var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
        {
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                onErr?.Invoke(req.error + " " + req.downloadHandler.text);
                yield break;
            }
            var wr = JsonUtility.FromJson<CoopInviteCreateRespWire>(req.downloadHandler.text);
            if (wr != null && wr.invite_id > 0)
                onOk?.Invoke(wr.invite_id);
            else
                onErr?.Invoke("无效响应");
        }
    }

    public IEnumerator CoCoopAccept(int inviteId, HostConnectPayload hostPayload, Action onOk, Action<string> onErr)
    {
        if (!IsReady) { onErr?.Invoke("未登录"); yield break; }
        string url = $"{BaseUrl}/api/coop/invite/accept";
        string inner = JsonUtility.ToJson(hostPayload);
        string json = JsonUtility.ToJson(new CoopAcceptWire
        {
            token = Token,
            invite_id = inviteId,
            host_connect_json = inner
        });
        using (var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
        {
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                onErr?.Invoke(req.error + " " + req.downloadHandler.text);
                yield break;
            }
        }
        onOk?.Invoke();
    }

    public IEnumerator CoCoopDecline(int inviteId, Action onOk, Action<string> onErr)
    {
        if (!IsReady) { onErr?.Invoke("未登录"); yield break; }
        string url = $"{BaseUrl}/api/coop/invite/decline";
        string json = JsonUtility.ToJson(new CoopDeclineWire { token = Token, invite_id = inviteId });
        using (var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
        {
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                onErr?.Invoke(req.error + " " + req.downloadHandler.text);
                yield break;
            }
        }
        onOk?.Invoke();
    }

    public IEnumerator CoCoopOutstanding(Action<CoopOutstandingResponse> onOk, Action<string> onErr)
    {
        if (!IsReady) { onErr?.Invoke("未登录"); yield break; }
        string url = $"{BaseUrl}/api/coop/invite/outstanding?token={UnityWebRequest.EscapeURL(Token)}";
        using (var req = UnityWebRequest.Get(url))
        {
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                onErr?.Invoke(req.error);
                yield break;
            }
            var r = JsonUtility.FromJson<CoopOutstandingResponse>(req.downloadHandler.text);
            onOk?.Invoke(r ?? new CoopOutstandingResponse());
        }
    }

    public IEnumerator CoCoopAck(int inviteId, Action onOk, Action<string> onErr)
    {
        if (!IsReady) { onErr?.Invoke("未登录"); yield break; }
        string url = $"{BaseUrl}/api/coop/invite/ack";
        string json = JsonUtility.ToJson(new CoopAckWire { token = Token, invite_id = inviteId });
        using (var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
        {
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                onErr?.Invoke(req.error + " " + req.downloadHandler.text);
                yield break;
            }
        }
        onOk?.Invoke();
    }

    public IEnumerator CoCoopPending(Action<CoopPendingResponse> onOk, Action<string> onErr)
    {
        if (!IsReady) { onErr?.Invoke("未登录"); yield break; }
        string url = $"{BaseUrl}/api/coop/pending?token={UnityWebRequest.EscapeURL(Token)}";
        using (var req = UnityWebRequest.Get(url))
        {
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                onErr?.Invoke(req.error);
                yield break;
            }
            var r = JsonUtility.FromJson<CoopPendingResponse>(req.downloadHandler.text);
            onOk?.Invoke(r ?? new CoopPendingResponse());
        }
    }

    public IEnumerator CoHostPublish(int port, string advertisedAddress, Action<CoopHostPublishResponse> onOk, Action<string> onErr)
    {
        if (!IsReady) { onErr?.Invoke("未登录"); yield break; }
        string url = $"{BaseUrl}/api/coop/host/publish";
        string adv = advertisedAddress != null ? advertisedAddress.Trim() : "";
        if (string.IsNullOrEmpty(adv))
        {
            onErr?.Invoke("advertised_address 必填，请先填写对外 IP/域名");
            yield break;
        }
        string json = JsonUtility.ToJson(new CoopHostPublishWire
        {
            token = Token,
            port = port,
            advertised_address = adv,
            transport = "Telepathy"
        });
        using (var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
        {
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                onErr?.Invoke(req.error + " " + req.downloadHandler.text);
                yield break;
            }
            var r = JsonUtility.FromJson<CoopHostPublishResponse>(req.downloadHandler.text);
            onOk?.Invoke(r ?? new CoopHostPublishResponse());
        }
    }

    public IEnumerator CoHostUnpublish(Action onOk, Action<string> onErr)
    {
        if (!IsReady) { onErr?.Invoke("未登录"); yield break; }
        string url = $"{BaseUrl}/api/coop/host/unpublish";
        string json = JsonUtility.ToJson(new CoopHostUnpublishWire { token = Token });
        using (var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
        {
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                onErr?.Invoke(req.error + " " + req.downloadHandler.text);
                yield break;
            }
        }
        onOk?.Invoke();
    }

    public IEnumerator CoCoopHostResolve(int hostUserId, Action<CoopHostResolveResponse> onOk, Action<string> onErr)
    {
        if (!IsReady) { onErr?.Invoke("未登录"); yield break; }
        string url = $"{BaseUrl}/api/coop/host/resolve?token={UnityWebRequest.EscapeURL(Token)}&host_user_id={hostUserId}";
        using (var req = UnityWebRequest.Get(url))
        {
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                onErr?.Invoke(req.error + " " + req.downloadHandler.text);
                yield break;
            }
            var r = JsonUtility.FromJson<CoopHostResolveResponse>(req.downloadHandler.text);
            onOk?.Invoke(r ?? new CoopHostResolveResponse());
        }
    }

    public IEnumerator CoPublicRoomCreate(Action<CoopPublicRoomCreateResponse> onOk, Action<string> onErr)
    {
        if (!IsReady) { onErr?.Invoke("未登录"); yield break; }
        string url = $"{BaseUrl}/api/coop/public-room/create";
        string json = JsonUtility.ToJson(new PublicRoomCreateWire { token = Token });
        using (var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
        {
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                onErr?.Invoke(req.error + " " + req.downloadHandler.text);
                yield break;
            }
            var r = JsonUtility.FromJson<CoopPublicRoomCreateResponse>(req.downloadHandler.text);
            onOk?.Invoke(r ?? new CoopPublicRoomCreateResponse());
        }
    }

    public IEnumerator CoPublicRoomBind(string roomCode, int port, string advertisedAddress, Action<CoopHostPublishResponse> onOk, Action<string> onErr)
    {
        if (!IsReady) { onErr?.Invoke("未登录"); yield break; }
        string url = $"{BaseUrl}/api/coop/public-room/bind";
        string adv = advertisedAddress != null ? advertisedAddress.Trim() : "";
        if (string.IsNullOrEmpty(adv))
        {
            onErr?.Invoke("advertised_address 必填，请先填写对外 IP/域名");
            yield break;
        }
        string code = roomCode != null ? roomCode.Trim().ToUpperInvariant() : "";
        string json = JsonUtility.ToJson(new PublicRoomBindWire
        {
            token = Token,
            room_code = code,
            port = port,
            advertised_address = adv,
            transport = "Telepathy"
        });
        using (var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
        {
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                onErr?.Invoke(req.error + " " + req.downloadHandler.text);
                yield break;
            }
            var r = JsonUtility.FromJson<CoopHostPublishResponse>(req.downloadHandler.text);
            onOk?.Invoke(r ?? new CoopHostPublishResponse());
        }
    }

    public IEnumerator CoPublicRoomClose(string roomCode, Action onOk, Action<string> onErr)
    {
        if (!IsReady) { onErr?.Invoke("未登录"); yield break; }
        string url = $"{BaseUrl}/api/coop/public-room/close";
        string code = roomCode != null ? roomCode.Trim().ToUpperInvariant() : "";
        string json = JsonUtility.ToJson(new PublicRoomCloseWire { token = Token, room_code = code });
        using (var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
        {
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                onErr?.Invoke(req.error + " " + req.downloadHandler.text);
                yield break;
            }
        }
        onOk?.Invoke();
    }

    public IEnumerator CoPublicRoomResolve(string roomCode, Action<CoopPublicRoomResolveResponse> onOk, Action<string> onErr)
    {
        if (!IsReady) { onErr?.Invoke("未登录"); yield break; }
        string code = UnityWebRequest.EscapeURL(roomCode != null ? roomCode.Trim().ToUpperInvariant() : "");
        string url = $"{BaseUrl}/api/coop/public-room/resolve?token={UnityWebRequest.EscapeURL(Token)}&room_code={code}";
        using (var req = UnityWebRequest.Get(url))
        {
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                onErr?.Invoke(req.error + " " + req.downloadHandler.text);
                yield break;
            }
            var r = JsonUtility.FromJson<CoopPublicRoomResolveResponse>(req.downloadHandler.text);
            onOk?.Invoke(r ?? new CoopPublicRoomResolveResponse());
        }
    }

    public IEnumerator CoSceneShardEnter(string sceneName, Action<SceneShardStatusResponse> onOk, Action<string> onErr)
    {
        if (!IsReady) { onErr?.Invoke("未登录"); yield break; }
        string url = $"{BaseUrl}/api/shard/enter";
        string json = JsonUtility.ToJson(new SceneShardEnterWire { token = Token, scene_name = sceneName });
        using (var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
        {
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                onErr?.Invoke(req.error + " " + req.downloadHandler.text);
                yield break;
            }
            var r = JsonUtility.FromJson<SceneShardStatusResponse>(req.downloadHandler.text);
            onOk?.Invoke(r ?? new SceneShardStatusResponse());
        }
    }

    public IEnumerator CoSceneShardHeartbeat(string sceneName, string shardId, Action<SceneShardStatusResponse> onOk, Action<string> onErr)
    {
        if (!IsReady) { onErr?.Invoke("未登录"); yield break; }
        string url = $"{BaseUrl}/api/shard/heartbeat";
        string json = JsonUtility.ToJson(new SceneShardHeartbeatWire { token = Token, scene_name = sceneName, shard_id = shardId });
        using (var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
        {
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                onErr?.Invoke(req.error + " " + req.downloadHandler.text);
                yield break;
            }
            var r = JsonUtility.FromJson<SceneShardStatusResponse>(req.downloadHandler.text);
            onOk?.Invoke(r ?? new SceneShardStatusResponse());
        }
    }

    public IEnumerator CoSceneShardLeave(string sceneName, Action onOk, Action<string> onErr)
    {
        if (!IsReady) { onErr?.Invoke("未登录"); yield break; }
        string url = $"{BaseUrl}/api/shard/leave";
        string json = JsonUtility.ToJson(new SceneShardLeaveWire { token = Token, scene_name = sceneName });
        using (var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
        {
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                onErr?.Invoke(req.error + " " + req.downloadHandler.text);
                yield break;
            }
        }
        onOk?.Invoke();
    }

    public IEnumerator CoSceneShardStatus(string sceneName, Action<SceneShardStatusResponse> onOk, Action<string> onErr)
    {
        if (!IsReady) { onErr?.Invoke("未登录"); yield break; }
        string sceneEscaped = UnityWebRequest.EscapeURL(sceneName ?? "");
        string url = $"{BaseUrl}/api/shard/status?token={UnityWebRequest.EscapeURL(Token)}&scene_name={sceneEscaped}";
        using (var req = UnityWebRequest.Get(url))
        {
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                onErr?.Invoke(req.error + " " + req.downloadHandler.text);
                yield break;
            }
            var r = JsonUtility.FromJson<SceneShardStatusResponse>(req.downloadHandler.text);
            onOk?.Invoke(r ?? new SceneShardStatusResponse());
        }
    }

    public IEnumerator CoProximityChatSend(string sceneName, int toUserId, string text, Action onOk, Action<string> onErr)
    {
        if (!IsReady) { onErr?.Invoke("未登录"); yield break; }
        string url = $"{BaseUrl}/api/proximity-chat/send";
        string json = JsonUtility.ToJson(new ProximityChatSendWire
        {
            token = Token,
            scene_name = sceneName,
            to_user_id = toUserId,
            text = text
        });
        using (var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
        {
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                onErr?.Invoke(req.error + " " + req.downloadHandler.text);
                yield break;
            }
        }
        onOk?.Invoke();
    }

    public IEnumerator CoProximityChatInbox(string sceneName, int sinceId, Action<ProximityChatInboxResponse> onOk, Action<string> onErr)
    {
        if (!IsReady) { onErr?.Invoke("未登录"); yield break; }
        string sceneEscaped = UnityWebRequest.EscapeURL(sceneName ?? "");
        string url =
            $"{BaseUrl}/api/proximity-chat/inbox?token={UnityWebRequest.EscapeURL(Token)}&scene_name={sceneEscaped}&since_id={sinceId}";
        using (var req = UnityWebRequest.Get(url))
        {
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                onErr?.Invoke(req.error + " " + req.downloadHandler.text);
                yield break;
            }
            var r = JsonUtility.FromJson<ProximityChatInboxResponse>(req.downloadHandler.text);
            onOk?.Invoke(r ?? new ProximityChatInboxResponse());
        }
    }

    [Serializable] class FriendRequestWire { public string target_username; }
    [Serializable] class FriendRespondWire { public int request_id; public bool accept; }
    [Serializable] class ChatSendWire { public string token; public int to_user_id; public string text; }
    [Serializable] class CoopInviteCreateWire { public string token; public int to_user_id; }
    [Serializable] class CoopInviteCreateRespWire { public bool ok; public int invite_id; }
    [Serializable] class CoopAcceptWire { public string token; public int invite_id; public string host_connect_json; }
    [Serializable] class CoopDeclineWire { public string token; public int invite_id; }
    [Serializable] class CoopAckWire { public string token; public int invite_id; }
    [Serializable] class CoopHostPublishWire { public string token; public int port; public string advertised_address; public string transport; }
    [Serializable] class CoopHostUnpublishWire { public string token; }
    [Serializable] class PublicRoomCreateWire { public string token; }
    [Serializable] class PublicRoomBindWire { public string token; public string room_code; public int port; public string advertised_address; public string transport; }
    [Serializable] class PublicRoomCloseWire { public string token; public string room_code; }
    [Serializable] class SceneShardEnterWire { public string token; public string scene_name; }
    [Serializable] class SceneShardHeartbeatWire { public string token; public string scene_name; public string shard_id; }
    [Serializable] class SceneShardLeaveWire { public string token; public string scene_name; }
    [Serializable] class ProximityChatSendWire { public string token; public string scene_name; public int to_user_id; public string text; }
    [Serializable] class SetNicknameWire { public string token; public string nickname; }
}
