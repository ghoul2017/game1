# Robot 自动化测试 — 使用手册

目标读者：AI。

任务目标：在用户授权的测试环境和数据范围内构建、运行或扩展 Robot 场景，用协议级断言验证服务端行为，并完整报告进程、数据副作用和结果。

Robot 是独立于 Unity 的命令行测试工具，通过 TCP 连接服务端，发送 Protobuf 消息模拟完整客户端行为，验证服务端逻辑。

## 执行边界

- 开始前运行 `git status --short --branch`，确认当前任务是否仅允许运行既有场景，还是同时授权修改 Robot 代码。
- 构建本身不改变游戏数据；连接服务端、登录、创建角色、进入游戏或运行其他业务场景会读写 MongoDB，执行前必须确认测试环境和账号范围。
- 默认只连接 `127.0.0.1:9000`。连接远程地址前必须确认目标是获准测试的环境，不得自行连接生产服务。
- 启动服务端时以仓库 `bin/` 为工作目录运行构建产物，并记录 PID。只停止本任务启动且已核对归属的进程。
- 场景失败时保留原始响应、日志和数据库状态，先定位协议、环境或业务根因，不新增绕过断言或伪成功分支。

## 环境要求

- .NET 8.0 SDK；以 `dotnet --version` 的当前输出为准
- 服务端运行中（默认 127.0.0.1:9000）

## 项目结构

```
robot/
├── Robot.csproj         # .NET 8 Console，ProjectReference 引用 shared/ + Logic/
├── Program.cs           # 入口，命令行解析，注册测试场景
├── RobotSession.cs      # NetworkComponent + TcpSession 薄包装（CallAsync / WaitForAsync / ReconnectAsync）
├── TestRunner.cs        # 加载场景 → 执行 → 收集 PASS/FAIL 结果
├── TestScenario.cs      # 测试场景抽象基类
├── Assertions.cs        # 断言工具（Equal / True / NotNull / Fail）
├── Handlers/
│   └── RobotClientUIEventHandler.cs # 客户端 UI 事件处理器（控制台输出）
├── LoginTest.cs         # 登录冒烟
└── TaskPersistenceTest.cs # 接取、重复请求、击杀进度和两次重登恢复
```

## 快速开始

```bash
# 1. 构建 Robot 和服务端
dotnet build robot/Robot.csproj
dotnet build server/Server.csproj
```

`Server.csproj` 会把 `appsettings.json` 和客户端唯一配置源 `client/Assets/StreamingAssets/Tables/*.csv` 复制到仓库统一输出目录 `bin/`。另开终端，以 `bin/` 为工作目录启动服务端：

```bash
cd bin
dotnet Server.dll
```

Rider 调试直接启动 `bin/Server` 时使用同一套输出配置和表。服务端只从构建后的 `bin/` 目录运行，不使用 `dotnet run --project server/Server.csproj` 启动。

回到仓库根目录运行当前已注册场景：

```bash
dotnet run --project robot/Robot.csproj -- --scenario login_test
dotnet run --project robot/Robot.csproj -- --scenario task_persistence_test
```

`task_persistence_test` 每次使用独立的 `robot_task_<时间戳>` 账号和角色，验证任务 1 的接取、重复接取、断线批次保存、重新登录、击杀 5 只野猪、进度恢复、提交领奖、重复提交和领奖后恢复。它同时检查 `QueryBagAck.Items` 列表中的 Uid 为正数且唯一、提交奖励只发送一次最终 `BagItemChangedNtf`，以及 `BagUseItemReq(Uid)` 稳定返回“道具使用功能未完成”且不扣物品。断线后会等待服务端异步保存批次再重新连接。野猪可能随机掉落任务奖励同款物品，因此奖励断言比较提交前后的 long 数量增量，而不是假设背包初始总数为 0。时间戳新账号不会加载旧背包数据，因此不要求删除整个 `role_bag_component`；只有复用已有旧角色时才必须先完成精确数据重建。该场景会在 MongoDB 创建账号及角色组件并改变怪物运行状态，只能在已授权测试环境执行；测试数据清理仍需独立授权，未授权时保留并报告。

输出示例：
```
Robot 自动化测试框架
=====================
=== Robot 测试开始 (1 个场景) ===

--- login_test ---
  [login_test] 发送 HandshakeReq...
  [login_test] 发送 LoginReq...
  [login_test] 登录成功: PlayerId=123456, 角色数=3, Token=abc...
  PASS: login_test

=== 完成: 1 通过, 0 失败 ===
```

## 命令行参数

| 参数 | 默认值 | 说明 |
|------|--------|------|
| `--host` | `127.0.0.1` | 服务端地址 |
| `--port` | `9000` | 服务端端口 |
| `--scenario` | 全部场景 | 指定运行场景名 |

```bash
# 运行全部场景
dotnet run --project robot/Robot.csproj

# 运行指定场景
dotnet run --project robot/Robot.csproj -- --scenario login_test
dotnet run --project robot/Robot.csproj -- --scenario task_persistence_test

# 连接远程服务端
dotnet run --project robot/Robot.csproj -- --host 192.168.1.100 --port 9000
```

## 编写测试场景

### 最小示例

```csharp
using System.Threading.Tasks;
using Shared.Common.Protos;

namespace Robot
{
    public class MyTest : TestScenario
    {
        public override string Name => "my_test";

        public override async Task Run()
        {
            // 1. 握手
            var handshakeRes = await Session.CallAsync<HandshakeRes>(new HandshakeReq());
            Assertions.True(string.IsNullOrEmpty(handshakeRes.ErrorMessage), "握手失败");

            // 2. 登录
            var loginRes = await Session.CallAsync<LoginRes>(new LoginReq
            {
                Username = "test",
                Password = "123456"
            });
            Assertions.True(string.IsNullOrEmpty(loginRes.ErrorMessage), "登录失败");

            // 3. 你的测试逻辑 ...
        }
    }
}
```

### 注册场景

在 `Program.cs` 中添加：
```csharp
runner.AddScenario(new MyTest());
```

### 消息收发

```csharp
// RPC：发送请求，等待指定类型响应
var res = await Session.CallAsync<LoginRes>(new LoginReq { ... });

// 等待服务端推送（如 EnterGameNtf）
var ntf = await Session.WaitForAsync<EnterGameNtf>();

// 仅发送（不等待响应）
await Session.SendAsync(new HeartbeatReq());

// 旧接口仍可用，但新测试优先使用 CallAsync / WaitForAsync
var legacyRes = await Session.SendAndWaitAsync<LoginRes>(new LoginReq { ... });
```

### 断言

```csharp
Assertions.True(string.IsNullOrEmpty(res.ErrorMessage), "应成功");
Assertions.Equal(100, res.Hp, "HP 应为 100");
Assertions.NotNull(res.Token, "Token 不应为空");
Assertions.True(res.Roles.Count > 0, "角色列表不应为空");
```

断言失败会抛出 `TestFailedException`，TestRunner 输出：
```
  FAIL: my_test — 应成功: 期望=true, 实际=false
```

### 使用 GameData

Robot 可以读写 `Client.Logic.GameData` 的静态字段：
```csharp
GameData.Token = loginRes.Token;
GameData.CurrentRole = new RoleData { RoleId = role.RoleId, Name = role.Name };
```

### 完整示例：登录 + 进入游戏

```csharp
public class EnterGameTest : TestScenario
{
    public override string Name => "enter_game_test";

    public override async Task Run()
    {
        // 握手
        var handshakeRes = await Session.CallAsync<HandshakeRes>(new HandshakeReq());
        Assertions.True(string.IsNullOrEmpty(handshakeRes.ErrorMessage), "握手失败");

        // 登录
        var loginRes = await Session.CallAsync<LoginRes>(new LoginReq
        {
            Username = "test",
            Password = "123456"
        });
        Assertions.True(string.IsNullOrEmpty(loginRes.ErrorMessage), "登录失败");
        Assertions.True(loginRes.Roles.Count > 0, "至少需要1个角色");

        // 进入游戏
        var role = loginRes.Roles[0];
        var enterRes = await Session.CallAsync<EnterGameRes>(new EnterGameReq
        {
            RoleId = role.RoleId
        });
        Assertions.True(string.IsNullOrEmpty(enterRes.ErrorMessage), $"进入游戏失败: {enterRes.ErrorMessage}");

        Log($"进入游戏成功: {role.Name}, Token={enterRes.Token}");
    }
}
```

## 调试

### 日志输出

Robot 使用共享的 `ILog` + `LogManager`。未初始化时使用 `ConsoleFallbackLogger` 直接输出到控制台。

服务端日志按进程工作目录解析，位于 `bin/log/`。

### 常见问题

| 问题 | 排查方向 |
|------|---------|
| 连接超时 | 服务端是否运行？端口是否正确？ |
| 握手失败 | 检查服务端 NetworkComponent 是否正常监听 |
| 等待响应超时 | 消息 MessageName 是否匹配？服务端 Handler 是否正确注册？ |
| 登录失败 | 账号密码是否正确？数据库是否有该用户？ |
| 编译失败 | `dotnet build robot/Robot.csproj` 检查错误，确认 .NET 8 SDK 已安装 |

## 设计决策

- **复用共享网络层**：RobotSession 通过 NetworkComponent.ConnectAsync 创建 TcpSession，不再自建 Socket、PacketCodec 和消息等待字典
- **RPC 优先**：请求/响应测试优先使用 TcpSession.CallAsync，经 Network.RpcRequest/Network.RpcResponse 自动关联响应
- **业务消息零额外字段**：LoginReq、EnterGameReq 等业务协议不携带 RpcId，RPC 关联信息只存在网络层内部 Envelope
- **ProjectReference 引用 shared/ + Logic/**：Robot 引用 Shared 和 ClientLogic，不引用 View/ 或 UnityEngine
- **每个场景独立连接**：TestRunner 为每个场景创建新 RobotSession，运行后 Disconnect，保证场景间隔离

## 停止分支和最终报告

- 服务端、MongoDB、配置表或测试数据不可用时，记录命令和原始错误后停止；不得改正式配置或伪造前置数据。
- 协议或业务预期存在分歧、场景会覆盖已有数据、或远程环境身份无法确认时，列出证据和影响并询问用户。
- 最终报告必须包含：Robot 与服务端构建结果、运行场景、地址和端口、测试账号/角色、PASS/FAIL 汇总、失败响应和日志、MongoDB 副作用、启动/停止的进程，以及未验证范围。
