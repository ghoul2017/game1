using System.Threading.Tasks;
using Game.Components.Player;
using Shared.Common.Protos;
using Shared.Core.Network;

namespace Game.Components.Expedition;

public sealed class ExpeditionStartReqHandler : PlayerRpcMessageHandler<ExpeditionStartReq, ExpeditionStartRes>
{
    protected override async Task ExecuteAsync(
        PlayerEntity player,
        ExpeditionStartReq request,
        ExpeditionStartRes response)
    {
        ExpeditionComponent expedition = Context.Server.GetComponent<ExpeditionComponent>();
        if (!expedition.TryStart(
                player,
                request.RequestId,
                out ExpeditionSnapshotData snapshot,
                out ExpeditionProgressData progress,
                out string errorMessage))
        {
            response.ErrorMessage = errorMessage;
        }
        else
        {
            response.Snapshot = snapshot;
            response.Progress = progress;
        }

        await Task.CompletedTask;
    }
}
