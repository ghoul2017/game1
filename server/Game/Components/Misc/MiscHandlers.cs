using System;
using System.Threading.Tasks;
using Game.Core.Network;
using Shared.Core.Log;
using Shared.Core.Network;
using Shared.Common.Protos;

namespace Game.Components.Misc;

/// <summary>
/// 握手+心跳处理。
/// </summary>
public static class MiscHandlers
{
    private static readonly ILog Log = LogManager.GetLogger(nameof(MiscHandlers));

    [MessageHandler(typeof(RpcMethodHandler))]
    public static async Task<IMessage> HandshakeHandler(TcpSession session, HandshakeReq req, HandshakeRes res)
    {
        await Task.CompletedTask;
        Log.Info($"Session:{session.SessionId} Token:{req.Token}");

        res.Code = 0;
        res.Message = "OK";
        return res;
    }

    [MessageHandler(typeof(RpcMethodHandler))]
    public static async Task<IMessage> HeartbeatHandler(TcpSession session, HeartbeatReq req, HeartbeatRes res)
    {
        await Task.CompletedTask;
        // 更新会话活跃时间
        session.UpdateActiveTime();

        res.ServerTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        res.ClientTime = req.ClientTime;
        return res;
    }
}
