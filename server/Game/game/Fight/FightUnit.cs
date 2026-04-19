namespace Game.Game.Fight;

/// <summary>
/// 战斗单元。代表一个可参与战斗的实体（玩家/NPC/怪物）。
/// 包含战斗属性、技能列表、仇恨列表等。
/// 参考: Java 项目的 Performer 类。
/// </summary>
public class FightUnit
{
    /// <summary>对象唯一ID</summary>
    public long ObjectId { get; }

    /// <summary>对象类型（MapObjectType）</summary>
    public int ObjectType { get; }

    /// <summary>名称</summary>
    public string Name { get; }

    /// <summary>配置ID（对应配置表）</summary>
    public int CfgId { get; }

    /// <summary>战斗属性</summary>
    public FightAttribute Attr { get; }

    /// <summary>已学技能列表</summary>
    public Dictionary<int, Skill> Skills { get; } = new();

    /// <summary>技能冷却 key=groupId, value=可使用时间戳(ms)</summary>
    public Dictionary<int, long> SkillCoolDown { get; } = new();

    /// <summary>仇恨列表 key=targetId, value=仇恨值</summary>
    public Dictionary<long, int> ThreatMap { get; } = new();

    /// <summary>是否已死亡</summary>
    public bool IsDead => Attr.Hp <= 0;

    /// <summary>击杀者ID</summary>
    public long KillerId { get; set; }

    /// <summary>击杀使用的技能ID</summary>
    public int KillerSkillId { get; set; }

    public FightUnit(long objectId, int objectType, string name, int cfgId, FightAttribute attr)
    {
        ObjectId = objectId;
        ObjectType = objectType;
        Name = name;
        CfgId = cfgId;
        Attr = attr;
    }

    /// <summary>添加技能</summary>
    public void AddSkill(Skill skill)
    {
        Skills[skill.SkillId] = skill;
    }

    /// <summary>获取技能</summary>
    public Skill? GetSkill(int skillId)
    {
        return Skills.TryGetValue(skillId, out var skill) ? skill : null;
    }

    /// <summary>受伤</summary>
    /// <returns>实际扣血量</returns>
    public int TakeDamage(int damage)
    {
        int actual = Math.Min(damage, Attr.Hp);
        Attr.Hp = Math.Max(0, Attr.Hp - damage);
        return actual;
    }

    /// <summary>回血</summary>
    public int Heal(int amount)
    {
        int actual = Math.Min(amount, Attr.MaxHp - Attr.Hp);
        Attr.Hp = Math.Min(Attr.MaxHp, Attr.Hp + amount);
        return actual;
    }

    /// <summary>添加仇恨</summary>
    public void AddThreat(long targetId, int value)
    {
        ThreatMap.TryGetValue(targetId, out var existing);
        ThreatMap[targetId] = existing + value;
    }

    /// <summary>获取仇恨最高的目标</summary>
    public long GetMaxThreatTarget()
    {
        if (ThreatMap.Count == 0) return 0;

        long targetId = 0;
        int maxThreat = 0;
        foreach (var kv in ThreatMap)
        {
            if (kv.Value > maxThreat)
            {
                maxThreat = kv.Value;
                targetId = kv.Key;
            }
        }
        return targetId;
    }

    /// <summary>重置战斗状态（满血复活）</summary>
    public void Reset()
    {
        Attr.Hp = Attr.MaxHp;
        ThreatMap.Clear();
        SkillCoolDown.Clear();
        KillerId = 0;
        KillerSkillId = 0;
    }
}
