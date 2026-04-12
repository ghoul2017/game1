using System.Data.Common;

namespace Server.Core.Entity;

/// <summary>
/// 持久化工厂接口。为特定实体类型定义数据库 CRUD 操作。
/// 参考: Java 项目的 PersistFactory 接口。
/// </summary>
public interface IPersistFactory<T> where T : class, IEntity
{
    /// <summary>持久化的实体类型</summary>
    Type PersistClass { get; }

    /// <summary>批量持久化的周期（毫秒），默认 60000</summary>
    int TaskPeriodMs { get; }

    /// <summary>插入 SQL</summary>
    string InsertSql { get; }

    /// <summary>更新 SQL</summary>
    string UpdateSql { get; }

    /// <summary>删除 SQL</summary>
    string DeleteSql { get; }

    /// <summary>查询 SQL</summary>
    string SelectSql { get; }

    /// <summary>创建插入参数（Dapper 匿名对象）</summary>
    object GetInsertParams(T entity);

    /// <summary>创建更新参数</summary>
    object GetUpdateParams(T entity);

    /// <summary>创建删除参数</summary>
    object GetDeleteParams(T entity);

    /// <summary>从数据库连接加载实体</summary>
    Task<T?> LoadAsync(DbConnection conn, long id);

    /// <summary>确保数据库表存在（启动时自动调用）</summary>
    Task EnsureTableAsync();
}
