# game1

目标读者：人类。

game1 是一个 2.5D RPG 项目，包含 Unity 客户端、.NET 8 权威服务端和 Robot 协议自动化测试。

## 环境要求

- Unity `2022.3.62f3c1`
- .NET SDK `8.0.423`（由 `global.json` 固定）
- MongoDB，默认地址 `mongodb://127.0.0.1:27017`，数据库 `game`

## 目录

- `client/`：Unity 客户端
- `client-logic/`：不依赖 UnityEngine 的客户端逻辑
- `shared/`：客户端、服务端和 Robot 共用代码
- `server/`：游戏服务端
- `robot/`：协议级自动化测试
- `tools/`：配置和工程工具
- `docs/`：缺陷、计划任务、工作记录、测试和 Robot 文档

## 快速启动

当前 UID 背包与旧 Index 背包 BSON 不兼容。时间戳新账号可以直接创建当前结构；复用旧角色前必须按 [Bag 模块文档](docs/模块文档/Bag.md) 核对环境与数据授权并完成精确重建。

先启动本地 MongoDB，再从仓库根目录构建服务端：

```powershell
dotnet build server/Server.csproj
Push-Location bin
dotnet Server.dll
Pop-Location
```

服务端默认监听 `0.0.0.0:9000`。客户端使用规定版本的 Unity 打开 `client/`，然后进入 PlayMode。

## 本地质量门禁

PowerShell 使用统一入口；脚本会自动定位 Git for Windows Bash，不会误用 WSL：

```powershell
.\scripts\check-ci.ps1
```

Git Bash 可以直接执行同一门禁：

```bash
bash scripts/check-ci
```

该入口只执行依赖边界、受管 .NET 项目构建、纯逻辑测试和协议一致性检查，不启动 Server、Robot、
MongoDB 或 Unity。测试结果写入已忽略的 `tests/GameplayCore.Tests/bin/TestResults/`。

本机已经由 Unity 生成 `Assembly-CSharp*.csproj` 时，可以追加完整 solution 构建：

```powershell
.\scripts\check-ci.ps1 -Full
```

也可以在 Git Bash 中执行：

```bash
bash scripts/check-all
```

Unity Editor 编译、PlayMode、Robot/MongoDB 和小游戏真机验证是独立门禁，不能由上述本地 CI 代替。

## 分项目验证

```powershell
dotnet build shared/Shared.csproj
dotnet build server/Server.csproj
dotnet build client-logic/ClientLogic.csproj
dotnet build robot/Robot.csproj
dotnet build tools/tools.csproj
bash scripts/check-client-boundary
bash scripts/check-protocol
```

背包、任务和持久化行为通过 Robot 连接真实服务端验证。Robot 登录时可能自动注册账号并写入 MongoDB，必须在获得对应环境和数据授权后运行。完整步骤和测试边界见：

- [当前功能测试文档](docs/test-manual.md)
- [Robot 使用手册](docs/Robot使用手册.md)
- [缺陷列表](docs/缺陷列表.md)
- [计划任务](docs/计划任务.md)
- [测试列表](docs/测试列表.md)
- [工作记录](docs/工作记录/README.md)
