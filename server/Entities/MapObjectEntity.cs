using Game.Components.Map;
using Game.Core.Aoi;
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
    public abstract int CfgId { get; }

    /// <summary>对象名称（地图上展示用）</summary>
    private string _name = "";
    public string Name => _name;

    /// <summary>所在地图ID（0 表示不在任何地图）</summary>
    public int MapId { get; set; }

    /// <summary>所在地图分线</summary>
    public int Line { get; set; }

    /// <summary>X 坐标（格子单位）</summary>
    public int X { get; set; }

    /// <summary>Y 坐标（格子单位）</summary>
    public int Y { get; set; }

    /// <summary>设置名称（Create 时调用一次）</summary>
    protected void SetName(string name) => _name = name;
}
