using System;
using UnityEngine;

namespace Multiplayer
{
    [Serializable] public class MeResponseDto { public int user_id; public string username; public string nickname; }

    [Serializable] public class FriendEntryDto { public int user_id; public string username; }
    [Serializable] public class FriendsListResponse { public FriendEntryDto[] friends; }

    [Serializable] public class PendingInboundDto
    {
        public int id;
        public int from_user_id;
        public string from_username;
        public float created_at;
    }

    [Serializable] public class PendingOutboundDto
    {
        public int id;
        public int to_user_id;
        public string to_username;
        public float created_at;
    }

    [Serializable] public class FriendsPendingResponse
    {
        public PendingInboundDto[] inbound;
        public PendingOutboundDto[] outbound;
    }

    [Serializable] public class ChatMessageDto
    {
        public int id;
        public int from_user_id;
        public int to_user_id;
        public string from_username;
        public string to_username;
        public string body;
        public float created_at;
    }

    [Serializable] public class ChatInboxResponse { public ChatMessageDto[] messages; }

    [Serializable] public class CoopPendingInviteDto
    {
        public int id;
        public int from_user_id;
        public string from_username;
        public float created_at;
        public float expires_at;
    }

    [Serializable] public class CoopPendingResponse { public CoopPendingInviteDto[] invites; }

    [Serializable]
    public class CoopOutstandingInviteDto
    {
        public int id;
        public int from_user_id;
        public int to_user_id;
        public string status;
        public string host_connect_json;
        public float created_at;
        public float expires_at;
    }

    [Serializable]
    public class CoopOutstandingResponse
    {
        public CoopOutstandingInviteDto invite;
    }

    [Serializable] public class HostConnectPayload
    {
        public string transport = "Telepathy";
        public string address;
        public int port;
    }

    /// <summary>POST /api/coop/host/publish 响应：effective_ip 为主机显式上报并登记到云端的地址。</summary>
    [Serializable]
    public class CoopHostPublishResponse
    {
        public bool ok;
        public string effective_ip;
        public int advertised_port;
        public string transport;
        public float expires_at;
    }

    /// <summary>GET /api/coop/host/resolve 响应：好友查询对方当前登记的主机地址。</summary>
    [Serializable]
    public class CoopHostResolveResponse
    {
        public bool ok;
        public bool has_host;
        public string effective_ip;
        public int advertised_port;
        public string transport;
        public float expires_at;
    }

    /// <summary>POST /api/coop/public-room/create</summary>
    [Serializable]
    public class CoopPublicRoomCreateResponse
    {
        public bool ok;
        public string room_code;
        public float expires_at;
    }

    /// <summary>GET /api/coop/public-room/resolve（无需好友）</summary>
    [Serializable]
    public class CoopPublicRoomResolveResponse
    {
        public bool ok;
        public bool has_room;
        public bool has_host;
        public string effective_ip;
        public int advertised_port;
        public string transport;
        public float expires_at;
    }

    [Serializable]
    public class SceneShardStatusResponse
    {
        public bool ok;
        public bool has_assignment;
        public bool assigned;
        public string scene_name;
        public string shard_id;
        public int online_count;
        public int capacity;
        public string status_label;
        public float expires_at;
    }

    [Serializable]
    public class ProximityChatMessageDto
    {
        public int id;
        public int from_user_id;
        public int to_user_id;
        public string from_username;
        public string to_username;
        public string scene_name;
        public string body;
        public float created_at;
    }

    [Serializable]
    public class ProximityChatInboxResponse
    {
        public ProximityChatMessageDto[] messages;
    }
}
