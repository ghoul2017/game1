using Game.Components.Fight;
using Game.Components.Player;
using Game.Entities;
using Shared.Core.ECS;

namespace Game.Components.Map;

/// <summary>
/// 玩家在地图上的投影实体。Id = RoleId。
/// 与 PlayerEntity（持久化数据容器）分离，通过 PlayerRefComponent 桥接。
/// </summary>
public class MapPlayerEntity : MapObjectEntity
{
    public override int ObjectType => MapObjectType.Player;
    public override string Name => Info.Name;

    public long RoleId => Id;

    private MapPlayerInfoComponent? _info;
    public MapPlayerInfoComponent Info => _info ??= GetComponent<MapPlayerInfoComponent>();

    private PlayerRefComponent? _playerRef;
    public PlayerRefComponent PlayerRef => _playerRef ??= GetComponent<PlayerRefComponent>();

    private FightUnitComponent? _fight;
    public FightUnitComponent Fight => _fight ??= GetComponent<FightUnitComponent>();

    public PlayerEntity? PlayerEntity => PlayerRef.PlayerEntity;

    public static MapPlayerEntity Create(long roleId, string name, int mapId, int line, int x, int y, PlayerEntity playerEntity)
    {
        var entity = CreateEntityById<MapPlayerEntity>(roleId);
        var pos = entity.AddComponent<MapPositionComponent>();
        pos.MapId = mapId;
        pos.Line = line;
        pos.X = x;
        pos.Y = y;
        var info = entity.AddComponent<MapPlayerInfoComponent>();
        info.Name = name;
        var playerRef = entity.AddComponent<PlayerRefComponent>();
        playerRef.PlayerEntity = playerEntity;
        // FightUnitComponent 在 RegisterUnit 时添加
        return entity;
    }
}
