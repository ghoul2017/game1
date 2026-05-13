using System;
using System.Collections.Generic;
using Game.Entities;
using Shared.Core;
using Shared.Core.Entities;
using Shared.Core.ECS;

namespace Game.Components.Monster;

/// <summary>
/// 怪物组件。挂载到根实体，管理在线怪物集合。
/// </summary>
[ComponentOf(typeof(Module))]
public class MonsterComponent : Component
{
    public readonly Dictionary<long, Dictionary<long, MonsterEntity>> MapMonsters = new();
    public readonly Dictionary<string, long> SpawnTimers = new();
    public GameTimerHandle UpdateTimer;
    public readonly Random Random = new();
}
