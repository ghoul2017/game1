using Game.Entities;
using Shared.Common.Config;
using Shared.Core.ECS;

namespace Game.Components.Monster;

/// <summary>
/// 怪物信息组件。存储怪物的配置和名称。
/// </summary>
[ComponentOf(typeof(MonsterEntity))]
public class MonsterInfoComponent : Component
{
    public MonsterConfig? Config;
    public string Name = "";

    public int CfgId => Config?.Id ?? 0;
}
