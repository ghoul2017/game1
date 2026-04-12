using System.Collections.Concurrent;

namespace Server.Core.Entity;

/// <summary>
/// 类型安全的实体内存缓存。
/// 参考: Java 项目的 PersistCache（Guava Cache）。
/// </summary>
public class EntityCache<T> where T : class, IEntity
{
    private readonly ConcurrentDictionary<long, T> _data = new();

    public int Count => _data.Count;

    public T? Get(long id) => _data.TryGetValue(id, out var entity) ? entity : null;

    public void Put(T entity) => _data[entity.Id] = entity;

    /// <summary>
    /// 仅在缓存中不存在时才放入，防止并发加载导致多实例。
    /// 返回 null 表示新放入成功，返回已有实例表示已存在。
    /// </summary>
    public T? PutIfAbsent(T entity)
    {
        var existing = _data.GetOrAdd(entity.Id, entity);
        return ReferenceEquals(existing, entity) ? null : existing;
    }

    public void Remove(long id) => _data.TryRemove(id, out _);

    public IEnumerable<T> GetAll() => _data.Values;

    public bool Contains(long id) => _data.ContainsKey(id);
}
