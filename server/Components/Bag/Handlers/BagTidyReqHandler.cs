using System.Linq;
using System.Threading.Tasks;
using Game.Components.Player;
using Shared.Common.Protos;
using Shared.Core.Network;

namespace Game.Components.Bag;

public sealed class BagTidyReqHandler : PlayerRpcMessageHandler<BagTidyReq, BagTidyAck>
{
    protected override async Task ExecuteAsync(PlayerEntity player, BagTidyReq req, BagTidyAck res)
    {
        player.RoleBag.Tidy();

        res.Items = player.RoleBag.Items.Values.Select(RoleBagHelper.ToInfo).ToList();
        await Task.CompletedTask;
    }
}
