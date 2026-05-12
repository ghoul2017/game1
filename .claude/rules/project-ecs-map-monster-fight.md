# ECS 重构踩坑：Map/Monster/Fight

## FightSystem.OnUnitKilled 直接调用 MonsterSystem

`OnUnitKilled` 中直接调用 `Context.Monster.OnMonsterKilled()`，同时发布 `MonsterKilledEvent`（GameTask 的 RoleTaskHandlers 订阅）。不要再用事件中转调用 MonsterSystem——FightSystem 直接调用，事件只给其他需要通知的模块。

## MapAoiNotifier 按 ObjectType 分发通知

MapAoiNotifier 已按 `ObjectType` 分发通知：Player → MapPlayerEnterNtf/LeaveNtf/MoveNtf，非 Player → MapObjectEnterNtf/LeaveNtf/MoveNtf。不要在 MonsterSystem 等地方手动广播——AOI 一站式处理，手动广播会导致双通知。

## MapObjectEntity 子类需要 using Game.Components.Map

MonsterEntity 继承 MapObjectEntity，需要 `using Game.Components.Map;` 才能访问 MapObjectType 和 MapPositionComponent。新建 Entity 子类时注意检查基类所在的命名空间是否已引入。

## RoleAttributeComponent 属性名是 PhyAttMin/PhyAttMax/PhyDef，不是 Attack/Defense

创建 FightAttribute 时从 RoleAttributeComponent 转换，属性名为 `PhyAttMin`/`PhyAttMax`/`PhyDef`，不是直觉上的 `Attack`/`Defense`。写转换代码前先读 RoleAttributeComponent 确认字段名。

## FightUnitComponent 挂载在 MapObjectEntity，不依赖 FightComponent

FightComponent 索引层已删除。注册战斗单元用 `entity.RegisterUnit(attr)`（MapObjectEntity 扩展方法），查找用 `FightSystem.GetFightUnit(objectId)`。不要写 `Context.Fight`。

## MonsterEntity 代替了 MapNpc + MonsterUnit

怪物在地图上不再以 Npc(2) 类型存在，而是 Monster(3)。MonsterEntity 包含 MonsterInfoComponent（配置+名字）、MonsterAiComponent（AI状态）、FightUnitComponent（战斗数据），不再需要 MapNpc 或 MonsterUnit 类。

## AOI 一站式 API

业务代码不再分别调用 AddObject+AddWatcher，统一用 `Aoi.AddAoiEntity(obj, viewRange)`。viewRange>0 时同时作为观察者。底层 API 仍可用但业务代码优先用一站式。

## CfgId 上提到 IMapObject

`CfgId` 已从 MonsterEntity 的普通属性上提到 `IMapObject` 接口和 `MapObjectEntity` 抽象类。MonsterEntity 返回 `Info.CfgId`，MapPlayerEntity 返回 0。MapAoiNotifier 和 MapHandlers 通过 `mapObj.CfgId` 统一访问，不再需要 `is MonsterEntity` 类型判断。

## 掉落/经验/金币逻辑归 PlayerSystem

MonsterSystem 的 `GrantExpToPlayer`/`GrantGoldToPlayer`/`GrantItemToPlayer` 已移到 PlayerSystem 作为 `GrantExpAsync`/`GrantGoldAsync`/`GrantItemAsync`。MonsterSystem 不再反向依赖 Player 模块的内部实现，通过 `Context.Players.GrantXxxAsync()` 调用。

## MonsterComponent 共享 Random 实例

MonsterComponent 持有 `Random` 字段，MonsterSystem 各处用 `self.Random.Next()` 而非 `new Random()`。避免频繁创建 Random 实例导致种子相近产生相同序列。

## MapPlayerEntity 是运行时投影，不是 PlayerEntity

PlayerEntity 管理持久化数据（背包、邮件、属性），不进入地图。MapPlayerEntity 是玩家在地图上的运行时投影（Id=RoleId），通过 PlayerRefComponent 桥接回 PlayerEntity。不要在 MapPlayerEntity 上放持久化数据。
