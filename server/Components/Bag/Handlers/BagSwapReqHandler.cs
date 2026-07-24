using System.Threading.Tasks;
using Game.Components.Player;
using Shared.Common.Protos;
using Shared.Core.Log;
using Shared.Core.Network;

namespace Game.Components.Bag;

public sealed class BagSwapReqHandler : PlayerRpcMessageHandler<BagSwapReq, BagSwapAck>
{
    private static readonly ILog Log = LogManager.GetLogger(nameof(BagSwapReqHandler));

    protected override async Task ExecuteAsync(PlayerEntity player, BagSwapReq req, BagSwapAck res)
    {
        var bagComponent = player.GetComponent<RoleBagComponent>();

        if (req.Index1 < 0 || req.Index1 >= bagComponent.Capacity ||
            req.Index2 < 0 || req.Index2 >= bagComponent.Capacity)
        {
            res.ErrorMessage = "背包格子索引错误";
            return;
        }

        bagComponent.Swap(req.Index1, req.Index2);

        Log.Info($"交换背包格子 RoleId:{player.Id} Index1:{req.Index1} Index2:{req.Index2}");
        await Task.CompletedTask;
    }
}
