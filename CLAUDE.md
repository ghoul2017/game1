# CLAUDE.md

2.5D RPG 游戏项目。Unity 客户端（`client/`）+ .NET 8 服务端（`server/`）。TCP 二进制 Protobuf 通信，服务端权威。

## 构建与运行

```bash
# 服务端
cd server && dotnet build
dotnet run --project server           # 监听 0.0.0.0:9000
dotnet run --project tools -- --list  # 辅助工具
scripts\xlsx2csv.cmd [args]           # xlsx 转 csv
scripts\xlsx2cs.cmd [args]            # xlsx 转 C# 配置类

# 客户端：Unity 2021.3.26f1c1 + URP 12.1.11
# Unity Editor 打开 client/ 目录构建
# 编译检查：在 client/ 目录下 dotnet build client/Assembly-CSharp.csproj
```

## 技术栈

| 模块 | 技术 |
|---|---|
| 协议 | TCP `[MsgId:2B][Length:4B][Payload]`，小端序 |
| 序列化 | protobuf-net (`[ProtoContract]`/`[ProtoMember]`) |
| 读表 | CsvHelper + `[ConfigTable]` 自动扫描 |
| 数据库 | MySQL(Dapper) + MongoDB |
| 缓存 | Write-Behind（内存 + 60s 批量持久化） |
| 实体 | IEntity + EntityFactory + ProtobufPersistFactory |
| 事件 | EventBus 发布订阅（类型安全） |
| 日志 | 自研 ILog + Appender（参考 SLF4J） |

## 目录结构

```
shared/Network/          # IMessage + PacketCodec（双端共享）
shared/Protos/           # MsgId + 全部消息定义（双端共享）
client/Assets/Scripts/
  GameShared/            # junction → shared/
  Core/                  # UIManager, UIPanel, UIBuilder
  Login/                 # 登录 & 角色选择
  Game/                  # MainPanel, PlayerController, MapGround
  Network/               # NetClient, MessageDispatcher
server/server/
  Core/                  # IModule, Entity, EventBus, Log
  Network/               # TcpServer, MessageRouter, SessionManager
  Config/                # CSV 配置 + 热更新
  Database/              # MySQL + MongoDB
  common/config/         # 配置表定义
  game/                  # 子系统：user, bag, mail, chat, task, map, fight, aoi
tools/                   # xlsx2csv, xlsx2cs
```

### 子系统模板

```
game/Xxx/
  XxxManager.cs       # 业务逻辑单例（静态 Instance）
  XxxModel.cs         # IModule 生命周期（注册 Handler + EntityFactory）
  XxxHandlers.cs      # 消息处理（合并到一个文件）
  RoleXxx.cs           # 数据模型
消息定义 → shared/Protos/XxxProtos.cs（双端共享）
```

## 关键约定

### 服务端

- **命名空间**: 小写 `server.game.player`，类名 PascalCase。共享代码 `GameShared.*`
- **Handler**: 无状态，无参构造，通过 `XxxManager.Instance` 访问依赖，不缓存单例引用
- **模块 Order**: Log=0, Database=10, Config=20, 业务=50~90, Network=100
- **数据库表**: `p_<类名蛇形>`，`(id BIGINT PK, data LONGBLOB)`
- **配置表**: `[ConfigTable("Xxx.csv")]` 注解自动扫描，CSV 放 `Data/Tables/`
- **日志**: `private static readonly ILog Log = LogManager.GetLogger<T>();`

### 客户端

- **命名空间**: `Client.Core`, `Client.Login`, `Client.Game`
- **UI**: UIPanel 基类 + UIManager 栈式管理 + UIBuilder 代码构建（无预制体）
- **面板流程**: LoadingPanel → LoginPanel → RoleSelectPanel → GameScene → MainPanel
- **配色**: RPG 风格，深蓝/紫底色 + 金色强调色
- **材质**: `Shader.Find("Universal Render Pipeline/Lit")`
- **.meta 文件**: Unity 资源跟踪文件，不要删除

### 共享代码（shared/）

- **消息定义**: `shared/Protos/`，命名空间 `GameShared.Protos`/`GameShared.Network`
- **消息命名**: `*Req`(请求)、`*Ack`(响应)、`*Ntf`(通知)
- **MsgId 分段**: 10101=user, 10201=misc, 10301=bag, 10401=mail, 10501=chat, 10601=task, 10701=map, 10801=fight（每段100）
- **兼容约束**: 不用 C# 10+ 语法、不用 .NET 5+ API、不用默认接口实现、用 `new List<T>()`
- **坐标**: 1格=1世界单位，服务端Y → Unity Z
- **引用规则**: 客户端可引用 shared/，禁止引用 server/ 业务代码
- **新增消息**: 只改 `shared/Protos/` 一处，双端自动生效
