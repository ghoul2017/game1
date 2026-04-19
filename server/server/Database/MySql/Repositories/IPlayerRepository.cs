using System;
using System.Threading.Tasks;

namespace Server.Database.MySql.Repositories;

public class Player
{
    public long Id { get; set; }
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string Nickname { get; set; } = "";
    public int Level { get; set; }
    public DateTime CreateTime { get; set; }
}

public interface IPlayerRepository
{
    Task<Player?> GetByIdAsync(long id);
    Task<Player?> GetByUsernameAsync(string username);
    Task<long> CreateAsync(Player player);
    Task UpdateAsync(Player player);
}
