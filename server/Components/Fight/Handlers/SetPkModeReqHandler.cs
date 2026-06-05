using System.Threading.Tasks;
using Game.Components.Player;
using Shared.Common.Protos;
using Shared.Core.Network;

namespace Game.Components.Fight;

public sealed class SetPkModeReqHandler : PlayerRpcMessageHandler<SetPkModeReq, PkModeChangedNtf>
{
    protected override Task ExecuteAsync(PlayerEntity player, SetPkModeReq req, PkModeChangedNtf res)
    {
        if (req.PkMode < PkMode.Peace || req.PkMode > PkMode.All)
            return Task.CompletedTask;

        res.PkMode = req.PkMode;
        return Task.CompletedTask;
    }
}
