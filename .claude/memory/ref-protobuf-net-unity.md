---
name: protobuf-net-unity-deps
description: protobuf-net v3 在 Unity 2021.3 中需要补齐的依赖 DLL 列表
type: reference
---

# protobuf-net v3.2.45 Unity 依赖

放在 `client/Assets/Plugins/protobuf-net/`，全部用 netstandard2.0 版本：

| DLL | NuGet 包路径 |
|---|---|
| protobuf-net.Core.dll | protobuf-net.core/3.2.45/lib/netstandard2.0/ |
| protobuf-net.dll | protobuf-net/3.2.45/lib/netstandard2.0/ |
| System.Reflection.Emit.dll | system.reflection.emit/4.7.0/lib/netstandard2.0/ |
| System.Reflection.Emit.ILGeneration.dll | system.reflection.emit.ilgeneration/4.7.0/lib/netstandard2.0/ |
| System.Collections.Immutable.dll | system.collections.immutable/7.0.0/lib/netstandard2.0/ |
| System.Runtime.CompilerServices.Unsafe.dll | system.runtime.compilerservices.unsafe/6.0.0/lib/netstandard2.0/ |

**Why:** Unity 2021.3 (.NET Framework 4.7.1) 不自带这些 netstandard2.0 依赖，缺少会导致 "broken assembly" 或 "Unable to resolve reference" 错误。

**How to apply:** 添加 protobuf-net 相关 DLL 时，从 NuGet 缓存 (`~/.nuget/packages/`) 中复制 netstandard2.0 版本到 Plugins 目录。
