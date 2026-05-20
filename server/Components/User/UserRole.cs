using Shared.Core.Database;
using MongoDB.Bson.Serialization.Attributes;

namespace Game.Components.User;

/// <summary>角色索引记录（MongoDB 文档映射，集合名 user_role）</summary>
public class UserRole : IEntity
{
    /// <summary>_id = roleId</summary>
    [BsonId]
    public long Id
    {
        get => RoleId;
        set => RoleId = value;
    }

    public long RoleId { get; set; }
    public long UserId { get; set; }
    public string Name { get; set; } = "";
    public int Level { get; set; }
    public int Job { get; set; }
}
