# Map 模块文档

## 概述

Map 模块实现地图系统，包含地图实例管理、玩家进/出/切换地图、移动同步、AOI 视野管理。使用 TowerAoi（九宫格）实现视野广播，玩家进入视野范围时自动推送对象信息。地图为纯内存对象，不持久化。

## 文件清单

| 文件 | 行数 | 职责 |
|------|------|------|
| `server/Game/Components/Map/IMapObject.cs` | 14 | 地图对象接口 + 对象类型常量 |
| `server/Game/Components/Map/MapComponent.cs` | 19 | Module 级 Component，地图集合 |
| `server/Game/Components/Map/MapSystem.cs` | 182 | 地图操作扩展方法 |
| `server/Game/Components/Map/GameMap.cs` | 242 | 地图实例（含 AOI） |
| `server/Game/Components/Map/MapPlayer.cs` | 28 | 玩家地图对象 |
| `server/Game/Components/Map/MapNpc.cs` | 31 | NPC/怪物地图对象 |
| `server/Game/Components/Map/MapAoiNotifier.cs` | 88 | AOI 事件 → 网络通知 |
| `server/Game/Components/Map/MapHandlers.cs` | 205 | 消息处理 + 事件订阅 |
| `client/Assets/Scripts/shared/Common/Protos/MapProtos.cs` | 207 | 消息定义 + 错误码 |
| `client/Assets/Scripts/shared/Common/MsgIdConst.cs` | — | MsgId 10701-10800 段 |

## 数据模型

### IMapObject（地图对象接口）

```csharp
namespace Game.Components.Map;

public static class MapObjectType
{
    public const int Player = 1;
    public const int Npc = 2;
    public const int Monster = 3;
}

public interface IMapObject : IAoiObject
{
    int ObjectType { get; }    // MapObjectType
    string Name { get; }
    int MapId { get; set; }
    int Line { get; set; }
}
```

### MapPlayer

```csharp
public class MapPlayer : IMapObject
{
    public long ObjectId => RoleId;
    public int ObjectType => MapObjectType.Player;
    public string Name { get; }
    public long RoleId { get; }
    public int MapId { get; set; }
    public int Line { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
}
```

### MapNpc

```csharp
public class MapNpc : IMapObject
{
    public long ObjectId => NpcId;
    public int ObjectType => MapObjectType.Npc;
    public string Name { get; }
    public int NpcId { get; }
    public int CfgId { get; }
    public int MapId { get; set; }
    public int Line { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
}
```

## Component

### MapComponent（Module 级）

```csharp
[ComponentOf(typeof(Module))]
public class MapComponent : Component
{
    public readonly Dictionary<long, GameMap> Maps;                // MapKey → GameMap
    public readonly Dictionary<long, GameMap> PlayerMapMap;        // roleId → GameMap
    public readonly Dictionary<long, MapPlayer> PlayerMapObjMap;   // roleId → MapPlayer
}
```

- 挂载目标：`Context.Server`
- Context 便利属性：`Context.Map`
- MapKey = CfgId × 1000 + Line

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

    // 对象管理
    bool AddObject(IMapObject obj);
    bool RemoveObject(long objectId);
    IMapObject? GetObject(long objectId);
    MapPlayer? GetPlayer(long roleId);
    List<MapPlayer> GetAllPlayers();
    List<MapNpc> GetAllNpcs();
    List<IMapObject> GetObjectsInRange(int x, int y, int range);
    bool MoveObject(long objectId, int newX, int newY);

    // NPC管理
    bool AddNpc(int npcId, int cfgId, string name, int x, int y);
    bool RemoveNpc(int npcId);
}
```

- AOI 视野范围：4格
- 线程安全：lock 保护内部操作

## System 详解

### MapSystem

| 方法 | 签名 | 说明 |
|------|------|------|
| CreateMap | `CreateMap(this MapComponent, int cfgId, int line, string name, int width, int height, int birthX, int birthY) → GameMap` | 创建地图实例 |
| GetMap | `GetMap(this MapComponent, int cfgId, int line) → GameMap?` | 获取地图 |
| GetPlayerMap | `GetPlayerMap(this MapComponent, long roleId) → GameMap?` | 获取玩家所在地图 |
| GetPlayerMapObj | `GetPlayerMapObj(this MapComponent, long roleId) → MapPlayer?` | 获取玩家地图对象 |
| GetAllMaps | `GetAllMaps(this MapComponent) → List<GameMap>` | 所有地图 |
| EnterMap | `EnterMap(this MapComponent, long roleId, string roleName, int cfgId, int line, int x, int y) → bool` | 进入地图 |
| QuitMap | `QuitMap(this MapComponent, long roleId) → bool` | 离开地图 |
| ChangeMap | `ChangeMap(this MapComponent, long roleId, string roleName, int newCfgId, int newLine, int newX, int newY) → bool` | 切换地图（Quit+Enter） |
| Move | `Move(this MapComponent, long roleId, int newX, int newY) → bool` | 移动 |
| GetPlayerPosition | `GetPlayerPosition(this MapComponent, long roleId) → (int,int,int,int)?` | 获取位置 |
| GetNearbyPlayers | `GetNearbyPlayers(this MapComponent, long roleId) → List<MapPlayer>` | 获取附近玩家 |
| InitDefaultMaps | `InitDefaultMaps(this MapComponent)` | 初始化默认地图 |
| RemoveAll | `RemoveAll(this MapComponent)` | 清除所有 |

### 默认地图

| 名称 | CfgId | 尺寸 | 出生点 |
|------|-------|------|--------|
| 新手村 | 1 | 200×200 | (100,100) |
| 主城 | 2 | 300×300 | (150,150) |
| 野外 | 3 | 500×500 | (250,250) |

## AOI 视野管理

### MapAoiNotifier（IAoiEventListener 实现）

| 回调 | 通知 | 说明 |
|------|------|------|
| OnObjectEnter | MapPlayerEnterNtf | 对象进入视野 |
| OnObjectLeave | MapPlayerLeaveNtf | 对象离开视野 |
| OnObjectMove | MapPlayerMoveNtf | 对象移动 |
| OnWatcherUpdate | — | 未实现（初始视野由 SendVisibleObjects 处理） |

### 视野推送流程

```
进入地图：
  1. EnterMapAck → 告知客户端地图坐标
  2. SendVisibleObjects() → MapObjectsNtf（视野内所有对象快照）
  3. AOI 自动通知后续的进入/离开/移动

移动：
  1. MapMoveAck → 确认坐标
  2. AOI OnObjectMove → 通知附近玩家
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
| 10713 | MapNpcEnterNtf | S→C | — |
| 10714 | MapNpcLeaveNtf | S→C | — |
| 10715 | MapNpcMoveNtf | S→C | — |
| 10716 | MonsterDropNtf | S→C | MonsterId, Gold, Items(List\<DropItemInfo\>) |

共享结构：
- `MapObjectInfo`：ObjectId, ObjectType, Name, X, Y, NpcId
- `DropItemInfo`：ConfigId, Count

## 模块依赖

```
Map 模块
  ├── 依赖 → Core.Aoi（TowerAoi 视野系统）
  ├── 依赖 → Player（Context.Players.SendToPlayer）
  ├── 依赖 → EventBus（PlayerEnterMapEvent 发布、PlayerOfflineEvent 订阅）
  │
  └── 被依赖 ←
       ├── Monster（OnPlayerEnterMap → 初始化怪物、MapNpc 管理怪物对象）
       ├── Fight（PlayerLeaveMapEvent → 注销战斗单元）
       └── Chat（间接：通过 Player 在线列表广播）
```

## 已知待办

1. **多线支持**：InitDefaultMaps 仅创建 Line=1，多线未实现
2. **移动校验**：仅检查边界，无碰撞检测和寻路
3. **OnWatcherUpdate**：MapAoiNotifier 未实现，初始视野由 SendVisibleObjects 处理
4. **动态地图**：地图在启动时固定创建，无运行时动态创建
