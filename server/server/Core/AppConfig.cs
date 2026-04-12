namespace Server.Core;

public record AppConfig(ServerOptions Server, MySqlOptions MySql, MongoDbOptions MongoDb, RedisOptions Redis, TablesOptions Tables, LogOptions Log);
