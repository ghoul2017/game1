using System;
using System.Threading.Tasks;
using Shared.Core;
using Shared.Core.Event;
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

    private const int CheckIntervalSeconds = 20;

    [EventSubscriber]
    public static void OnServerInit(EventType.ServerInitEvent e)
    {
        GameTimer.Start(CheckIntervalSeconds * 1000, () =>
        {
            Context.Network.DoHeartbeatCheck();
        });
        Log.Info("心跳检测定时器已启动");
    }

    [MessageHandler(typeof(RpcMethodHandler))]
    public static async Task<IMessage> HandshakeHandler(TcpSession session, HandshakeReq req, HandshakeRes res)
    {
        await Task.CompletedTask;
        Log.Info($"Session:{session.SessionId} Token:{req.Token}");

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
