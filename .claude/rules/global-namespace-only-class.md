# 删除命名空间唯一的类后要批量清理 using

如果被删除的类是其命名空间下唯一的类，所有 `using ThatNamespace;` 都会编译报错。

## 影响面

本次案例：删除 `SessionManager.cs`（`Game.Network` 命名空间唯一的类），14 个文件的 `using Game.Network;` 报错。

## 检查方法

删除文件前：
1. 搜索该文件的命名空间（`namespace X.Y.Z;`）
2. 搜索项目中 `using X.Y.Z;` 的引用数量
3. 如果引用多，准备批量清理

## 注意

移除 `using` 后要确认不影响同命名空间下其他类型的引用。例如 `using Game.Core.Network;` 中 `RpcMethodHandler` 等类型仍在使用，不能删除。