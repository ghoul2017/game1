using Game.Components.Fight;
using Game.Components.Player;
using Game.Entities;
using Shared.Common.Const;
using Shared.Core.ECS;

namespace Game.Components.Map;

/// <summary>
/// 玩家在地图上的投影实体。Id = RoleId。
/// 与 PlayerEntity（持久化数据容器）分离，直接持有引用。
/// </summary>
public class MapPlayerEntity : MapObjectEntity
{
    public override int ObjectType => MapObjectType.Player;
    public override int CfgId => 0;

    public long RoleId => Id;

    /// <summary>桥接回 PlayerEntity</summary>
    public PlayerEntity? PlayerEntity { get; private set; }

    private FightUnitComponent? _fight;
    public FightUnitComponent Fight => _fight ??= GetComponent<FightUnitComponent>();

    public static MapPlayerEntity Create(int mapId, int line, int x, int y, PlayerEntity playerEntity)
    {
        var entity = CreateEntityById<MapPlayerEntity>(playerEntity.Id);
        entity.MapId = mapId;
        entity.Line = line;
        entity.X = x;
        entity.Y = y;
        entity.SetName(playerEntity.RoleData.Name);
        entity.PlayerEntity = playerEntity;
        var fight = entity.AddComponent<FightUnitComponent>();
        fight.Attr = playerEntity.RoleAttribute.ToFightAttribute();
        fight.AddSkill(new Skill(1));
        return entity;
    }
}
