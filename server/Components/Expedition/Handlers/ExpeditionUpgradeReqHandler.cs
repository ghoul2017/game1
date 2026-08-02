using System.Threading.Tasks;
using Game.Components.Player;
using Shared.Common.Protos;
using Shared.Core.Network;

namespace Game.Components.Expedition;

public sealed class ExpeditionUpgradeReqHandler : PlayerRpcMessageHandler<ExpeditionUpgradeReq, ExpeditionUpgradeRes>
{
    protected override async Task ExecuteAsync(
        PlayerEntity player,
        ExpeditionUpgradeReq request,
        ExpeditionUpgradeRes response)
    {
        ExpeditionComponent expedition = Context.Server.GetComponent<ExpeditionComponent>();
        if (!expedition.TryUpgrade(
                player,
                request.FacilityId,
                out ExpeditionUpgradeRes result,
                out string errorMessage))
        {
            response.ErrorMessage = errorMessage;
        }
        else
        {
            response.Progress = result.Progress;
            response.SpentResource = result.SpentResource;
            response.OldFacilityLevel = result.OldFacilityLevel;
            response.NewFacilityLevel = result.NewFacilityLevel;
            response.OldRepresentativeDamage = result.OldRepresentativeDamage;
            response.NewRepresentativeDamage = result.NewRepresentativeDamage;
        }

        await Task.CompletedTask;
    }
}
