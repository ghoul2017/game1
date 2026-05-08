namespace Game.Components.User;

/// <summary>角色索引记录（MongoDB 文档映射，集合名 user_role）</summary>
public class UserRole
{
    /// <summary>_id = roleId</summary>
    public long RoleId { get; set; }
    public long UserId { get; set; }
    public string Name { get; set; } = "";
    public int Level { get; set; }
    public int Job { get; set; }
}
