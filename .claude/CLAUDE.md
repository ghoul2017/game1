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

