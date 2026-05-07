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
| 数据库 | MongoDB（BSON 文档存储） |
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
  Program.cs            # 入口：单线程调度 → Component 挂载 + 子系统初始化
  Context.cs            # 静态上下文：Context.Server(Module 实体), Context.Config
  Entities/Module.cs    # 根实体（所有 Component 挂载于此）
  Components/
    User/               # UserComponent + UserComponentSystem + UserHandler
    Player/             # PlayerComponent
    Bag/                # BagComponent
    Mail/               # MailComponent
    Fight/              # FightComponent
    Chat/               # ChatComponent
    Map/                # MapComponent
    Task/               # TaskComponent
    Monster/            # MonsterComponent
  Core/
    AppConfig.cs        # 配置根（Server, MongoDb, Tables, Log）
    Options.cs          # 各子配置 Options 类
    GameSynchronizationContext.cs  # 单线程调度器
    GameTimer.cs        # 定时器（回调自动回主线程）
    ShutdownToken.cs    # 统一关闭信号
    TokenStore.cs       # 内存 Token 存储
    Database/           # DatabaseManager, MongoDbManager, MongoEntityRepository, MongoPlayerRepository, MongoRoleRepository
    Entity/             # IEntity
    Event/              # EventBus, IEvent, IEventListener
    Network/            # NetworkComponent, SessionManager, RpcMethodHandler, NtfMethodHandler
  game/                 # 子系统
    Player/             # PlayerManager, PlayerEntity, PlayerOnlineComponent, RoleDataComponent, Role, RoleAttribute
    Bag/                # BagManager, BagHandlers, RoleBag, BagItem, ItemEffectManager
    Mail/               # MailManager, MailHandlers, RoleMail
    Chat/               # ChatManager, ChatHandlers
    GameTask/           # TaskManager, TaskHandlers, RoleTaskList
    Map/                # MapManager, MapHandlers, GameMap, MapPlayer, MapNpc, MapAoiNotifier
    Fight/              # FightManager, FightHandlers, FightCore, FightUnit, Skill
    Monster/            # MonsterManager, MonsterUnit
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
  XxxHandlers.cs      # 消息处理（static + [MessageHandler(typeof(RpcMethodHandler))]）
  RoleXxx.cs           # 数据模型（IEntity + [ProtoContract] + [BsonId] + [BsonIgnore]）
Components/Xxx/
  XxxComponent.cs     # Component（[ComponentOf(typeof(Module))]）
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
- **模块初始化**: Component 挂载到 Context.Server，有实质逻辑的子系统在 Program.cs 中直接调用初始化
- **数据库**: MongoDB BSON 文档存储，集合名=类名蛇形（如 `role_bag`），`_id`=entity.Id
- **实体**: 实现 IEntity + `[BsonId]` 标记 Id，运行时索引用 `[BsonIgnore]`，MongoEntityRepository 统一 CRUD
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
- **核心约束必须写在 rules 和 CLAUDE.md 里，不要只写在 memory 里——memory 不自动加载，你不会主动查**
- 记忆文件写在项目目录 `.claude/memory/` 下，便于 Git 管理

## TODO：架构迁移

详细方案见 `.claude/plans/architecture-migration.md`。

- [x] Phase 1: IModule→Component + 去缓存层 + MongoDB
  - [x] Step 1.1: Entity 基类增强（GetAllComponents, TryGetComponent, HasComponent）
  - [x] Step 1.2: MongoDB 持久化基础设施（MongoEntityRepository, MongoPlayerRepository, MongoRoleRepository）
  - [x] Step 1.3: 修改 DatabaseManager + AppConfig（去掉 MySql/Redis）
  - [x] Step 1.4: 创建 8 个 Component 替代 IModule
  - [x] Step 1.5: 修改 Manager 使用 MongoDB（替代 EntityFactory）
  - [x] Step 1.6: 切换 Program.cs
  - [x] Step 1.7: 清理旧代码（删除 IModule/ModuleRegistry/EntityFactory/MySQL/Redis 等）
- [x] Phase 2: Handler 统一为扩展方法风格（Bag/Mail/Chat/Map/Task/Fight）
- [x] Phase 3: Player 变 Entity（PlayerEntity + PlayerOnlineComponent + RoleDataComponent）
- [ ] Phase 3 后续: MapEntity + MonsterEntity（改动面大，需逐步推进）

## 开发工作流

执行准则见 `.claude/rules/dev-workflow.md` — 自主判断、底线规则、子 agent 分工、需求拆解、进度记录。

## 记忆系统

项目知识存储在 `.claude/memory/` 目录（Git 管理，团队共享）。通过下方索引按需查阅。
详细规则见 `.claude/memory/memory-rules.md`。
