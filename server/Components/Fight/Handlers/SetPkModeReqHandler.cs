using System.Threading.Tasks;
using Game.Components.Player;
using Shared.Common.Protos;
using Shared.Core.Network;

namespace Game.Components.Fight;

public sealed class SetPkModeReqHandler : PlayerRpcMessageHandler<SetPkModeReq, PkModeChangedNtf>
{
    protected override async Task ExecuteAsync(PlayerEntity player, SetPkModeReq req, PkModeChangedNtf res)
    {
        if (req.PkMode < PkMode.Peace || req.PkMode > PkMode.All)
            return;

        res.PkMode = req.PkMode;
        await Task.CompletedTask;
    }
}
