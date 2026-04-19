namespace Game.Game.Map;

/// <summary>
/// 地图中的 NPC 对象。由配置表驱动，不持久化。
/// </summary>
public class MapNpc : IMapObject
{
    public long ObjectId => NpcId;
    public int ObjectType => MapObjectType.Npc;
    public string Name { get; }
    public int NpcId { get; }
    public int MapId { get; set; }
    public int Line { get; set; }
    public int X { get; set; }
    public int Y { get; set; }

    /// <summary>NPC 配置ID（对应 NPC 配置表）</summary>
    public int CfgId { get; }

    public MapNpc(int npcId, int cfgId, string name, int mapId, int line, int x, int y)
    {
        NpcId = npcId;
        CfgId = cfgId;
        Name = name;
        MapId = mapId;
        Line = line;
        X = x;
        Y = y;
    }
}
