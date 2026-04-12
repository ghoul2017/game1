using Server.Core;
using Server.Core.Log;
using StackExchange.Redis;

namespace Server.Database.Redis;

public class RedisManager
{
    private static readonly ILog Log = LogManager.GetLogger<RedisManager>();
    private readonly string _connectionString;
    private ConnectionMultiplexer? _redis;
    private IDatabase? _db;

    public RedisManager(RedisOptions options)
    {
        _connectionString = options.ConnectionString;
    }

    public async Task InitializeAsync()
    {
        _redis = await ConnectionMultiplexer.ConnectAsync(_connectionString);
        _db = _redis.GetDatabase();
        Log.Info("连接成功");
    }

    public IDatabase Database => _db ?? throw new InvalidOperationException("Redis 未初始化");

    // String
    public Task<bool> StringSetAsync(string key, string value, TimeSpan? expiry = null)
        => Database.StringSetAsync(key, value, expiry);

    public Task<RedisValue> StringGetAsync(string key)
        => Database.StringGetAsync(key);

    public Task<bool> KeyDeleteAsync(string key)
        => Database.KeyDeleteAsync(key);

    public Task<long> StringIncrementAsync(string key, long value = 1)
        => Database.StringIncrementAsync(key, value);

    // Hash
    public Task HashSetAsync(string key, HashEntry[] fields)
        => Database.HashSetAsync(key, fields);

    public Task<HashEntry[]> HashGetAllAsync(string key)
        => Database.HashGetAllAsync(key);

    // SortedSet (排行榜)
    public Task<bool> SortedSetAddAsync(string key, string member, double score)
        => Database.SortedSetAddAsync(key, member, score);

    public Task<SortedSetEntry[]> SortedSetRangeByRankWithScoresAsync(string key, long start = 0, long stop = -1)
        => Database.SortedSetRangeByRankWithScoresAsync(key, start, stop, Order.Descending);

}
