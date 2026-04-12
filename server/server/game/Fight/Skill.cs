using ProtoBuf;

namespace Server.Game.Fight;

/// <summary>
/// 技能实体。存储技能的基础信息。
/// 参考: Java 项目的 Skill 类。
/// </summary>
[ProtoContract]
public class Skill
{
    /// <summary>技能ID</summary>
    [ProtoMember(1)]
    public int SkillId { get; set; }

    /// <summary>技能等级</summary>
    [ProtoMember(2)]
    public int Level { get; set; }

    /// <summary>上次使用时间（毫秒时间戳）</summary>
    [ProtoMember(3)]
    public long LastUseTime { get; set; }

    public Skill() { }

    public Skill(int skillId, int level = 1)
    {
        SkillId = skillId;
        Level = level;
    }
}
