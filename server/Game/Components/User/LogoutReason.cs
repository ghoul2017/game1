namespace Game.Components.User;

/// <summary>
/// 登出原因
/// </summary>
public enum LogoutReason
{
    /// <summary>网络断开</summary>
    NetDisconnected,
    /// <summary>服务器关闭</summary>
    ServerClose,
    /// <summary>顶号</summary>
    AnotherSession,
    /// <summary>封禁</summary>
    BanClose,
    /// <summary>GM踢出</summary>
    GmKick,
    /// <summary>心跳超时</summary>
    HeartbeatTimeout,
}
