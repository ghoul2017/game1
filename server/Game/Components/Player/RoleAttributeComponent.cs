using Game.Components.Fight;
using Game.Core.Database;
using MongoDB.Bson.Serialization.Attributes;
using Shared.Core.ECS;

namespace Game.Components.Player;

/// <summary>
/// 角色属性组件。持久化到 MongoDB（集合名 role_attribute）。
/// </summary>
[ComponentOf(typeof(PlayerEntity))]
public class RoleAttributeComponent : Component, IEntity
{
    [BsonId]
    public long Id { get; set; }

    /// <summary>当前HP</summary>
    public int Hp { get; set; } = 100;

    /// <summary>最大HP</summary>
    public int MaxHp { get; set; } = 100;

    /// <summary>当前MP</summary>
    public int Mp { get; set; } = 50;

    /// <summary>最大MP</summary>
    public int MaxMp { get; set; } = 50;

    /// <summary>最小物理攻击</summary>
    public int PhyAttMin { get; set; } = 10;

    /// <summary>最大物理攻击</summary>
    public int PhyAttMax { get; set; } = 12;

    /// <summary>最小魔法攻击</summary>
    public int MagicAttMin { get; set; } = 5;

    /// <summary>最大魔法攻击</summary>
    public int MagicAttMax { get; set; } = 7;

    /// <summary>物理防御</summary>
    public int PhyDef { get; set; } = 5;

    /// <summary>魔法防御</summary>
    public int MagicDef { get; set; } = 3;

    /// <summary>移动速度</summary>
    public int MoveSpeed { get; set; } = 5;

    /// <summary>攻击速度</summary>
    public int AttackSpeed { get; set; } = 100;

    /// <summary>暴击率（万分比）</summary>
    public int CriticalRate { get; set; } = 100;

    /// <summary>暴击伤害（万分比）</summary>
    public int CriticalDamage { get; set; } = 150;

    /// <summary>经验值</summary>
    public int Exp { get; set; }

    /// <summary>等级</summary>
    public int Level { get; set; } = 1;

    /// <summary>金币</summary>
    public int Gold { get; set; }

    /// <summary>转换为战斗属性</summary>
    public FightAttribute ToFightAttribute()
    {
        return new FightAttribute
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
