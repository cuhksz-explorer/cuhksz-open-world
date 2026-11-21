using Mirror;

/// <summary>
/// 局域网联机会话状态：同意邀请的一方为 Host（Server+Client），另一方仅 Client。
/// 客方暂停向 GameServer 写入位置与任务存档（由主机权威）。
/// </summary>
public static class CoopSessionState
{
    public static bool InSession => NetworkClient.active;

    public static bool IsSessionHost => NetworkServer.active && NetworkClient.active;

    public static bool IsSessionGuest => NetworkClient.active && !NetworkServer.active;

    /// <summary>客方不应向 HTTP 游戏服上报位置、任务、金币等。</summary>
    public static bool BlockGuestCloudWrites => IsSessionGuest;
}
