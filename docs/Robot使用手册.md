# Robot 自动化测试 — 使用手册

Robot 是独立于 Unity 的命令行测试工具，通过 TCP 连接服务端，发送 Protobuf 消息模拟完整客户端行为，验证服务端逻辑。

## 环境要求

- .NET 8.0 SDK（本机路径 `~/.dotnet/`，使用 `~/.dotnet/dotnet` 运行）
- 服务端运行中（默认 127.0.0.1:9000）

## 项目结构

```
robot/
├── Robot.csproj         # .NET 8 Console，ProjectReference 引用 shared/ + Logic/
├── Program.cs           # 入口，命令行解析，注册测试场景
├── RobotSession.cs      # NetworkComponent + TcpSession 薄包装（CallAsync / WaitForAsync）
├── TestRunner.cs        # 加载场景 → 执行 → 收集 PASS/FAIL 结果
├── TestScenario.cs      # 测试场景抽象基类
├── Assertions.cs        # 断言工具（Equal / True / NotNull / Fail）
├── RobotClientUI.cs     # 客户端 UI 接口的空实现（控制台输出）
└── LoginTest.cs         # 示例：登录流程测试
```

## 快速开始

```bash
# 1. 构建
dotnet build robot/

# 2. 启动服务端（另开终端）
dotnet run --project server/Server.csproj

# 3. 运行测试
dotnet run --project robot/
```

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
dotnet run --project robot/

# 运行指定场景
dotnet run --project robot/ -- --scenario login_test

# 连接远程服务端
dotnet run --project robot/ -- --host 192.168.1.100 --port 9000
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

服务端日志：`server/logs/game-YYYYMMDD.log`

### 常见问题

| 问题 | 排查方向 |
|------|---------|
| 连接超时 | 服务端是否运行？端口是否正确？ |
| 握手失败 | 检查服务端 NetworkComponent 是否正常监听 |
| 等待响应超时 | 消息 MessageName 是否匹配？服务端 Handler 是否正确注册？ |
| 登录失败 | 账号密码是否正确？数据库是否有该用户？ |
| 编译失败 | `dotnet build robot/` 检查错误，确认 .NET 8 SDK 已安装 |

## 设计决策

- **复用共享网络层**：RobotSession 通过 NetworkComponent.ConnectAsync 创建 TcpSession，不再自建 Socket、PacketCodec 和消息等待字典
- **RPC 优先**：请求/响应测试优先使用 TcpSession.CallAsync，经 Network.RpcRequest/Network.RpcResponse 自动关联响应
- **业务消息零额外字段**：LoginReq、EnterGameReq 等业务协议不携带 RpcId，RPC 关联信息只存在网络层内部 Envelope
- **ProjectReference 引用 shared/ + Logic/**：Robot 引用 Shared 和 ClientLogic，不引用 View/ 或 UnityEngine
- **每个场景独立连接**：TestRunner 为每个场景创建新 RobotSession，运行后 Disconnect，保证场景间隔离
