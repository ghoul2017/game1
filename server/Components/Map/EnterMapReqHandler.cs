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

    protected override Task ExecuteAsync(PlayerEntity player, EnterMapReq req, EnterMapAck res)
    {
        var currentPos = Context.Map.GetPlayerPosition(player.Id);
        if (currentPos != null)
        {
            var (mapId, line, px, py) = currentPos.Value;
            res.MapCfgId = mapId;
            res.Line = line;
            res.X = px;
            res.Y = py;
            var currentMap = Context.Map.GetMap(mapId, line);
            if (currentMap != null)
                MapHandlers.SendVisibleObjects(player.Id, currentMap, px, py);
            Log.Info($"玩家重进地图 RoleId:{player.Id} MapId:{mapId}");
            return Task.CompletedTask;
        }

        var map = Context.Map.GetMap(req.MapCfgId, req.Line);
        if (map == null)
        {
            res.ErrorMessage = "目标地图不存在";
            return Task.CompletedTask;
        }

        int x = map.BirthX;
        int y = map.BirthY;

        if (!Context.Map.EnterMap(req.MapCfgId, req.Line, x, y, player))
        {
            res.ErrorMessage = "目标地图不存在";
            return Task.CompletedTask;
        }

        res.MapCfgId = req.MapCfgId;
        res.Line = req.Line;
        res.X = x;
        res.Y = y;

        MapHandlers.SendVisibleObjects(player.Id, map, x, y);

        EventBus.Instance.Publish(new EventType.PlayerEnterMapEvent
        {
            RoleId = player.Id,
            MapCfgId = req.MapCfgId,
            Line = req.Line,
        });

        Log.Info($"玩家进入地图 RoleId:{player.Id} MapId:{req.MapCfgId}");
        return Task.CompletedTask;
    }
}
