using System;
using System.Threading.Tasks;
using Game.Core;
using Game.Database.MySql;
using Game.Database.MySql.Repositories;
using Game.Database.Redis;
using Shared.Core.Log;

namespace Game.Database;

public class DatabaseManager
{
    private static readonly ILog Log = LogManager.GetLogger<DatabaseManager>();

    public static DatabaseManager Instance { get; } = new();

    public MySqlManager MySql { get; private set; } = null!;
    public RedisManager Redis { get; private set; } = null!;
    public PlayerRepository PlayerRepo { get; private set; } = null!;
    public RoleRepository RoleRepo { get; private set; } = null!;

    public async Task Init(AppConfig config)
    {
        MySql = new MySqlManager(config.MySql);
        Redis = new RedisManager(config.Redis);

        try
        {
            await MySql.InitializeAsync();
        }
        catch (Exception ex)
        {
            Log.Error("MySql 初始化失败", ex);
        }

        try
        {
            // await Redis.InitializeAsync();
        }
        catch (Exception ex)
        {
            Log.Warn($"Redis 初始化异常: {ex.Message}");
        }

        PlayerRepo = new PlayerRepository(MySql);
        RoleRepo = new RoleRepository(MySql);

        // 自动建表
        await PlayerRepo.EnsureTableAsync();
        await RoleRepo.EnsureTableAsync();

        Log.Info("数据库模块初始化完成");
    }

}
