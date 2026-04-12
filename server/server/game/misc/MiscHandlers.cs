using Server.Core.Log;
using Server.Network;
using server.common.protos;

namespace Server.Game.Misc;

/// <summary>
/// 握手处理。
/// </summary>
public class HandshakeHandler : MessageHandler<HandshakeReq>
{
    private static readonly ILog Log = LogManager.GetLogger<HandshakeHandler>();

    protected override Task<IMessage?> ExecuteAsync(TcpSession session, HandshakeReq req)
    {
        Log.Info($"Session:{session.SessionId} Token:{req.Token}");

        var resp = new HandshakeResp { Code = 0, Message = "OK" };
        return Task.FromResult<IMessage?>(resp);
    }
}

/// <summary>
/// 心跳处理。更新会话活跃时间 + 返回服务器时间。
/// </summary>
public class HeartbeatHandler : MessageHandler<HeartbeatReq>
{
    private static readonly ILog Log = LogManager.GetLogger<HeartbeatHandler>();

    protected override Task<IMessage?> ExecuteAsync(TcpSession session, HeartbeatReq req)
    {
        // 更新会话活跃时间
        session.UpdateActiveTime();

        var resp = new HeartbeatResp
        {
            ServerTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            ClientTime = req.ClientTime
        };

        return Task.FromResult<IMessage?>(resp);
    }
}
