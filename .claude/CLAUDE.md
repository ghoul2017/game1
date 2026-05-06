# CLAUDE.md

2.5D RPG 游戏项目。Unity 客户端（`client/`）+ .NET 8 服务端（`server/`）。TCP 二进制 Protobuf 通信，服务端权威。

## 构建与运行

```bash
# 服务端
cd server && dotnet build
dotnet run --project server/Game           # 监听 0.0.0.0:9000
dotnet run --project server/tools -- --list  # 辅助工具
scripts\xlsx2csv.cmd [args]               # xlsx 转 csv
scripts\xlsx2cs.cmd [args]                # xlsx 转 C# 配置类

# 客户端：Unity 2021.3.26f1c1 + URP 12.1.11
# Unity Editor 打开 client/ 目录构建
# 编译检查：在项目根目录 dotnet build game1.sln
```

## 技术栈

| 模块 | 技术 |
|---|---|
| 协议 | TCP `[MsgId:2B][Length:4B][Payload]`，小端序 |
| 序列化 | protobuf-net (`[ProtoContract]`/`[ProtoMember]`) |
| 读表 | CsvHelper + `[ConfigTable]` 自动扫描 |
| 数据库 | MySQL(Dapper) + MongoDB |
| 缓存 | Write-Behind（内存 + 60s 批量持久化） |
| 实体 | Entity + Component + ComponentOf（ECS 风格） |
| 事件 | EventBus 发布订阅（类型安全） |
| 日志 | 自研 ILog + Appender（参考 SLF4J） |
| 线程模型 | GameSynchronizationContext 单线程调度，await 自动回主线程 |
| 定时器 | GameTimer，回调自动 Post 到主线程 |
| Token | TokenStore（内存实现，替代 Redis 做重连 Token） |

## 目录结构

```
client/Assets/Scripts/
  shared/               # 双端共享代码（服务端通过 csproj Compile Include 引用）
    Core/
      ECS/              # Entity, Component, ComponentOfAttribute
      Network/          # IMessage, PacketCodec, MessageRouter, TcpSession, IMessageHandler
      Config/           # ConfigManager, ConfigTableAttribute, CsvLoader
      Log/              # ILog, LogManager, Console/File Appender
      IdGenerator.cs    # 雪花算法 ID 生成
    Common/
      Protos/           # MsgIdConst + 全部消息定义（双端共享）
      Config/           # 配置表定义（ItemConfig, SkillConfig, MapConfig 等）
  Core/                 # UIManager, UIPanel, UIBuilder, GameEntry, UIToast, UIMessageBox
  Loading/              # 加载界面（LoadingPanel）
  Login/                # 登录 & 角色选择（LoginPanel, RoleSelectPanel）
  Game/                 # MainPanel, PlayerController, MapGround, BagPanel, MailPanel 等
  Network/              # NetClient, MessageDispatcher, NetworkStatusPanel, NtfMethodHandler
  Config/               # ClientConfigManager
  Editor/               # SceneCreator

server/Game/
  Program.cs            # 入口：单线程调度 → 模块化启动
  Context.cs            # 静态上下文：Context.Server(Module 实体), Context.Config
  Entities/Module.cs    # 根实体（所有 Component 挂载于此）
  Components/User/      # UserComponent + UserComponentSystem + UserHandler
  Core/
    IModule.cs          # 模块接口（Order, InitAsync, StartAsync, StopAsync）
    ModuleRegistry.cs   # 自动扫描 + 按 Order 管理生命周期
    AppConfig.cs        # 配置根（Server, MySql, MongoDb, Redis, Tables, Log）
    Options.cs          # 各子配置 Options 类
    GameSynchronizationContext.cs  # 单线程调度器
    GameTimer.cs        # 定时器（回调自动回主线程）
    ShutdownToken.cs    # 统一关闭信号
    TokenStore.cs       # 内存 Token 存储（替代 Redis）
    Database/           # DatabaseManager, MySqlManager, MongoDbManager, RedisManager, Repositories
    Entity/             # IEntity, EntityFactory, ProtobufPersistFactory, EntityCache, PersistTask
    Event/              # EventBus, IEvent, IEventListener
    Network/            # NetworkComponent, SessionManager, RpcMethodHandler, NtfMethodHandler
  game/                 # 子系统
    Player/             # PlayerManager, PlayerModel(IModule), Player, Role, RoleAttribute
    Bag/                # BagManager, BagModel(IModule), BagHandlers, RoleBag, BagItem, ItemEffectManager
    Mail/               # MailManager, MailModel(IModule), MailHandlers, RoleMail
    Chat/               # ChatManager, ChatModel(IModule), ChatHandlers
    GameTask/           # TaskManager, TaskModel(IModule), TaskHandlers, RoleTaskList
    Map/                # MapManager, MapModel(IModule), MapHandlers, GameMap, MapPlayer, MapNpc, MapAoiNotifier
    Fight/              # FightManager, FightModel(IModule), FightHandlers, FightCore, FightUnit, Skill
    Monster/            # MonsterManager, MonsterModule(IModule)
    Aoi/                # TowerAoi, Tower, IAoiObject, AoiEventListener
    misc/               # HandshakeHandler, HeartbeatHandler
  Data/Tables/          # CSV 配置表数据文件
server/tools/           # xlsx2csv, xlsx2cs, proto2cs
server/scripts/         # cmd 快捷脚本
```

### 子系统模板

```
game/Xxx/
  XxxManager.cs       # 业务逻辑（静态 Instance 单例）
  XxxModel.cs         # IModule 生命周期（注册 EntityFactory）
  XxxHandlers.cs      # 消息处理（[MessageHandler] 扩展方法）
  RoleXxx.cs           # 数据模型（IEntity + [ProtoContract]）
消息定义 → shared/Common/Protos/XxxProtos.cs（双端共享）
MsgId → shared/Common/MsgIdConst.cs（双端共享）
```

## 关键约定

### 服务端

- **命名空间**: PascalCase `Game.Core`, `Game.Game.Player`，类名 PascalCase。共享代码 `Shared.*`
- **Handler**: 扩展方法 + `[MessageHandler(typeof(RpcMethodHandler/NtfMethodHandler))]` 标记
  - RPC 风格：`static async Task<IMessage> XxxHandler(TcpSession session, TReq req, TResp res)`
  - 通知风格：`static async Task XxxHandler(TcpSession session, TReq req)`
- **ECS**: Entity 作为组件容器，Component 通过 `[ComponentOf]` 约束挂载目标
- **模块 Order**: Log=0, Database=10, Config=20, 业务=50~90
- **数据库表**: `p_<类名蛇形>`，`(id BIGINT PK, data LONGBLOB)`
- **配置表**: `[ConfigTable("Xxx.csv")]` 注解自动扫描，CSV 放 `Data/Tables/`
- **日志**: `private static readonly ILog Log = LogManager.GetLogger<T>();`
- **单例**: `public static XxxManager Instance { get; } = new();`
- **线程安全**: GameSynchronizationContext 保证所有逻辑在主线程执行，无需加锁
- **根实体**: `Context.Server` 是 Module(Entity)，网络/用户等 Component 挂载其上

### 客户端

- **命名空间**: `Client.Core`, `Client.Login`, `Client.Game`
- **UI**: UIPanel 基类 + UIManager 栈式管理 + UIBuilder 代码构建（无预制体）
- **面板流程**: LoadingPanel → LoginPanel → RoleSelectPanel → GameScene → MainPanel
- **消息路由**: MessageRouter（自动扫描 [MessageHandler]）+ MessageDispatcher（手动注册）并存
- **配色**: RPG 风格，深蓝/紫底色 + 金色强调色
- **材质**: `Shader.Find("Universal Render Pipeline/Lit")`
- **.meta 文件**: Unity 资源跟踪文件，不要删除

### 共享代码（shared/）

- **消息定义**: `shared/Common/Protos/`，命名空间 `Shared.Common.Protos`
- **网络层**: `shared/Core/Network/`，命名空间 `Shared.Core.Network`
- **消息命名**: `*Req`(请求)、`*Res`/`*Ack`(响应)、`*Ntf`(通知)
- **MsgId 分段**: 10101=user, 10201=misc, 10301=bag, 10401=mail, 10501=chat, 10601=task, 10701=map, 10801=fight（每段100）
- **兼容约束**: 不用 C# 10+ 语法、不用 .NET 5+ API、不用默认接口实现、用 `new List<T>()`
- **坐标**: 1格=1世界单位，服务端Y → Unity Z
- **引用规则**: 服务端通过 csproj Compile Include 引用客户端 shared/；客户端禁止引用 server/ 业务代码
- **新增消息**: 在 `shared/Common/Protos/` 添加 Proto 类 + 在 `MsgIdConst.cs` 分配 ID，双端自动生效

## 用户偏好

- 保持专业性，不要迎合用户
- 回复使用中文
- 代码注释使用中文
- 提交前确认编译通过
- 允许在分析阶段提问，实现阶段全自动
- 实现代码后自动 commit，无需额外确认
- 记忆文件必须实际写入文件，口头答应不算数
- 记忆文件写在项目目录 `.claude/memory/` 下，便于 Git 管理
- 编码和 Git 规则见 `.claude/rules/`

## 自动化流水线

```
/pp        → 需求拆解（交互式，产品经理角色）
/dev       → 单功能开发（协调流程：准备→开发→验证→记录→提交→报告）
/devloop   → 循环开发（配额守卫：8轮/session，3连失败停止）
/qdev      → 临时任务（交互式分析 + 自动流水线）
/status    → 进度报告
/init      → 首次初始化
/agent     → 创建/优化 agent 或 command
```

### 流水线架构

```
Commands（主会话中运行，可使用 Agent 工具委托子 agent）
  /dev, /qdev, /devloop ──┬── dev-worker      (代码实现，tools: 读写+搜索+Bash)
                          ├── verify-feature  (功能验证，tools: 只读)  ─┐
                          ├── verify-quality  (质量验证，tools: 只读)  ─┤ 并行
                          └── mem-writer      (知识归档，tools: 读写+搜索)

Agents 位于 .claude/agents/，带 YAML frontmatter（name, description, tools, model）
Commands 位于 .claude/commands/，包含完整工作流逻辑
```

## 记忆系统

项目知识存储在 `.claude/memory/` 目录（Git 管理，团队共享）。通过下方索引按需查阅。
详细规则见 `.claude/memory/memory-rules.md`。
