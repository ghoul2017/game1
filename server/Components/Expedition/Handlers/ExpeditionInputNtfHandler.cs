using System.Threading.Tasks;
using Game.Components.Player;
using Shared.Common.Protos;
using Shared.Core.Network;

namespace Game.Components.Expedition;

public sealed class ExpeditionInputNtfHandler : PlayerNtfMessageHandler<ExpeditionInputNtf>
{
    protected override async Task ExecuteAsync(PlayerEntity player, ExpeditionInputNtf notification)
    {
        ExpeditionComponent expedition = Context.Server.GetComponent<ExpeditionComponent>();
        expedition.AcceptInput(player, notification);
        await Task.CompletedTask;
    }
}
