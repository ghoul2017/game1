using Game.Components.Fight;
using Game.Components.Map;
using Game.Entities;
using Shared.Common.Config;
using Shared.Core.ECS;

namespace Game.Components.Monster;

/// <summary>
/// 怪物实体。每个怪物实例对应一个 MonsterEntity。
/// </summary>
public class MonsterEntity : MapObjectEntity
{
    public override int ObjectType => MapObjectType.Monster;
    public override string Name => Info.Name;
    public override int CfgId => Info.CfgId;

    private MonsterInfoComponent? _info;
    public MonsterInfoComponent Info => _info ??= GetComponent<MonsterInfoComponent>();

    private MonsterAiComponent? _ai;
    public MonsterAiComponent Ai => _ai ??= GetComponent<MonsterAiComponent>();

    private FightUnitComponent? _fight;
    public FightUnitComponent Fight => _fight ??= GetComponent<FightUnitComponent>();

    public static MonsterEntity Create(long monsterId, MonsterConfig config, int mapId, int line, int x, int y)
    {
        var entity = CreateEntityById<MonsterEntity>(monsterId);
        var pos = entity.AddComponent<MapPositionComponent>();
        pos.MapId = mapId;
        pos.Line = line;
        pos.X = x;
        pos.Y = y;
        var info = entity.AddComponent<MonsterInfoComponent>();
        info.Config = config;
        info.Name = config.Name;
        var ai = entity.AddComponent<MonsterAiComponent>();
        ai.AiState = AiState.Idle;
        ai.LastUpdateTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        // FightUnitComponent 在 RegisterUnit 时添加
        return entity;
    }
}
