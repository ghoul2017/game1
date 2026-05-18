---
always_apply: true
---

# 踩坑记录

## Component + IEntity 持久化

Component 合并 IEntity 数据类时，Component 实现 IEntity 保持 MongoEntityRepository 兼容：

- **`[BsonId]`** 必须标记在 `Id` 属性上
- **集合数据直接用 `Dictionary<K,V>`**（MongoDB 原生支持，key 存储为 BSON document 的 string key），不需要 `List<T>` + `Dictionary<K,V>` 双结构
- **Proto 属性**：仅网络 Protobuf 序列化的类才需要 `[ProtoContract]`/`[ProtoMember]`，纯 MongoDB 持久化不需要

### 推荐加载模式：AddComponent(instance)

```csharp
var bag = await MongoDb.Instance.LoadAsync<RoleBagComponent>(player.Id);
if (bag == null)
{
    bag = new RoleBagComponent { Id = player.Id, Capacity = 100 };
    await MongoDb.Instance.InsertAsync(bag);
}
player.AddComponent(bag);
```

**Why:** `AddComponent(instance)` 内部调用 `component.SetParent(this)`，DB 加载实例的 `_parent` 引用正确。Dictionary 由 MongoDB 直接反序列化，无需 `RebuildIndex()`。

### 已淘汰模式

- **List + Dictionary 双结构** — 曾用于 Bag/Mail/Task，Dictionary 标记 `[BsonIgnore]` 通过 `RebuildIndex()` 重建。已统一为 Dictionary 主结构，MongoDB 直接序列化。
- **LoadAsync 拷贝** — 先 `AddComponent()` 再逐字段拷贝，已替换为 AddComponent(instance)。

## ConfigManager 双端共享

- namespace 从 `Game.Config` 迁移到 `Shared.Core.Config`，服务端需同步改 using
- CsvLoader 依赖 CsvHelper NuGet 包（Unity 不可用），统一用 SimpleCsvReader
- SimpleCsvReader 需提供非泛型 `Load(Type, string)` 重载供反射调用
- 客户端开发环境 CSV 路径：`Application.dataPath + "/../../server/Game/Data/Tables"`
- 删除 .cs 文件后 Unity csproj 仍引用，需 Unity Editor 刷新重新生成

## 反常规/反直觉的代码模式

- **ItemEffectSystem 反向依赖 Fight** — Bag → Fight 的反向依赖（物品效果修改战斗状态）
- **FightComponent 已删除** — 不再有 `Context.Fight`，查找走 `FightSystem.GetFightUnit(objectId)`
- **Handler 上线加载直接拷贝字段** — `LoadAsync<T>` 返回新实例 `_parent` 为 null，必须拷贝+RebuildIndex（已淘汰，用 AddComponent(instance) 替代）
- **MonoBehaviour 方法改 static 时 Destroy 仍可用** — `UnityEngine.Object.Destroy(Object)` 是静态方法

## ECS 重构：Map/Monster/Fight

- **FightSystem.OnUnitKilled 直接调用 map.OnMonsterKilled()**，事件只给其他模块通知，不要用事件中转
- **MapAoiNotifier 一站式分发** — 按 ObjectType 自动分发通知，不要手动广播（导致双通知）
- **MonsterEntity 代替了 MapNpc + MonsterUnit** — 怪物类型 Monster(3)，不再有 Npc(2)
- **FightUnitComponent 常驻** — 在 Entity.Create 中直接创建，不再需要 RegisterUnit/UnregisterUnit
- **CfgId 上提到 IMapObject** — MapAoiNotifier 通过 `mapObj.CfgId` 统一访问，不需要 `is MonsterEntity` 判断
- **掉落/经验/金币归 PlayerSystem** — MonsterSystem 不反向依赖 Player，通过 `Context.Players.GrantXxxAsync()` 调用
- **GameMap.SharedRandom** — 用 `map.Random.Next()`，不要 `new Random()`
- **MapPlayerEntity 直接存 PlayerEntity** — 不放持久化数据，Id=RoleId，直接字段引用，无 Component 中转
- **Name 在 MapObjectEntity** — `SetName()` 一次赋值，不再需要 MapPlayerInfoComponent/MonsterInfoComponent.Name
- **MapId/Line/X/Y 在 MapObjectEntity** — 不再需要 MapPositionComponent，直接字段存取
- **MonsterSystem 是 GameMap 扩展方法** — `this GameMap map`，无状态设计，逻辑与数据分离
- **RoleAttributeComponent 属性名**：PhyAttMin/PhyAttMax/PhyDef，不是 Attack/Defense
- **AOI 一站式 API**：`Aoi.AddAoiEntity(obj, viewRange)`，业务代码不要分别调 AddObject+AddWatcher

## Entity 迁移模式：普通类 → Entity + Component

1. 创建 Entity 子类，Id 设为业务主键，提供静态工厂 `Create()`
2. 拆分字段为 `[ComponentOf]` Component
3. Entity 提供便利属性（`GetComponent<T>()` 快捷访问）
4. 更新 Manager 和所有调用方

## 客户端 Logic/View 分离

- **分离原则**：Logic 层零 UnityEngine 依赖，View 层持有所有 Unity 引用
- **命名空间**：Logic/ → `Client.Logic`，View/ → `Client.View`，Editor/ → `Client.View.Editor`
- **面板解耦**：Handler 通过 `ClientContext.UI?.GetPanel<IPanel>()` 获取面板，不直接引用 Panel 类
- **Panel 接口**：每个被 Handler 调用的 Panel 需在 Logic/IPanels.cs 定义接口，Panel 在 View 端实现
- **删除 .asmdef 后无需程序集隔离**：Unity 单 Assembly-CSharp 编译，目录分隔纯为代码组织
- **未删除 .meta 导致残留**：删除 .asmdef 或 .cs 文件后务必检查并删除对应 .meta，否则 Unity 仍尝试加载
- **bin/obj 目录**：.csproj 的构建产物若放在 Unity Assets/ 下会被 Unity 当成预编译 DLL 导致 "Multiple precompiled assemblies" 错误。Robot.csproj 放在 client/ 外部（robot/ 目录），避免此问题
- **Robot.csproj 用 net8.0**：netstandard2.0 不支持 shared 代码中的 C# 9 特性（target-typed new、GetValueOrDefault、Deconstruct），Robot 通过 Compile Include 直接引用 shared/ 和 Logic/ 源文件

## 调试方法论

### 服务端

- 日志：`ILog` + `LogManager`，ConsoleAppender + FileAppender（`log/game-YYYYMMDD.log`，错误单独 `error-`，7天滚动）
- 用法：`private static readonly ILog Log = LogManager.GetLogger<T>();`（静态类用 `nameof(XxxSystem)`）
- 单线程模型：所有逻辑在主线程，await 自动回主线程
- Handler 入口追踪请求，`session.UserId` / `session.RoleId` 判断会话状态

### 客户端

- 初始化：`GameEntry.Start` → `LogClientInitializer.Init()`
- 输出：UnityAppender（UnityEngine.Debug）+ FileAppender（`Application.persistentDataPath/logs/`）

### 数据库

- MongoDB，统一入口 `MongoDb.Instance`
- IEntity：`LoadAsync<T>(id)` / `InsertAsync` / `UpdateAsync` / `DeleteAsync<T>(id)`
- 非 IEntity：`QueryAsync<T>(filter)` / `SaveAsync` / `ReplaceAsync` / `RemoveAsync<T>(filter)`
- 集合名自动蛇形（`RoleBagComponent` → `role_bag_component`）
- `AfterDeserialize()` / `RebuildIndex()` 通过反射自动调用

### 常见调试场景

| 场景 | 方法 |
|------|------|
| 请求未到达 Handler | 检查 MsgIdConst ID 匹配、`[MessageHandler]` 标记 |
| 数据未持久化 | 检查 UpdateAsync/SaveAsync 调用、`[BsonId]` 标记 |
| Component 字段为 null | 检查分级、AddComponent 时机、AfterDeserialize |
| 事件未触发 | 检查 AutoRegister 执行、`[EventSubscriber]` 标记、同步/异步 |
