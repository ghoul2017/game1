using ProtoBuf;

namespace Game.Game.Fight;

/// <summary>
/// 战斗属性。用于战斗中的属性计算（攻击、防御、生命等）。
/// 参考: Java 项目的 Attribute 类。
/// </summary>
[ProtoContract]
public class FightAttribute
{
    /// <summary>最小物理攻击</summary>
    [ProtoMember(1)]
    public int PhyAttMin { get; set; }

    /// <summary>最大物理攻击</summary>
    [ProtoMember(2)]
    public int PhyAttMax { get; set; }

    /// <summary>最小魔法攻击</summary>
    [ProtoMember(3)]
    public int MagicAttMin { get; set; }

    /// <summary>最大魔法攻击</summary>
    [ProtoMember(4)]
    public int MagicAttMax { get; set; }

    /// <summary>最小物理防御</summary>
    [ProtoMember(5)]
    public int PhyDefMin { get; set; }

    /// <summary>最大物理防御</summary>
    [ProtoMember(6)]
    public int PhyDefMax { get; set; }

    /// <summary>最小魔法防御</summary>
    [ProtoMember(7)]
    public int MagicDefMin { get; set; }

    /// <summary>最大魔法防御</summary>
    [ProtoMember(8)]
    public int MagicDefMax { get; set; }

    /// <summary>最大生命值</summary>
    [ProtoMember(9)]
    public int MaxHp { get; set; }

    /// <summary>暴击率（万分比）</summary>
    [ProtoMember(10)]
    public int CriticalRate { get; set; }

    /// <summary>暴击伤害加成（万分比）</summary>
    [ProtoMember(11)]
    public int CriticalDamageRate { get; set; }

    /// <summary>闪避率（万分比）</summary>
    [ProtoMember(12)]
    public int DodgeRate { get; set; }

    /// <summary>命中率（万分比）</summary>
    [ProtoMember(13)]
    public int HitRate { get; set; }

    /// <summary>伤害加成百分比（万分比）</summary>
    [ProtoMember(14)]
    public int DamagePct { get; set; }

    /// <summary>伤害减免百分比（万分比）</summary>
    [ProtoMember(15)]
    public int DamageReducePct { get; set; }

    /// <summary>当前生命值</summary>
    [ProtoMember(16)]
    public int Hp { get; set; }
}
