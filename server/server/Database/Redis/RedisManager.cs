using System.Collections.Concurrent;
using Server.Core;
using Server.Core.Log;
using StackExchange.Redis;

namespace Server.Database.Redis;

/// <summary>
/// Redis 管理器。支持两种模式：
/// - Redis 模式：连接真实 Redis 服务
/// - 本地模式：连接失败时自动降级，使用内存存储（适用于开发环境）
/// </summary>
public class RedisManager
{
    private static readonly ILog Log = LogManager.GetLogger<RedisManager>();
    private readonly string _connectionString;
    private ConnectionMultiplexer? _redis;
    private IDatabase? _db;

    // 本地模式存储
    private readonly ConcurrentDictionary<string, (string Value, DateTime ExpireAt)> _localCache = new();
    private readonly ConcurrentDictionary<string, long> _localCounters = new();

    public bool IsLocalMode => _redis == null;

    public RedisManager(RedisOptions options)
    {
        _connectionString = options.ConnectionString;
    }

    public async Task InitializeAsync()
    {
        if (string.IsNullOrEmpty(_connectionString))
        {
            Log.Warn("Redis 连接字符串为空，使用本地内存模式");
            return;
        }

        try
        {
            _redis = await ConnectionMultiplexer.ConnectAsync(_connectionString);
            _db = _redis.GetDatabase();
            Log.Info("连接成功");
        }
        catch (Exception ex)
        {
            Log.Warn($"Redis 连接失败，使用本地内存模式: {ex.Message}");
            _redis = null;
            _db = null;
        }
    }

    // String
    public Task<bool> StringSetAsync(string key, string value, TimeSpan? expiry = null)
    {
        if (_db != null)
            return _db.StringSetAsync(key, value, expiry);

        _localCache[key] = (value, expiry.HasValue ? DateTime.UtcNow + expiry.Value : DateTime.MaxValue);
        return Task.FromResult(true);
    }

    public Task<RedisValue> StringGetAsync(string key)
    {
        if (_db != null)
            return _db.StringGetAsync(key);

        if (_localCache.TryGetValue(key, out var entry) && entry.ExpireAt > DateTime.UtcNow)
            return Task.FromResult(new RedisValue(entry.Value));

        _localCache.TryRemove(key, out _);
        return Task.FromResult(RedisValue.Null);
    }

    public Task<bool> KeyDeleteAsync(string key)
    {
        if (_db != null)
            return _db.KeyDeleteAsync(key);

        _localCache.TryRemove(key, out _);
        return Task.FromResult(true);
    }

    public Task<long> StringIncrementAsync(string key, long value = 1)
    {
        if (_db != null)
            return _db.StringIncrementAsync(key, value);

        var result = _localCounters.AddOrUpdate(key, value, (_, old) => old + value);
        return Task.FromResult(result);
    }

    // Hash
    public Task HashSetAsync(string key, HashEntry[] fields)
    {
        if (_db != null)
            return _db.HashSetAsync(key, fields);

        throw new NotImplementedException("Hash not supported in local mode");
    }

    public Task<HashEntry[]> HashGetAllAsync(string key)
    {
        if (_db != null)
            return _db.HashGetAllAsync(key);

        throw new NotImplementedException("Hash not supported in local mode");
    }

    // SortedSet (排行榜)
    public Task<bool> SortedSetAddAsync(string key, string member, double score)
    {
        if (_db != null)
            return _db.SortedSetAddAsync(key, member, score);

        throw new NotImplementedException("SortedSet not supported in local mode");
    }

    public Task<SortedSetEntry[]> SortedSetRangeByRankWithScoresAsync(string key, long start = 0, long stop = -1)
    {
        if (_db != null)
            return _db.SortedSetRangeByRankWithScoresAsync(key, start, stop, Order.Descending);

        throw new NotImplementedException("SortedSet not supported in local mode");
    }
}
