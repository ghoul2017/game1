# ECS 重构踩坑：Map/Monster/Fight

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

## MapPlayerEntity 是运行时投影，不是 PlayerEntity

PlayerEntity 管理持久化数据（背包、邮件、属性），不进入地图。MapPlayerEntity 是玩家在地图上的运行时投影（Id=RoleId），通过 PlayerRefComponent 桥接回 PlayerEntity。不要在 MapPlayerEntity 上放持久化数据。
