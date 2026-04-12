using MongoDB.Driver;
using Server.Core;
using Server.Core.Log;

namespace Server.Database.MongoDb;

public class MongoDbManager
{
    private static readonly ILog Log = LogManager.GetLogger<MongoDbManager>();
    private readonly string _connectionString;
    private readonly string _databaseName;
    private MongoClient? _client;
    private IMongoDatabase? _database;

    public MongoDbManager(MongoDbOptions options)
    {
        _connectionString = options.ConnectionString;
        _databaseName = options.DatabaseName;
    }

    public async Task InitializeAsync()
    {
        _client = new MongoClient(_connectionString);
        _database = _client.GetDatabase(_databaseName);

        // 测试连接
        await _client.ListDatabaseNamesAsync();
        Log.Info("连接成功");
    }

    public IMongoCollection<T> GetCollection<T>(string? name = null)
    {
        if (_database == null) throw new InvalidOperationException("MongoDb 未初始化");
        return _database.GetCollection<T>(name ?? typeof(T).Name);
    }

}
