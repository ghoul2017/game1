using Dapper;

namespace Server.Database.MySql.Repositories;

public class PlayerRepository : IPlayerRepository
{
    private readonly MySqlManager _db;

    public PlayerRepository(MySqlManager db)
    {
        _db = db;
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
