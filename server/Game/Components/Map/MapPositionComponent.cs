using Game.Entities;
using Shared.Core.ECS;

namespace Game.Components.Map;

/// <summary>
/// 地图位置组件。存储地图对象在世界中的位置和所属地图信息。
/// </summary>
[ComponentOf(typeof(MapObjectEntity))]
public class MapPositionComponent : Component
{
    public int X;
    public int Y;
    public int MapId;
    public int Line;
}
