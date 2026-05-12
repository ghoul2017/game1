# 反常规/反直觉的代码模式

阅读各模块源码时发现的违反直觉的设计，新开发者容易踩坑。

## ~~MonsterProtos.cs 包含通用通知~~ [已解决]

通用通知（ExpChangedNtf、GoldChangedNtf、LevelUpNtf、ItemGainNtf、MpChangedNtf）已移到 FightProtos.cs，MonsterProtos.cs 已删除。

## ~~MapNpcEnterNtf 同时用于 NPC 和怪物~~ [已解决]

已重命名为 `MapObjectEnterNtf`/`MapObjectLeaveNtf`/`MapObjectMoveNtf`，新增 `ObjectType` 和 `CfgId` 字段，按 ObjectType 区分玩家/怪物/NPC。

## ErrorCode 是服务端专属，客户端只有 int

`Bag.ErrorCode`、`ChatErrorCode`、`MailErrorCode`、`TaskErrorCode`、`FightErrorCode` 等常量类定义在服务端 System 文件中，客户端通过 Protobuf 收到的 Code 是 `int`，没有对应的枚举/常量。

**影响**：客户端需要硬编码或自行维护一套错误码映射，双端容易不同步。新增错误码时注意同步客户端。

## ItemEffectSystem（Bag 模块）反向依赖 Fight 模块

`ItemEffectSystem.ApplyReviveEffect()` 调用 `FightSystem.GetFightUnit()` 复活玩家。这是 Bag → Fight 的反向依赖（正常方向是 Fight → Bag 做掉落发放）。

**原因**：物品效果需要修改战斗状态，但物品系统在 Bag 模块。后续如果有更多跨模块物品效果，应考虑抽独立的效果处理层。

## FightComponent 已删除，战斗单元通过 MapObjectEntity 访问

FightComponent 索引层已删除，不再有 `Context.Fight`。所有战斗单元查找走 `FightSystem.GetFightUnit(objectId)`，内部通过 `Context.Map.FindMapObject()` → `FightUnitComponent`。

**影响**：不要写 `Context.Fight.xxx`，用 `FightSystem.GetFightUnit(roleId)?.xxx` 替代。

## 怪物 ID 是简单递增，不是雪花算法

`MonsterComponent.NextMonsterId` 从 100000 开始递增，不像其他实体用 `IdGenerator.Instance.NextId()`（雪花算法）。

**原因**：怪物是纯运行时对象，不需要持久化 ID，服务器重启后重新从 100000 开始。不要对怪物 ID 做跨重启持久化假设。

## ~~单线程模型下的 Interlocked.Increment~~ [已解决]

MonsterSystem 的 `Interlocked.Increment(ref self.NextMonsterId)` 已改为 `self.NextMonsterId++`。RoleBagComponent 的 `_nextItemUid` 仍用 `Interlocked.Increment`，但项目是单线程模型，不影响正确性。

## Handler 上线加载直接拷贝字段，不走 System 的 LoadAsync

`RoleBagHandlers.OnPlayerOnline` 直接调用 `MongoDb.Instance.LoadAsync<RoleBagComponent>()` 然后逐字段拷贝到已挂载的 Component，而不是调用 `RoleBagSystem` 的方法。

**原因**：`LoadAsync<T>` 返回新实例，`_parent` 为 null，不能直接替代已挂载的 Component。必须拷贝字段 + RebuildIndex。详见 `project-component-ientity-persistence.md` 规则。
