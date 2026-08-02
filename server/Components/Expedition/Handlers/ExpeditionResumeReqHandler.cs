using System.Threading.Tasks;
using Game.Components.Player;
using Shared.Common.Protos;
using Shared.Core.Network;

namespace Game.Components.Expedition;

public sealed class ExpeditionResumeReqHandler : PlayerRpcMessageHandler<ExpeditionResumeReq, ExpeditionResumeRes>
{
    protected override async Task ExecuteAsync(
        PlayerEntity player,
        ExpeditionResumeReq request,
        ExpeditionResumeRes response)
    {
        ExpeditionComponent expedition = Context.Server.GetComponent<ExpeditionComponent>();
        if (!expedition.TryResume(
                player,
                request.RunId,
                out bool hasActiveRun,
                out ExpeditionSnapshotData snapshot,
                out ExpeditionSettlementData settlement,
                out ExpeditionProgressData progress,
                out string errorMessage))
        {
            response.ErrorMessage = errorMessage;
        }
        else
        {
            response.HasActiveRun = hasActiveRun;
            response.Snapshot = snapshot;
            response.Settlement = settlement;
            response.Progress = progress;
        }

        await Task.CompletedTask;
    }
}
