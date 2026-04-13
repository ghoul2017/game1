using System.Collections.Concurrent;
using Server.Core.Log;

namespace Server.Core;

/// <summary>
/// 内存 Token 存储。替代 Redis 用于重连 Token 管理。
/// 支持过期时间和后台自动清理。
/// </summary>
public class TokenStore
{
    private static readonly ILog Log = LogManager.GetLogger<TokenStore>();

    public static TokenStore Instance { get; } = new();

    private readonly ConcurrentDictionary<string, (string Value, DateTime ExpireAt)> _store = new();
    private Timer? _cleanupTimer;

    public TokenStore()
    {
        // 每 30 秒清理过期条目
        _cleanupTimer = new Timer(_ => Cleanup(), null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        Log.Info("TokenStore 初始化");
    }

    public Task SetAsync(string key, string value, TimeSpan? expiry = null)
    {
        _store[key] = (value, expiry.HasValue ? DateTime.UtcNow + expiry.Value : DateTime.MaxValue);
        return Task.CompletedTask;
    }

    public Task<string?> GetAsync(string key)
    {
        if (_store.TryGetValue(key, out var entry) && entry.ExpireAt > DateTime.UtcNow)
            return Task.FromResult<string?>(entry.Value);

        _store.TryRemove(key, out _);
        return Task.FromResult<string?>(null);
    }

    public Task RemoveAsync(string key)
    {
        _store.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    private void Cleanup()
    {
        var now = DateTime.UtcNow;
        foreach (var kvp in _store)
        {
            if (kvp.Value.ExpireAt <= now)
                _store.TryRemove(kvp.Key, out _);
        }
    }
}
