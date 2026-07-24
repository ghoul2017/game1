# game1

目标读者：人类。

game1 是一个 2.5D RPG 项目，包含 Unity 客户端、.NET 8 权威服务端和 Robot 协议自动化测试。

## 环境要求

- Unity `2022.3.62f3c1`
- .NET 8 SDK
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

先启动本地 MongoDB，再从仓库根目录构建服务端：

```powershell
dotnet build server/Server.csproj
Push-Location bin
dotnet Server.dll
Pop-Location
```

服务端默认监听 `0.0.0.0:9000`。客户端使用规定版本的 Unity 打开 `client/`，然后进入 PlayMode。

## 最小验证

```powershell
dotnet build server/Server.csproj
dotnet build robot/Robot.csproj
dotnet run --project robot/Robot.csproj -- --scenario login_test
```

Robot 会连接本地服务端，登录时可能自动注册账号并写入 MongoDB。完整步骤和测试边界见：

- [当前功能测试文档](docs/test-manual.md)
- [Robot 使用手册](docs/Robot使用手册.md)
- [缺陷列表](docs/缺陷列表.md)
- [计划任务](docs/计划任务.md)
- [测试列表](docs/测试列表.md)
- [工作记录](docs/工作记录/README.md)
