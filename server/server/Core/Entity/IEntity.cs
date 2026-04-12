namespace Server.Core.Entity;

/// <summary>
/// 可缓存的实体接口。所有需要持久化的游戏实体都应实现此接口。
/// 参考: Java 项目的 Cacheable 接口。
/// </summary>
public interface IEntity
{
    long Id { get; set; }
}
