namespace Server.Game.Map;

/// <summary>
/// 地图对象类型常量
/// </summary>
public static class MapObjectType
{
    public const int Player = 1;
    public const int Npc = 2;
    public const int Monster = 3;
}

/// <summary>
/// 地图对象接口。所有进入地图的实体需实现此接口。
/// 继承 IAoiObject 以支持 AOI 视野管理。
/// </summary>
public interface IMapObject : Aoi.IAoiObject
{
    /// <summary>对象类型（MapObjectType）</summary>
    new int ObjectType { get; }

    /// <summary>对象名称</summary>
    string Name { get; }

    /// <summary>所在地图ID（0 表示不在任何地图）</summary>
    int MapId { get; set; }

    /// <summary>所在地图分线</summary>
    int Line { get; set; }
}
