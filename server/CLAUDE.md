# CLAUDE.md

本文件为 Claude Code (claude.ai/code) 在本仓库中处理代码时提供指导。

## 构建与运行

```bash
dotnet build                # 构建解决方案
dotnet run --project server # 运行项目（监听 0.0.0.0:9000）

# 工具（也可通过 scripts/*.cmd 调用）
dotnet run --project tools -- --list          # 列出所有工具
dotnet run --project tools -- xlsx2csv [args] # xlsx 转 csv
scripts\xlsx2csv.cmd [args]                   # 同上（cmd 脚本）
```

## 项目概览

- **解决方案**: `server.sln` — .NET 8 C# 游戏服务器
- **项目**: `server/server.csproj` — 游戏服务器，目标框架 `net8.0`
- **项目**: `tools/tools.csproj` — 辅助工具集（xlsx2csv、proto2cs 等）
- **入口点**: `server/Program.cs` — 模块化启动，ModuleRegistry 自动扫描并管理所有 IModule
- **客户端**: Unity (C#)
- **架构**: 单进程单服，TCP + Protobuf
- **构建输出**: `Directory.Build.props` 统一管理 bin/obj 到解决方案根目录（`bin/`、`obj/<项目名>/`）

## 技术栈

| 模块 | 技术 |
|---|---|
| 网络 | System.Net.Sockets + async/await |
| 协议 | TCP 二进制协议 `[MsgId:2B][Length:4B][Payload]`，小端序 |
| 序列化 | protobuf-net (`[ProtoContract]`/`[ProtoMember]`) |
| 读表 | CsvHelper + [ConfigTable] 自动扫描 + FileSystemWatcher 热更新 |
| MySQL | MySqlConnector + Dapper |
| MongoDB | MongoDB.Driver |
| Redis | StackExchange.Redis |
| 日志 | 自研 ILog + Appender 机制（参考 SLF4J + Logback） |
| 实体 | IEntity + EntityFactory + IPersistFactory（参考 Java DataCache/DataCenter） |
| 事件 | EventBus 发布订阅（类型安全，支持同步/异步/IEventListener） |

## 目录结构

```
server/
├── common/
│   └── config/       # 配置表定义（命名空间 server.common.config，[ConfigTable] 自动扫描）
│           ├── ConfigTableAttribute.cs  # 配置表标注 attribute
│           ├── ItemConfig.cs            # 物品配置
│           ├── SkillConfig.cs           # 技能配置
│           └── MapConfig.cs             # 地图配置
├── Network/          # 网络模块（NetworkModule 管理生命周期, TcpServer, MessageRouter, SessionManager；IMessage/PacketCodec 在 shared/）
├── Config/           # 读表模块（ConfigModule 管理生命周期, ConfigManager 自动扫描 [ConfigTable], CsvLoader, 热更新）
├── Database/         # 数据库模块（DatabaseModule 统一管理 MySql/Redis 生命周期）
│   ├── MySql/        #   MySqlManager + PlayerRepository (Dapper)
│   ├── MongoDb/      #   MongoDbManager
│   └── Redis/        #   RedisManager (String/Hash/SortedSet)
├── Core/             # 公共（IModule, ModuleRegistry, AppConfig, Options）
│   ├── Entity/       #   实体模块（IEntity, IPersistFactory, EntityFactory, EntityCache, ProtobufPersistFactory, PersistTask）
│   ├── Event/        #   事件总线（IEvent, IEventListener, EventBus 发布订阅）
│   └── Log/          #   日志模块（ILog, LogManager, Console/File Appender, Formatter）
├── game/             # 游戏系统模块（每个子系统一个目录）
│   ├── User/         #   用户系统（登录/注册/重连/角色创建/选角 + UserHandlers）
│   ├── Misc/         #   杂项（握手/心跳 + MiscHandlers）
│   ├── Player/       #   玩家系统（PlayerManager 在线管理, PlayerModel 生命周期, Role 实体）
│   ├── Bag/          #   背包系统（BagManager + BagModel + RoleBag + BagHandlers）
│   └── Mail/         #   邮件系统（MailManager + MailModel + RoleMailList + MailHandlers）
│   └── Chat/         #   聊天系统（ChatManager + ChatModel + ChatHandlers）
│   └── Task/         #   任务系统（TaskManager + TaskModel + TaskHandlers）
│   └── Aoi/          #   AOI 系统（TowerAoi 灯塔视野 + 进出视野事件）
│       ├── IAoiObject.cs     # AOI 对象接口
│       ├── AoiEventListener.cs # AOI 事件监听器
│       ├── Tower.cs          # 灯塔（对象/观察者管理）
│       └── TowerAoi.cs       # 灯塔 AOI 核心（对象/观察者 CRUD + 视野范围查询）
│   └── Map/          #   地图系统（MapManager + MapModel + MapHandlers）
│       ├── IMapObject.cs     # 地图对象接口 + MapObjectType 常量
│       ├── MapPlayer.cs      # 地图玩家对象
│       ├── MapNpc.cs         # 地图 NPC 对象
│       ├── GameMap.cs        # 地图实例（对象管理 + AOI 集成 + 移动）
│       ├── MapManager.cs     # 地图管理器单例（创建/销毁地图 + 进出/切图 + 移动）
│       ├── MapModel.cs       # 地图模块生命周期（InitDefaultMaps）
│   └── Fight/        #   战斗系统（FightManager + FightModel + FightHandlers）
│       ├── FightManager.cs   # 战斗管理器单例（战斗单元管理 + 技能使用 + 死亡处理 + AOI广播）
│       ├── FightCore.cs      # 战斗计算核心（伤害公式 + 命中/暴击/闪避判定）
│       ├── FightUnit.cs      # 战斗单元（属性 + 技能 + 仇恨 + CD + 生命值）
│       ├── FightAttribute.cs # 战斗属性类（攻防/暴击/闪避/伤害加成减免）
│       ├── FightErrorCode.cs # 错误码定义
│       ├── Skill.cs          # 技能实体（技能ID + 等级 + 冷却）
│       ├── SkillConfig.cs    # 技能配置（已移至 common/config/）
│       ├── FightModel.cs     # 战斗模块生命周期（注册Handler + ConfigManager 加载技能配置）
├── Data/Tables/      # CSV 配置表数据文件
└── Program.cs        # 入口 + StartupService（按序初始化：日志→数据库→读表→网络）
tools/
├── Program.cs        # 入口：自动扫描 ITool 实现，按命令名路由
├── ITool.cs          # 工具接口（Name/Desc/Run）
├── Xlsx2CsvTool.cs   # xlsx 转 csv 工具（ClosedXML）
├── Xlsx2CsTool.cs    # xlsx 转 C# 配置类（读取表头生成 [ConfigTable] 类）
└── Proto2CsTool.cs   # proto 转 cs 工具（待实现）
scripts/              # cmd 快捷脚本（xlsx2csv.cmd, xlsx2cs.cmd, proto2cs.cmd, tools-list.cmd）
```

## 关键约定

- **添加新消息**: 在 `shared/Protos/` 定义 Protobuf 类实现 `IMessage`（命名空间 `GameShared.Protos`）+ 在 `MsgId.cs` 分配 ID + 在对应系统目录创建继承 `MessageHandler<TReq>` 的 Handler（自动扫描注册）
- **目录和命名空间**: 目录名和命名空间一律小写，保持一致（如 `server.game.fight`）。类名仍用 PascalCase。共享代码使用 `GameShared.*` 命名空间。
- **添加新配置表**: 在 `common/config/` 定义类并标注 `[ConfigTable("Xxx.csv", KeyName = "主键属性")]`（命名空间 `server.common.config`）→ 放 CSV 到 `Data/Tables/`。ConfigManager 自动扫描 `[ConfigTable]` 并加载，无需手动注册。通过 `ConfigManager.Get<T>(id)` / `GetAll<T>()` / `GetDict<T>()` 访问数据
- **协议格式**: `[MsgId:UInt16LE][Length:Int32LE][ProtobufPayload]`
- **日志使用**: `private static readonly ILog Log = LogManager.GetLogger<T>();` → `Log.Info/Warn/Error(msg)`
- **日志配置**: `appsettings.json` 的 `Log` 节（全局级别、按模块级别、Console/File 开关、格式 Pattern）
- **添加新工具**: 在 `tools/` 下创建实现 `ITool` 接口的类（自动注册），在 `scripts/` 加对应 cmd 脚本
- **构建输出**: 由 `Directory.Build.props` 统一管理，`bin/` 和 `obj/<项目名>/` 均在解决方案根目录，csproj 中不单独配置路径
- **添加新模块**: 创建实现 `IModule` 的类（自动扫描注册），设置 `Order` 控制初始化顺序，通过 `ModuleRegistry.Instance` 获取依赖
- **模块 Order 约定**: Log=0, Database=10, Config=20, 业务系统=50~90, Network=100
- **事件总线使用**: 定义事件类实现 `IEvent` → 用 `EventBus.Instance.Subscribe<T>(handler)` 订阅 → 用 `EventBus.Instance.PublishAsync(event)` 发布
- **实体持久化**: 新实体实现 `IEntity`（需有 `[ProtoContract]` + `[ProtoMember]`）→ 创建 `ProtobufPersistFactory<T>` → 调用 `EntityFactory.Instance.Register(factory)` 注册 → 通过 `EntityFactory.Instance.GetAsync/InsertAsync/UpdateAsync/DeleteAsync` 操作
- **Write-Behind**: Insert/Update/Delete 默认 `immediately=false`，变更进入 PersistTask 脏队列，定时批量写入；设 `immediately=true` 可立即写入。变更合并规则：Insert+Update=Insert, Insert+Delete=忽略, Update+Delete=Delete
- **会话管理**: SessionManager 单例管理三级映射（sessionId/userId/roleId → TcpSession），登录后调用 `BindUser`，选角后调用 `BindRole`，断线自动清理。顶号自动踢掉旧连接。踢人用 `Kick/KickByRoleId`，广播用 `Broadcast`
- **数据库表名**: ProtobufPersistFactory 自动生成 `p_<类名蛇形>` 表名（如 RoleBag → p_role_bag），表结构为 `(id BIGINT PK, data LONGBLOB)`
- **命名空间**: 目录名和命名空间一律小写（如 `server.game.player`、`server.game.task`），类名仍 PascalCase。共享代码使用 `GameShared.Protos` / `GameShared.Network`
- **Handler 规范**: Handler 不使用构造函数注入，直接通过单例访问依赖（`DatabaseManager.Instance.Redis`、`SessionManager.Instance` 等）。Handler 无状态，AutoRegister 要求无参构造函数
- **单例访问**: 不要缓存单例引用到字段或局部变量（如 `var db = DatabaseManager.Instance;`），直接用 `DatabaseManager.Instance.Redis.xxx` 访问
- **Handler 文件**: 每个模块的 Handler 合并到模块根目录的 `XxxHandlers.cs`（如 `BagHandlers.cs`），不使用 Handlers/ 子目录
- **Proto 文件**: 每个模块合并为一个 Proto 文件（如 `UserProtos.cs`、`MiscProtos.cs`），放在 `shared/Protos/`（双端共享），不按消息拆分

## 参考项目代码风格

参考 Java 游戏服务器的架构风格，写 C# 代码时需遵循以下设计模式：

### 核心架构模式

| 模式 | 说明 | C# 适配 |
|---|---|---|
| Manager 单例 | 每个子系统一个 `XxxManager` 单例，持有状态+业务逻辑 | 静态 Instance + 私有构造 |
| Message-Handler | Protobuf 消息 + Handler 类，命令模式分发 | protobuf-net + `IMessage` + `MessageHandler<TReq>` |
| Registry 注册 | 消息/Handler/Module 通过注册表管理（反射扫描或手动） | 反射自动扫描 + Dictionary 映射 |
| Write-Behind Cache | 内存缓存 + 定时批量持久化（60s） | Timer + dirty 标记 |
| Per-User 线程队列 | 每个玩家串行队列，跨玩家并行 | Channel\<T\> |
| Config 表系统 | CSV 配置 + 注解映射 + 缓存池 + 热更新 | CsvHelper + FileSystemWatcher |
| 热更脚本 | 接口与实现分离，运行时可替换 | 待定 |

### 目录结构模式（每个子系统）

```
game/Bag/
├── BagManager.cs       # 业务逻辑单例
├── BagModel.cs         # 注册 + 生命周期钩子
├── Bag.cs              # 数据模型
└── BagHandlers.cs      # 消息处理器（一个文件包含该模块所有 Handler）
```

消息定义统一在 `shared/Protos/BagProtos.cs`（客户端和服务端共享），不再在各系统目录下放 Messages/。

### 命名约定

- 消息后缀：Req（请求）、Ack（响应）、Ntf（通知）
- 消息 ID 按功能分段：10101=user, 10201=misc, 10301=bag, 10401=mail, 10501=chat, 10601=task, 10701=map, 10801=fight（每段 100）
- 数据库表：`p_` 前缀（p_user, p_role）
- Manager 类持有业务逻辑，Model 类负责注册和生命周期

## 参考项目路径

- **E:\work\sgserver** — 成熟三国游戏服务器（Java 8, Maven 多模块: game-server, game-logic）
- **E:\work\server** — 游戏服务器雏形（Java 21, Maven 多模块: lib-core, lib-common, server-game, link-game, script-game）

## 自我进化约定

项目工具链（agents、commands、scripts）在开发过程中持续自我优化。

### 触发规则

| 触发条件 | 动作 | 举例 |
|---|---|---|
| 相同操作手动执行 3 次以上 | 创建 command 自动化 | 多次手动创建消息+Handler → `/newmsg` |
| 发现新的命名/目录约定 | 更新 CLAUDE.md 约定章节 | 发现表名需要统一前缀 → 补充到命名约定 |
| 复杂流程有固定模式 | 创建 agent 封装 | review+提交模式 → `review-commit` agent |
| 现有 agent/command 不够好 | 直接优化，不改名 | devloop 发现遗漏步骤 → 补充到 devloop.md |
| 从 Java 项目学到新模式 | 添加到「核心架构模式」表 | 发现新的设计模式 → 补充到架构模式表 |
| 生成器命令可用后 | 立即在 devloop 中使用 | 创建了 `/newsystem` → devloop 用它生成目录结构 |

### 进化日志

每次创建或优化工具链时，在下方追加记录（不删除旧记录）。

<!-- EVOLVE_LOG_START -->
- `2026-04-10` 初始化：review-commit agent, /rc 命令, /dev 命令, /devloop 命令, devloop.cmd 脚本
- `2026-04-10` M14 背包系统：发现子命名空间中 MsgId 引用冲突问题，更新命名约定（使用 global:: 前缀）
- `2026-04-11` M17 任务系统：发现 `Server.Game.Task` 命名空间与 `System.Threading.Tasks.Task` 类型冲突，改用 `Server.Game.TaskSystem`，补充到命名空间避让约定
- `2026-04-11` 消息系统重构：所有 Req/Ack/Ntf 实现 IMessage 接口 + MessageHandler<TReq> 泛型基类 + Router 自动扫描注册 + PlayerManager.SendToPlayer 统一发送。proto 文件收拢到 `common/protos/`，命名空间 `server.common.protos`。目录名和命名空间统一小写。
- `2026-04-12` 共享代码迁移：消息定义、MsgId、IMessage、PacketCodec 迁移到 `shared/`（命名空间 `GameShared.Protos`/`GameShared.Network`）。服务端通过 csproj Compile Include 引用，客户端通过 directory junction 引用。消除双端重复代码。
- `2026-04-11` Config 系统重构：`[ConfigTable]` attribute 自动扫描 + 配置类收拢到 `common/config/`（ItemConfig/SkillConfig/MapConfig）+ ConfigManager.Get/GetAll/GetDict API + CsvLoader 支持 int[] 分号分隔 + xlsx2cs 工具从 Excel 生成 C# 配置类。删除旧 Config/Tables/ 和 ITable 接口。
- `2026-04-11` 目录结构重构：Network 只保留核心基础设施，登录/角色 Handler 移至 `game/user/`，握手/心跳移至 `game/misc/`。Protos 按模块合并（LoginProtos+RoleProtos→UserProtos, HandshakeProtos+HeartbeatProtos→MiscProtos）。Handler 文件从 Handlers/ 子目录移到模块根目录。MsgId 重编为 10101 起步每段 100。Handler 去掉构造函数注入改为单例直接访问。DatabaseManager 新增 PlayerRepo/RoleRepo 单例属性。EntityFactory/PersistTask 去掉 _db 字段缓存。
<!-- EVOLVE_LOG_END -->

### 现有工具链清单

#### Agents（`.claude/agents/`）

| Agent | 用途 | 触发方式 |
|---|---|---|
| review-commit | 全面 review 代码变更并提交 git | `/rc` 命令调用 |

#### Commands（`.claude/commands/`）

| 命令 | 用途 |
|---|---|
| `/rc` | review + commit 当前变更 |
| `/dev` | 执行下一个里程碑（单次，手动触发） |
| `/devloop` | 同 /dev，带配额守卫和结尾提示 |
| `/newsystem <name>` | 生成新游戏系统目录骨架（Manager + Model + Handlers + Messages） |
| `/newmsg <name>` | 生成消息类 + Handler + 注册 MsgId |
| `/evolve` | 主动触发工具链自我进化 |

#### Scripts（`scripts/`）

| 脚本 | 用途 |
|---|---|
| `devloop.cmd` | 无人值守自动开发循环（非交互模式） |
| `xlsx2csv.cmd` | xlsx 转 csv |
| `xlsx2cs.cmd` | xlsx 转 C# 配置类 |
| `proto2cs.cmd` | proto 转 cs |
