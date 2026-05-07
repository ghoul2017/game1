# CLAUDE.md — 服务端

本文件为 Claude Code 在 `server/` 目录下工作时提供指导。项目级约定见根目录 `CLAUDE.md`。

## 目录结构

```
server/Game/
├── Program.cs            # 入口：单线程调度 → Component 挂载 + 子系统初始化
├── Context.cs            # 静态上下文：Context.Server(Module), Context.Players, Context.Fight 等
├── Entities/Module.cs    # 根实体（所有 Module 级 Component 挂载于此）
├── Components/
│   ├── User/             # UserComponent + UserComponentSystem + UserHandler
│   ├── Player/           # PlayerComponent + PlayerSystem + PlayerEntity + PlayerOnlineComponent + RoleDataComponent + RoleAttributeComponent + Role + RoleAttribute
│   ├── Bag/              # BagComponent + BagSystem + BagHandlers + RoleBag + BagItem + ItemEffectSystem
│   ├── Mail/             # MailComponent + MailSystem + MailHandlers + RoleMailList + RoleMail
│   ├── Chat/             # ChatComponent + ChatSystem + ChatHandlers
│   ├── GameTask/         # TaskComponent + TaskSystem + TaskHandlers + RoleTaskList + RoleTask
│   ├── Fight/            # FightComponent + FightSystem + FightHandlers + FightCore + FightUnit + FightAttribute + Skill
│   ├── Map/              # MapComponent + MapSystem + MapHandlers + GameMap + MapPlayer + MapNpc + MapAoiNotifier + IMapObject
│   ├── Monster/          # MonsterComponent + MonsterSystem + MonsterUnit + AiState
│   └── Misc/             # MiscHandlers（握手+心跳）
├── Core/
│   ├── AppConfig.cs      # 配置根（Server, MongoDb, Tables, Log）
│   ├── Options.cs        # 各子配置 Options 类
│   ├── GameSynchronizationContext.cs  # 单线程调度器
│   ├── GameTimer.cs      # 定时器（回调自动回主线程）
│   ├── ShutdownToken.cs  # 统一关闭信号
│   ├── TokenStore.cs     # 内存 Token 存储
│   ├── Aoi/              # TowerAoi, Tower, IAoiObject, IAoiEventListener
│   ├── Database/         # DatabaseManager, MongoDbManager, MongoEntityRepository, MongoPlayerRepository, MongoRoleRepository
│   ├── Entity/           # IEntity
│   ├── Event/            # EventBus, IEvent, IEventListener
│   └── Network/          # NetworkComponent, SessionManager, RpcMethodHandler, NtfMethodHandler
├── Data/Tables/          # CSV 配置表数据文件
tools/
├── Program.cs            # 入口：自动扫描 ITool 实现
├── Xlsx2CsvTool.cs       # xlsx 转 csv
├── Xlsx2CsTool.cs        # xlsx 转 C# 配置类
└── Proto2CsTool.cs       # proto 转 cs（待实现）
scripts/                  # cmd 快捷脚本
```

### 子系统模板

```
Components/Xxx/
  XxxComponent.cs     # Component（纯数据，[ComponentOf(typeof(Module))] 或 [ComponentOf(typeof(PlayerEntity))]）
  XxxSystem.cs        # System（静态扩展方法，this XxxComponent self）
  XxxHandlers.cs      # Handler（static + [MessageHandler(typeof(RpcMethodHandler))]）
  RoleXxx.cs           # IEntity 数据模型（[ProtoContract] + [BsonId] + [BsonIgnore]）
消息定义 → shared/Common/Protos/XxxProtos.cs（双端共享）
MsgId → shared/Common/MsgIdConst.cs（双端共享）
```

### Component 分级

| 级别 | 挂载目标 | 用途 | 示例 |
|------|---------|------|------|
| Module 级 | Context.Server | 全局数据集合 | PlayerComponent, FightComponent, MapComponent, MonsterComponent, ChatComponent |
| PlayerEntity 级 | PlayerEntity | 玩家数据 | BagComponent, MailComponent, TaskComponent, RoleAttributeComponent, PlayerOnlineComponent, RoleDataComponent |

### System 规则

- System 是纯静态扩展方法类（`public static class XxxSystem`），只有方法和常量，不持有字段
- 通过 `this XxxComponent self` 访问 Component 数据
- Module 级：`Context.Fight.RegisterUnit(...)` — Context 提供便利属性
- PlayerEntity 级：`player.Bag.AddItem(...)` — PlayerEntity 提供便利属性
- 日志用 `LogManager.GetLogger(nameof(XxxSystem))`（静态类不能做泛型参数）

### 登录加载策略

选角入场时一次性挂载所有 PlayerEntity 级 Component 并从 MongoDB 加载全部数据：
```csharp
var player = PlayerEntity.Create(roleId, userId, role);
var bagComp = player.AddComponent<BagComponent>();
bagComp.RoleBag = await bagComp.LoadBagAsync(role.Id);
// ... Mail, Task, RoleAttribute 同理
Context.Players.AddPlayer(player);
```

## 关键约定

- **添加新消息**: 在 `shared/Common/Protos/` 定义 Protobuf 类实现 `IMessage` + 在 `MsgIdConst.cs` 分配 ID + 创建带 `[MessageHandler]` 标记的静态方法
- **添加新配置表**: 在 `shared/Common/Config/` 定义类并标注 `[ConfigTable("Xxx.csv")]` → 放 CSV 到 `Data/Tables/`
- **添加新工具**: 在 `tools/` 下创建实现 `ITool` 接口的类（自动注册），在 `scripts/` 加对应 cmd 脚本
- **添加新 Module 级组件**: 创建继承 `Component` 的类 + `[ComponentOf(typeof(Module))]` → 在 Program.cs 中 `Context.Server.AddComponent<T>()` → 在 Context.cs 加便利属性
- **添加新 PlayerEntity 级组件**: 创建继承 `Component` 的类 + `[ComponentOf(typeof(PlayerEntity))]` → 在 UserHandler.SelectRoleHandler 中 AddComponent + 加载数据 → 在 PlayerEntity 加便利属性
- **添加新实体**: 实现 `IEntity` + `[ProtoContract]` + `[BsonId]` → 通过 `DatabaseManager.Instance.EntityRepo` 进行 CRUD
- **事件总线**: 定义事件类实现 `IEvent` → `EventBus.Instance.Subscribe<T>()` → `EventBus.Instance.PublishAsync()`
- **Handler 规范**: static 类 + `[MessageHandler(typeof(RpcMethodHandler/NtfMethodHandler))]`，无状态
- **会话管理**: SessionManager 单例管理 userId/roleId → TcpSession 映射，BindRole，顶号踢旧连接，Kick/Broadcast
- **线程模型**: GameSynchronizationContext 保证所有逻辑在主线程执行，Timer 回调通过 Post 回主线程
- **日志配置**: `appsettings.json` 的 `Log` 节
- **构建输出**: `Directory.Build.props` 统一管理
