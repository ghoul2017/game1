# CLAUDE.md — 服务端

本文件为 Claude Code 在 `server/` 目录下工作时提供指导。项目级约定见根目录 `CLAUDE.md`。

## 目录结构

```
server/
├── common/
│   └── config/       # 配置表定义（命名空间 server.common.config，[ConfigTable] 自动扫描）
│           ├── ConfigTableAttribute.cs
│           ├── ItemConfig.cs
│           ├── SkillConfig.cs
│           └── MapConfig.cs
├── Network/          # 网络模块（NetworkModule 管理生命周期, TcpServer, MessageRouter, SessionManager）
├── Config/           # 读表模块（ConfigModule, ConfigManager 自动扫描, CsvLoader, 热更新）
├── Database/         # 数据库模块（DatabaseModule 统一管理 MySql/Redis 生命周期）
│   ├── MySql/        #   MySqlManager + PlayerRepository (Dapper)
│   ├── MongoDb/      #   MongoDbManager
│   └── Redis/        #   RedisManager (String/Hash/SortedSet)
├── Core/             # 公共（IModule, ModuleRegistry, AppConfig, Options）
│   ├── Entity/       #   实体模块（IEntity, IPersistFactory, EntityFactory, ProtobufPersistFactory, PersistTask）
│   ├── Event/        #   事件总线（IEvent, IEventListener, EventBus）
│   └── Log/          #   日志模块（ILog, LogManager, Console/File Appender）
├── game/             # 游戏系统模块（每个子系统一个目录）
│   ├── User/         #   登录/注册/重连/角色创建/选角 + UserHandlers
│   ├── Misc/         #   握手/心跳 + MiscHandlers
│   ├── Player/       #   PlayerManager 在线管理, PlayerModel 生命周期, Role 实体
│   ├── Bag/          #   BagManager + BagModel + RoleBag + BagHandlers
│   ├── Mail/         #   MailManager + MailModel + RoleMailList + MailHandlers
│   ├── Chat/         #   ChatManager + ChatModel + ChatHandlers
│   ├── Task/         #   TaskManager + TaskModel + TaskHandlers
│   ├── Aoi/          #   TowerAoi 灯塔视野 + 进出视野事件
│   ├── Map/          #   MapManager + GameMap + MapHandlers
│   └── Fight/        #   FightManager + FightCore + FightUnit + FightHandlers
├── Data/Tables/      # CSV 配置表数据文件
└── Program.cs        # 入口：模块化启动（日志→数据库→读表→网络）
tools/
├── Program.cs        # 入口：自动扫描 ITool 实现
├── Xlsx2CsvTool.cs   # xlsx 转 csv
├── Xlsx2CsTool.cs    # xlsx 转 C# 配置类
└── Proto2CsTool.cs   # proto 转 cs（待实现）
scripts/              # cmd 快捷脚本
```

## 关键约定

- **添加新消息**: 在 `shared/Protos/` 定义 Protobuf 类实现 `IMessage`（命名空间 `GameShared.Protos`）+ 在 `MsgId.cs` 分配 ID + 在对应系统目录创建继承 `MessageHandler<TReq>` 的 Handler（自动扫描注册）
- **添加新配置表**: 在 `common/config/` 定义类并标注 `[ConfigTable("Xxx.csv", KeyName = "主键属性")]` → 放 CSV 到 `Data/Tables/`。通过 `ConfigManager.Get<T>(id)` / `GetAll<T>()` / `GetDict<T>()` 访问
- **添加新工具**: 在 `tools/` 下创建实现 `ITool` 接口的类（自动注册），在 `scripts/` 加对应 cmd 脚本
- **添加新模块**: 创建实现 `IModule` 的类（自动扫描注册），设置 `Order` 控制初始化顺序
- **事件总线**: 定义事件类实现 `IEvent` → `EventBus.Instance.Subscribe<T>()` → `EventBus.Instance.PublishAsync()`
- **实体持久化**: 新实体实现 `IEntity`（`[ProtoContract]` + `[ProtoMember]`）→ `ProtobufPersistFactory<T>` → `EntityFactory.Instance.Register(factory)`
- **Write-Behind**: 默认 `immediately=false`，变更合并规则：Insert+Update=Insert, Insert+Delete=忽略, Update+Delete=Delete
- **会话管理**: SessionManager 单例管理三级映射（sessionId/userId/roleId → TcpSession）。BindUser/BindRole，顶号自动踢旧连接，Kick/Broadcast
- **Handler 规范**: 无状态，无参构造，通过 `XxxManager.Instance` 单例访问依赖，不缓存单例引用
- **Handler 文件**: 每个模块合并到 `XxxHandlers.cs`，不使用 Handlers/ 子目录
- **日志配置**: `appsettings.json` 的 `Log` 节（全局级别、按模块级别、Console/File 开关、格式 Pattern）
- **构建输出**: `Directory.Build.props` 统一管理，`bin/` 和 `obj/<项目名>/` 在解决方案根目录
