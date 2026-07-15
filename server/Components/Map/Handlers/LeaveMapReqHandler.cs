using System.Threading.Tasks;
using Game.Components.Player;
using Shared.Common.Protos;
using Shared.Core.Network;

namespace Game.Components.Map;

public sealed class LeaveMapReqHandler : PlayerRpcMessageHandler<LeaveMapReq, LeaveMapAck>
{
    protected override async Task ExecuteAsync(PlayerEntity player, LeaveMapReq req, LeaveMapAck res)
    {
        if (!Context.Map.QuitMap(player.Id))
        {
            res.ErrorMessage = "不在地图中";
        }

        await Task.CompletedTask;
    }
}
