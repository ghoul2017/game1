using ProtoBuf;
using Server.Core.Entity;

namespace Server.Game.Player;

/// <summary>
/// 角色战斗属性实体。存储角色的战斗相关属性（HP/MP/攻击/防御等）。
/// 对应数据库表: p_role_attribute (id, data)
/// </summary>
[ProtoContract]
public class RoleAttribute : IEntity
{
    [ProtoMember(1)]
    public long Id { get; set; }

    /// <summary>当前HP</summary>
    [ProtoMember(2)]
    public int Hp { get; set; } = 100;

    /// <summary>最大HP</summary>
    [ProtoMember(3)]
    public int MaxHp { get; set; } = 100;

    /// <summary>当前MP</summary>
    [ProtoMember(4)]
    public int Mp { get; set; } = 50;

    /// <summary>最大MP</summary>
    [ProtoMember(5)]
    public int MaxMp { get; set; } = 50;

    /// <summary>最小物理攻击</summary>
    [ProtoMember(6)]
    public int PhyAttMin { get; set; } = 10;

    /// <summary>最大物理攻击</summary>
    [ProtoMember(7)]
    public int PhyAttMax { get; set; } = 12;

    /// <summary>最小魔法攻击</summary>
    [ProtoMember(8)]
    public int MagicAttMin { get; set; } = 5;

    /// <summary>最大魔法攻击</summary>
    [ProtoMember(9)]
    public int MagicAttMax { get; set; } = 7;

    /// <summary>物理防御</summary>
    [ProtoMember(10)]
    public int PhyDef { get; set; } = 5;

    /// <summary>魔法防御</summary>
    [ProtoMember(11)]
    public int MagicDef { get; set; } = 3;

    /// <summary>移动速度</summary>
    [ProtoMember(12)]
    public int MoveSpeed { get; set; } = 5;

    /// <summary>攻击速度</summary>
    [ProtoMember(13)]
    public int AttackSpeed { get; set; } = 100;

    /// <summary>暴击率（万分比）</summary>
    [ProtoMember(14)]
    public int CriticalRate { get; set; } = 100;

    /// <summary>暴击伤害（万分比）</summary>
    [ProtoMember(15)]
    public int CriticalDamage { get; set; } = 150;

    /// <summary>经验值</summary>
    [ProtoMember(16)]
    public int Exp { get; set; }

    /// <summary>等级</summary>
    [ProtoMember(17)]
    public int Level { get; set; } = 1;

    /// <summary>金币</summary>
    [ProtoMember(18)]
    public int Gold { get; set; }

    /// <summary>从 ProtoContract 转换到 FightAttribute</summary>
    public Fight.FightAttribute ToFightAttribute()
    {
        return new Fight.FightAttribute
        {
            PhyAttMin = PhyAttMin,
            PhyAttMax = PhyAttMax,
            MagicAttMin = MagicAttMin,
            MagicAttMax = MagicAttMax,
            PhyDefMin = PhyDef,
            PhyDefMax = PhyDef,
            MagicDefMin = MagicDef,
            MagicDefMax = MagicDef,
            MaxHp = MaxHp,
            Hp = Hp,
            CriticalRate = CriticalRate,
            CriticalDamageRate = CriticalDamage,
        };
    }
}
