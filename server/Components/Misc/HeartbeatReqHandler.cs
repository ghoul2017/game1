using System;
using System.Threading.Tasks;
using Shared.Common.Protos;
using Shared.Core.Network;

namespace Game.Components.Misc;

public sealed class HeartbeatReqHandler : RpcMessageHandler<HeartbeatReq, HeartbeatRes>
{
    protected override Task ExecuteAsync(TcpSession session, HeartbeatReq req, HeartbeatRes res)
    {
        session.UpdateActiveTime();

        res.ServerTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        res.ClientTime = req.ClientTime;
        return Task.CompletedTask;
    }
}
