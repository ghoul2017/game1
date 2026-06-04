using System.Linq;
using System.Threading.Tasks;
using Game.Components.Player;
using Shared.Common.Protos;
using Shared.Core.Network;

namespace Game.Components.Bag;

public sealed class QueryBagReqHandler : PlayerRpcMessageHandler<QueryBagReq, QueryBagAck>
{
    protected override Task ExecuteAsync(PlayerEntity player, QueryBagReq req, QueryBagAck res)
    {
        res.Capacity = player.RoleBag.Capacity;
        res.Items = player.RoleBag.Items.Values.Select(RoleBagHandlers.ToInfo).ToList();
        return Task.CompletedTask;
    }
}
