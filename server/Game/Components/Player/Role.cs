using Game.Core.Database;
using MongoDB.Bson.Serialization.Attributes;
using ProtoBuf;

namespace Game.Components.Player;

/// <summary>
/// 角色持久化实体。存储角色的持久化数据。
/// 对应 MongoDB 集合: role
/// </summary>
[ProtoContract]
public class Role : IEntity
{
    [ProtoMember(1)]
    [BsonId]
    public long Id { get; set; }

    /// <summary>所属用户ID</summary>
    [ProtoMember(2)]
    public long UserId { get; set; }

    /// <summary>角色名</summary>
    [ProtoMember(3)]
    public string Name { get; set; } = string.Empty;

    /// <summary>等级</summary>
    [ProtoMember(4)]
    public int Level { get; set; } = 1;

    /// <summary>职业类型</summary>
    [ProtoMember(5)]
    public int Job { get; set; }

    /// <summary>经验值</summary>
    [ProtoMember(6)]
    public long Exp { get; set; }

    /// <summary>VIP等级</summary>
    [ProtoMember(7)]
    public int VipLevel { get; set; }

    /// <summary>最后下线时间（Unix时间戳毫秒）</summary>
    [ProtoMember(8)]
    public long LastLogoutTime { get; set; }

    /// <summary>创建时间（Unix时间戳毫秒）</summary>
    [ProtoMember(9)]
    public long CreateTime { get; set; }

    /// <summary>最近一次重连Token，用于单次有效验证</summary>
    [BsonIgnore]
    public string LastToken { get; set; } = "";
}
