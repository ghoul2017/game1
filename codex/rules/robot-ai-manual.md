# Robot AI 使用手册

本文件为 Codex 在 `robot/` 目录下工作时提供指导。

## 项目定位

Robot 是独立 .NET 8 Console 项目，通过 TCP 连接服务端，发送 Protobuf 消息模拟客户端行为，验证服务端逻辑。**自身代码只有基础设施（~300 行），90% 引用 Shared/ + Logic/ 代码。**

## 架构

```
Robot.csproj (net8.0)
  ├── Compile Include: ../client/Assets/Scripts/Shared/**/*.cs    （网络层、ECS、配置、Protos）
  ├── Compile Include: ../client/Assets/Scripts/Logic/**/*.cs     （GameData、IClientUI、IPanels）
  │   └── Remove: Logic/Handlers/**/*.cs                           （排除客户端 UI Handler，Robot 自建消息处理）
  └── 自身代码:
      ├── Program.cs           # 入口，命令行解析，注册场景
      ├── RobotSession.cs      # TCP 连接 + 消息收发（自建 Socket，不用 TcpSession/MessageRouter）
      ├── TestRunner.cs        # 场景加载 → 顺序执行 → 收集结果
      ├── TestScenario.cs      # 测试场景抽象基类
      ├── Assertions.cs        # 断言工具 + TestFailedException
      ├── RobotClientUI.cs     # IClientUI 空实现（控制台输出）
      └── LoginTest.cs         # 示例场景
```

## Robot 自身消息处理

Robot **不使用** MessageRouter/TcpSession。RobotSession 自建 Socket 连接，直接调用 `PacketCodec.Encode/Decode` + `Serializer.Deserialize<T>`。

消息收发模式：
- **请求/响应**：`await Session.SendAndWaitAsync<TRes>(request)` — 发送请求后等待指定类型响应
- **推送等待**：`await Session.WaitForMsgAsync<T>(timeoutMs)` — 等待服务端主动推送
- **单向发送**：`await Session.SendAsync(msg)` — 仅发送，不等待响应

内部机制：`WaitForMsgAsync<T>` 注册 `msgId → Action<byte[]>` 处理器，接收线程解析到对应 MsgId 后反序列化并 Complete TCS。

## 代码规范

### 命名空间与文件

- **命名空间**：`Robot`，子目录不创建子命名空间
- **测试场景文件**：放 `robot/` 根目录，命名 `XxxTest.cs`
- **场景类名**：`XxxTest`，继承 `TestScenario`

### 测试场景约定

```csharp
public class BagTest : TestScenario
{
    public override string Name => "bag_test";  // 命令行 --scenario 匹配此名称

    public override async Task Run()
    {
        // 1. 握手 + 登录（每个场景自行处理）
        // 2. 测试步骤
        // 3. 断言验证
    }
}
```

- `Name` 用于 `--scenario` 参数匹配
- `Run()` 内自行处理完整流程（含握手登录），不依赖其他场景
- 失败通过 `Assertions.xxx()` 抛出 `TestFailedException`，TestRunner 自动 catch

### 断言

```csharp
Assertions.True(condition, "描述");
Assertions.Equal(expected, actual, "描述");
Assertions.NotNull(obj, "描述");
Assertions.Null(obj, "描述");
Assertions.Fail("描述");  // 无条件失败
```

所有断言失败抛 `TestFailedException`，TestRunner 捕获并输出 `FAIL: 场景名 — 原因`。

### 消息构造

```csharp
var req = new LoginReq { Username = "test", Password = "123456" };
var res = await Session.SendAndWaitAsync<LoginRes>(req);
Assertions.True(string.IsNullOrEmpty(res.ErrorMessage), $"登录失败: {res.ErrorMessage}");
```

所有消息类型定义在 `Shared.Common.Protos`，MsgId 在 `MsgIdConst`。

## 与 Logic 层协作

- **引用**：Logic/Data/（GameData、MapObjectType）、Logic/IClientUI.cs、Logic/IPanels.cs
- **不引用**：Logic/Handlers/（那些是客户端 UI Handler，Robot 用 SendAndWaitAsync 替代）
- **不引用**：View/ 任何文件（含 UnityEngine 依赖）
- `ClientContext.UI` 在 Program.cs 启动时设为 `RobotClientUI` 实例（GetPanel 返回 null，UI 操作转控制台输出）

## 新增测试场景步骤

1. 在 `robot/` 下创建 `XxxTest.cs`，继承 `TestScenario`
2. 实现 `Name` 属性（小写蛇形，如 `"enter_game_test"`）
3. 实现 `Run()` 方法
4. 在 `Program.cs` 的 `runner.AddScenario(new XxxTest())` 注册

## 新增消息

1. 在 `Shared/Common/Protos/XxxProtos.cs` 定义 `[ProtoContract]` 类，实现 `IMessage`
2. 在 `Shared/Common/MsgIdConst.cs` 分配 ID
3. Robot 自动通过 Compile Include 获取新消息类型
4. 测试场景中直接 `SendAndWaitAsync<T>()` 使用

## 构建与运行

```bash
# 构建
dotnet build robot/Robot.csproj

# 运行
dotnet run --project robot/                    # 运行默认场景
dotnet run --project robot/ -- --scenario login_test
dotnet run --project robot/ -- --host 192.168.1.1 --port 9000 --scenario bag_test

# 编译检查（含 Robot）
dotnet build game1.sln
```

## 已知局限

- **单 handler 覆盖**：同一 MsgId 只能有一个等待者，并发请求同类型响应时后者覆盖前者。测试步骤应串行执行
- **无 MessageRouter 集成**：不测试客户端 Handler 注册和路由逻辑，仅验证服务端消息收发
- **无配置表**：Robot 不加载 CSV 配置表，依赖配置数据的测试需要额外处理（ConfigManager.Init 需 csv 路径）
- **无自动重连**：断线后直接 FAIL，不尝试重连

## 禁止事项

- **禁止引用 View/** 或 UnityEngine 相关代码
- **禁止在 TestScenario 中 `new RobotSession()`**：Session 由 TestRunner 注入
- **禁止在场景间共享状态**：每个场景独立连接，TestRunner 在场景结束后 Disconnect
- **禁止修改 Shared/ 代码以适应 Robot**：Shared/ 是双端共享，Robot 只能适配自己
