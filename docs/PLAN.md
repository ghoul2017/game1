# 游戏服务器前期基础设施方案

## Context

从零构建 .NET 8 游戏服务器的前期基础设施，包含三大模块：网络通信、配置读表、数据库。
- 客户端: Unity (C#)
- 序列化: Protobuf (protobuf-net, [ProtoContract] 标注, 无需 .proto 文件)
- 架构: 单进程单服
- 配置表热更新: 支持

---

## 目录结构

```
server/
├── server.sln
├── server/
│   ├── server.csproj
│   ├── Program.cs                    # Generic Host 入口
│   ├── appsettings.json              # 配置文件
│   │
│   ├── Network/                      # 网络模块
│   │   ├── TcpServer.cs              # TCP 监听 (BackgroundService)
│   │   ├── TcpSession.cs             # 客户端会话
│   │   ├── IMessageHandler.cs        # Handler 接口
│   │   ├── MessageRouter.cs          # 消息路由 (MessageName → Handler)
│   │   ├── PacketCodec.cs            # 编解码 (消息头 + Protobuf 体)
│   │   ├── Messages/                 # 消息定义
│   │   │   ├── MessageAttribute.cs   # 消息名标记
│   │   │   ├── MessageRegistry.cs    # 消息名注册表
│   │   │   ├── HandshakeReq.cs
│   │   │   └── HandshakeResp.cs
│   │   └── Handlers/                 # Handler 实现
│   │       └── HandshakeHandler.cs
│   │
│   ├── Config/                       # 读表模块
│   │   ├── IConfigManager.cs         # 接口 (含热更新支持)
│   │   ├── ConfigManager.cs          # 实现
│   │   ├── CsvLoader.cs             # CSV 加载器 (CsvHelper)
│   │   └── Tables/                   # 表定义类
│   │       ├── ITable.cs
│   │       ├── TbItem.cs
│   │       └── TbSkill.cs
│   │
│   ├── Database/                     # 数据库模块
│   │   ├── MySql/
│   │   │   ├── MySqlManager.cs
│   │   │   └── Repositories/
│   │   │       ├── IPlayerRepository.cs
│   │   │       └── PlayerRepository.cs
│   │   ├── MongoDb/
│   │   │   └── MongoDbManager.cs
│   │   └── Redis/
│   │       └── RedisManager.cs
│   │
│   ├── Core/                         # 公共基础设施
│   │   └── Options.cs                # 配置选项类
│   │
│   └── Data/Tables/                  # CSV 数据文件
│       ├── Item.csv
│       └── Skill.csv
│
├── tools/                            # 工具
│   ├── xlsx2csv.py                   # xlsx → csv 转换
│   └── xlsx/                         # 原始 xlsx
```

---

## NuGet 包

| 包 | 用途 |
|---|---|
| `Microsoft.Extensions.Hosting` | Generic Host + DI |
| `Microsoft.Extensions.Configuration.Json` | JSON 配置 |
| `protobuf-net` | Protobuf 序列化 |
| `CsvHelper` | CSV 读写 |
| `MySqlConnector` | MySQL 异步驱动 |
| `Dapper` | 轻量 ORM |
| `MongoDB.Driver` | MongoDB 官方驱动 |
| `StackExchange.Redis` | Redis 客户端 |

---

## 协议格式

```
[FrameLength:4B][NameLength:2B][Name:UTF8][Payload]
```
- FrameLength: Int32，小端序，不包含自身 4 字节
- NameLength: UInt16，小端序，消息名 UTF-8 字节长度
- Name: 显式稳定消息名，例如 `User.LoginReq`
- Payload: protobuf-net 序列化的消息体

---

## 关键接口设计

### 网络模块

```csharp
// TcpServer: BackgroundService, 由 Host 管理生命周期
class TcpServer : BackgroundService
    - ExecuteAsync(): 启动监听, Accept 循环创建 TcpSession
    - Sessions: 当前在线会话列表

// TcpSession: 代表一个客户端连接
class TcpSession : IDisposable
    - SessionId: ulong
    - Send(IMessage msg)
    - Send(string messageName, byte[] payload)
    - Disconnect()
    - 内部: 接收循环 → PacketCodec 解包 → MessageRouter 分发

// 消息处理器接口
interface IMessageHandler
    - HandleAsync(TcpSession, byte[] payload) → byte[]

// 消息路由器
class MessageRouter
    - Register(IMessageHandler)
    - RouteAsync(TcpSession, string messageName, byte[] payload)

// 编解码 (粘包处理)
class PacketCodec
    - Encode(string messageName, byte[] payload) → byte[]
    - TryDecode(ReadOnlySpan<byte>) → (messageName, payload, consumed)
```

### 读表模块

```csharp
// 表行接口
interface ITable
    - Id: int (主键)

// CSV 加载器
class CsvLoader
    - Load<T>(string filePath) → List<T>

// 配置管理器 (含热更新)
interface IConfigManager
    - LoadAll(string dir)
    - GetTable<T>() → List<T>
    - OnReload: event (热更新通知)

class ConfigManager : IConfigManager
    - FileSystemWatcher 监听 CSV 目录变化
    - 文件变化时自动重载对应表
```

### 数据库模块

```csharp
// MySQL (MySqlConnector + Dapper)
class MySqlManager
    - InitializeAsync() / ShutdownAsync()
    - CreateConnection() → IDbConnection

class PlayerRepository
    - GetByIdAsync(long id) → Player?
    - CreateAsync(Player) / UpdateAsync(Player)

// MongoDB
class MongoDbManager
    - InitializeAsync() / ShutdownAsync()
    - GetCollection<T>(string name) → IMongoCollection<T>

// Redis
class RedisManager
    - InitializeAsync() / ShutdownAsync()
    - StringSetAsync / StringGetAsync
    - SortedSetAddAsync / SortedSetRangeByRankAsync (排行榜)
```

---

## 实现步骤

### Step 1: 项目骨架
- 编辑 `server.csproj` 添加 NuGet 包
- 创建 `appsettings.json` (CopyToOutputDirectory)
- 创建 `Core/Options.cs`
- 改造 `Program.cs` 为 Generic Host

### Step 2: 网络模块
- `PacketCodec.cs` — 编解码 + 粘包处理
- `TcpSession.cs` — 单连接收发
- `TcpServer.cs` — TCP 监听 BackgroundService
- `IMessageHandler.cs` — Handler 接口
- `MessageRouter.cs` — 消息路由
- `MessageAttribute.cs` / `MessageRegistry.cs` — 消息名声明和注册
- Protobuf 消息类 (HandshakeReq/Resp)
- `HandshakeHandler` — 示例 Handler

### Step 3: 读表模块
- `tools/xlsx2csv.py` — xlsx → csv 工具
- `ITable.cs` — 表行接口
- `CsvLoader.cs` — CSV 加载器
- 示例表类 (TbItem, TbSkill)
- `ConfigManager.cs` — 含 FileSystemWatcher 热更新

### Step 4: 数据库模块
- `MySqlManager.cs` + `PlayerRepository.cs`
- `MongoDbManager.cs`
- `RedisManager.cs`

### Step 5: 集成串联
- `Program.cs` 完整 DI 注册
- 启动顺序: 数据库 → 读表 → 网络
- LoginHandler 集成示例

---

## 技术要点

- **网络**: System.Net.Sockets + async/await, 无外部网络库
- **粘包**: PacketCodec 内部用 `ReadOnlySpan<byte>` 精确解析消息边界
- **Protobuf**: protobuf-net 的 `[ProtoContract]`/`[ProtoMember]` 标注, 无需 .proto 编译
- **热更新**: FileSystemWatcher 监听 Data/Tables/, 变化时重载对应表
- **数据库**: Dapper 手写 SQL, 不用 EF Core
- **生命周期**: Generic Host 管理所有模块启停顺序
