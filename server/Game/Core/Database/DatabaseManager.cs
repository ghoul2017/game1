using System;
using System.Threading.Tasks;
using Game.Core;
using Game.Database.MongoDb;
using Game.Database.MongoDb.Repositories;
using Shared.Core.Log;

namespace Game.Database;

public class DatabaseManager
{
    private static readonly ILog Log = LogManager.GetLogger<DatabaseManager>();

    public static DatabaseManager Instance { get; } = new();

    public MongoDbManager MongoDb { get; private set; } = null!;
    public MongoPlayerRepository PlayerRepo { get; private set; } = null!;
    public MongoRoleRepository RoleRepo { get; private set; } = null!;
    public MongoEntityRepository EntityRepo { get; private set; } = null!;

    public async Task Init(AppConfig config)
    {
        MongoDb = new MongoDbManager(config.MongoDb);

        try
        {
            await MongoDb.InitializeAsync();
        }
        catch (Exception ex)
        {
            Log.Error("MongoDB 初始化失败", ex);
            throw;
        }

        PlayerRepo = new MongoPlayerRepository(MongoDb);
        RoleRepo = new MongoRoleRepository(MongoDb);
        EntityRepo = new MongoEntityRepository(MongoDb);

        // 创建索引
        await PlayerRepo.EnsureIndexAsync();
        await RoleRepo.EnsureIndexAsync();

        Log.Info("数据库模块初始化完成（MongoDB）");
    }
}
