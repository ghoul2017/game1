using server.common.config;

namespace Server.Game.Fight;

/// <summary>
/// 战斗核心计算。包含伤害公式、命中判定、暴击判定等。
/// 参考: Java 项目的 SkillCore + SkillUse。
/// </summary>
public static class FightCore
{
    private static readonly Random _random = new();

    #region 伤害计算

    /// <summary>
    /// 计算技能伤害。
    /// 公式: MAX((攻击 * 伤害系数 + 附加伤害 - 防御) * 伤害减免, 随机最小伤害) * 暴击系数
    /// </summary>
    public static int CalculateDamage(FightUnit attacker, FightUnit target, SkillConfig skillCfg, bool isCritical)
    {
        // 随机攻击值
        int attack = RandomRange(attacker.Attr.PhyAttMin, attacker.Attr.PhyAttMax);
        // 随机防御值
        int defence = RandomRange(target.Attr.PhyDefMin, target.Attr.PhyDefMax);

        // 技能伤害系数（千分比）+ 附加伤害
        int hurtPoint = skillCfg.HurtPoint + skillCfg.HurtPointPerLevel * (1 - 1); // TODO: skill.Level
        int exHurt = skillCfg.ExHurt + skillCfg.ExHurtPerLevel * (1 - 1);

        // 增伤系数
        int addDamagePct = attacker.Attr.DamagePct;

        // 基础伤害 = (攻击 * (伤害系数/1000 + 增伤/10000) + 附加伤害 - 防御)
        long baseDamage = (long)(attack * (hurtPoint / 1000.0 + addDamagePct / 10000.0) + exHurt - defence);

        // 伤害减免
        long damageDeductPct = 10000 - target.Attr.DamageReducePct;
        int damage1 = (int)(baseDamage * damageDeductPct / 10000);
        int damage2 = RandomRange(1, Math.Max(1, (int)(attack * 0.05)));
        int damage = Math.Max(damage1, damage2);

        // 暴击加成
        if (isCritical)
        {
            int criticalDamageRate = 5000 + attacker.Attr.CriticalDamageRate; // 基础暴击伤害150%
            damage += (int)(damage * (criticalDamageRate / 10000.0));
        }

        return Math.Max(0, damage);
    }

    #endregion

    #region 命中/暴击/闪避判定

    /// <summary>命中判定。命中概率 = 基础命中率 + 攻击者命中 - 目标闪避</summary>
    public static bool IsHit(FightUnit attacker, FightUnit target)
    {
        int hitRate = 95 + (attacker.Attr.HitRate - target.Attr.DodgeRate) / 100;
        hitRate = Math.Clamp(hitRate, 10, 99);
        return _random.Next(100) < hitRate;
    }

    /// <summary>暴击判定</summary>
    public static bool IsCritical(FightUnit attacker, FightUnit target)
    {
        int rate = attacker.Attr.CriticalRate;
        return _random.Next(10000) < rate;
    }

    #endregion

    #region 技能使用

    /// <summary>
    /// 执行一次技能攻击。
    /// 返回伤害结果列表。
    /// </summary>
    public static List<SkillHitResult> UseSkill(FightUnit attacker, FightUnit target, SkillConfig skillCfg)
    {
        var results = new List<SkillHitResult>();

        // 命中判定
        if (!IsHit(attacker, target))
        {
            results.Add(new SkillHitResult { TargetId = target.ObjectId, Missed = true });
            return results;
        }

        // 暴击判定
        bool critical = IsCritical(attacker, target);

        // 伤害计算
        int damage = CalculateDamage(attacker, target, skillCfg, critical);

        // 造伤
        int actualDamage = target.TakeDamage(damage);

        // 仇恨
        attacker.AddThreat(target.ObjectId, damage);

        var result = new SkillHitResult
        {
            TargetId = target.ObjectId,
            Damage = actualDamage,
            Hp = target.Attr.Hp,
            IsCritical = critical,
            IsDead = target.IsDead,
        };

        if (target.IsDead)
        {
            target.KillerId = attacker.ObjectId;
            target.KillerSkillId = skillCfg.SkillId;
        }

        results.Add(result);
        return results;
    }

    /// <summary>AOE 技能攻击多个目标</summary>
    public static List<SkillHitResult> UseSkillAoe(FightUnit attacker, List<FightUnit> targets, SkillConfig skillCfg)
    {
        var results = new List<SkillHitResult>();
        foreach (var target in targets)
        {
            if (target.IsDead) continue;
            results.AddRange(UseSkill(attacker, target, skillCfg));
        }
        return results;
    }

    #endregion

    #region 工具方法

    private static int RandomRange(int min, int max)
    {
        if (min >= max) return min;
        return _random.Next(min, max + 1);
    }

    #endregion
}

/// <summary>技能命中结果</summary>
public class SkillHitResult
{
    public long TargetId { get; set; }
    public int Damage { get; set; }
    public int Hp { get; set; }
    public bool IsCritical { get; set; }
    public bool IsDead { get; set; }
    public bool Missed { get; set; }
}
