# Task 命名空间冲突：避免 Game.Components.Task

`Game.Components.Task` 与 `System.Threading.Tasks.Task` 冲突，导致编译歧义。即使 `using System.Threading.Tasks;` 存在，编译器仍无法区分同名类型。

**解决方案**：任务相关组件使用 `Game.Components.GameTask` 命名空间，目录也对应为 `Components/GameTask/`。

```csharp
// 错误：命名空间冲突
namespace Game.Components.Task;  // 与 System.Threading.Tasks.Task 冲突

// 正确
namespace Game.Components.GameTask;
```

**判断方法**：新建命名空间前，检查是否与 `System.*` 中的常用类型同名（Task, Action, Attribute, Event 等）。
