using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Driver;
using Shared.Core.Log;

namespace Game.Database.MongoDb.Repositories;

/// <summary>
/// 角色索引仓库（MongoDB）。替代 RoleRepository(MySQL)。
/// 集合名: user_role，文档 { _id, userId, name, level, job }。
/// 用于快速查询用户的角色列表，完整角色数据存储在 role 集合（MongoEntityRepository）。
/// </summary>
public class MongoRoleRepository
{
    private static readonly ILog Log = LogManager.GetLogger<MongoRoleRepository>();
    private readonly MongoDbManager _mongoDb;
    private IMongoCollection<UserRole> Collection => _mongoDb.GetCollection<UserRole>("user_role");

    public MongoRoleRepository(MongoDbManager mongoDb)
    {
        _mongoDb = mongoDb;
    }

    public async Task EnsureIndexAsync()
    {
        await Collection.Indexes.CreateOneAsync(
            new CreateIndexModel<UserRole>(Builders<UserRole>.IndexKeys.Ascending(r => r.UserId),
                new CreateIndexOptions { }));
        await Collection.Indexes.CreateOneAsync(
            new CreateIndexModel<UserRole>(Builders<UserRole>.IndexKeys.Ascending(r => r.Name),
                new CreateIndexOptions { Unique = true }));
        Log.Info("user_role 索引就绪");
    }

    public async Task<List<UserRole>> GetByUserIdAsync(long userId)
    {
        return await Collection.Find(r => r.UserId == userId).ToListAsync();
    }

    public async Task<bool> NameExistsAsync(string name)
    {
        return await Collection.Find(r => r.Name == name).AnyAsync();
    }

    public async Task AddAsync(UserRole role)
    {
        await Collection.InsertOneAsync(role);
    }

    public async Task UpdateAsync(UserRole role)
    {
        await Collection.ReplaceOneAsync(r => r.RoleId == role.RoleId, role);
    }

    public async Task DeleteAsync(long roleId)
    {
        await Collection.DeleteOneAsync(r => r.RoleId == roleId);
    }
}

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
