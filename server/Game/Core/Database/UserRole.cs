namespace Game.Database;

/// <summary>角色索引记录（MongoDB 文档映射）</summary>
public class UserRole
{
    /// <summary>_id = roleId</summary>
    public long RoleId { get; set; }
    public long UserId { get; set; }
    public string Name { get; set; } = "";
    public int Level { get; set; }
    public int Job { get; set; }
}
