你是一个代码生成器。根据参数创建一个新的游戏系统模块的完整目录骨架。

## 用法

`/newsystem <系统名>`，例如 `/newsystem Bag`、`/newsystem Chat`、`/newsystem Task`

## 参数

系统名通过 $ARGUMENTS 传入。要求 PascalCase 格式（首字母大写）。

## 工作流程

### 1. 确认参数
- 如果 $ARGUMENTS 为空，提示用户用法并停止
- 提取系统名，例如 `Bag`

### 2. 确认 MsgId 分段
- 读取 `server/Network/Messages/MsgId.cs`，查看已使用的 ID 分段
- 为新系统分配一个未使用的分段（参考 CLAUDE.md 的分段约定：101=user, 103=bag, 105=fight 等）
- 例如 Bag 系统 → 103 段（10301, 10302, ...）

### 3. 生成目录和文件

在 `server/` 下创建以下结构：

```
server/System/{SystemName}/
├── {SystemName}Manager.cs       # 业务逻辑单例
├── {SystemName}Model.cs         # 注册 + 生命周期钩子（IModule 实现）
├── {SystemName}Data.cs          # 数据模型（用于持久化）
├── Handlers/
│   └── (空目录，.gitkeep)
└── Messages/
    └── (空目录，.gitkeep)
```

### 4. 生成代码模板

**{SystemName}Manager.cs** 模板：
```csharp
using GameServer.Core.Log;

namespace GameServer.System.{SystemName};

public class {SystemName}Manager
{
    private static readonly ILog Log = LogManager.GetLogger<{SystemName}Manager>();
    public static {SystemName}Manager Instance { get; } = new();

    // TODO: 实现业务逻辑
}
```

**{SystemName}Model.cs** 模板：
```csharp
namespace GameServer.System.{SystemName};

public class {SystemName}Model // : IModule (实现模块系统后启用)
{
    public static {SystemName}Model Instance { get; } = new();

    public void Init()
    {
        // 注册消息处理器、初始化数据
    }

    public void Start()
    {
        // 启动定时任务等
    }

    public void Stop()
    {
        // 保存数据、清理资源
    }
}
```

**{SystemName}Data.cs** 模板：
```csharp
namespace GameServer.System.{SystemName};

public class {SystemName}Data
{
    // TODO: 定义持久化数据字段
}
```

### 5. 注册到 CLAUDE.md
- 更新 CLAUDE.md 的目录结构章节，添加新系统
- 在 CLAUDE.md 的进化日志中追加记录

### 6. 验证
- `dotnet build` 确保编译通过

## 约束

- 全自动执行，不提问
- 回复使用中文
- 只创建骨架，不实现具体业务逻辑
- 遵循 CLAUDE.md 的目录结构模式和命名约定

$ARGUMENTS
