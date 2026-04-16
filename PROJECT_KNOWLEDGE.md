# Game1 项目技术知识库

> 本文档是 CodeBuddy 的项目知识库，用于快速准确定位问题根源并解决。
> 文档按模块组织，包含关键代码路径、常见问题排查、数据流图。

---

## 目录

1. [项目概览](#1-项目概览)
2. [目录结构速查](#2-目录结构速查)
3. [核心架构图](#3-核心架构图)
4. [网络通信协议](#4-网络通信协议)
5. [消息系统](#5-消息系统)
6. [服务端核心模块](#6-服务端核心模块)
7. [客户端核心模块](#7-客户端核心模块)
8. [数据持久化](#8-数据持久化)
9. [配置系统](#9-配置系统)
10. [AOI视野系统](#10-aoi视野系统)
11. [游戏子系统](#11-游戏子系统)
12. [开发规范与约定](#12-开发规范与约定)
13. [问题排查指南](#13-问题排查指南)

---

## 1. 项目概览

### 1.1 基本信息

| 项目 | 说明 |
|------|------|
| 项目名称 | game1 - 2.5D RPG |
| 客户端 | Unity 2021.3.26f1c1 + URP 12.1.11 |
| 服务端 | .NET 8 C# |
| 通信协议 | TCP 二进制 Protobuf |
| 序列化 | protobuf-net |
| 数据库 | MySQL + Redis + MongoDB |
| 开发日期 | 2026-04 |

### 1.2 核心特性

- **服务端权威**：所有游戏逻辑由服务端完成，客户端仅负责 UI 展示
- **共享代码**：客户端和服务端共享消息定义、网络编解码、配置表定义
- **Write-Behind 缓存**：内存缓存 + 定时批量持久化（60s）
- **Per-User 线程队列**：每个玩家串行队列，跨玩家并行

---

## 2. 目录结构速查

```
game1/
├── shared/                          # ⭐ 共享代码（双端共用）
│   ├── Network/                     #   IMessage、PacketCodec
│   ├── Protos/                     #   MsgId 常量 + 全部消息定义
│   └── Config/                     #   配置表定义（双端共用）
│
├── server/                          # .NET 8 服务端
│   ├── server/
│   │   ├── Core/                   #   框架核心
│   │   │   ├── IModule.cs          #     模块接口（Init/Start/Stop）
│   │   │   ├── ModuleRegistry.cs   #     模块注册表
│   │   │   ├── Event/              #     EventBus 发布订阅
│   │   │   ├── Entity/             #     实体工厂、Write-Behind
│   │   │   ├── Log/                #     日志系统（ILog + Appender）
│   │   │   ├── IdGenerator.cs      #     雪花算法ID生成
│   │   │   └── TokenStore.cs       #     内存Token存储
│   │   ├── Network/                #   TCP网络层
│   │   │   ├── TcpServer.cs        #     服务器监听
│   │   │   ├── TcpSession.cs       #     会话管理
│   │   │   ├── SessionManager.cs   #     会话注册表
│   │   │   └── MessageRouter.cs    #     消息路由
│   │   ├── Database/               #   数据库层
│   │   ├── Config/                 #   配置表加载
│   │   └── game/                   #   游戏子系统（每个一目录）
│   │       ├── user/               #     登录注册
│   │       ├── player/             #     玩家管理器
│   │       ├── bag/                #     背包系统
│   │       ├── mail/               #     邮件系统
│   │       ├── chat/               #     聊天系统
│   │       ├── task/               #     任务系统
│   │       ├── map/                #     地图系统
│   │       ├── fight/              #     战斗系统
│   │       └── aoi/                #     AOI视野系统
│   └── tools/                      #   辅助工具
│
└── client/                         # Unity 客户端
    └── Assets/Scripts/
        ├── GameShared/             #   → junction → shared/
        ├── Core/                   #   UI框架
        │   ├── UIManager.cs        #     面板管理
        │   ├── UIPanel.cs          #     面板基类
        │   └── UIBuilder.cs        #     代码构建UI
        ├── Network/                #   网络层
        │   ├── NetClient.cs        #     TCP客户端
        │   └── MessageDispatcher.cs #    消息分发
        ├── Loading/                 #   加载界面
        ├── Login/                  #   登录选角
        └── Game/                   #   主游戏场景
```

---

## 3. 核心架构图

### 3.1 整体架构

```
┌─────────────────────────────────────────────────────────────────┐
│                        Unity 客户端                              │
│  ┌─────────┐  ┌──────────┐  ┌────────────┐  ┌─────────────┐    │
│  │ Loading │→ │  Login   │→ │RoleSelect  │→ │  GameScene  │    │
│  │ Panel   │  │  Panel   │  │  Panel     │  │  MainPanel  │    │
│  └─────────┘  └──────────┘  └────────────┘  └─────────────┘    │
│        ↑            ↑            ↑                ↑             │
│        └────────────┴────────────┴────────────────┘             │
│                         UIManager                                 │
│  ┌─────────────────────────────────────────────────────────────┐│
│  │                      NetClient                               ││
│  │  Connect/Disconnect/Send/Receive + 心跳 + 断线重连         ││
│  └─────────────────────────────────────────────────────────────┘│
└───────────────────────────┬─────────────────────────────────────┘
                            │ TCP [MsgId:2B][Len:4B][Payload]
                            ▼
┌───────────────────────────────────────────────────────────────────┐
│                      .NET 8 服务端                                  │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────────────────┐  │
│  │  TcpServer  │→ │ TcpSession  │→ │    MessageRouter         │  │
│  │  端口监听    │  │  会话管理    │  │  Handler 自动注册/分发   │  │
│  └─────────────┘  └─────────────┘  └─────────────────────────┘  │
│                                              │                    │
│  ┌─────────────────────────────────────────────────────────────┐ │
│  │                     ModuleRegistry                           │ │
│  │  LogManager → DatabaseManager → ConfigManager → 游戏子系统   │ │
│  └─────────────────────────────────────────────────────────────┘ │
│         │                │                │                      │
│  ┌──────┴──┐    ┌───────┴────┐   ┌──────┴──────┐               │
│  │EntityFact│    │MySQL/Redis │   │ ConfigManager│               │
│  │ory       │    │            │   │  CSV热更新   │               │
│  │WriteBehind│    └────────────┘   └─────────────┘               │
│  └──────────┘                                                    │
└───────────────────────────────────────────────────────────────────┘
```

### 3.2 服务端子系统结构模式

每个游戏子系统遵循统一模式：

```
game/Xxx/
├── XxxManager.cs       # 业务逻辑单例（静态 Instance）
├── XxxModel.cs         # IModule 生命周期
├── XxxHandlers.cs      # 所有 Handler（消息处理）
├── RoleXxx.cs          # 数据模型
└── XxxConfig.cs        # 配置表定义
```

---

## 4. 网络通信协议

### 4.1 协议格式

```
┌────────────┬─────────────┬─────────────────┐
│  MsgId     │   Length    │     Payload      │
│  (2字节)   │   (4字节)   │   (Length字节)   │
│ UInt16LE   │  Int32LE    │   Protobuf       │
└────────────┴─────────────┴─────────────────┘
```

- **字节序**：小端序 (Little Endian)
- **最大包大小**：1MB

### 4.2 关键代码路径

| 端 | 文件 | 说明 |
|----|------|------|
| 共享 | `shared/Network/PacketCodec.cs` | 编解码（双端共用） |
| 服务端 | `server/Network/TcpSession.cs` | 会话收发 |
| 客户端 | `client/Assets/Scripts/Network/NetClient.cs` | TCP客户端 |

### 4.3 客户端连接流程

```
Connect() → TcpClient.BeginConnect
         → ReceiveLoop (后台线程)
         → ProcessReceivedBytes (主线程)
         → PacketCodec.TryDecode
         → MessageDispatcher.Dispatch
```

### 4.4 心跳机制

- **发送间隔**：30秒（`heartbeatInterval`）
- **超时检测**：60秒（`recvTimeout`）
- **Ping计算**：`PingMs = (now - resp.ClientTime) / TicksPerMs`

---

## 5. 消息系统

### 5.1 MsgId 分段规则

每模块 100 个 ID，预留扩展空间：

| 模块 | 范围 | 说明 |
|------|------|------|
| User | 10101-10200 | 登录/角色 |
| Misc | 10201-10300 | 握手/心跳 |
| Bag | 10301-10400 | 背包 |
| Mail | 10401-10500 | 邮件 |
| Chat | 10501-10600 | 聊天 |
| Task | 10601-10700 | 任务 |
| Map | 10701-10800 | 地图 |
| Fight | 10801-10900 | 战斗 |

### 5.2 消息命名规范

- `*Req` - 请求（Client → Server）
- `*Ack` - 响应（Server → Client）
- `*Ntf` - 通知（Server → Client，单向）

### 5.3 消息定义位置

```
shared/Protos/
├── MsgId.cs           # 消息ID常量
├── UserProtos.cs      # 登录/角色
├── MiscProtos.cs      # 握手/心跳
├── BagProtos.cs       # 背包
├── MailProtos.cs      # 邮件
├── ChatProtos.cs      # 聊天
├── TaskProtos.cs      # 任务
├── MapProtos.cs       # 地图
└── FightProtos.cs     # 战斗
```

### 5.4 消息处理流程

#### 服务端

```
TcpSession.Receive
    → MessageRouter.RouteAsync(MsgId, Payload)
    → IMessageHandler.HandleAsync(session, payload)
    → 业务逻辑
    → session.Send(response)
```

Handler 自动注册：`MessageRouter.AutoRegister()` 扫描所有 `IMessageHandler` 实现

#### 客户端

```
NetClient.ProcessReceivedBytes
    → PacketCodec.TryDecode
    → MessageDispatcher.Dispatch(MsgId, Payload)
    → Action<IMessage> handler
    → UI 更新
```

---

## 6. 服务端核心模块

### 6.1 模块生命周期 (IModule)

```csharp
public interface IModule
{
    int Order { get; }                    // 初始化顺序
    Task InitAsync();                     // 按 Order 升序执行
    Task StartAsync(CancellationToken ct); // 按 Order 升序执行
    Task StopAsync();                     // 按 Order 降序执行
}
```

### 6.2 模块启动顺序

| Order | 模块 | 说明 |
|-------|------|------|
| 0 | LogManager | 日志系统 |
| 10 | DatabaseManager | MySQL + Redis |
| 20 | ConfigManager | CSV 配置表 |
| 50-90 | 游戏子系统 | 业务逻辑 |
| 100 | TcpServer | 网络监听 |

### 6.3 入口：Program.cs

```csharp
var config = JsonSerializer.Deserialize<AppConfig>(json);
LogManager.Init(config.Log);
await DatabaseManager.Instance.Init(config);
var registry = ModuleRegistry.Create(config);
await registry.InitAllAsync();
await registry.StartAllAsync(cts.Token);
```

---

## 7. 客户端核心模块

### 7.1 面板流程

```
LoadingPanel → LoginPanel → RoleSelectPanel → GameScene → MainPanel
```

### 7.2 UIManager

| 方法 | 说明 |
|------|------|
| `ShowPanel<T>()` | 显示面板，压入栈 |
| `HidePanel<T>()` | 隐藏面板 |
| `Back()` | 返回上一个面板 |
| `ReplacePanel<T>()` | 替换当前面板 |

### 7.3 UIPanel 基类

```csharp
public abstract class UIPanel : MonoBehaviour
{
    public virtual void OnInit() { }      // 初始化
    public virtual void OnShow() { }     // 显示时
    public virtual void OnHide() { }     // 隐藏时
}
```

### 7.4 网络连接状态

```csharp
public enum ConnState { Disconnected, Connecting, Handshaking, Connected, Reconnecting }
public bool IsConnected { get; }
public int PingMs { get; }
public float ReconnectCountdown { get; }
```

---

## 8. 数据持久化

### 8.1 Write-Behind 缓存

```
┌─────────────┐     变更     ┌─────────────┐     定时(60s)     ┌─────────────┐
│  EntityCache │ ← ─────── → │  业务逻辑   │ → ─────────── →  │   MySQL    │
│  (内存缓存)  │             │             │                  │  (持久化)   │
└─────────────┘             └─────────────┘                  └─────────────┘
```

### 8.2 关键类

| 类 | 位置 | 说明 |
|----|------|------|
| `EntityFactory` | `Core/Entity/EntityFactory.cs` | 实体生命周期管理 |
| `IEntity` | `Core/Entity/IEntity.cs` | 实体接口 |
| `IPersistFactory` | `Core/Entity/IPersistFactory.cs` | 持久化工厂接口 |
| `PersistTask` | `Core/Entity/PersistTask.cs` | 定时批量持久化 |

### 8.3 数据库表命名

格式：`p_<类名蛇形>`

| 实体 | 表名 |
|------|------|
| `Role` | `p_role` |
| `RoleBag` | `p_role_bag` |

### 8.4 Token 存储

从 Redis 迁移到内存存储：

- `TokenStore.cs` - `ConcurrentDictionary<token, TokenInfo>`
- 后台定时清理过期条目
- 登录时：`TokenStore.Set(token, userId, roleId)`
- 重连时：`TokenStore.Get(token)`

---

## 9. 配置系统

### 9.1 CSV 配置表

```
server/Data/Tables/
├── Item.csv
├── Skill.csv
├── Map.csv
└── ...
```

### 9.2 配置加载流程

```
ConfigManager.InitAsync()
    → FileSystemWatcher 监听目录
    → CsvHelper 解析
    → 缓存到 Dictionary<int, T>
```

### 9.3 配置表定义

```csharp
[ConfigTable("Item.csv")]
public class ItemConfig { ... }
```

### 9.4 客户端配置

```csharp
// 同步服务端配置表定义到 shared/Config/
ClientConfigManager.Load("Data/Tables/");
var item = ClientConfigManager.Get<ItemConfig>(1001);
```

---

## 10. AOI视野系统

### 10.1 九宫格 AOI

```
┌─────┬─────┬─────┐
│  1  │  2  │  3  │
├─────┼─────┼─────┤
│  4  │  Me │  5  │
├─────┼─────┼─────┤
│  6  │  7  │  8  │
└─────┴─────┴─────┘
```

### 10.2 关键文件

| 文件 | 说明 |
|------|------|
| `server/game/Aoi/TowerAoi.cs` | 灯塔式 AOI 实现 |
| `server/game/Aoi/MapAoiNotifier.cs` | AOI 事件通知 |
| `server/game/Map/GameMap.cs` | 地图集成 AOI |

### 10.3 视野事件

| 事件 | 说明 |
|------|------|
| 对象进入 | 玩家/NPC 进入视野 |
| 对象离开 | 玩家/NPC 离开视野 |
| 对象移动 | 位置更新 |

### 10.4 视野广播

```csharp
// 玩家进入地图
MapManager.EnterMap()
    → GameMap.AddObject()
    → Aoi.AddWatcher(object, 4)  // 视野半径 4 格
    → MapAoiNotifier.OnEnter()
    → 广播 MapPlayerEnterNtf 给周围玩家
```

---

## 11. 游戏子系统

### 11.1 子系统概览

| 系统 | Manager | Handler | 说明 |
|------|---------|---------|------|
| 用户 | `UserManager` | `UserHandlers` | 登录/注册/Token |
| 玩家 | `PlayerManager` | - | 在线玩家管理 |
| 背包 | `BagManager` | `BagHandlers` | 物品增删改查 |
| 邮件 | `MailManager` | `MailHandlers` | 邮件收发 |
| 聊天 | `ChatManager` | `ChatHandlers` | 世界/私聊 |
| 任务 | `TaskManager` | `TaskHandlers` | 任务接取/提交 |
| 地图 | `MapManager` | `MapHandlers` | 进出/移动/切图 |
| 战斗 | `FightManager` | `FightHandlers` | 技能/伤害 |

### 11.2 玩家生命周期

```
登录 → Token验证 → 加载角色 → 选择角色 → 进入地图
  ↓                                        ↓
退出 ← 保存数据 ← 离开地图 ← 断线/退出 ← 玩家下线
```

### 11.3 战斗流程

```
UseSkillReq
    → FightManager.UseSkill()
    → CD检查/距离检查/目标检查
    → FightCore.UseSkill() 计算伤害
    → HpChangedNtf 广播血量变化
    → 如果死亡 → KilledByNtf
    → UseSkillAck 返回给攻击者
```

---

## 12. 开发规范与约定

### 12.1 命名规范

| 类型 | 规范 | 示例 |
|------|------|------|
| 命名空间 | 小写 + 点分隔 | `server.game.player` |
| 类名 | PascalCase | `PlayerManager` |
| 方法名 | PascalCase | `GetPlayerById` |
| 私有字段 | `_camelCase` | `_players` |
| 常量 | 全大写 + 下划线 | `MAX_PLAYER_COUNT` |
| 消息类 | PascalCase + 尾缀 | `LoginReq`, `LoginResp` |

### 12.2 shared/ 代码约束

- **禁止** C# 10+ 语法（file-scoped namespace、global using）
- **禁止** C# 12 语法（collection expressions `[]`）
- **禁止** .NET 5+ only API（`BinaryPrimitives`、`ReadOnlySpan`）
- **使用** `new List<T>()` 而非 `new()`
- **使用** 手动字节操作代替 `BinaryPrimitives`

### 12.3 Handler 编写规范

```csharp
public class XxxHandler : IMessageHandler
{
    public ushort ReqMsgId => MsgId.XxxReq;

    public async Task<IMessage?> HandleAsync(TcpSession session, byte[] payload)
    {
        var req = Serializer.Deserialize<XxxReq>(payload);
        // 业务逻辑
        return new XxxAck { Code = 0 };
    }
}
```

### 12.4 日志规范

```csharp
private static readonly ILog Log = LogManager.GetLogger<T>();

Log.Debug("调试信息");  // 开发用
Log.Info("操作信息");   // 重要操作
Log.Warn("警告信息");   // 可恢复错误
Log.Error("错误信息");  // 严重错误
```

---

## 13. 问题排查指南

### 13.1 网络问题

| 症状 | 可能原因 | 排查步骤 |
|------|----------|----------|
| 连接失败 | 服务端未启动/端口错误 | 检查 `appsettings.json` 端口配置 |
| 断线重连 | 网络波动/服务端重启 | 查看日志 `HandleDisconnect` |
| 消息无响应 | MsgId 不匹配/Handler 未注册 | 检查 MsgId 和 Handler 注册 |
| 粘包/拆包 | 编解码实现错误 | 检查 `PacketCodec.TryDecode` |

### 13.2 数据问题

| 症状 | 可能原因 | 排查步骤 |
|------|----------|----------|
| 数据丢失 | Write-Behind 未及时持久化 | 停服前调用 `FlushAllAsync` |
| 加载失败 | 数据库连接异常 | 检查 MySQL 配置和连接 |
| 缓存不一致 | 多实例缓存不同步 | 检查 EntityFactory 缓存逻辑 |

### 13.3 性能问题

| 症状 | 可能原因 | 排查步骤 |
|------|----------|----------|
| 高延迟 | 数据库查询慢/GC压力 | 查看日志 Perf 相关 |
| 内存泄漏 | 缓存未清理/事件未取消订阅 | 检查 Dispose/Unsubscribe |
| CPU 高 | 死循环/频繁 GC | 性能分析工具检查 |

### 13.4 调试技巧

```csharp
// 服务端调试：查看消息收发
Log.Debug($"收到消息 MsgId:{msgId} Length:{payload.Length}");

// 客户端调试：查看网络状态
Debug.Log($"[NetClient] State:{_state} Ping:{PingMs}ms");

// 断点调试：消息路由
MessageRouter.RouteAsync()
```

### 13.5 日志文件

| 文件 | 位置 | 说明 |
|------|------|------|
| 服务端日志 | `server/log/game.log` | 主日志 |
| 错误日志 | `server/log/error.log` | 错误单独记录 |
| 客户端日志 | Unity Console | 通过 Debug.Log |

---

## 附录 A：关键文件快速定位

| 问题类型 | 检查文件 |
|----------|----------|
| 登录失败 | `server/game/user/UserHandlers.cs`, `server/game/player/PlayerManager.cs` |
| 背包异常 | `server/game/bag/BagManager.cs`, `client/Game/BagPanel.cs` |
| 地图加载 | `server/game/map/MapManager.cs`, `server/game/map/MapHandlers.cs` |
| 战斗计算 | `server/game/fight/FightManager.cs`, `server/game/fight/FightCore.cs` |
| 断线重连 | `client/Assets/Scripts/Network/NetClient.cs` |
| 配置表错误 | `server/server/Config/ConfigManager.cs`, `shared/Config/*.cs` |

---

## 附录 B：消息流对照表

| 功能 | 客户端发送 | 服务端响应 | 备注 |
|------|-----------|-----------|------|
| 登录 | `LoginReq` | `LoginResp` | 返回 Token |
| 角色列表 | `RoleListReq` | `RoleListResp` | |
| 创建角色 | `CreateRoleReq` | `CreateRoleResp` | |
| 选择角色 | `SelectRoleReq` | `SelectRoleResp` | |
| 进入地图 | `EnterMapReq` | `EnterMapAck` + `MapObjectsNtf` | |
| 移动 | `MapMoveReq` | `MapMoveAck` + `MapPlayerMoveNtf` | |
| 使用背包 | `BagUseItemReq` | `BagUseItemAck` + `BagItemChangedNtf` | |
| 发送聊天 | `ChatReq` | `ChatAck` + `ChatNtf` | |
| 使用技能 | `UseSkillReq` | `UseSkillAck` + `HpChangedNtf` | |

---

## 附录 C：踩坑记录与关键陷阱

> 来自 claudecode 开发过程中的实际踩坑记录，务必注意。

### C.1 MsgId 引用冲突

**问题**：客户端使用 `MsgId.LoginReq` 时，可能与同名常量冲突
**解决**：使用 `global::GameShared.Protos.MsgId.LoginReq` 前缀避免歧义

### C.2 Task 命名空间冲突

**问题**：`Server.Game.Task` 与 `System.Threading.Tasks.Task` 冲突
**解决**：任务系统改用 `TaskSystem` 命名，避免与系统 Task 类型冲突

### C.3 Windows .cmd 脚本调用

**问题**：.cmd 脚本中直接调用另一个 .cmd 只执行一轮就退出
**解决**：必须用 `call` 前缀调用其他 .cmd/.bat 脚本

### C.4 protobuf-net Unity 依赖

**问题**：protobuf-net v3.2.45 在 Unity 2021.3 中缺少依赖 DLL
**解决**：放入 `client/Assets/Plugins/protobuf-net/`，全部用 netstandard2.0 版本：

| DLL | NuGet 包路径 |
|---|---|
| protobuf-net.Core.dll | protobuf-net.core/3.2.45/lib/netstandard2.0/ |
| protobuf-net.dll | protobuf-net/3.2.45/lib/netstandard2.0/ |
| System.Reflection.Emit.dll | system.reflection.emit/4.7.0/lib/netstandard2.0/ |
| System.Reflection.Emit.ILGeneration.dll | system.reflection.emit.ilgeneration/4.7.0/lib/netstandard2.0/ |
| System.Collections.Immutable.dll | system.collections.immutable/7.0.0/lib/netstandard2.0/ |
| System.Runtime.CompilerServices.Unsafe.dll | system.runtime.compilerservices.unsafe/6.0.0/lib/netstandard2.0/ |

### C.5 Redis 开发环境

**问题**：开发阶段需要 Redis 但不想启动
**解决**：RedisManager 自动降级为内存存储模式（ConcurrentDictionary），无需启动 Redis 服务
- 内存模式下 Token 存储正常工作（带过期时间）
- 内存模式下角色 ID 用 Interlocked 自增，重启后从 0 开始
- Hash/SortedSet 在内存模式下抛 NotImplementedException，当前未使用
- 需 Redis 时只需启动 Redis 服务，无需改代码

### C.6 默认接口实现

**问题**：shared/ 代码使用了默认接口实现，Unity 2021.3 不支持
**解决**：shared/ 下禁止使用默认接口实现（C# 8 特性，netstandard2.0 不支持）

---

## 附录 D：开发工作流（源自 claudecode）

### D.1 自动化开发流程

基于 Anthropic 长时间运行 Agent 最佳实践：

| 文件 | 用途 |
|------|------|
| `features.json` | 功能清单（id/category/priority/steps/passes） |
| `progress.txt` | 跨 session 进度记录 |

### D.2 开发命令

| 命令 | 用途 |
|------|------|
| `/init` | 初始化自动化开发环境 |
| `/dev` | 实现一个功能（单次） |
| `/devloop` | 循环开发（带配额守卫：8轮/session，3连失败停止） |
| `/status` | 查看进度报告 |
| `/rc` | review + commit |
| `/plan` | 需求拆解（产品经理角色，交互式） |

### D.3 Agent 体系

| Agent | 用途 |
|-------|------|
| initializer | 首次运行环境初始化 |
| coding-agent | 增量开发（每 session 一个功能） |
| planner-agent | 需求拆解（交互式讨论） |

### D.4 关键规则

- **每次只做一个功能** — 严禁一次实现多个
- **严禁删除 features.json 中的条目** — 只允许修改 passes 字段
- **认真测试后才标记 passes: true** — 编译必须通过
- **session 结束时代码必须干净** — 可编译、可 commit
- **构建失败无法修复时回退** — `git checkout -- <file>`

### D.5 自我进化约定

| 触发条件 | 动作 |
|----------|------|
| 相同操作手动执行 3 次以上 | 创建 command 自动化 |
| 发现新的命名/目录约定 | 更新 CLAUDE.md 约定章节 |
| 复杂流程有固定模式 | 创建 agent 封装 |
| 从参考项目学到新模式 | 添加到约定章节 |

### D.6 进化日志

- `2026-04-10` 初始化：review-commit agent, /rc 命令, /dev 命令, /devloop 命令, devloop.cmd 脚本
- `2026-04-10` M14 背包系统：MsgId 引用冲突，更新命名约定（使用 global:: 前缀）
- `2026-04-11` M17 任务系统：`Server.Game.Task` 与 `System.Threading.Tasks.Task` 冲突，改用 `TaskSystem`
- `2026-04-11` 消息系统重构：IMessage + MessageHandler\<TReq\> + Router 自动扫描
- `2026-04-11` Config 系统重构：`[ConfigTable]` 自动扫描 + ConfigManager API + xlsx2cs 工具
- `2026-04-11` 目录结构重构：Handler 按模块合并，Proto 按模块合并，MsgId 重编 10101 起步
- `2026-04-12` 共享代码迁移：消息/MsgId/IMessage/PacketCodec 迁移到 `shared/`（`GameShared.*`）
- `2026-04-13` F019 战斗表现：伤害飘字、受击闪烁、死亡渐隐
- `2026-04-13` F025 移除业务层Redis：IdGenerator雪花算法 + TokenStore内存存储

---

## 附录 E：参考项目

| 项目 | 路径 | 说明 |
|------|------|------|
| sgserver | `E:\work\sgserver` | 成熟三国游戏服务器（Java 8, Maven 多模块） |
| server | `E:\work\server` | 游戏服务器雏形（Java 21, Maven 多模块） |

---

*本文档最后更新：2026-04-14 | 整合自 CLAUDE.md + .claude/memory/*
