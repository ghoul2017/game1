using Game.Components.Map;
using Shared.Core.Aoi;
using Shared.Core.ECS;

namespace Game.Entities;

/// <summary>
/// 地图对象实体基类。所有地图上的实体（玩家投影、怪物等）继承此类。
/// 实现 IMapObject 保持与 AOI 系统的兼容。
/// </summary>
public abstract class MapObjectEntity : Entity, IMapObject
{
    public long ObjectId => Id;
    public abstract int ObjectType { get; }
    public abstract string Name { get; }
    public abstract int CfgId { get; }

    private MapPositionComponent? _pos;
    public MapPositionComponent Pos => _pos ??= GetComponent<MapPositionComponent>();

    public int MapId { get => Pos.MapId; set => Pos.MapId = value; }
    public int Line { get => Pos.Line; set => Pos.Line = value; }
    public int X { get => Pos.X; set => Pos.X = value; }
    public int Y { get => Pos.Y; set => Pos.Y = value; }
}
