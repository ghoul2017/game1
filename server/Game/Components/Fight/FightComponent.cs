using System.Collections.Generic;
using Shared.Core.Entities;
using Game.Components.Fight;
using Shared.Core.ECS;

namespace Game.Components.Fight;

/// <summary>
/// 战斗组件。挂载到根实体，管理在线战斗单元集合。
/// </summary>
[ComponentOf(typeof(Module))]
public class FightComponent : Component
{
    public readonly Dictionary<long, FightUnit> Units = new();  // objectId → FightUnit
}
