using System.Threading.Tasks;
using Game.Components.Player;
using Shared.Common.Protos;
using Shared.Core.Network;

namespace Game.Components.Map;

public sealed class MapMoveReqHandler : PlayerRpcMessageHandler<MapMoveReq, MapMoveAck>
{
    protected override async Task ExecuteAsync(PlayerEntity player, MapMoveReq req, MapMoveAck res)
    {
        if (!Context.Map.Move(player.Id, req.X, req.Y))
        {
            res.ErrorMessage = "不在地图中";
            return;
        }

        res.X = req.X;
        res.Y = req.Y;
        await Task.CompletedTask;
    }
}
