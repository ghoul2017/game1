# 架构迁移方案：IModule→Component + Handler统一 + Entity化 + MongoDB

## 背景

当前架构存在三套并行模式：
1. **IModule 模块系统 vs Component 系统** — IModule 管理生命周期，Component 管理组件挂载，两套体系并存
2. **继承式 Handler vs 扩展方法 Handler** — Bag/Mail/Chat/Map/Task/Fight 用继承式，User/Misc 用扩展方法
3. **MySQL+Redis+EntityFactory 缓存 vs MongoDB** — MySQL 存 LONGBLOB + Write-Behind 缓存，MongoDB 仅存日志

统一为：**Component 体系 + 扩展方法 Handler + MongoDB 原生 BSON 存储**。

## 决策

| 决策项 | 选择 | 理由 |
|--------|------|------|
| IModule 生命周期 | 去掉 Init/Start/Stop | 大部分是空壳，有实质逻辑的直接在 Component 或 Program 中处理 |
| EntityFactory/EntityCache/PersistTask | 全部删除 | MongoDB 原生读写替代 Write-Behind 缓存层 |
| MongoDB 存储格式 | BSON 文档 | 可直接查询，优于 Protobuf 二进制 |
| Manager 单例 | 保留，后续再迁 | 本次聚焦架构骨架，降低风险 |
| Player 拆分 | 拆为多 Component | 符合 ECS 理念 |

---

## Phase 1: IModule → Component + 去缓存层 + MongoDB

### Step 1.1: Entity 基类增强 ✅

`shared/Core/ECS/Entity.cs` 新增：
- `GetAllComponents()` — 返回所有组件
- `TryGetComponent<T>()` — 安全获取
- `HasComponent<T>()` — 检查存在

### Step 1.2: MongoDB 持久化基础设施

新增文件：
- `server/Game/Core/Database/Repositories/MongoEntityRepository.cs` — 通用实体仓库（BSON 文档存储）
  - `LoadAsync<T>(long id)` / `InsertAsync<T>` / `UpdateAsync<T>` / `DeleteAsync<T>`
  - 集合名 = 类名蛇形（如 `role_bag`, `role_mail_list`）
  - `_id` = entity.Id
- `server/Game/Core/Database/Repositories/MongoPlayerRepository.cs` — 替代 PlayerRepository(MySQL)
  - 存 `player` collection：`{ _id, username, password, nickname, level, createTime }`
- `server/Game/Core/Database/Repositories/MongoRoleRepository.cs` — 替代 RoleRepository(MySQL)
  - 存 `user_role` collection：`{ _id, userId, name, level, job }`
  - 建 `userId` 索引

### Step 1.3: 修改 DatabaseManager + AppConfig

- DatabaseManager：去掉 MySql/Redis，保留 MongoDb，新增 MongoEntityRepo
- AppConfig：去掉 MySql/Redis Options
- Options.cs：去掉 MySqlOptions, RedisOptions

### Step 1.4: 创建 8 个 Component 替代 IModule

| 旧 IModule | 新 Component | 实质逻辑 |
|---|---|---|
| PlayerModel | PlayerComponent | 保存角色（Stop） |
| BagModel | BagComponent | 无 |
| MailModel | MailComponent | 无 |
| FightModel | FightComponent | SetConfigManager（Init），RemoveAll（Stop） |
| ChatModel | ChatComponent | 无 |
| MapModel | MapComponent | InitDefaultMaps（Start），RemoveAll（Stop） |
| TaskModel | TaskComponent | 无 |
| MonsterModule | MonsterComponent | Start/Stop |

### Step 1.5: 修改 Manager 使用 MongoDB

所有 `EntityFactory.Instance.GetAsync/InsertAsync/UpdateAsync` → `DatabaseManager.Instance.EntityRepo.LoadAsync/InsertAsync/UpdateAsync`

涉及：PlayerManager, BagManager, MailManager, TaskManager, ItemEffectManager

### Step 1.6: 切换 Program.cs

- 去掉 ModuleRegistry，改为 AddComponent + 直接调用有实质逻辑的初始化
- 停服逻辑：保存角色 → 清理战斗/地图/怪物

### Step 1.7: 清理旧代码

删除：IModule.cs, ModuleRegistry.cs, IPersistFactory.cs, ProtobufPersistFactory.cs, EntityFactory.cs, EntityCache.cs, PersistTask.cs, MySqlManager.cs, RedisManager.cs, PlayerRepository.cs, RoleRepository.cs, IPlayerRepository.cs, 8个XxxModel/MonsterModule.cs

修改：AppConfig.cs, Options.cs, Game.csproj（移除 MySqlConnector/Dapper/StackExchange.Redis 包）

---

## Phase 2: Handler 统一为扩展方法风格

所有 `MessageHandler<TReq>` 继承式 Handler 改为 static 类 + `[MessageHandler(typeof(RpcMethodHandler))]`。

| 文件 | Handler 数 |
|---|---|
| game/Bag/BagHandlers.cs | 4 |
| game/Mail/MailHandlers.cs | 4 |
| game/Chat/ChatHandlers.cs | 1 |
| game/Map/MapHandlers.cs | 4 |
| game/GameTask/TaskHandlers.cs | 4 |
| game/Fight/FightHandlers.cs | 2 |

签名：`static async Task<IMessage> Xxx(TcpSession session, TReq req, TRes res)`

---

## Phase 3: 玩家等对象变 Entity

### PlayerEntity
- `PlayerEntity : Entity`，Id = RoleId
- `PlayerOnlineComponent`：UserId, LoginTime, LastActiveTime
- `RoleDataComponent`：Role 引用
- 删除 Player.cs

### MapEntity
- `MapEntity : Entity`
- `MapDataComponent`：地图配置数据
- 删除 GameMap.cs

### MonsterEntity
- `MonsterEntity : Entity`
- `MonsterDataComponent`：怪物数据
- 删除 MonsterUnit.cs

---

## 执行顺序

```
Phase 1 → Phase 2 → Phase 3
```

每步完成后 `dotnet build server/Game` 验证编译通过。
