# Map 模块文档

## 概述

Map 模块实现地图系统，包含地图实例管理、玩家进/出/切换地图、移动同步、AOI 视野管理。使用 TowerAoi（九宫格）实现视野广播，玩家进入视野范围时自动推送对象信息。地图为纯内存对象，不持久化。ECS 重构后，地图对象为 MapObjectEntity 体系（MapPlayerEntity / MonsterEntity）。

## 文件清单

| 文件 | 行数 | 职责 |
|------|------|------|
| `server/Game/Entities/MapObjectEntity.cs` | 30 | 地图对象实体基类（实现 IMapObject） |
| `server/Game/Components/Map/IMapObject.cs` | 14 | 地图对象接口 + 对象类型常量 |
| `server/Game/Components/Map/MapComponent.cs` | 35 | Module 级 Component，地图集合 + 全局查找 |
| `server/Game/Components/Map/MapSystem.cs` | 230 | 地图操作扩展方法 |
| `server/Game/Components/Map/GameMap.cs` | 188 | 地图实例（含 AOI） |
| `server/Game/Components/Map/MapPlayerEntity.cs` | 45 | 玩家地图投影实体 |
| `server/Game/Components/Map/MapPositionComponent.cs` | 15 | 地图位置 Component |
| `server/Game/Components/Map/MapPlayerInfoComponent.cs` | 12 | 玩家地图信息 Component |
| `server/Game/Components/Map/PlayerRefComponent.cs` | 14 | 玩家引用桥接 Component |
| `server/Game/Components/Map/MapAoiNotifier.cs` | 120 | AOI 事件 → 网络通知 |
| `server/Game/Components/Map/MapHandlers.cs` | 200 | 消息处理 + 事件订阅 |
| `client/Assets/Scripts/shared/Core/Aoi/` | — | AOI 系统实现（TowerAoi/Tower/IAoiObject/IAoiEventListener） |
| `client/Assets/Scripts/shared/Common/Protos/MapProtos.cs` | 207 | 消息定义 + 错误码 |
| `client/Assets/Scripts/shared/Common/MsgIdConst.cs` | — | MsgId 10701-10800 段 |

## 实体体系

### MapObjectEntity（抽象基类）

```csharp
public abstract class MapObjectEntity : Entity, IMapObject
{
    public long ObjectId => Id;
    public abstract int ObjectType { get; }
    public abstract string Name { get; }

    // 便利属性，代理到 MapPositionComponent
    public int MapId { get => Pos.MapId; set => Pos.MapId = value; }
    public int Line { get => Pos.Line; set => Pos.Line = value; }
    public int X { get => Pos.X; set => Pos.X = value; }
    public int Y { get => Pos.Y; set => Pos.Y = value; }

    // 组件访问
    private MapPositionComponent? _pos;
    public MapPositionComponent Pos => _pos ??= GetComponent<MapPositionComponent>();
}
```

### MapPlayerEntity

```csharp
public class MapPlayerEntity : MapObjectEntity
{
    public override int ObjectType => MapObjectType.Player;
    public override string Name => Info.Name;
    public long RoleId => Id;

    // 组件访问
    public MapPlayerInfoComponent Info => ...;
    public PlayerRefComponent PlayerRef => ...;
    public FightUnitComponent Fight => ...;

    // 桥接到 PlayerEntity
    public PlayerEntity? PlayerEntity => PlayerRef.PlayerEntity;
}
```

- Id = RoleId，是玩家在地图上的运行时投影
- PlayerRefComponent 桥接回 PlayerEntity（持久化数据）

### IMapObject 接口

```csharp
public static class MapObjectType
{
    public const int Player = 1;
    public const int Npc = 2;      // 保留，非怪物NPC后续用
    public const int Monster = 3;
}

public interface IMapObject : IAoiObject
{
    int ObjectType { get; }
    string Name { get; }
    int MapId { get; set; }
    int Line { get; set; }
}
```

## Component

### MapComponent（Module 级）

```csharp
[ComponentOf(typeof(Module))]
public class MapComponent : Component
{
    public readonly Dictionary<long, GameMap> Maps = new();                        // MapKey → GameMap
    public readonly Dictionary<long, MapPlayerEntity> PlayerMapEntities = new();   // roleId → MapPlayerEntity
}
```

- 挂载目标：`Context.Server`
- Context 便利属性：`Context.Map`
- MapKey = CfgId × 1000 + Line
- `FindMapObject(long objectId)` — 全局查找，先查 PlayerMapEntities 再遍历地图

### MapPositionComponent

```csharp
[ComponentOf(typeof(MapObjectEntity))]
public class MapPositionComponent : Component
{
    public int X;
    public int Y;
    public int MapId;
    public int Line;
}
```

### MapPlayerInfoComponent

```csharp
[ComponentOf(typeof(MapPlayerEntity))]
public class MapPlayerInfoComponent : Component
{
    public string Name = "";
}
```

### PlayerRefComponent

```csharp
[ComponentOf(typeof(MapPlayerEntity))]
public class PlayerRefComponent : Component
{
    public PlayerEntity? PlayerEntity;
}
```

## GameMap

```csharp
public class GameMap
{
    // 配置属性
    public int CfgId { get; }
    public int Line { get; }
    public string Name { get; }
    public int Width { get; }
    public int Height { get; }
    public int BirthX { get; }
    public int BirthY { get; }

    // 系统属性
    public TowerAoi Aoi { get; }
    public long MapKey { get; }      // CfgId * 1000 + Line
    public int PlayerCount { get; }
    public int ObjectCount { get; }

    // 对象管理（内部字典）
    // Dictionary<long, MapObjectEntity> _objects
    // Dictionary<long, MapPlayerEntity> _players
    // Dictionary<long, MonsterEntity> _monsters

    bool AddObject(MapObjectEntity obj);
    bool RemoveObject(long objectId);
    MapObjectEntity? GetObject(long objectId);
    MapPlayerEntity? GetPlayer(long roleId);
    List<MapPlayerEntity> GetAllPlayers();
    List<MonsterEntity> GetAllMonsters();
    List<MapObjectEntity> GetObjectsInRange(int x, int y, int range);
    bool MoveObject(long objectId, int newX, int newY);
}
```

- AOI 视野范围：4格
- AddObject 内部调用 AOI 一站式 API（`AddAoiEntity`）
- MoveObject 内部调用 `UpdateAoiEntity`

## System 详解

### MapSystem

| 方法 | 签名 | 说明 |
|------|------|------|
| CreateMap | `CreateMap(this MapComponent, int cfgId, int line, string name, int width, int height, int birthX, int birthY) → GameMap` | 创建地图实例 |
| GetMap | `GetMap(this MapComponent, int cfgId, int line) → GameMap?` | 获取地图 |
| GetPlayerMap | `GetPlayerMap(this MapComponent, long roleId) → GameMap?` | 获取玩家所在地图 |
| GetPlayerMapEntity | `GetPlayerMapEntity(this MapComponent, long roleId) → MapPlayerEntity?` | 获取玩家地图投影 |
| GetAllMaps | `GetAllMaps(this MapComponent) → List<GameMap>` | 所有地图 |
| EnterMap | `EnterMap(this MapComponent, long roleId, string roleName, int cfgId, int line, int x, int y, PlayerEntity playerEntity) → bool` | 进入地图 |
| QuitMap | `QuitMap(this MapComponent, long roleId) → bool` | 离开地图 |
| ChangeMap | `ChangeMap(this MapComponent, long roleId, string roleName, int newCfgId, int newLine, int newX, int newY, PlayerEntity playerEntity) → bool` | 切换地图 |
| Move | `Move(this MapComponent, long roleId, int newX, int newY) → bool` | 移动 |
| GetPlayerPosition | `GetPlayerPosition(this MapComponent, long roleId) → (int,int,int,int)?` | 获取位置 |
| GetNearbyPlayers | `GetNearbyPlayers(this MapComponent, long roleId) → List<MapPlayerEntity>` | 获取附近玩家 |
| FindMapObject | `FindMapObject(long objectId) → MapObjectEntity?` | 全局查找地图对象 |
| InitDefaultMaps | `InitDefaultMaps(this MapComponent)` | 初始化默认地图 |
| RemoveAll | `RemoveAll(this MapComponent)` | 清除所有 |

### 默认地图

| 名称 | CfgId | 尺寸 | 出生点 |
|------|-------|------|--------|
| 新手村 | 1 | 200×200 | (100,100) |
| 主城 | 2 | 300×300 | (150,150) |
| 野外 | 3 | 500×500 | (250,250) |

## AOI 视野管理

### 一站式 API（TowerAoi）

| 方法 | 签名 | 说明 |
|------|------|------|
| AddAoiEntity | `AddAoiEntity(IAoiObject obj, int viewRange = 0)` | 同时添加对象和观察者 |
| RemoveAoiEntity | `RemoveAoiEntity(IAoiObject obj)` | 同时移除对象和观察者 |
| UpdateAoiEntity | `UpdateAoiEntity(IAoiObject obj, int oldX, int oldY, int newX, int newY)` | 同时更新位置 |

- viewRange > 0 时同时作为观察者（玩家=4，怪物=0）
- 底层 AddObject/RemoveObject/UpdateObject、AddWatcher/RemoveWatcher/UpdateWatcher 仍可用

### MapAoiNotifier（IAoiEventListener 实现）

| 回调 | 通知 | 说明 |
|------|------|------|
| OnObjectEnter | MapPlayerEnterNtf / MapNpcEnterNtf | 对象进入视野（区分Player/Monster） |
| OnObjectLeave | MapPlayerLeaveNtf / MapNpcLeaveNtf | 对象离开视野 |
| OnObjectMove | MapPlayerMoveNtf / MapNpcMoveNtf | 对象移动 |
| OnWatcherUpdate | MapPlayerEnterNtf/LeaveNtf | 观察者跨塔移动时视野差集推送 |

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

## Handler 详解

### 消息处理

| Handler | MsgId | 请求 | 响应 | 说明 |
|---------|-------|------|------|------|
| EnterMapHandler | 10701→10702 | EnterMapReq(cfgId, line) | EnterMapAck(cfgId, line, x, y) | 进入地图 |
| LeaveMapHandler | 10703→10704 | LeaveMapReq | LeaveMapAck | 离开地图 |
| ChangeMapHandler | 10705→10706 | ChangeMapReq(targetCfgId, targetLine, x, y) | ChangeMapAck | 切换地图 |
| MapMoveHandler | 10707→10708 | MapMoveReq(x, y) | MapMoveAck(x, y) | 移动 |

### 错误码

| Code | 常量 | 含义 |
|------|------|------|
| 0 | Ok | 成功 |
| 1 | NotLoggedIn | 未登录 |
| 2 | MapNotFound | 地图不存在 |
| 3 | AlreadyInMap | 已在该地图 |
| 4 | NotInMap | 不在任何地图 |
| 5 | InvalidPosition | 坐标越界 |

### 事件订阅

| 事件 | 方法 | 说明 |
|------|------|------|
| PlayerOfflineEvent | OnPlayerOffline | 自动退出地图 |

### 发布事件

| 事件 | 触发场景 |
|------|---------|
| PlayerEnterMapEvent | 玩家成功进入地图 |

## 协议汇总

| MsgId | 消息类 | 方向 | 关键字段 |
|-------|--------|------|----------|
| 10701 | EnterMapReq | C→S | MapCfgId, Line |
| 10702 | EnterMapAck | S→C | Code, MapCfgId, Line, X, Y |
| 10703 | LeaveMapReq | C→S | （无） |
| 10704 | LeaveMapAck | S→C | Code |
| 10705 | ChangeMapReq | C→S | TargetMapCfgId, TargetLine, X, Y |
| 10706 | ChangeMapAck | S→C | Code, MapCfgId, Line, X, Y |
| 10707 | MapMoveReq | C→S | X, Y |
| 10708 | MapMoveAck | S→C | Code, X, Y |
| 10709 | MapObjectsNtf | S→C | Objects(List\<MapObjectInfo\>) |
| 10710 | MapPlayerEnterNtf | S→C | Player(MapObjectInfo) |
| 10711 | MapPlayerLeaveNtf | S→C | RoleId |
| 10712 | MapPlayerMoveNtf | S→C | RoleId, X, Y |
| 10713 | MapNpcEnterNtf | S→C | ObjectId, NpcId, Name, X, Y |
| 10714 | MapNpcLeaveNtf | S→C | ObjectId |
| 10715 | MapNpcMoveNtf | S→C | ObjectId, X, Y |
| 10716 | MonsterDropNtf | S→C | MonsterId, Gold, Items(List\<DropItemInfo\>) |

共享结构：
- `MapObjectInfo`：ObjectId, ObjectType, Name, X, Y, NpcId
- `DropItemInfo`：ConfigId, Count

## 模块依赖

```
Map 模块
  ├── 依赖 → Shared.Core.Aoi（TowerAoi 视野系统，已迁移至 shared/）
  ├── 依赖 → Player（Context.Players.SendToPlayer）
  ├── 依赖 → EventBus（PlayerEnterMapEvent 发布、PlayerOfflineEvent 订阅）
  ├── 依赖 → Fight（MapPlayerEntity.RegisterUnit/UnregisterUnit）
  │
  └── 被依赖 ←
       ├── Monster（OnPlayerEnterMap → 初始化怪物、MonsterEntity 管理怪物）
       ├── Fight（FindMapObject 全局查找、PlayerLeaveMapEvent → 注销战斗单元）
       └── Chat（间接：通过 Player 在线列表广播）
```

## 已知待办

1. **多线支持**：InitDefaultMaps 仅创建 Line=1，多线未实现
2. **移动校验**：仅检查边界，无碰撞检测和寻路
3. **动态地图**：地图在启动时固定创建，无运行时动态创建
