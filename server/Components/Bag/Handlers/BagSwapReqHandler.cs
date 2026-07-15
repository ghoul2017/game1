using System.Threading.Tasks;
using Game.Components.Player;
using Shared.Common.Protos;
using Shared.Core.Network;

namespace Game.Components.Bag;

public sealed class BagSwapReqHandler : PlayerRpcMessageHandler<BagSwapReq, BagSwapAck>
{
    protected override async Task ExecuteAsync(PlayerEntity player, BagSwapReq req, BagSwapAck res)
    {
        player.RoleBag.Swap(req.Index1, req.Index2);
        await Task.CompletedTask;
    }
}
