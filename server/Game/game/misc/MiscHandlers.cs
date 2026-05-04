using System;
using System.Threading.Tasks;
using Game.Core.Network;
using Game.Network;
using Shared.Core.Log;
using Shared.Core.Network;
using Shared.Common.Protos;

namespace Game.Game.Misc;

/// <summary>
/// 握手处理。
/// </summary>
public class HandshakeHandler
{
    private static readonly ILog Log = LogManager.GetLogger<HandshakeHandler>();

    [MessageHandler(typeof(RpcMethodHandler))]
    public static async Task<IMessage> ExecuteAsync(TcpSession session, HandshakeReq req, HandshakeRes res)
    {
        Log.Info($"Session:{session.SessionId} Token:{req.Token}");

        res.Code = 0;
        res.Message = "OK";
        return res;
    }
}

/// <summary>
/// 心跳处理。更新会话活跃时间 + 返回服务器时间。
/// </summary>
public class HeartbeatHandler
{
    private static readonly ILog Log = LogManager.GetLogger<HeartbeatHandler>();

    [MessageHandler(typeof(RpcMethodHandler))]
    public static async Task<IMessage?> ExecuteAsync(TcpSession session, HeartbeatReq req, HeartbeatRes res)
    {
        // 更新会话活跃时间
        session.UpdateActiveTime();


        res.ServerTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        res.ClientTime = req.ClientTime;
        return res;
    }
}