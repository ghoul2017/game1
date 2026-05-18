# Robot 自动化测试项目

## 概述

创建独立的 .NET 8 Console 项目 `robot/`，自身只有基础设施（TCP 连接、消息收发）+ 控制代码（测试脚本、断言）。完全引用 shared/ + Logic 层代码，可脱离 Unity 秒级运行。

Robot 架构：**10% 自身代码 + 90% 引用代码**

## Robot 自身职责

| 模块 | 说明 |
|------|------|
| 入口 `Program.cs` | 解析命令行，加载测试场景，运行 |
| `RobotSession` | 封装 TcpSession，管理连接生命周期 |
| `TestRunner` | 加载测试场景，顺序执行，收集结果 |
| `TestScenario` | 测试场景基类，定义步骤模板 |
| `Assertions` | 断言工具（消息内容、状态变化、时序） |
| `RobotClientUI` | 实现 IClientUI，控制台输出替代 Unity UI |

## Robot 引用部分（不新增代码）

| 引用 | 来源 | 说明 |
|------|------|------|
| 网络层 | `shared/Core/Network/` | TcpSession, PacketCodec, MessageRouter |
| 协议 | `shared/Common/Protos/` | 全部消息定义 |
| ECS | `shared/Core/ECS/` | Entity, Component |
| 配置 | `shared/Core/Config/` | ConfigManager, SimpleCsvReader |
| 日志 | `shared/Core/Log/` | ILog, LogManager |
| 客户端逻辑 | `client/Logic/` | Handler 类、GameData、MapData（待分离） |

## 新增文件

### robot/Robot.csproj
- 类型：.NET 8 Console
- Compile Include：`../client/Assets/Scripts/Shared/**/*.cs` + `../client/Assets/Scripts/Logic/**/*.cs`
- 包：protobuf-net

### robot/Program.cs
```csharp
// 入口：解析命令行 → 创建 RobotSession → 创建 TestRunner → 加载场景 → 运行 → 输出结果
// 命令行：robot --host 127.0.0.1 --port 9000 --scenario login_test
```

### robot/RobotSession.cs
- 封装 TcpSession，管理连接生命周期
- `ConnectAsync(host, port)` → 建立 TCP 连接
- `SendAsync<T>(msg)` → 发送消息
- `WaitForMsg<T>(timeout)` → 等待特定消息（用于断言）
- `DisconnectAsync()` → 断开

### robot/TestRunner.cs
- `List<TestScenario> Scenarios` — 已注册的测试场景
- `TestResult Run(TestScenario scenario)` — 运行单个场景，返回结果
- `List<TestResult> RunAll()` — 运行全部

### robot/TestScenario.cs
```csharp
public abstract class TestScenario
{
    public abstract string Name { get; }
    protected RobotSession Session { get; set; }
    public abstract Task Run(); // 子类实现测试步骤
}
```

### robot/Assertions.cs
- `AssertEquals<T>(T expected, T actual, string msg)`
- `AssertMsgReceived<T>(T expected, TimeSpan timeout)`
- `AssertState<T>(Func<T> getter, T expected, string msg)`

### robot/RobotClientUI.cs
- 实现 `IClientUI` 接口（Logic 层定义）
- 所有 UI 操作转为 Console.WriteLine 输出
- 机器人场景中不需要真实 UI

### robot/Scenarios/（测试场景目录）
- `LoginTest.cs` — 登录流程测试
- `BagTest.cs` — 背包操作测试
- `MailTest.cs` — 邮件收发测试
- 等

## 依赖关系

```
Robot.csproj (net8.0)
  ├── Compile Include: ../client/Assets/Scripts/Shared/**/*.cs
  │   └── Core/* + Common/*
  └── Compile Include: ../client/Assets/Scripts/Logic/**/*.cs
      └── Handlers/* + Data/* + IClientUI.cs + IPanels.cs
```

## 执行顺序

1. **前提**：Logic/View 分离完成（规划 9）
2. 创建 `robot/` 目录和 `Robot.csproj`
3. 实现 `RobotSession.cs`
4. 实现 `Assertions.cs`
5. 实现 `TestScenario.cs` 基类
6. 实现 `TestRunner.cs`
7. 实现 `RobotClientUI.cs`
8. 实现 `Program.cs` 入口
9. 编写示例测试场景 `Scenarios/LoginTest.cs`
10. 编译验证（`dotnet build robot/`）

## 验证

- [ ] `dotnet build robot/` 编译通过
- [ ] Robot 能连接服务端并完成登录握手
- [ ] Robot 能发送 EnterGameReq 并收到 EnterGameRes
- [ ] 断言失败时输出明确错误信息
- [ ] 测试场景脚本化运行，无需人工干预
