# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 项目概览

2.5D RPG 游戏项目，包含 **Unity 客户端**（`client/`）和 **.NET 8 C# 服务端**（`server/`）。客户端和服务端之前独立开发，现进入联合开发阶段。

- **通信协议**: TCP 二进制 Protobuf — `[MsgId:UInt16LE][Length:Int32LE][Payload]`（小端序）
- **序列化**: protobuf-net（`[ProtoContract]`/`[ProtoMember]`），无 .proto 文件
- **服务端权威**: 所有游戏逻辑由服务端完成，客户端仅负责 UI 展示和表现

## 构建与运行

### 服务端

```bash
cd server
dotnet build                          # 构建解决方案
dotnet run --project server           # 启动服务器（监听 0.0.0.0:9000）
dotnet run --project tools -- --list  # 列出辅助工具

# 快捷脚本（在 server/scripts/ 下）
scripts\xlsx2csv.cmd [args]           # xlsx 转 csv
scripts\xlsx2cs.cmd [args]            # xlsx 转 C# 配置类
```

### 客户端

- Unity 2021.3.26f1c1 + URP 12.1.11
- 通过 Unity Editor 打开 `client/` 目录，File > Build Settings 构建
- `client.sln` 仅用于 IDE 智能提示，不要手动编辑 `.csproj` 文件
- 编译检查: `dotnet build client/Assembly-CSharp.csproj`（需在 client/ 目录下）

## 架构总览

```
shared/                          # 客户端+服务端共享代码（唯一源文件）
  Network/                       #   IMessage 接口, PacketCodec 编解码
  Protos/                        #   MsgId 常量, 全部8模块消息定义
client/                          # Unity 客户端
  Assets/Scripts/
    GameShared/                  #   directory junction → shared/（Unity 自动编译）
    Core/                        # 框架层（UIManager, UIPanel, UIBuilder, GameEntry）
    Login/                       # 登录 & 角色选择
    Game/                        # 主界面 & 游戏场景（PlayerController, MapGround 等）
    Network/                     # 网络层（MessageDispatcher, MsgTypeRegistry）
    Editor/                      # 编辑器工具

server/                          # .NET 8 服务端
  server/
    Program.cs                   # 入口：模块化启动（日志→数据库→读表→网络）
    Core/                        # 框架（IModule, Entity, EventBus, Log）
    Network/                     # TCP 网络层（TcpServer, MessageRouter, SessionManager）
    Config/                      # 配置表系统（CSV + 热更新）
    Database/                    # MySQL + Redis + MongoDB
    common/config/               # 配置表定义（ItemConfig, SkillConfig, MapConfig）
    game/                        # 游戏子系统（每系统一目录：user, bag, mail, chat, task, map, fight）
  tools/                         # 辅助工具（xlsx2csv, xlsx2cs, proto2cs）
```

### 服务端子系统结构模式

```
game/Bag/
  BagManager.cs       # 业务逻辑单例（静态 Instance）
  BagModel.cs         # IModule 生命周期（注册 Handler + EntityFactory）
  RoleBag.cs           # 数据模型
  BagHandlers.cs       # 所有 Handler 合并到一个文件
消息定义 → shared/Protos/BagProtos.cs（双端共享）
```

### 客户端 UI 架构

- **UIPanel 基类** → 所有面板继承，重写 `OnInit()`/`OnShow()`/`OnHide()`
- **UIManager** → 单例，栈式面板管理（`ShowPanel<T>()`/`ReplacePanel<T>()`），Canvas 跨场景持久
- **UIBuilder** → 静态工具类，代码动态构建 UI（无需预制体）
- **面板流程**: LoadingPanel → LoginPanel → RoleSelectPanel → GameScene → MainPanel

## 联合开发关键约定

### 消息系统

- **消息定义**: `shared/Protos/` 目录，客户端和服务端共享同一份代码
- **命名空间**: `GameShared.Protos`（消息类）、`GameShared.Network`（IMessage、PacketCodec）
- **消息命名**: `*Req`（请求）、`*Ack`（响应）、`*Ntf`（通知）
- **MsgId 分段**: 10101=user, 10201=misc, 10301=bag, 10401=mail, 10501=chat, 10601=task, 10701=map, 10801=fight（每段 100）

### 引用规则

- 客户端**可引用**共享代码（`shared/` 下的 Network、Protos 等）
- 客户端**禁止引用**服务端业务代码（`server/` 下的 Player、Bag、Mail、Chat 等子系统）
- **新增消息**: 只改 `shared/Protos/` 一处，双端自动生效

### 跨端共享代码方法论

当客户端和服务端都需要同一份代码时，必须遵循以下流程，**严禁复制两份**：

**判断标准** — 以下情况的代码应放入 `shared/`：
- 双端都需要的数据结构（消息定义、配置表定义、常量）
- 双端都需要的基础设施（编解码、序列化、工具方法）
- 任何"改一处就要同步改另一处"的代码

**共享流程**：
1. 在 `shared/` 下创建文件，使用 `GameShared.*` 命名空间
2. 代码必须兼容两端：.NET 8 + Unity 2021.3（.NET Framework 4.7.1）
3. 服务端通过 `server.csproj` 的 `<Compile Include="..\..\shared\**\*.cs" />` 引用
4. 客户端通过 directory junction（`client/Assets/Scripts/GameShared/` → `shared/`）引用
5. junction 路径加入 `.gitignore`，避免 git 重复跟踪

**兼容性约束**（shared/ 下的代码必须遵守）：
- 不使用 C# 10+ 语法（file-scoped namespace、global using）
- 不使用 C# 12 语法（collection expressions `[]`）
- 不使用 .NET 5+ only API（`BinaryPrimitives`、`ReadOnlySpan`、`Range`）
- 不使用默认接口实现（C# 8 特性，netstandard2.0 不支持）
- 使用 `new List<T>()` 代替 `new()` 或 `[]`
- 使用手动字节操作代替 `BinaryPrimitives`

### 坐标映射

- 1格 = 1世界单位，服务端 Y 轴 → Unity Z 轴（`MapData.GridToWorld`/`WorldToGrid`）
- Unity 正交相机 45度俯视角（rotation X=45, Size=8）

## 服务端关键约定

- **命名空间**: 目录名和命名空间一律小写（`server.game.player`、`server.game.fight`），类名 PascalCase。共享代码用 `GameShared.*`
- **Handler**: 无状态，无参构造，通过 `XxxManager.Instance` 单例访问依赖，不缓存单例引用
- **模块 Order**: Log=0, Database=10, Config=20, 业务=50~90, Network=100
- **实体持久化**: IEntity + ProtobufPersistFactory → Write-Behind 缓存（60s 批量写入）
- **数据库表**: `p_<类名蛇形>`（如 RoleBag → p_role_bag），`(id BIGINT PK, data LONGBLOB)`
- **配置表**: `[ConfigTable("Xxx.csv")]` 注解自动扫描，CSV 文件放 `Data/Tables/`
- **日志**: `private static readonly ILog Log = LogManager.GetLogger<T>();`

## 客户端关键约定

- **命名空间**: `Client.Core`、`Client.Loading`、`Client.Login`、`Client.Game`
- **UI 配色**: RPG 风格 — 深蓝/紫底色 + 金色强调色（常量在 UIBuilder 中）
- **材质**: 所有程序化材质使用 `Shader.Find("Universal Render Pipeline/Lit")`
- **Unity 兼容**: 保持 .NET Framework 4.7.1 + Unity 2021.3 API 兼容
- **.meta 文件**: Unity 资源跟踪文件，不要删除

## 自动化开发工作流

基于 [Anthropic 长时间运行 Agent 最佳实践](https://www.anthropic.com/engineering/effective-harnesses-for-long-running-agents)，采用双 Agent 架构：

### 核心文件

| 文件 | 用途 |
|---|---|
| `features.json` | 功能清单（JSON 格式，每个功能有 id/category/priority/steps/passes） |
| `progress.txt` | 跨 session 进度记录 |
| `.claude/agents/initializer.md` | 初始化 Agent（首次运行） |
| `.claude/agents/coding-agent.md` | 编码 Agent（每 session 一个功能） |

### 开发流程

```
/init          → 首次：初始化环境、创建 features.json 和 progress.txt
/dev           → 单次：读取进度 → 验证环境 → 实现一个功能 → 测试 → commit
/devloop       → 循环：连续执行 /dev，带配额守卫（8轮/session，3连失败停止）
/status        → 查看当前进度
/rc            → review + commit
```

### 每个 session 标准流程（/dev）

```
pwd → 读 progress.txt → 读 features.json → git log
→ dotnet build（服务端+客户端，确认环境未坏）
→ 选 priority 最小且 passes=false 的功能
→ 实现 → 编译测试 → 更新 features.json（只改 passes 字段）
→ git commit → 更新 progress.txt → 输出 session 报告
```

### 关键规则

- **每次只做一个功能** — 严禁一次实现多个
- **严禁删除或修改 features.json 中的测试条目** — 只允许修改 passes 字段
- **认真测试后才标记 passes: true** — 编译必须通过
- **session 结束时代码必须干净** — 可编译、可 commit
- **构建失败无法修复时回退** — `git checkout -- <file>`

### 无人值守脚本

```bash
dev.cmd          # 单次开发（非交互模式）
devloop.cmd [N]  # 循环开发，默认 8 轮
```

## 记忆系统

- **所有记忆保存在项目目录** `.claude/memory/` 下，不使用 `~/.claude/projects/`
- **项目知识/约定** → `CLAUDE.md`
- **本地/工作记录** → `CLAUDE.local.md`
- **结构化记忆** → `.claude/memory/`（索引文件：`.claude/memory/MEMORY.md`）

## 自我进化约定

| 触发条件 | 动作 |
|---|---|
| 相同操作手动执行 3 次以上 | 创建 command 自动化 |
| 发现新的命名/目录约定 | 更新 CLAUDE.md 约定章节 |
| 复杂流程有固定模式 | 创建 agent 封装 |
| 现有 agent/command 不够好 | 直接优化，不改名 |
| 从参考项目学到新模式 | 添加到约定章节 |

### 参考项目

- **E:\work\sgserver** — 成熟三国游戏服务器（Java 8, Maven 多模块）
- **E:\work\server** — 游戏服务器雏形（Java 21, Maven 多模块）

### 工具链

#### Commands（`.claude/commands/`）

| 命令 | 用途 |
|---|---|
| `/init` | 初始化自动化开发环境 |
| `/dev` | 实现一个功能（单次） |
| `/devloop` | 循环开发（带配额守卫） |
| `/status` | 查看进度报告 |
| `/rc` | review + commit |

#### Agents（`.claude/agents/`）

| Agent | 用途 |
|---|---|
| initializer | 首次运行环境初始化 |
| coding-agent | 增量开发（每 session 一个功能） |
| build-check | 客户端代码修改后自动编译检查 |
| review-commit | `/rc` 调用 |

#### Scripts

| 脚本 | 用途 |
|---|---|
| `dev.cmd` | 单次自动化开发（非交互） |
| `devloop.cmd` | 循环自动化开发（非交互） |
