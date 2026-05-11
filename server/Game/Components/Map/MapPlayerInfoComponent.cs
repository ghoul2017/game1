using Game.Entities;
using Shared.Core.ECS;

namespace Game.Components.Map;

/// <summary>
/// 玩家地图信息组件。存储玩家在地图上的展示名称。
/// </summary>
[ComponentOf(typeof(MapPlayerEntity))]
public class MapPlayerInfoComponent : Component
{
    public string Name = "";
}
