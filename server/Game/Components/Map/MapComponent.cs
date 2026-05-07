using System.Collections.Generic;
using Game.Entities;
using Game.Components.Map;
using Game.Components.Player;
using Shared.Core.ECS;

namespace Game.Components.Map;

/// <summary>
/// 地图组件。挂载到根实体，管理地图实例和玩家-地图映射。
/// </summary>
[ComponentOf(typeof(Module))]
public class MapComponent : Component
{
    public readonly Dictionary<long, GameMap> Maps = new();                // MapKey → GameMap
    public readonly Dictionary<long, GameMap> PlayerMapMap = new();        // roleId → GameMap
    public readonly Dictionary<long, MapPlayer> PlayerMapObjMap = new();   // roleId → MapPlayer
}
