using System.Threading.Tasks;
using Dapper;
using Shared.Core.Log;

namespace Game.Database.MySql.Repositories;

public class PlayerRepository : IPlayerRepository
{
    private static readonly ILog Log = LogManager.GetLogger<PlayerRepository>();
    private readonly MySqlManager _db;

    public PlayerRepository(MySqlManager db)
    {
        _db = db;
    }

    /// <summary>确保 player 表存在</summary>
    public async Task EnsureTableAsync()
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS player (
                Id BIGINT PRIMARY KEY AUTO_INCREMENT,
                Username VARCHAR(32) NOT NULL UNIQUE,
                Password VARCHAR(64) NOT NULL,
                Nickname VARCHAR(32) NOT NULL,
                Level INT DEFAULT 1,
                CreateTime DATETIME NOT NULL,
                INDEX idx_username (Username)
            )");
        Log.Info("player 表就绪");
    }

    public async Task<Player?> GetByIdAsync(long id)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<Player>(
            "SELECT * FROM player WHERE Id = @Id", new { Id = id });
    }

    public async Task<Player?> GetByUsernameAsync(string username)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<Player>(
            "SELECT * FROM player WHERE Username = @Username", new { Username = username });
    }

    public async Task<long> CreateAsync(Player player)
    {
        using var conn = _db.CreateConnection();
        var sql = @"INSERT INTO player (Username, Password, Nickname, Level, CreateTime)
                     VALUES (@Username, @Password, @Nickname, @Level, @CreateTime);
                     SELECT LAST_INSERT_ID();";
        return await conn.ExecuteScalarAsync<long>(sql, player);
    }

    public async Task UpdateAsync(Player player)
    {
        using var conn = _db.CreateConnection();
        var sql = @"UPDATE player SET Nickname = @Nickname, Level = @Level
                     WHERE Id = @Id";
        await conn.ExecuteAsync(sql, player);
    }
}
