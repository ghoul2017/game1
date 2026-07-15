using System.Threading.Tasks;
using Game.Components.Player;
using Shared.Common.Protos;
using Shared.Core.Event;
using Shared.Core.Log;
using Shared.Core.Network;

namespace Game.Components.Map;

public sealed class EnterMapReqHandler : PlayerRpcMessageHandler<EnterMapReq, EnterMapAck>
{
    private static readonly ILog Log = LogManager.GetLogger(nameof(EnterMapReqHandler));

    protected override async Task ExecuteAsync(PlayerEntity player, EnterMapReq req, EnterMapAck res)
    {
        var currentPos = Context.Map.GetPlayerPosition(player.Id);
        if (currentPos != null)
        {
            var (mapId, line, px, py) = currentPos.Value;
            res.MapCfgId = mapId;
            res.Line = line;
            res.X = px;
            res.Y = py;
            Context.Map.NotifyCurrentMap(player.Id);
            Log.Info($"玩家重进地图 RoleId:{player.Id} MapId:{mapId}");
            return;
        }

        var map = Context.Map.GetMap(req.MapCfgId, req.Line);
        if (map == null)
        {
            res.ErrorMessage = "目标地图不存在";
            return;
        }

        int x = map.BirthX;
        int y = map.BirthY;

        if (!Context.Map.EnterMapAndNotify(req.MapCfgId, req.Line, x, y, player))
        {
            res.ErrorMessage = "目标地图不存在";
            return;
        }

        res.MapCfgId = req.MapCfgId;
        res.Line = req.Line;
        res.X = x;
        res.Y = y;

        EventBus.Instance.Publish(new EventType.PlayerEnterMapEvent
        {
            RoleId = player.Id,
            MapCfgId = req.MapCfgId,
            Line = req.Line,
        });

        Log.Info($"玩家进入地图 RoleId:{player.Id} MapId:{req.MapCfgId}");
        await Task.CompletedTask;
    }
}
