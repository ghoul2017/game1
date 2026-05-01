using Shared.Core.Log;

namespace Game.Core;

public class AppConfig
{
    public static AppConfig Instance { get; set; } = null!;
    public required ServerOptions Server { get; init; }
    public required MySqlOptions MySql { get; init; } 
    public required MongoDbOptions MongoDb { get; init; } 
    public required RedisOptions Redis { get; init; } 
    public required TablesOptions Tables { get; init; } 
    public required LogOptions Log { get; init; } 
}