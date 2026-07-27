using System.Threading.Tasks;
using Game.Components.Player;
using Shared.Common.Protos;
using Shared.Core.Network;

namespace Game.Components.Bag;

public sealed class QueryBagReqHandler : PlayerRpcMessageHandler<QueryBagReq, QueryBagAck>
{
    protected override async Task ExecuteAsync(PlayerEntity player, QueryBagReq req, QueryBagAck res)
    {
        RoleBagComponent bagComponent = player.GetComponent<RoleBagComponent>();

        res.Capacity = bagComponent.Capacity;
        foreach (var item in bagComponent.Items.Values)
            res.Items.Add(RoleBagHelper.ToInfo(item));
        await Task.CompletedTask;
    }
}
