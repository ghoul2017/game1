using System.Linq;
using System.Threading.Tasks;
using Game.Components.Player;
using Shared.Common.Protos;
using Shared.Core.Log;
using Shared.Core.Network;

namespace Game.Components.Map;

public sealed class ChangeMapReqHandler : PlayerRpcMessageHandler<ChangeMapReq, ChangeMapAck>
{
    private static readonly ILog Log = LogManager.GetLogger(nameof(ChangeMapReqHandler));

    protected override Task ExecuteAsync(PlayerEntity player, ChangeMapReq req, ChangeMapAck res)
    {
        var targetMap = Context.Map.GetMap(req.TargetMapCfgId, req.TargetLine);
        if (targetMap == null)
        {
            res.ErrorMessage = "目标地图不存在";
            return Task.CompletedTask;
        }

        int x = req.X > 0 ? req.X : targetMap.BirthX;
        int y = req.Y > 0 ? req.Y : targetMap.BirthY;

        if (!Context.Map.ChangeMap(req.TargetMapCfgId, req.TargetLine, x, y, player))
        {
            res.ErrorMessage = "目标地图不存在";
            return Task.CompletedTask;
        }

        res.MapCfgId = req.TargetMapCfgId;
        res.Line = req.TargetLine;
        res.X = x;
        res.Y = y;

        var objects = targetMap.GetObjectsInRange(x, y, 4);
        var ntf = new MapObjectsNtf
        {
            Objects = objects
                .Where(o => o.ObjectId != player.Id)
                .Select(o => new MapObjectInfo
                {
                    ObjectId = o.ObjectId,
                    ObjectType = o.ObjectType,
                    Name = o.Name,
                    X = o.X,
                    Y = o.Y
                }).ToList()
        };

        if (ntf.Objects.Count > 0)
        {
            player.Online.Session?.Send(ntf);
        }

        Log.Info($"玩家切图 RoleId:{player.Id} → MapId:{req.TargetMapCfgId}");
        return Task.CompletedTask;
    }
}
