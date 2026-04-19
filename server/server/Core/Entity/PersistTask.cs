using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Server.Core.Log;
using Server.Database;

namespace Server.Core.Entity;

/// <summary>
/// 持久化变更类型
/// </summary>
public enum PersistType
{
    Insert,
    Update,
    Delete
}

/// <summary>
/// 单个实体类型的 Write-Behind 持久化任务。
/// 维护脏数据变更队列，定时批量写入数据库。
/// 参考: Java 项目的 PersistTask。
/// </summary>
public class PersistTask<T> where T : class, IEntity
{
    private static readonly ILog Log = LogManager.GetLogger<PersistTask<T>>();

    private readonly IPersistFactory<T> _factory;
    private readonly ConcurrentDictionary<long, PersistType> _pending = new();
    private readonly ConcurrentQueue<long> _errorQueue = new();
    private readonly Timer _timer;

    /// <summary>批量写入阈值，达到此数量立即触发持久化</summary>
    public int BatchThreshold { get; set; } = 300;

    public PersistTask(IPersistFactory<T> factory)
    {
        _factory = factory;
        _timer = new Timer(_ => FlushAsync().GetAwaiter().GetResult(), null,
            factory.TaskPeriodMs, factory.TaskPeriodMs);
    }

    /// <summary>记录一个变更操作</summary>
    public void Mark(long id, PersistType type)
    {
        _pending.AddOrUpdate(id, type, (_, existing) => Merge(existing, type));

        // 达到批量阈值立即触发
        if (_pending.Count >= BatchThreshold)
        {
            _ = FlushAsync();
        }
    }

    /// <summary>执行一次批量持久化</summary>
    public async Task FlushAsync()
    {
        if (_pending.IsEmpty) return;

        // 取出当前所有待处理项
        var batch = new Dictionary<long, PersistType>();
        foreach (var kvp in _pending)
        {
            if (_pending.TryRemove(kvp.Key, out _))
                batch[kvp.Key] = kvp.Value;
        }

        if (batch.Count == 0) return;

        var cache = EntityFactory.Instance.GetCache<T>();

        try
        {
            using var conn = DatabaseManager.Instance.MySql.CreateConnection();
            await conn.OpenAsync();
            using var tx = await conn.BeginTransactionAsync();

            try
            {
                foreach (var (id, type) in batch)
                {
                    await ExecuteAsync(conn, tx, cache, id, type);
                }
                await tx.CommitAsync();
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            Log.Error($"批量持久化 {typeof(T).Name} 失败（{batch.Count} 条），将重试", ex);
            // 放回待处理队列，下次重试
            foreach (var (id, type) in batch)
            {
                _pending.TryAdd(id, type);
                _errorQueue.Enqueue(id);
            }
        }
    }

    /// <summary>获取待处理数量</summary>
    public int PendingCount => _pending.Count;

    /// <summary>停止定时器</summary>
    public void Stop()
    {
        _timer.Dispose();
    }

    private async Task ExecuteAsync(DbConnection conn, DbTransaction tx, EntityCache<T> cache, long id, PersistType type)
    {
        switch (type)
        {
            case PersistType.Insert:
            {
                var entity = cache.Get(id);
                if (entity == null) return; // 已被删除，跳过
                await conn.ExecuteAsync(_factory.InsertSql, _factory.GetInsertParams(entity), tx);
                break;
            }
            case PersistType.Update:
            {
                var entity = cache.Get(id);
                if (entity == null) return; // 已被删除，跳过
                await conn.ExecuteAsync(_factory.UpdateSql, _factory.GetUpdateParams(entity), tx);
                break;
            }
            case PersistType.Delete:
                await conn.ExecuteAsync(_factory.DeleteSql, new { Id = id }, tx);
                break;
        }
    }

    /// <summary>
    /// 合并变更类型：
    /// Insert + Update = Insert（还没入库，只需插入最新数据）
    /// Insert + Delete = 忽略（还没入库就删了，什么都不用做）
    /// Update + Delete = Delete（更新后删除，只需删除）
    /// </summary>
    private static PersistType Merge(PersistType existing, PersistType incoming) => (existing, incoming) switch
    {
        (PersistType.Insert, PersistType.Update) => PersistType.Insert,
        (PersistType.Insert, PersistType.Delete) => PersistType.Delete, // 标记为删除，执行时跳过即可
        (PersistType.Update, PersistType.Delete) => PersistType.Delete,
        (PersistType.Delete, PersistType.Insert) => PersistType.Update, // 删了又插回来，等同于更新
        _ => incoming
    };
}
