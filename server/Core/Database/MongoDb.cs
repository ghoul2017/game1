using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Game.Components.User;
using Shared.Core;
using Shared.Core.Database;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Shared.Core.Log;

namespace Game.Database;

/// <summary>
/// 统一数据库操作入口。合并原 DatabaseManager + MongoDbManager + 3 个 Repository。
/// 业务代码通过 MongoDb.Instance.LoadAsync&lt;T&gt;(id) 直接操作。
/// </summary>
public class MongoDb
{
    private static readonly ILog Log = LogManager.GetLogger<MongoDb>();

    public static MongoDb Instance { get; } = new();

    private MongoClient? _client;
    private IMongoDatabase? _database;

    public async Task Init(MongoDbOptions options)
    {
        _client = new MongoClient(options.ConnectionString);
        _database = _client.GetDatabase(options.DatabaseName);

        // 测试连接
        await _client.ListDatabaseNamesAsync();

        // 创建索引
        await EnsureIndexAsync<UserRole>(r => r.UserId);
        await EnsureIndexAsync<UserRole>(r => r.Name, unique: true);

        Log.Info("数据库模块初始化完成（MongoDB）");
    }


    public async Task<T?> LoadAsync<T>(long id) where T : class, IEntity
    {
        var collection = GetEntityCollection<T>();
        var filter = Builders<BsonDocument>.Filter.Eq("_id", id);
        var doc = await collection.Find(filter).FirstOrDefaultAsync();
        if (doc == null) return null;

        var entity = BsonSerializer.Deserialize<T>(doc);
        return entity;
    }

    public async Task InsertAsync<T>(T entity) where T : class, IEntity
    {
        var collection = GetEntityCollection<T>();
        var doc = entity.ToBsonDocument();
        doc["_id"] = entity.Id;
        await collection.InsertOneAsync(doc);
    }

    public async Task UpdateAsync<T>(T entity) where T : class, IEntity
    {
        var collection = GetEntityCollection<T>();
        var doc = entity.ToBsonDocument();
        doc["_id"] = entity.Id;
        var filter = Builders<BsonDocument>.Filter.Eq("_id", entity.Id);
        await collection.ReplaceOneAsync(filter, doc, new ReplaceOptions { IsUpsert = true });
    }

    public async Task DeleteAsync<T>(long id) where T : class, IEntity
    {
        var collection = GetEntityCollection<T>();
        var filter = Builders<BsonDocument>.Filter.Eq("_id", id);
        await collection.DeleteOneAsync(filter);
    }



    public async Task<List<T>> QueryAsync<T>(Expression<Func<T, bool>> filter)
    {
        var collection = GetTypedCollection<T>();
        return await collection.Find(filter).ToListAsync();
    }

    public async Task<T?> QueryFirstOrDefaultAsync<T>(Expression<Func<T, bool>> filter)
    {
        var collection = GetTypedCollection<T>();
        return await collection.Find(filter).FirstOrDefaultAsync();
    }

    public async Task<bool> AnyAsync<T>(Expression<Func<T, bool>> filter)
    {
        var collection = GetTypedCollection<T>();
        return await collection.Find(filter).AnyAsync();
    }



    public async Task SaveAsync<T>(T document) where T : notnull
    {
        var collection = GetTypedCollection<T>();
        await collection.InsertOneAsync(document);
    }

    public async Task ReplaceAsync<T>(Expression<Func<T, bool>> filter, T document) where T : notnull
    {
        var collection = GetTypedCollection<T>();
        await collection.ReplaceOneAsync(filter, document);
    }

    public async Task RemoveAsync<T>(Expression<Func<T, bool>> filter) where T : notnull
    {
        var collection = GetTypedCollection<T>();
        await collection.DeleteOneAsync(filter);
    }



    public async Task EnsureIndexAsync<T>(Expression<Func<T, object>> field, bool unique = false)
    {
        var collection = GetTypedCollection<T>();
        await collection.Indexes.CreateOneAsync(
            new CreateIndexModel<T>(Builders<T>.IndexKeys.Ascending(field),
                new CreateIndexOptions { Unique = unique }));
    }



    private IMongoCollection<BsonDocument> GetEntityCollection<T>()
    {
        if (_database == null) throw new InvalidOperationException("MongoDb 未初始化");
        return _database.GetCollection<BsonDocument>(ConvertToUnderscore(typeof(T).Name));
    }

    private IMongoCollection<T> GetCollection<T>(string? name = null)
    {
        if (_database == null) throw new InvalidOperationException("MongoDb 未初始化");
        return _database.GetCollection<T>(name ?? typeof(T).Name);
    }

    private IMongoCollection<T> GetTypedCollection<T>()
    {
        if (_database == null) throw new InvalidOperationException("MongoDb 未初始化");
        return _database.GetCollection<T>(ConvertToUnderscore(typeof(T).Name));
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
