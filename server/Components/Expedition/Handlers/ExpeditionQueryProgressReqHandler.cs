using System.Threading.Tasks;
using Game.Components.Player;
using Shared.Common.Protos;
using Shared.Core.Network;

namespace Game.Components.Expedition;

public sealed class ExpeditionQueryProgressReqHandler
    : PlayerRpcMessageHandler<ExpeditionQueryProgressReq, ExpeditionQueryProgressRes>
{
    protected override async Task ExecuteAsync(
        PlayerEntity player,
        ExpeditionQueryProgressReq request,
        ExpeditionQueryProgressRes response)
    {
        ExpeditionComponent expedition = Context.Server.GetComponent<ExpeditionComponent>();
        response.Progress = expedition.QueryProgress(player);
        await Task.CompletedTask;
    }
}
