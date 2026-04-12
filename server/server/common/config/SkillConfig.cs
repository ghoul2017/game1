namespace server.common.config;

/// <summary>
/// 技能配置。对应 Skill.csv。
/// 参考: Java 项目的 SkillConfig。
/// </summary>
[ConfigTable("Skill.csv", KeyName = "SkillId")]
public class SkillConfig
{
    /// <summary>技能ID</summary>
    public int SkillId { get; set; }

    /// <summary>技能名称</summary>
    public string Name { get; set; } = "";

    /// <summary>技能类型（1=伤害, 2=治疗, 3=BUFF）</summary>
    public int Type { get; set; }

    /// <summary>攻击类型（0=物理, 1=魔法）</summary>
    public int AttackType { get; set; }

    /// <summary>技能范围类型（1=单体, 2=圆形AOE, 3=扇形AOE, 4=自身周围）</summary>
    public int EffectArea { get; set; }

    /// <summary>技能范围参数（半径/角度等）</summary>
    public int EffectParam { get; set; }

    /// <summary>最大目标数</summary>
    public int MaxTarget { get; set; } = 1;

    /// <summary>施法距离（格子）</summary>
    public int CastRange { get; set; }

    /// <summary>伤害系数（千分比）</summary>
    public int HurtPoint { get; set; }

    /// <summary>每级增加伤害系数</summary>
    public int HurtPointPerLevel { get; set; }

    /// <summary>附加伤害</summary>
    public int ExHurt { get; set; }

    /// <summary>每级增加附加伤害</summary>
    public int ExHurtPerLevel { get; set; }

    /// <summary>CD时间（毫秒）</summary>
    public int CdTime { get; set; }

    /// <summary>消耗MP</summary>
    public int CostMp { get; set; }

    /// <summary>BUFF ID（施加给目标的）</summary>
    public int BuffId { get; set; }

    /// <summary>自身BUFF ID</summary>
    public int BuffSelfId { get; set; }

    /// <summary>BUFF概率（万分比）</summary>
    public int BuffRate { get; set; }

    /// <summary>BUFF参数（分号分隔）</summary>
    public int[] BuffParam { get; set; } = [];

    /// <summary>仇恨值</summary>
    public int HatePoint { get; set; }
}
