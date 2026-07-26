using System.Threading.Tasks;
using Game.Components.Player;
using Shared.Common.Protos;
using Shared.Core.Network;

namespace Game.Components.Bag;

public sealed class QueryBagReqHandler : PlayerRpcMessageHandler<QueryBagReq, QueryBagAck>
{
    protected override async Task ExecuteAsync(PlayerEntity player, QueryBagReq req, QueryBagAck res)
    {
        var bagComponent = player.GetComponent<RoleBagComponent>();
        RoleBagHelper.EnsureValidState(bagComponent);

        res.Capacity = bagComponent.Capacity;
        foreach (var (uid, item) in bagComponent.Items)
            res.Items[uid] = RoleBagHelper.ToInfo(item);
        await Task.CompletedTask;
    }
}
