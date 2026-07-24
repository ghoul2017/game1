using System;

namespace Game.Components.Fight;

/// <summary>
/// 战斗组件系统。提供战斗单元自身状态的可复用操作。
/// </summary>
public static class FightSystem
{
    /// <summary>受伤，返回实际扣血量</summary>
    public static int TakeDamage(this FightUnitComponent self, int damage)
    {
        var actual = Math.Min(damage, self.Attr.Hp);
        self.Attr.Hp = Math.Max(0, self.Attr.Hp - damage);
        return actual;
    }

    /// <summary>回血，返回实际回血量</summary>
    public static int Heal(this FightUnitComponent self, int amount)
    {
        var actual = Math.Min(amount, self.Attr.MaxHp - self.Attr.Hp);
        self.Attr.Hp = Math.Min(self.Attr.MaxHp, self.Attr.Hp + amount);
        return actual;
    }

    /// <summary>添加仇恨</summary>
    public static void AddThreat(this FightUnitComponent self, long targetId, int value)
    {
        self.ThreatMap.TryGetValue(targetId, out var existing);
        self.ThreatMap[targetId] = existing + value;
    }

    /// <summary>获取仇恨最高的目标</summary>
    public static long GetMaxThreatTarget(this FightUnitComponent self)
    {
        if (self.ThreatMap.Count == 0)
            return 0;

        long targetId = 0;
        var maxThreat = 0;
        foreach (var (candidateId, threat) in self.ThreatMap)
        {
            if (threat <= maxThreat)
                continue;

            maxThreat = threat;
            targetId = candidateId;
        }

        return targetId;
    }

    /// <summary>添加技能</summary>
    public static void AddSkill(this FightUnitComponent self, Skill skill)
    {
        self.Skills[skill.SkillId] = skill;
    }

    /// <summary>获取技能</summary>
    public static Skill? GetSkill(this FightUnitComponent self, int skillId)
    {
        return self.Skills.TryGetValue(skillId, out var skill) ? skill : null;
    }

    /// <summary>重置战斗状态（满血复活）</summary>
    public static void ResetFightState(this FightUnitComponent self)
    {
        self.Attr.Hp = self.Attr.MaxHp;
        self.ThreatMap.Clear();
        self.SkillCoolDown.Clear();
        self.KillerId = 0;
        self.KillerSkillId = 0;
    }
}
