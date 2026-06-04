using System.Threading.Tasks;
using Shared.Common.Protos;
using Shared.Core.Log;
using Shared.Core.Network;

namespace Game.Components.Misc;

public sealed class HandshakeReqHandler : RpcMessageHandler<HandshakeReq, HandshakeRes>
{
    private static readonly ILog Log = LogManager.GetLogger(nameof(HandshakeReqHandler));

    protected override Task ExecuteAsync(TcpSession session, HandshakeReq req, HandshakeRes res)
    {
        Log.Info($"Session:{session.SessionId} Token:{req.Token}");
        return Task.CompletedTask;
    }
}
