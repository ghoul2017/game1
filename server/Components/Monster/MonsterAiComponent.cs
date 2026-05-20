using Game.Entities;
using Shared.Core.ECS;

namespace Game.Components.Monster;

/// <summary>
/// 怪物AI组件。存储怪物的AI状态、巡逻目标、追击目标等运行时数据。
/// </summary>
[ComponentOf(typeof(MonsterEntity))]
public class MonsterAiComponent : Component
{
    public AiState AiState = AiState.Idle;
    public (int X, int Y)? PatrolTarget;
    public long? ChaseTarget;
    public long LastAttackTime;
    public long LastUpdateTime;
}
