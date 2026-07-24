using System.Linq;
using System.Threading.Tasks;
using Game.Components.Player;
using Shared.Common.Protos;
using Shared.Core.Log;
using Shared.Core.Network;

namespace Game.Components.Bag;

public sealed class BagTidyReqHandler : PlayerRpcMessageHandler<BagTidyReq, BagTidyAck>
{
    private static readonly ILog Log = LogManager.GetLogger(nameof(BagTidyReqHandler));

    protected override async Task ExecuteAsync(PlayerEntity player, BagTidyReq req, BagTidyAck res)
    {
        var bagComponent = player.GetComponent<RoleBagComponent>();

        bagComponent.Tidy();
        res.Items = bagComponent.Items.Values.Select(RoleBagHelper.ToInfo).ToList();

        Log.Info($"整理背包 RoleId:{player.Id} ItemCount:{res.Items.Count}");
        await Task.CompletedTask;
    }
}
