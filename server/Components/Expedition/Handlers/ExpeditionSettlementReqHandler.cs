using System.Threading.Tasks;
using Game.Components.Player;
using Shared.Common.Protos;
using Shared.Core.Network;

namespace Game.Components.Expedition;

public sealed class ExpeditionSettlementReqHandler
    : PlayerRpcMessageHandler<ExpeditionSettlementReq, ExpeditionSettlementRes>
{
    protected override async Task ExecuteAsync(
        PlayerEntity player,
        ExpeditionSettlementReq request,
        ExpeditionSettlementRes response)
    {
        ExpeditionComponent expedition = Context.Server.GetComponent<ExpeditionComponent>();
        if (!expedition.TrySettle(
                player,
                request.RunId,
                out ExpeditionSettlementData settlement,
                out ExpeditionProgressData progress,
                out string errorMessage))
        {
            response.ErrorMessage = errorMessage;
        }
        else
        {
            response.Settlement = settlement;
            response.Progress = progress;
        }

        await Task.CompletedTask;
    }
}
