namespace Server.Core;

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

public class LogOptions
{
    public string Level { get; set; } = "Info";
    public string? Format { get; set; }
    public Dictionary<string, string>? Loggers { get; set; }
    public LogConsoleOptions? Console { get; set; }
    public LogFileOptions? File { get; set; }
}

public class LogConsoleOptions
{
    public bool Enabled { get; set; } = true;
    public bool Colorful { get; set; } = true;
}

public class LogFileOptions
{
    public bool Enabled { get; set; } = true;
    public string Path { get; set; } = "log/game.log";
    public string ErrorPath { get; set; } = "log/error.log";
    public int MaxHistoryDays { get; set; } = 7;
}
