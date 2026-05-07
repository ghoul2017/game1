using System;
using System.Threading.Tasks;
using MongoDB.Driver;
using Shared.Core.Log;

namespace Game.Database.MongoDb.Repositories;

/// <summary>
/// 玩家账号仓库（MongoDB）。替代 PlayerRepository(MySQL)。
/// 集合名: player，文档 { _id, username, password, nickname, level, createTime }。
/// </summary>
public class MongoPlayerRepository
{
    private static readonly ILog Log = LogManager.GetLogger<MongoPlayerRepository>();
    private readonly MongoDbManager _mongoDb;
    private IMongoCollection<PlayerAccount> Collection => _mongoDb.GetCollection<PlayerAccount>("player");

    public MongoPlayerRepository(MongoDbManager mongoDb)
    {
        _mongoDb = mongoDb;
    }

    public async Task EnsureIndexAsync()
    {
        await Collection.Indexes.CreateOneAsync(
            new CreateIndexModel<PlayerAccount>(Builders<PlayerAccount>.IndexKeys.Ascending(p => p.Username),
                new CreateIndexOptions { Unique = true }));
        Log.Info("player 索引就绪");
    }

    public async Task<PlayerAccount?> GetByIdAsync(long id)
    {
        return await Collection.Find(p => p.Id == id).FirstOrDefaultAsync();
    }

    public async Task<PlayerAccount?> GetByUsernameAsync(string username)
    {
        return await Collection.Find(p => p.Username == username).FirstOrDefaultAsync();
    }

    public async Task<long> CreateAsync(PlayerAccount player)
    {
        await Collection.InsertOneAsync(player);
        return player.Id;
    }

    public async Task UpdateAsync(PlayerAccount player)
    {
        await Collection.ReplaceOneAsync(p => p.Id == player.Id, player);
    }
}

/// <summary>玩家账号数据（MongoDB 文档映射）</summary>
public class PlayerAccount
{
    public long Id { get; set; }
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string Nickname { get; set; } = "";
    public int Level { get; set; }
    public DateTime CreateTime { get; set; }
}
