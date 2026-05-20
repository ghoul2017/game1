using Game.Entities;
using Shared.Core.ECS;

namespace Game.Components.Fight;

/// <summary>
/// 战斗单元组件。挂载到 MapObjectEntity 上，存储战斗属性、技能、仇恨等数据。
/// 从原 FightUnit 类迁移而来，方法移至 FightSystem 扩展方法。
/// </summary>
[ComponentOf(typeof(MapObjectEntity))]
public class FightUnitComponent : Component
{
    public FightAttribute Attr = new();
    public Dictionary<int, Skill> Skills = new();
    public Dictionary<int, long> SkillCoolDown = new();
    public Dictionary<long, int> ThreatMap = new();
    public bool IsDead => Attr.Hp <= 0;
    public long KillerId;
    public int KillerSkillId;
}
