# Map 模块

## 定位

Map 模块管理游戏地图——地图实例、玩家进/出/切换地图、移动同步、AOI 视野广播。它是所有空间概念的载体：玩家和怪物通过地图获得坐标，通过 AOI 获得视野可见性。

地图是**纯内存对象**，不持久化。服务重启后从配置重建。

## 核心设计

### 地图对象继承体系

```
MapObjectEntity（抽象基类，实现 IMapObject）
  ├── MapPlayerEntity    玩家地图投影
  └── MonsterEntity      怪物实体
```

`MapObjectEntity` 直接持有坐标（MapId/Line/X/Y）和名称（Name），不再通过 Component 中转。ECS 重构后已删除 `MapPositionComponent`、`MapPlayerInfoComponent`。

### MapPlayerEntity 是玩家在地图上的投影

`MapPlayerEntity` 不存持久化数据，Id=RoleId，通过 `PlayerEntity` 字段直接引用持久化实体。ECS 重构后已删除 `PlayerRefComponent`，改为直接字段引用。

### AOI 一站式管理

TowerAoi（九宫格灯塔）提供一站式 API：`AddAoiEntity` / `RemoveAoiEntity` / `UpdateAoiEntity`。业务代码不需要分别调 AddObject + AddWatcher，AOI 内部根据 viewRange 决定是否同时注册观察者。

- 玩家：viewRange=4（既被看到，也看到别人）
- 怪物：viewRange=0（只被看到，不观察）

### MapAoiNotifier 按类型分发

AOI 事件回调到 `MapAoiNotifier`，按 `ObjectType` 自动分发通知：
- Player → `MapPlayerEnterNtf` / `MapPlayerLeaveNtf` / `MapPlayerMoveNtf`
- Monster → `MapObjectEnterNtf` / `MapObjectLeaveNtf` / `MapObjectMoveNtf`

业务代码不要手动广播，否则导致双通知。

### 保留的文件

| 文件 | 职责 |
|------|------|
| `MapComponent.cs` | Module 级 Component，地图集合 + 全局查找 |
| `MapSystem.cs` | 地图操作扩展方法 |
| `GameMap.cs` | 地图实例（含 AOI + 怪物管理） |
| `MapPlayerEntity.cs` | 玩家地图投影实体 |
| `MapAoiNotifier.cs` | AOI 事件 → 网络通知（按 ObjectType 分发） |
| `MapHandlers.cs` | PlayerOfflineEvent 订阅 + 视野推送辅助 |
| `EnterMapReqHandler.cs` | 进入地图消息处理 |
| `LeaveMapReqHandler.cs` | 离开地图消息处理 |
| `ChangeMapReqHandler.cs` | 切换地图消息处理 |
| `MapMoveReqHandler.cs` | 地图移动消息处理 |
| `IMapObject.cs` | 地图对象接口 + 对象类型常量 |

已删除：`MapPositionComponent.cs`（坐标直接放在 MapObjectEntity）、`MapPlayerInfoComponent.cs`（Name 直接放在 MapObjectEntity）、`PlayerRefComponent.cs`（直接字段引用 PlayerEntity）。

## 地图操作

### 进入地图

```
EnterMap(roleId, cfgId, line, playerEntity)
  │
  ├── 1. MapPlayerEntity.Create（创建地图投影，含 FightUnitComponent）
  ├── 2. map.AddObject → AOI 注册 + 广播进入视野
  ├── 3. PlayerMapEntities 索引注册
  ├── 4. SendVisibleObjects → 推送视野内所有对象快照
  └── 5. PublishAsync(PlayerEnterMapEvent)
```

### 移动

```
MapMove(roleId, newX, newY)
  │
  ├── 1. 边界校验
  ├── 2. map.MoveObject → 更新坐标 + AOI UpdateAoiEntity
  └── 3. AOI 自动触发 OnObjectMove + OnWatcherUpdate（视野差集推送）
```

### 离开地图

```
QuitMap(roleId)
  │
  ├── 1. map.RemoveObject → AOI 移除 + 广播离开视野
  ├── 2. PlayerMapEntities 索引移除
  └── 3. PublishAsync(PlayerLeaveMapEvent) → Fight 注销战斗单元
```

## AOI 视野管理

### 视野推送流程

```
进入地图：
  1. EnterMapAck → 告知客户端地图坐标
  2. SendVisibleObjects() → MapObjectsNtf（视野内所有对象快照）
  3. AOI 自动通知后续的进入/离开/移动

移动：
  1. MapMoveAck → 确认坐标
  2. AOI UpdateAoiEntity → 自动触发 OnObjectMove + OnWatcherUpdate
```

### 默认地图

| 名称 | CfgId | 尺寸 | 出生点 |
|------|-------|------|--------|
| 新手村 | 1 | 200×200 | (100,100) |
| 主城 | 2 | 300×300 | (150,150) |
| 野外 | 3 | 500×500 | (250,250) |

## 跨模块协作

```
                ┌─────────────┐
                │   Config    │ MapConfig 配置表
                └──────┬──────┘
                       │
┌──────────┐    ┌──────▼──────┐    ┌──────────┐
│  Fight   │◄──►│     Map     │◄──►│  Player  │
│ 战斗注册  │    │ 进出/移动/AOI│    │ 消息推送  │
└──────────┘    └──────┬──────┘    └──────────┘
                       │
                ┌──────▼──────┐
                │  Monster    │ GameMap 扩展方法管理怪物
                └─────────────┘
```

| 交互方 | 方向 | 内容 |
|--------|------|------|
| Fight | 双向 | MapPlayerEntity 挂载 FightUnitComponent；OnUnitKilled → 移除对象 |
| Monster | 双向 | MonsterEntity 继承 MapObjectEntity；刷怪/AI/掉落由 GameMap 扩展方法管理 |
| Player | 单向 | `SendToPlayer` 推送地图通知 |
| Config | 单向 | 读取 `MapConfig` 驱动地图参数 |

## 已知局限

1. **多线暂不实现**：InitDefaultMaps 仅创建 Line=1，短期只维护单线地图
2. **无碰撞检测**：移动只做边界校验，无寻路和碰撞
3. **动态地图**：地图在启动时固定创建，无运行时动态创建
