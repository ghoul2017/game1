using Game.Components.Fight;
using Game.Components.Map;
using Game.Entities;
using Shared.Common.Config;
using Shared.Common.Const;
using Shared.Core.ECS;

namespace Game.Components.Monster;

/// <summary>
/// 怪物实体。每个怪物实例对应一个 MonsterEntity。
/// </summary>
public class MonsterEntity : MapObjectEntity
{
    public override int ObjectType => MapObjectType.Monster;
    public override int CfgId => Info.CfgId;

    private MonsterInfoComponent? _info;
    public MonsterInfoComponent Info => _info ??= GetComponent<MonsterInfoComponent>();

    private MonsterAiComponent? _ai;
    public MonsterAiComponent Ai => _ai ??= GetComponent<MonsterAiComponent>();

    private FightUnitComponent? _fight;
    public FightUnitComponent Fight => _fight ??= GetComponent<FightUnitComponent>();

    public static MonsterEntity Create(long monsterId, MonsterConfig config, int mapId, int line, int x, int y, FightAttribute fightAttr)
    {
        var entity = CreateEntityById<MonsterEntity>(monsterId);
        entity.MapId = mapId;
        entity.Line = line;
        entity.X = x;
        entity.Y = y;
        entity.SetName(config.Name);
        var info = entity.AddComponent<MonsterInfoComponent>();
        info.Config = config;
        var ai = entity.AddComponent<MonsterAiComponent>();
        ai.AiState = AiState.Idle;
        ai.LastUpdateTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var fight = entity.AddComponent<FightUnitComponent>();
        fight.Attr = fightAttr;
        fight.AddSkill(new Skill(1));
        return entity;
    }
}
