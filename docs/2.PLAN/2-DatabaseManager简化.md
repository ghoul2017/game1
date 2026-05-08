# DatabaseManager 简化

## 概述
将 DatabaseManager 简化为单一入口，业务代码通过 `MongoDb.LoadAsync<T>(id)` / `MongoDb.UpdateAsync<T>(entity)` 等泛型方法直接操作，去掉中间 Repository 层。

## 现状分析

当前 3 层结构：`DatabaseManager → MongoXxxRepository → MongoDbManager`

- `MongoEntityRepository`：通用 IEntity CRUD，集合名自动转换
- `MongoPlayerRepository`：PlayerAccount 专用，4 个方法
- `MongoRoleRepository`：UserRole 专用，6 个方法
- `MongoDbManager`：底层连接管理

调用方式冗长：`DatabaseManager.Instance.EntityRepo.LoadAsync<RoleBagComponent>(roleId)`

## 目标调用方式

```csharp
// 通用实体（IEntity）
var bag = await MongoDb.LoadAsync<RoleBagComponent>(roleId);
await MongoDb.UpdateAsync(bag);
await MongoDb.InsertAsync(bag);
await MongoDb.DeleteAsync<RoleBagComponent>(roleId);

// 条件查询
var roles = await MongoDb.QueryAsync<UserRole>(r => r.UserId == userId);
var account = await MongoDb.QueryFirstOrDefaultAsync<PlayerAccount>(p => p.Username == username);
```

## 新增文件

### `Game.Database.MongoDb` — `server/Game/Core/Database/MongoDb.cs`
- 用途：统一的数据库操作入口，替代 DatabaseManager + 3 个 Repository
- 单例模式：`public static MongoDb Instance { get; } = new();`
- 字段：
  - `MongoClient? _client`
  - `IMongoDatabase? _database`
  - `MongoDbOptions _options`（暂存，Init 时使用）
- 方法：
  - `public async Task Init(MongoDbOptions options)` — 初始化连接、创建索引
  - `public async Task<T?> LoadAsync<T>(long id) where T : class, IEntity` — 按 ID 加载
  - `public async Task InsertAsync<T>(T entity) where T : class, IEntity` — 插入
  - `public async Task UpdateAsync<T>(T entity) where T : class, IEntity` — 更新（Upsert）
  - `public async Task DeleteAsync<T>(long id) where T : class, IEntity` — 删除
  - `public async Task<List<T>> QueryAsync<T>(Expression<Func<T, bool>> filter)` — 条件查询（非 IEntity 也可用）
  - `public async Task<T?> QueryFirstOrDefaultAsync<T>(Expression<Func<T, bool>> filter)` — 条件查首条
  - `public async Task InsertDirectAsync<T>(string collectionName, T document)` — 直接插入指定集合
  - `public async Task UpdateDirectAsync<T>(string collectionName, Expression<Func<T, bool>> filter, T document)` — 直接更新指定集合
  - `public async Task DeleteDirectAsync<T>(string collectionName, Expression<Func<T, bool>> filter)` — 直接删除指定集合
  - `public async Task<bool> AnyAsync<T>(Expression<Func<T, bool>> filter)` — 条件判断是否存在
  - `public async Task EnsureIndexAsync<T>(Expression<Func<T, object>> field, bool unique = false)` — 创建索引
- 内部方法：
  - `private IMongoCollection<BsonDocument> GetEntityCollection<T>()` — IEntity 集合（蛇形命名）
  - `private IMongoCollection<T> GetCollection<T>(string? name = null)` — 通用集合获取
  - `private static string ConvertToUnderscore(string input)` — PascalCase → snake_case
  - `private static void InvokeAfterDeserialize<T>(T entity)` — 反射调用 AfterDeserialize/RebuildIndex

## 修改文件

### `server/Game/Core/Database/DatabaseManager.cs`: DatabaseManager
- 变更类型：删除
- 原因：功能合并到 MongoDb.cs

### `server/Game/Core/Database/MongoDbManager.cs`: MongoDbManager
- 变更类型：删除
- 原因：功能合并到 MongoDb.cs

### `server/Game/Core/Database/Repositories/MongoEntityRepository.cs`: MongoEntityRepository
- 变更类型：删除
- 原因：功能合并到 MongoDb.cs

### `server/Game/Core/Database/Repositories/MongoPlayerRepository.cs`: MongoPlayerRepository + PlayerAccount
- 变更类型：删除
- 原因：功能合并到 MongoDb.cs，PlayerAccount 类移到 MongoDb.cs 同文件或独立文件

### `server/Game/Core/Database/Repositories/MongoRoleRepository.cs`: MongoRoleRepository + UserRole
- 变更类型：删除
- 原因：功能合并到 MongoDb.cs，UserRole 类移到 MongoDb.cs 同文件或独立文件

### `server/Game/Context.cs`: Context
- 变更类型：修改
- 修改字段：`DatabaseManager.Instance` → `MongoDb.Instance`
- 修改属性：`public static MongoDb Db => MongoDb.Instance;`（新增便利属性）

### `server/Game/AppStart.cs`: AppStart
- 变更类型：修改
- 修改调用：`await DatabaseManager.Instance.Init()` → `await MongoDb.Instance.Init(Context.Config.MongoDb)`

### `server/Game/Components/User/UserHandler.cs`: UserHandler
- 变更类型：修改
- 所有 `DatabaseManager.Instance.PlayerRepo.xxx` → `MongoDb.Instance.xxx`
- 所有 `DatabaseManager.Instance.RoleRepo.xxx` → `MongoDb.Instance.xxx`

### 所有使用 EntityRepo 的 System/Handlers
- 变更类型：修改
- `DatabaseManager.Instance.EntityRepo.LoadAsync<T>(id)` → `MongoDb.Instance.LoadAsync<T>(id)`
- `DatabaseManager.Instance.EntityRepo.InsertAsync(entity)` → `MongoDb.Instance.InsertAsync(entity)`
- `DatabaseManager.Instance.EntityRepo.UpdateAsync(entity)` → `MongoDb.Instance.UpdateAsync(entity)`

## 执行顺序

1. 新建 `MongoDb.cs`，合并 DatabaseManager + MongoDbManager + 3 个 Repository 的全部功能
2. 将 PlayerAccount、UserRole 类移到独立文件（`server/Game/Core/Database/PlayerAccount.cs`、`server/Game/Core/Database/UserRole.cs`）
3. 修改 Context.cs — 更新数据库访问入口
4. 修改 AppStart.cs — 更新初始化调用
5. 批量替换所有 `DatabaseManager.Instance.EntityRepo.xxx` → `MongoDb.Instance.xxx`
6. 批量替换所有 `DatabaseManager.Instance.PlayerRepo.xxx` → `MongoDb.Instance.xxx`
7. 批量替换所有 `DatabaseManager.Instance.RoleRepo.xxx` → `MongoDb.Instance.xxx`
8. 删除旧文件：DatabaseManager.cs、MongoDbManager.cs、3 个 Repository 文件
9. 编译验证

## 验证

- [ ] `dotnet build server/Game` 编译通过
- [ ] 所有旧调用方式已替换
- [ ] PlayerAccount、UserRole 独立文件存在
