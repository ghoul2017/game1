using Server.Core;
using Server.Core.Log;
using Server.Database.MySql;
using Server.Database.MySql.Repositories;
using Server.Database.Redis;

namespace Server.Database;

public class DatabaseManager
{
    private static readonly ILog Log = LogManager.GetLogger<DatabaseManager>();

    public static DatabaseManager Instance { get; } = new();

    public MySqlManager MySql { get; private set; } = null!;
    public RedisManager Redis { get; private set; } = null!;
    public PlayerRepository PlayerRepo { get; private set; } = null!;
    public RoleRepository RoleRepo { get; private set; } = null!;

    public int Order => 10;

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
            await Redis.InitializeAsync();
        }
        catch (Exception ex)
        {
            Log.Error("Redis 初始化失败", ex);
        }

        PlayerRepo = new PlayerRepository(MySql);
        RoleRepo = new RoleRepository(MySql);

        Log.Info("数据库模块初始化完成");
    }

}
