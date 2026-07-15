using Shared.Core.Database;
using MongoDB.Bson.Serialization.Attributes;
using Shared.Core.ECS;

namespace Game.Components.Player;

/// <summary>
/// 角色持久化数据组件。直接持有所有角色字段，实现 IEntity。
/// 对应 MongoDB 集合: role_data_component
/// </summary>
[ComponentOf(typeof(PlayerEntity))]
public class RoleDataComponent : Component, IEntity
{
    [BsonId]
    public long Id { get; set; }

    /// <summary>所属用户ID</summary>
    public long UserId { get; set; }

    /// <summary>角色名</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>等级</summary>
    public int Level { get; set; } = 1;

    /// <summary>职业类型</summary>
    public int Job { get; set; }

    /// <summary>经验值</summary>
    public long Exp { get; set; }

    /// <summary>VIP等级</summary>
    public int VipLevel { get; set; }

    /// <summary>最后下线时间（Unix时间戳毫秒）</summary>
    public long LastLogoutTime { get; set; }

    /// <summary>创建时间（Unix时间戳毫秒）</summary>
    public long CreateTime { get; set; }

    /// <summary>最近一次重连Token，用于单次有效验证</summary>
    [BsonIgnore]
    public string LastToken { get; set; } = "";
}
