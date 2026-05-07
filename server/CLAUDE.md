# CLAUDE.md — 服务端

本文件为 Claude Code 在 `server/` 目录下工作时提供指导。项目级约定见根目录 `CLAUDE.md`。

## 目录结构

```
server/Game/
├── Program.cs            # 入口：单线程调度 → Component 挂载 + 子系统初始化
├── Context.cs            # 静态上下文：Context.Server(Module 实体), Context.Config
├── Entities/Module.cs    # 根实体（所有 Component 挂载于此）
├── Components/
│   ├── User/             # UserComponent + UserComponentSystem + UserHandler
│   ├── Player/           # PlayerComponent
│   ├── Bag/              # BagComponent
│   ├── Mail/             # MailComponent
│   ├── Fight/            # FightComponent
│   ├── Chat/             # ChatComponent
│   ├── Map/              # MapComponent
│   ├── Task/             # TaskComponent
│   └── Monster/          # MonsterComponent
├── Core/
│   ├── AppConfig.cs      # 配置根（Server, MongoDb, Tables, Log）
│   ├── Options.cs        # 各子配置 Options 类
│   ├── GameSynchronizationContext.cs  # 单线程调度器
│   ├── GameTimer.cs      # 定时器（回调自动回主线程）
│   ├── ShutdownToken.cs  # 统一关闭信号
│   ├── TokenStore.cs     # 内存 Token 存储
│   ├── Database/
│   │   ├── DatabaseManager.cs      # 统一管理 MongoDB 生命周期
│   │   ├── MongoDbManager.cs       # MongoDB 连接
│   │   └── Repositories/           # MongoEntityRepository, MongoPlayerRepository, MongoRoleRepository
│   ├── Entity/
│   │   └── IEntity.cs              # 实体接口
│   ├── Event/
│   │   ├── EventBus.cs             # 事件总线
│   │   ├── IEvent.cs              # 事件接口
│   │   └── IEventListener.cs      # 事件监听接口
│   └── Network/
│       ├── NetworkComponent.cs     # 网络组件（监听/接受/会话/心跳）
│       ├── SessionManager.cs       # 会话管理（userId/roleId → TcpSession）
│       ├── RpcMethodHandler.cs     # RPC 适配器
│       └── NtfMethodHandler.cs     # 通知适配器
├── game/
│   ├── Player/           # PlayerManager, PlayerEntity, PlayerOnlineComponent, RoleDataComponent, Role, RoleAttribute
│   ├── Bag/              # BagManager, BagHandlers, RoleBag, BagItem, ItemEffectManager
│   ├── Mail/             # MailManager, MailHandlers, RoleMail
│   ├── Chat/             # ChatManager, ChatHandlers
│   ├── GameTask/         # TaskManager, TaskHandlers, RoleTaskList
│   ├── Map/              # MapManager, MapHandlers, GameMap, MapPlayer, MapNpc, MapAoiNotifier
│   ├── Fight/            # FightManager, FightHandlers, FightCore, FightUnit, Skill
│   ├── Monster/          # MonsterManager, MonsterUnit
│   ├── Aoi/              # TowerAoi, Tower, IAoiObject, AoiEventListener
│   └── misc/             # HandshakeHandler, HeartbeatHandler
├── Data/Tables/          # CSV 配置表数据文件
tools/
├── Program.cs            # 入口：自动扫描 ITool 实现
├── Xlsx2CsvTool.cs       # xlsx 转 csv
├── Xlsx2CsTool.cs        # xlsx 转 C# 配置类
└── Proto2CsTool.cs       # proto 转 cs（待实现）
scripts/                  # cmd 快捷脚本
```

## 关键约定

- **添加新消息**: 在 `shared/Common/Protos/` 定义 Protobuf 类实现 `IMessage` + 在 `MsgIdConst.cs` 分配 ID + 创建带 `[MessageHandler]` 标记的静态方法
- **添加新配置表**: 在 `shared/Common/Config/` 定义类并标注 `[ConfigTable("Xxx.csv")]` → 放 CSV 到 `Data/Tables/`
- **添加新工具**: 在 `tools/` 下创建实现 `ITool` 接口的类（自动注册），在 `scripts/` 加对应 cmd 脚本
- **添加新组件**: 创建继承 `Component` 的类 + `[ComponentOf(typeof(Module))]` → 在 Program.cs 中 `Context.Server.AddComponent<T>()`
- **添加新实体**: 实现 `IEntity` + `[ProtoContract]` + `[BsonId]` → 通过 `DatabaseManager.Instance.EntityRepo` 进行 CRUD
- **事件总线**: 定义事件类实现 `IEvent` → `EventBus.Instance.Subscribe<T>()` → `EventBus.Instance.PublishAsync()`
- **Handler 规范**: static 类 + `[MessageHandler(typeof(RpcMethodHandler/NtfMethodHandler))]`，无状态
- **会话管理**: SessionManager 单例管理 userId/roleId → TcpSession 映射，BindRole，顶号踢旧连接，Kick/Broadcast
- **线程模型**: GameSynchronizationContext 保证所有逻辑在主线程执行，Timer 回调通过 Post 回主线程
- **日志配置**: `appsettings.json` 的 `Log` 节
- **构建输出**: `Directory.Build.props` 统一管理

## TODO：架构迁移

详细方案见 `.claude/plans/architecture-migration.md`。

- [x] Phase 1: IModule→Component + 去缓存层 + MongoDB
- [x] Phase 2: Handler 统一为扩展方法风格
- [x] Phase 3: Player 变 Entity（PlayerEntity + PlayerOnlineComponent + RoleDataComponent）
- [ ] Phase 3 后续: MapEntity + MonsterEntity（改动面大，需逐步推进）
