# 反常规/反直觉的代码模式

阅读各模块源码时发现的违反直觉的设计，新开发者容易踩坑。

## ItemEffectSystem（Bag 模块）反向依赖 Fight 模块

`ItemEffectSystem.ApplyReviveEffect()` 调用 `FightSystem.GetFightUnit()` 复活玩家。这是 Bag → Fight 的反向依赖（正常方向是 Fight → Bag 做掉落发放）。

**原因**：物品效果需要修改战斗状态，但物品系统在 Bag 模块。后续如果有更多跨模块物品效果，应考虑抽独立的效果处理层。

## FightComponent 已删除，战斗单元通过 MapObjectEntity 访问

FightComponent 索引层已删除，不再有 `Context.Fight`。所有战斗单元查找走 `FightSystem.GetFightUnit(objectId)`，内部通过 `Context.Map.FindMapObject()` → `FightUnitComponent`。

**影响**：不要写 `Context.Fight.xxx`，用 `FightSystem.GetFightUnit(roleId)?.xxx` 替代。

## 怪物 ID 是简单递增，不是雪花算法

`MonsterComponent.NextMonsterId` 从 100000 开始递增，不像其他实体用 `IdGenerator.Instance.NextId()`（雪花算法）。

**原因**：怪物是纯运行时对象，不需要持久化 ID，服务器重启后重新从 100000 开始。不要对怪物 ID 做跨重启持久化假设。

## Handler 上线加载直接拷贝字段，不走 System 的 LoadAsync

`RoleBagHandlers.OnPlayerOnline` 直接调用 `MongoDb.Instance.LoadAsync<RoleBagComponent>()` 然后逐字段拷贝到已挂载的 Component，而不是调用 `RoleBagSystem` 的方法。

**原因**：`LoadAsync<T>` 返回新实例，`_parent` 为 null，不能直接替代已挂载的 Component。必须拷贝字段 + RebuildIndex。详见 `project-component-ientity-persistence.md` 规则。
