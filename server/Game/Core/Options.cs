using System.Collections.Generic;

namespace Game.Core;

public class ServerOptions
{
    public string Host { get; set; } = "0.0.0.0";
    public int Port { get; set; } = 9000;
    public int MaxConnections { get; set; } = 5000;
}

public class MySqlOptions
{
    public string ConnectionString { get; set; } = "";
}

public class MongoDbOptions
{
    public string ConnectionString { get; set; } = "";
    public string DatabaseName { get; set; } = "";
}

public class RedisOptions
{
    public string ConnectionString { get; set; } = "";
}

public class TablesOptions
{
    public string Directory { get; set; } = "Data/Tables";
}


