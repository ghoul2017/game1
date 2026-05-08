using System;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Game.Core.Database;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Shared.Core.Log;

namespace Game.Database.MongoDb.Repositories;

/// <summary>
/// 通用实体仓库。基于 MongoDB BSON 文档存储，替代 ProtobufPersistFactory + EntityFactory。
/// 集合名 = 类名蛇形（如 RoleBag → role_bag），文档 _id = entity.Id。
/// </summary>
public class MongoEntityRepository
{
    private static readonly ILog Log = LogManager.GetLogger<MongoEntityRepository>();
    private readonly MongoDbManager _mongoDb;

    public MongoEntityRepository(MongoDbManager mongoDb)
    {
        _mongoDb = mongoDb;
    }

    public async Task<T?> LoadAsync<T>(long id) where T : class, IEntity
    {
        var collection = GetCollection<T>();
        var filter = Builders<BsonDocument>.Filter.Eq("_id", id);
        var doc = await collection.Find(filter).FirstOrDefaultAsync();
        if (doc == null) return null;

        var entity = BsonSerializer.Deserialize<T>(doc);
        InvokeAfterDeserialize(entity);
        return entity;
    }

    public async Task InsertAsync<T>(T entity) where T : class, IEntity
    {
        var collection = GetCollection<T>();
        var doc = entity.ToBsonDocument();
        doc["_id"] = entity.Id;
        await collection.InsertOneAsync(doc);
    }

    public async Task UpdateAsync<T>(T entity) where T : class, IEntity
    {
        var collection = GetCollection<T>();
        var doc = entity.ToBsonDocument();
        doc["_id"] = entity.Id;
        var filter = Builders<BsonDocument>.Filter.Eq("_id", entity.Id);
        await collection.ReplaceOneAsync(filter, doc, new ReplaceOptions { IsUpsert = true });
    }

    public async Task DeleteAsync<T>(long id) where T : class, IEntity
    {
        var collection = GetCollection<T>();
        var filter = Builders<BsonDocument>.Filter.Eq("_id", id);
        await collection.DeleteOneAsync(filter);
    }

    /// <summary>确保集合存在并创建必要索引</summary>
    public async Task EnsureCollectionAsync<T>() where T : class, IEntity
    {
        var collectionName = GetCollectionName<T>();
        // MongoDB 自动创建集合，无需显式创建
        Log.Info($"集合 {collectionName} 就绪");
    }

    /// <summary>
    /// 调用实体上的 AfterDeserialize / RebuildIndex 方法。
    /// MongoDB 反序列化不会自动触发 [ProtoAfterDeserialization] 回调，
    /// 需要手动调用以重建运行时索引。
    /// </summary>
    private static void InvokeAfterDeserialize<T>(T entity) where T : class, IEntity
    {
        var method = typeof(T).GetMethod("AfterDeserialize",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        method?.Invoke(entity, null);

        var rebuildMethod = typeof(T).GetMethod("RebuildIndex",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        rebuildMethod?.Invoke(entity, null);
    }

    private IMongoCollection<BsonDocument> GetCollection<T>()
    {
        return _mongoDb.GetCollection<BsonDocument>(GetCollectionName<T>());
    }

    private static string GetCollectionName<T>()
    {
        return ConvertToUnderscore(typeof(T).Name);
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
}
