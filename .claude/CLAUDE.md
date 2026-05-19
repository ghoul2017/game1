# CLAUDE.md

2.5D RPG 游戏项目。Unity 客户端（`client/`）+ .NET 8 服务端（`server/`）。TCP 二进制 Protobuf 通信，服务端权威。

## 构建与运行

```bash
# 服务端（本机 .NET 8 SDK 在 ~/.dotnet/，需用完整路径）
~/.dotnet/dotnet build server/Game
~/.dotnet/dotnet run --project server/Game           # 监听 0.0.0.0:9000
~/.dotnet/dotnet run --project server/tools -- --list  # 辅助工具
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
| 读表 | SimpleCsvReader + `[ConfigTable]` 自动扫描（双端共享） |
| 数据库 | MongoDB（BSON 文档存储） |
| 实体 | Entity + Component + System（ECS 风格：E 节点 / C 放数据 / S 负责方法） |
| 事件 | EventBus 发布订阅（类型安全） |
| 日志 | 自研 ILog + Appender（参考 SLF4J） |
| 线程模型 | GameSynchronizationContext 单线程调度，await 自动回主线程 |
| 定时器 | GameTimer，回调自动 Post 到主线程 |
| Token | TokenSigner（HMAC-SHA256 签名，无状态验证，Role.LastToken 单次有效） |

## 目录结构

```
client/Assets/Scripts/
  shared/               # 双端共享代码（服务端和 Robot 均通过 csproj Compile Include 引用）
    Core/
      ECS/              # Entity, Component, ComponentOfAttribute
      Network/          # IMessage, PacketCodec, MessageRouter, TcpSession, IMessageHandler
      Config/           # ConfigManager, ConfigTableAttribute, SimpleCsvReader
      Log/              # ILog, LogManager, Console/File Appender
      IdGenerator.cs    # 雪花算法 ID 生成
    Common/
      Protos/           # MsgIdConst + 全部消息定义（双端共享）
      Config/           # 配置表定义（ItemConfig, SkillConfig, MapConfig 等）
  Logic/                # 客户端纯逻辑层（零 UnityEngine 依赖，供 Robot 引用）
    IClientUI.cs        # UI 桥接接口 + INetworkSender + ClientContext
    IPanels.cs           # 面板空标记接口（仅用于 UIManager.GetPanel<T> 类型参数）
    NetClient.cs         # 网络管理器（连接/心跳/重连，纯 C# 类，非 MonoBehaviour）
    Context.cs           # 客户端上下文（Module + NetworkComponent）
    Handlers/           # 消息 Handler（LoginHandlers, BagHandlers 等 8 个，含 MapHandlers）
    Controllers/        # 业务控制器（Login/Bag/Mail/Task/Chat/Fight/RoleSelect/Map 共 8 个）
    Data/               # GameData, MapObjectType, MapData（坐标转换，返回元组）
  View/                 # Unity 表现层
    Core/               # UIManager, UIPanel, UIBuilder, GameEntry, LogClientInitializer 等
    Panels/             # 面板（LoginPanel, BagPanel, MainPanel 等）
    Scene/              # 场景对象（PlayerController, MapGround, MonsterView 等）
    Editor/             # SceneCreator

server/Game/
  Program.cs            # 入口：单线程调度 → Component 挂载 + 子系统初始化
  Context.cs            # 静态上下文：Context.Server(Module), Context.Players, Context.Map 等
  Entities/Module.cs    # 根实体（所有 Module 级 Component 挂载于此）
  Entities/MapObjectEntity.cs  # 地图对象实体基类（MapPlayerEntity/MonsterEntity 继承之）
  Components/
    User/               # UserComponent + UserComponentSystem + UserHandler
    Player/             # PlayerComponent + PlayerSystem + PlayerEntity + PlayerOnlineComponent + RoleDataComponent + RoleAttributeComponent + RoleAttribute
    Bag/                # RoleBagComponent + RoleBagSystem + ItemEffectSystem + RoleBagHandlers + BagItem
    Mail/               # RoleMailComponent + RoleMailSystem + RoleMailHandlers + RoleMail
    Chat/               # ChatComponent + ChatSystem + ChatHandlers
    GameTask/            # RoleTaskComponent + RoleTaskSystem + RoleTaskHandlers + RoleTask
    Fight/              # FightSystem + FightHandlers + FightCore + FightUnitComponent + FightAttribute + Skill
    Map/                # MapComponent + MapSystem + MapHandlers + GameMap + MapPlayerEntity + MapAoiNotifier + IMapObject
    Monster/            # MonsterEntity + MonsterInfoComponent + MonsterAiComponent + AiState（刷怪/AI逻辑在 GameMap 扩展方法中）
    Misc/               # MiscHandlers（握手+心跳）
  Core/
    AppConfig.cs        # 配置根（Server, MongoDb, Tables, Log）
    Options.cs          # 各子配置 Options 类
    GameSynchronizationContext.cs  # 单线程调度器
    GameTimer.cs        # 定时器（回调自动回主线程）
    ShutdownToken.cs    # 统一关闭信号
    TokenSigner.cs      # HMAC-SHA256 Token 签名器
    Aoi/                # IAoiObject, IAoiEventListener, Tower, TowerAoi（灯塔AOI，服务端专属）
    Database/           # MongoDb (统一入口), IEntity, IEntityExtensions
    Entity/             # IEntity
    Event/              # EventBus, IEvent, IEventListener
    Network/            # NetworkComponent, RpcMethodHandler, NtfMethodHandler
  Data/Tables/          # CSV 配置表数据文件
server/tools/           # xlsx2csv, xlsx2cs, proto2cs
server/scripts/         # cmd 快捷脚本
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
| Module 级 | Context.Server | 全局数据集合 | PlayerComponent, MapComponent, ChatComponent |
| PlayerEntity 级 | PlayerEntity | 玩家数据 | BagComponent, MailComponent, TaskComponent, RoleAttributeComponent, PlayerOnlineComponent, RoleDataComponent |

### System 规则

- System 是纯静态扩展方法类（`public static class XxxSystem`），只有方法和常量，不持有字段
- 通过 `this XxxComponent self` 访问 Component 数据
- Module 级：`Context.Players.GrantExpAsync(...)` — Context 提供便利属性
- PlayerEntity 级：`player.Bag.AddItem(...)` — PlayerEntity 提供便利属性
- 日志用 `LogManager.GetLogger(nameof(XxxSystem))`（静态类不能做泛型参数）

### 登录加载策略

选角入场时一次性挂载所有 PlayerEntity 级 Component 并从 MongoDB 加载全部数据：
```csharp
var player = PlayerEntity.Create(roleId, userId, role);
var bagComp = player.AddComponent<BagComponent>();
bagComp.RoleBag = await bagComp.LoadBagAsync(roleId);
// ... Mail, Task, RoleAttribute 同理
Context.Players.AddPlayer(player);
```

## 关键约定

### 服务端

- **命名空间**: PascalCase `Game.Core`, `Game.Components.Player`，类名 PascalCase。共享代码 `Shared.*`
- **Handler**: 扩展方法 + `[MessageHandler(typeof(RpcMethodHandler/NtfMethodHandler))]` 标记
  - RPC 风格：`static async Task<IMessage> XxxHandler(TcpSession session, TReq req, TResp res)`
  - 通知风格：`static async Task XxxHandler(TcpSession session, TReq req)`
- **ECS**: Entity 节点 / Component 放数据 / System 负责方法，Component 通过 `[ComponentOf]` 约束挂载目标
- **模块初始化**: Component 挂载到 Context.Server（Module 级）或 PlayerEntity（PlayerEntity 级），初始化通过 System 扩展方法调用
- **数据库**: MongoDB BSON 文档存储，集合名=类名蛇形（如 `role_bag`），`_id`=entity.Id
- **实体**: 实现 IEntity + `[BsonId]` 标记 Id，运行时索引用 `[BsonIgnore]`，MongoDb.Instance 统一 CRUD
- **日志**: `private static readonly ILog Log = LogManager.GetLogger<T>();`
- **单例**: `public static XxxManager Instance { get; } = new();`
- **线程安全**: GameSynchronizationContext 保证所有逻辑在主线程执行，无需加锁
- **根实体**: `Context.Server` 是 Module(Entity)，网络/用户等 Component 挂载其上

### 客户端

- **命名空间**: `Client.Core`, `Client.Login`, `Client.Game`
- **UI**: UIPanel 基类 + UIManager 栈式管理 + UIBuilder 代码构建（无预制体）
- **面板流程**: LoadingPanel → LoginPanel → RoleSelectPanel → GameScene → MainPanel
- **消息路由**: MessageRouter（自动扫描 `[MessageHandler]` 属性），双端统一。客户端 Handler 类通过 `UIManager.GetPanel<T>()` 访问面板
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
- **错误处理**: Ack/Res 使用 `ErrorMessage`（`string`，ProtoMember 编号 1），成功时为空字符串，失败时填中文错误文本。客户端通过 `!string.IsNullOrEmpty(ack.ErrorMessage)` 判断失败并直接显示

## 用户偏好

- 保持专业性，不要迎合用户
- 回复使用中文
- 代码注释使用中文
- 提交前确认编译通过
- 允许在分析阶段提问，实现阶段全自动
- 实现代码后自动 commit，无需额外确认
- 记忆文件写在项目目录 `.claude/memory/` 下，便于 Git 管理

## 新对话启动指南

1. 读 `docs/1.需求.md` — 检查是否有未规划条目，有则按 docs-workflow 规则拆解
2. 读 `docs/3.TODO.md` — 按 TODO 优先级推进工作，不要重复已完成的事
3. 读 `docs/2.PLAN/` — 了解当前需求的设计方案
4. 读 `docs/4.发现需求.md` — 自行发现的问题和待办，按优先级自行解决
5. 修改 robot/ 代码时参考 `.claude/rules/robot-ai-manual.md`

## 自定义命令

- `/save` — 退出会话前保存进度（更新待办、记录决策、踩坑记录、检查规则更新、输出总结）

## 记忆系统

项目知识存储在 `.claude/memory/` 目录（Git 管理，团队共享）。通过下方索引按需查阅。
详细规则见 `.claude/memory/memory-rules.md`。
