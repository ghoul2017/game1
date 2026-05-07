using Game.Components.Fight;
using Game.Components.Map;
using Shared.Common.Config;

namespace Game.Components.Monster;

/// <summary>
/// 怪物单位
/// </summary>
public class MonsterUnit
{
    /// <summary>怪物唯一ID</summary>
    public long ObjectId { get; }

    /// <summary>配置</summary>
    public MonsterConfig? Config { get; }

    /// <summary>战斗单元</summary>
    public FightUnit? FightUnit { get; set; }

    /// <summary>地图NPC对象</summary>
    public MapNpc? Npc { get; set; }

    /// <summary>当前X坐标</summary>
    public int X { get; set; }

    /// <summary>当前Y坐标</summary>
    public int Y { get; set; }

    /// <summary>AI状态</summary>
    public AiState AiState { get; set; } = AiState.Idle;

    /// <summary>巡逻目标</summary>
    public (int X, int Y)? PatrolTarget { get; set; }

    /// <summary>追击目标（玩家RoleId）</summary>
    public long? ChaseTarget { get; set; }

    /// <summary>上次攻击时间</summary>
    public long LastAttackTime { get; set; }

    /// <summary>上次AI更新时间</summary>
    public long LastUpdateTime { get; set; }

    /// <summary>配置ID</summary>
    public int CfgId => Config?.Id ?? 0;

    public MonsterUnit(long objectId, MonsterConfig config)
    {
        ObjectId = objectId;
        Config = config;
    }
}
