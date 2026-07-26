using System.Threading.Tasks;
using Game.Components.Player;
using Shared.Common.Protos;
using Shared.Core.Network;

namespace Game.Components.Bag;

public sealed class BagUseItemReqHandler : PlayerRpcMessageHandler<BagUseItemReq, BagUseItemAck>
{
    protected override async Task ExecuteAsync(PlayerEntity player, BagUseItemReq req, BagUseItemAck res)
    {
        res.Error = "道具使用功能未完成";
        await Task.CompletedTask;
    }
}
