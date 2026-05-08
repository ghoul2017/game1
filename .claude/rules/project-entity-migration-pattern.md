---
paths: ["server/**/*.cs"]
---

# Entity 迁移模式：普通类 → Entity + Component

将普通数据类（如 Player）迁移为 Entity + Component 的标准模式：

## 步骤

1. **创建 Entity 子类** — Id 设为业务主键（如 RoleId），提供静态工厂方法 `Create()`
2. **拆分字段为 Component** — 每组语义独立的字段独立为 `[ComponentOf]` Component
3. **Entity 提供便利属性** — `GetComponent<T>()` 的快捷访问
4. **更新 Manager** — 内部字典值类型改为 Entity，字段访问改为 Component 属性
5. **更新所有调用方** — 批量替换字段访问模式

## 字段映射规则

| 旧模式 | 新模式 |
|--------|--------|
| `player.RoleId` | `player.Id`（Entity.Id 就是 RoleId） |
| `player.UserId` | `player.Online.UserId` |
| `player.RoleName` | `player.RoleData.Role?.Name ?? ""` |
| `player.RoleData` | `player.RoleData.Role`（Component 持有引用） |
| `new Player { ... }` | `PlayerEntity.Create(roleId, userId, role)` |

## 注意

- Component 字段初始化在 Entity.Create 工厂方法中完成，不要在 Component 构造函数中放业务逻辑
- 先迁移 Player（改动面可控），Map/Monster 等内部逻辑复杂的对象可后续逐步迁移
