using Dapper;
using Server.Core.Log;

namespace Server.Database.MySql.Repositories;

/// <summary>
/// 角色索引表。用于快速查询用户的角色列表，完整的角色数据存储在 p_role（EntityFactory）。
/// </summary>
public class RoleRepository
{
    private static readonly ILog Log = LogManager.GetLogger<RoleRepository>();
    private readonly MySqlManager _db;

    public RoleRepository(MySqlManager db)
    {
        _db = db;
    }

    /// <summary>确保 user_role 表存在</summary>
    public async Task EnsureTableAsync()
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS user_role (
                role_id BIGINT PRIMARY KEY,
                user_id BIGINT NOT NULL,
                name VARCHAR(32) NOT NULL,
                level INT DEFAULT 1,
                job INT DEFAULT 0,
                INDEX idx_user_id (user_id)
            )");
        Log.Info("user_role 表就绪");
    }

    /// <summary>查询用户的所有角色</summary>
    public async Task<List<UserRole>> GetByUserIdAsync(long userId)
    {
        using var conn = _db.CreateConnection();
        var rows = await conn.QueryAsync<UserRole>(
            "SELECT role_id RoleId, user_id UserId, name Name, level Level, job Job FROM user_role WHERE user_id = @UserId",
            new { UserId = userId });
        return rows.ToList();
    }

    /// <summary>检查角色名是否已存在</summary>
    public async Task<bool> NameExistsAsync(string name)
    {
        using var conn = _db.CreateConnection();
        return await conn.ExecuteScalarAsync<bool>(
            "SELECT COUNT(1) FROM user_role WHERE name = @Name", new { Name = name });
    }

    /// <summary>添加角色索引记录</summary>
    public async Task AddAsync(UserRole role)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(
            "INSERT INTO user_role (role_id, user_id, name, level, job) VALUES (@RoleId, @UserId, @Name, @Level, @Job)",
            role);
    }

    /// <summary>更新角色索引（等级变化等）</summary>
    public async Task UpdateAsync(UserRole role)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(
            "UPDATE user_role SET name=@Name, level=@Level, job=@Job WHERE role_id=@RoleId",
            role);
    }

    /// <summary>删除角色索引</summary>
    public async Task DeleteAsync(long roleId)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync("DELETE FROM user_role WHERE role_id = @RoleId", new { RoleId = roleId });
    }
}

/// <summary>角色索引记录</summary>
public class UserRole
{
    public long RoleId { get; set; }
    public long UserId { get; set; }
    public string Name { get; set; } = "";
    public int Level { get; set; }
    public int Job { get; set; }
}
