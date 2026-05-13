using Game.Entities;
using Shared.Common.Config;
using Shared.Core.ECS;

namespace Game.Components.Monster;

/// <summary>
/// 怪物信息组件。存储怪物的配置引用。
/// </summary>
[ComponentOf(typeof(MonsterEntity))]
public class MonsterInfoComponent : Component
{
    public MonsterConfig? Config;

    public int CfgId => Config?.Id ?? 0;
}
