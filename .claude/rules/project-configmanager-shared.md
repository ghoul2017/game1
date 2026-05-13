# ConfigManager 双端共享踩坑

## namespace 从 Game.Config 迁移到 Shared.Core.Config

ConfigManager 原在 `Game.Config` 命名空间（服务端专属），改为 `Shared.Core.Config` 后双端通用。服务端 7 个文件的 `using Game.Config;` 需同步改为 `using Shared.Core.Config;`。

## SimpleCsvReader 替换 CsvLoader

CsvLoader 依赖 CsvHelper NuGet 包，Unity 客户端不可用。统一用 SimpleCsvReader（纯 .NET 实现，兼容 .NET 4.7.1+）。

SimpleCsvReader 需提供非泛型 `Load(Type, string)` 重载供 ConfigManager 反射调用。泛型方法 `Load<T>(string)` 调用非泛型版本实现，避免重复逻辑。

## 客户端 .NET 4.7.1 缺少 LINQ

`Dictionary.FirstOrDefault()` 等 LINQ 扩展方法需要 `using System.Linq;`。服务端 .NET 8 全局引入但 Unity 客户端需要显式 using。

## 热更新改为可选参数

`LoadAll(string, bool enableHotReload = false)`，服务端通过 `Init()` 调用（内部 `LoadAll(dir, true)`），客户端直接 `LoadAll(dir)` 不启用。

## 客户端 CSV 文件路径

开发环境使用相对路径 `Application.dataPath + "/../../server/Game/Data/Tables"`。Unity Editor 中 `Application.dataPath` 指向 `client/Assets/`。生产环境需改用 StreamingAssets。

## 删除文件后 Unity csproj 需刷新

删除 `.cs` 文件后 `Assembly-CSharp.csproj` 仍引用已删除文件，`dotnet build` 报 CS2001。需要 Unity Editor 刷新（Assets → Refresh）重新生成 csproj，或临时手动移除 csproj 中的引用。
