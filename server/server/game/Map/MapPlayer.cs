namespace Server.Game.Map;

/// <summary>
/// 地图中的玩家对象。包装 Player 在地图中的位置信息。
/// 实现 IMapObject 和 IAoiObject 接口。
/// </summary>
public class MapPlayer : IMapObject
{
    public long ObjectId => RoleId;
    public int ObjectType => MapObjectType.Player;
    public string Name { get; }
    public long RoleId { get; }
    public int MapId { get; set; }
    public int Line { get; set; }
    public int X { get; set; }
    public int Y { get; set; }

    public MapPlayer(long roleId, string name, int mapId, int line, int x, int y)
    {
        RoleId = roleId;
        Name = name;
        MapId = mapId;
        Line = line;
        X = x;
        Y = y;
    }
}
