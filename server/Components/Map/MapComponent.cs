using System.Collections.Generic;
using Game.Components.Monster;
using Game.Entities;
using Shared.Core.Entities;
using Shared.Core.ECS;

namespace Game.Components.Map;

/// <summary>
/// 地图组件。挂载到根实体，管理地图实例和玩家-地图映射。
/// </summary>
[ComponentOf(typeof(Module))]
public class MapComponent : Component
{
    public readonly Dictionary<long, GameMap> Maps = new();                        // MapKey → GameMap
    public readonly Dictionary<long, MapPlayerEntity> PlayerMapEntities = new();   // roleId → MapPlayerEntity

    /// <summary>全局查找地图对象（玩家投影或怪物）</summary>
    public MapObjectEntity? FindMapObject(long objectId)
    {
        // 先查玩家投影
        if (PlayerMapEntities.TryGetValue(objectId, out var playerEntity))
            return playerEntity;

        // 遍历地图查怪物
        foreach (var map in Maps.Values)
        {
            var obj = map.GetObject(objectId);
            if (obj != null)
                return obj;
        }

        return null;
    }
}
