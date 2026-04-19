using System;
using System.Data.Common;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using ProtoBuf;
using Server.Core.Log;
using Server.Database;

namespace Server.Core.Entity;

/// <summary>
/// 基于 Protobuf 序列化的通用持久化工厂。
/// 将实体序列化为二进制数据存储在 p_&lt;类名&gt; 表中（id, data 两列）。
/// 参考: Java 项目的 ProtostuffPersistFactory。
/// </summary>
public class ProtobufPersistFactory<T> : IPersistFactory<T> where T : class, IEntity
{
    private static readonly ILog Log = LogManager.GetLogger<ProtobufPersistFactory<T>>();
    private readonly string _tableName;

    public Type PersistClass => typeof(T);

    public int TaskPeriodMs { get; }

    public string InsertSql { get; }
    public string UpdateSql { get; }
    public string DeleteSql { get; }
    public string SelectSql { get; }

    public ProtobufPersistFactory(int taskPeriodMs = 60000)
    {
        TaskPeriodMs = taskPeriodMs;
        _tableName = GetTableName();
        InsertSql = $"INSERT INTO {_tableName} (id, data) VALUES (@Id, @Data)";
        UpdateSql = $"UPDATE {_tableName} SET data = @Data WHERE id = @Id";
        DeleteSql = $"DELETE FROM {_tableName} WHERE id = @Id";
        SelectSql = $"SELECT id, data FROM {_tableName} WHERE id = @Id";
    }

    /// <summary>确保表存在（启动时调用）</summary>
    public async Task EnsureTableAsync()
    {
        using var conn = DatabaseManager.Instance.MySql.CreateConnection();
        await conn.ExecuteAsync($@"
            CREATE TABLE IF NOT EXISTS {_tableName} (
                id BIGINT NOT NULL,
                data LONGBLOB,
                PRIMARY KEY (id)
            )");
        Log.Info($"{_tableName} 表就绪");
    }

    public object GetInsertParams(T entity)
    {
        return new { Id = entity.Id, Data = Serialize(entity) };
    }

    public object GetUpdateParams(T entity)
    {
        return new { Id = entity.Id, Data = Serialize(entity) };
    }

    public object GetDeleteParams(T entity)
    {
        return new { Id = entity.Id };
    }

    public async Task<T?> LoadAsync(DbConnection conn, long id)
    {
        var row = await conn.QueryFirstOrDefaultAsync<(long Id, byte[]? Data)>(SelectSql, new { Id = id });
        if (row.Data == null || row.Data.Length == 0) return null;
        return Deserialize(row.Data);
    }

    private static string GetTableName()
    {
        var name = typeof(T).Name;
        return "p_" + ConvertToUnderscore(name);
    }

    private static string ConvertToUnderscore(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        var sb = new StringBuilder();
        for (var i = 0; i < input.Length; i++)
        {
            var c = input[i];
            if (char.IsUpper(c))
            {
                if (i > 0) sb.Append('_');
                sb.Append(char.ToLower(c));
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }

    private static byte[] Serialize(T entity)
    {
        using var ms = new MemoryStream();
        Serializer.Serialize(ms, entity);
        return ms.ToArray();
    }

    private static T Deserialize(byte[] data)
    {
        using var ms = new MemoryStream(data);
        return Serializer.Deserialize<T>(ms);
    }
}
