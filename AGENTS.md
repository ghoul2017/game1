# AGENTS.md

2.5D RPG 游戏项目。Unity 客户端（`client/`）+ .NET 8 服务端（`server/`）。TCP 二进制 Protobuf 通信，服务端权威。

## 最高优先级

- 回复使用中文，保持专业，不迎合。
- 代码注释使用中文。
- 状态判断、代码引用、完成声明必须有工具输出或已读取文件支撑。
- 未重现的问题不要硬改代码、配置或全局环境；标记为“未重现”，记录复查命令、现象和后续处理条件。
- 只改完成目标必需的内容，不顺手重构、优化或删除无关代码。
- 代码变更后必须按影响范围编译/验证；验证失败继续修，不能把失败包装成完成。
- 实现代码后自动 commit；不自动 `git push`、`git reset`、`git checkout` 回退、递归删除，除非用户明确要求。
- 长期规则优先写入 `AGENTS.md`、`codex/rules/` 或 `docs/`，不要只写 memory。项目记忆如需新增，写在 `codex/memory/` 下。

## 规则入口

- `AGENTS.md`：入口索引、硬性原则、项目事实、常用命令。
- `codex/rules/behavior.md`：行为细则，包括证据、未重现、精确修改、规则沉淀。
- `codex/rules/workflow.md`：需求拆解、执行流程、验证、Git 规则。
- `codex/rules/code-conventions.md`：修改 C# 前按需读取。
- `codex/rules/pitfalls.md`：历史踩坑和反直觉设计。
- `codex/rules/robot-ai-manual.md`：修改 `robot/` 时必须读取。
- `docs/`：需求、计划、TODO、模块文档、遗留问题和归档。

## 新对话启动

1. 读 `codex/rules/behavior.md`、`codex/rules/workflow.md`、`codex/rules/pitfalls.md`。
2. 读 `docs/1.需求.md`，检查未规划条目；从需求文档新拆需求时必须先规划并等待审批。
3. 读 `docs/3.TODO.md`，按 TODO 优先级推进，不重复已完成事项。
4. 按需读 `docs/2.PLAN/`、`docs/4.发现需求.md`、`docs/7.遗留问题.md`。
5. 修改 `*.cs` 前按需读 `codex/rules/code-conventions.md`；修改 `robot/` 必须读 `codex/rules/robot-ai-manual.md`。

用户在当前对话中直接下达的实现请求，按实现阶段全自动执行；分析阶段可以提问，实现阶段默认自主推进。

## 构建与运行

```bash
# 服务端
dotnet build server/Server.csproj
dotnet run --project server/Server.csproj           # 监听 0.0.0.0:9000

# 工具
dotnet run --project tools/tools.csproj -- --list
scripts\xlsx2csv.cmd [args]
scripts\xlsx2cs.cmd [args]

# 客户端：Unity 2021.3.26f1c1 + URP 12.1.11
# Unity Editor 打开 client/ 目录构建

# 整体编译检查
dotnet build game1.sln
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
| 日志 | 自研 ILog + Appender |
| 线程模型 | GameSynchronizationContext 单线程调度，await 自动回主线程 |
| 定时器 | GameTimer，回调自动 Post 到主线程 |
| Token | TokenSigner（HMAC-SHA256 签名，无状态验证，Role.LastToken 单次有效） |

## 目录结构

```text
client/Assets/Scripts/
  Shared/               # 双端共享代码，服务端和 Robot 通过 Compile Include 引用
    Core/               # ECS、Network、Config、Log、IdGenerator
    Common/             # MsgIdConst、Protos、Config
  Logic/                # 客户端纯逻辑层，零 UnityEngine 依赖，供 Robot 引用
    Controllers/        # Login/Bag/Mail/Task/Chat/Fight/RoleSelect/Map
    Handlers/           # 客户端消息处理
    Data/               # GameData、MapObjectType、MapData
  View/                 # Unity 表现层：UI、场景、Editor

server/
  Program.cs
  AppStart.cs
  Context.cs
  Components/           # User/Player/Bag/Mail/Chat/GameTask/Fight/Map/Monster/Misc
  Core/                 # AppConfig、Database、Aoi、Event、Network、Timer、Token
  Data/Tables/          # CSV 配置表
  Entities/             # Module、MapObjectEntity

robot/                  # 独立 .NET 8 Console 自动化测试
tools/                  # xlsx2csv、xlsx2cs 等工具
scripts/                # 工具快捷脚本
docs/                   # 需求、计划、TODO、模块文档、遗留问题
codex/rules/            # 长期规则库
```

目录名固定使用小写 `server/`、`tools/`、`scripts/`；文档、脚本、`.sln`、`.csproj` 中不要写成 `Server/`。

## 服务端约定

- 命名空间 PascalCase：`Game.Core`、`Game.Components.Player`；共享代码命名空间 `Shared.*`。
- Handler 使用静态方法 + `[MessageHandler(typeof(RpcMethodHandler/NtfMethodHandler))]`。
- RPC 风格：`static async Task<IMessage> XxxHandler(TcpSession session, TReq req, TResp res)`。
- 通知风格：`static async Task XxxHandler(TcpSession session, TReq req)`。
- ECS：Entity 节点 / Component 放数据 / System 负责方法；Component 通过 `[ComponentOf]` 约束挂载目标。
- Module 级 Component 挂到 `Context.Server`，PlayerEntity 级 Component 挂到 `PlayerEntity`。
- System 是纯静态扩展方法类，不持有字段；静态类日志用 `LogManager.GetLogger(nameof(XxxSystem))`。
- MongoDB 集合名=类名蛇形，`_id`=entity.Id；实体实现 `IEntity`，Id 标 `[BsonId]`。
- 线程安全由 `GameSynchronizationContext` 保证，业务逻辑无需加锁。

## 客户端约定

- 命名空间：`Client.Logic`、`Client.View`、`Client.View.Editor`。
- `Logic/` 零 UnityEngine 依赖；`View/` 可依赖 Logic，Logic 不可依赖 View。
- UI：`UIPanel` + `UIManager` 栈式管理 + `UIBuilder` 代码构建，无预制体。
- 面板流程：LoadingPanel → LoginPanel → RoleSelectPanel → GameScene → MainPanel。
- Controller 持有业务状态并暴露 Action；Panel 纯渲染并订阅 Controller 事件。
- UI 配色保持 RPG 风格：深蓝/紫底色 + 金色强调色。
- 材质使用 `Shader.Find("Universal Render Pipeline/Lit")`。
- `.meta` 是 Unity 资源跟踪文件，不要删除；删除 `.cs` 后需注意 Unity csproj 刷新。

## 共享代码约定

- 消息定义：`client/Assets/Scripts/Shared/Common/Protos/`。
- 网络层：`client/Assets/Scripts/Shared/Core/Network/`。
- 消息命名：`*Req` 请求、`*Res`/`*Ack` 响应、`*Ntf` 通知。
- MsgId 分段：10101=user，10201=misc，10301=bag，10401=mail，10501=chat，10601=task，10701=map，10801=fight。
- 新增消息：添加 Proto 类 + 在 `MsgIdConst.cs` 分配 ID，双端自动生效。
- 错误处理：Ack/Res 使用 `ErrorMessage`，成功为空字符串，失败填中文错误文本。
- 坐标：1格=1世界单位，服务端 Y → Unity Z。
- 兼容约束：Shared/ 客户端相关代码避免 C# 10+ 语法、.NET 5+ API 和默认接口实现。
- 服务端可通过 csproj Compile Include 引用 Shared；客户端禁止引用 `server/` 业务代码。

## 自定义命令

- `/save`：退出会话前保存进度，更新 TODO/需求/决策/踩坑/模块文档/遗留问题，并输出下次继续点。
