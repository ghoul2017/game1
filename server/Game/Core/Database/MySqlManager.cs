using System.Threading.Tasks;
using Game.Core;
using MySqlConnector;
using Shared.Core.Log;

namespace Game.Database.MySql;

public class MySqlManager
{
    private static readonly ILog Log = LogManager.GetLogger<MySqlManager>();
    private readonly string _connectionString;

    public MySqlManager(MySqlOptions options)
    {
        _connectionString = options.ConnectionString;
    }

    public async Task InitializeAsync()
    {
        using var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync();
        Log.Info("连接成功");
    }

    public MySqlConnection CreateConnection()
    {
        return new MySqlConnection(_connectionString);
    }

}
