using System;
using System.Collections.Concurrent;
using System.Data.Common;
using System.Threading.Tasks;
using Dapper;
using Game.Database;
using Shared.Core.Log;
using Game.Database.MySql;

namespace Game.Core.Entity;

/// <summary>
/// 实体工厂。管理实体的生命周期（创建、加载、缓存、持久化）。
/// 集成 Write-Behind 缓存：脏数据变更通过 PersistTask 定时批量持久化。
/// 参考: Java 项目的 DataCache + DataCenter + PersistTask。
/// </summary>
public class EntityFactory
{
    private static readonly ILog Log = LogManager.GetLogger<EntityFactory>();
    public static EntityFactory Instance { get; } = new();

    private readonly ConcurrentDictionary<Type, object> _caches = new();
    private readonly ConcurrentDictionary<Type, object> _factories = new();
    private readonly ConcurrentDictionary<Type, object> _tasks = new();

    private EntityFactory() { }

    /// <summary>注册一个实体类型的持久化工厂</summary>
    public async Task RegisterAsync<T>(IPersistFactory<T> factory) where T : class, IEntity
    {
        await factory.EnsureTableAsync();
        _factories[typeof(T)] = factory;
        _caches[typeof(T)] = new EntityCache<T>();
        _tasks[typeof(T)] = new PersistTask<T>(factory);
        Log.Info($"注册实体工厂: {typeof(T).Name}, 周期={factory.TaskPeriodMs}ms");
    }

    /// <summary>从缓存或数据库获取实体</summary>
    public async Task<T?> GetAsync<T>(long id) where T : class, IEntity
    {
        var cache = GetCache<T>();
        var factory = GetFactory<T>();

        // 先从缓存获取
        var cached = cache.Get(id);
        if (cached != null) return cached;

        // 缓存未命中，从数据库加载
        using var conn = DatabaseManager.Instance.MySql.CreateConnection();
        var entity = await factory.LoadAsync(conn, id);
        if (entity == null) return null;

        // 放入缓存（防止并发加载导致多实例）
        var existing = cache.PutIfAbsent(entity);
        return existing ?? entity;
    }

    /// <summary>插入新实体</summary>
    /// <param name="immediately">true=立即写入数据库；false=加入 Write-Behind 队列</param>
    public async Task InsertAsync<T>(T entity, bool immediately = false) where T : class, IEntity
    {
        var cache = GetCache<T>();
        var factory = GetFactory<T>();

        cache.Put(entity);

        if (immediately)
        {
            using var conn = DatabaseManager.Instance.MySql.CreateConnection();
            await conn.ExecuteAsync(factory.InsertSql, factory.GetInsertParams(entity));
        }
        else
        {
            GetTask<T>().Mark(entity.Id, PersistType.Insert);
        }
    }

    /// <summary>更新实体</summary>
    /// <param name="immediately">true=立即写入数据库；false=加入 Write-Behind 队列</param>
    public async Task UpdateAsync<T>(T entity, bool immediately = false) where T : class, IEntity
    {
        var factory = GetFactory<T>();

        if (immediately)
        {
            using var conn = DatabaseManager.Instance.MySql.CreateConnection();
            await conn.ExecuteAsync(factory.UpdateSql, factory.GetUpdateParams(entity));
        }
        else
        {
            GetTask<T>().Mark(entity.Id, PersistType.Update);
        }
    }

    /// <summary>删除实体</summary>
    /// <param name="immediately">true=立即写入数据库；false=加入 Write-Behind 队列</param>
    public async Task DeleteAsync<T>(T entity, bool immediately = false) where T : class, IEntity
    {
        var cache = GetCache<T>();
        var factory = GetFactory<T>();

        cache.Remove(entity.Id);

        if (immediately)
        {
            using var conn = DatabaseManager.Instance.MySql.CreateConnection();
            await conn.ExecuteAsync(factory.DeleteSql, factory.GetDeleteParams(entity));
        }
        else
        {
            GetTask<T>().Mark(entity.Id, PersistType.Delete);
        }
    }

    /// <summary>强制持久化所有脏数据（停服时调用）</summary>
    public async Task FlushAllAsync()
    {
        Log.Info("开始持久化所有脏数据...");
        foreach (var kvp in _tasks)
        {
            var taskType = typeof(PersistTask<>).MakeGenericType(kvp.Key);
            var flushMethod = taskType.GetMethod("FlushAsync")!;
            await (Task)flushMethod.Invoke(kvp.Value, null)!;
        }
        Log.Info("所有脏数据持久化完成");
    }

    /// <summary>停止所有定时任务（停服时调用）</summary>
    public void StopAllTasks()
    {
        foreach (var kvp in _tasks)
        {
            var taskType = typeof(PersistTask<>).MakeGenericType(kvp.Key);
            var stopMethod = taskType.GetMethod("Stop")!;
            stopMethod.Invoke(kvp.Value, null);
        }
    }

    /// <summary>获取指定类型实体的缓存数量</summary>
    public int GetCacheCount<T>() where T : class, IEntity
    {
        return GetCache<T>().Count;
    }

    internal EntityCache<T> GetCache<T>() where T : class, IEntity
    {
        return (EntityCache<T>)_caches[typeof(T)];
    }

    private IPersistFactory<T> GetFactory<T>() where T : class, IEntity
    {
        return (IPersistFactory<T>)_factories[typeof(T)];
    }

    private PersistTask<T> GetTask<T>() where T : class, IEntity
    {
        return (PersistTask<T>)_tasks[typeof(T)];
    }
}
