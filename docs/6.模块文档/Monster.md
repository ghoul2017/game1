# Monster 模块文档

## 概述

Monster 模块实现怪物系统，包含怪物生成、刷新、AI 状态机、战斗、掉落。AI 采用有限状态机（Idle/Patrol/Chase/Attack），每秒更新一次。怪物为纯运行时对象，不持久化到数据库。

## 文件清单

| 文件 | 行数 | 职责 |
|------|------|------|
| `server/Game/Components/Monster/AiState.cs` | 13 | AI 状态枚举 |
| `server/Game/Components/Monster/MonsterComponent.cs` | 20 | Module 级 Component，怪物集合+刷新计时 |
| `server/Game/Components/Monster/MonsterSystem.cs` | 697 | 怪物系统核心（生成/AI/掉落） |
| `server/Game/Components/Monster/MonsterHandlers.cs` | 40 | 事件订阅 |
| `server/Game/Components/Monster/MonsterUnit.cs` | 54 | 怪物运行时数据 |

## 数据模型

### AiState（AI 状态枚举）

```csharp
namespace Game.Components.Monster;

public enum AiState
{
    Idle = 0,      // 待机
    Patrol = 1,    // 巡逻
    Chase = 2,     // 追击
    Attack = 3,    // 攻击
}
```

### MonsterUnit（怪物单元）

```csharp
public class MonsterUnit
{
    public long ObjectId { get; }              // 唯一ID（从 100000 递增）
    public MonsterConfig? Config { get; }      // 怪物配置
    public FightUnit? FightUnit { get; set; }  // 战斗单元
    public MapNpc? Npc { get; set; }           // 地图NPC对象
    public int X { get; set; }                 // 当前X坐标
    public int Y { get; set; }                 // 当前Y坐标
    public AiState AiState { get; set; }       // AI 状态
    public (int X, int Y)? PatrolTarget { get; set; }  // 巡逻目标
    public long? ChaseTarget { get; set; }     // 追击目标（玩家RoleId）
    public long LastAttackTime { get; set; }   // 上次攻击时间
    public long LastUpdateTime { get; set; }   // 上次AI更新时间
    public int CfgId => Config?.Id ?? 0;       // 配置ID
}
```

## Component

### MonsterComponent（Module 级）

```csharp
[ComponentOf(typeof(Module))]
public class MonsterComponent : Component
{
    public readonly Dictionary<long, Dictionary<long, MonsterUnit>> MapMonsters = new();  // MapKey → 怪物列表
    public readonly Dictionary<string, long> SpawnTimers = new();  // timerKey → 下次刷新时间
    public long NextMonsterId = 100000;    // 怪物ID生成器
    public GameTimerHandle UpdateTimer;    // AI更新定时器
}
```

- 挂载目标：`Context.Server`
- Context 便利属性：`Context.Monster`
- MapKey = mapCfgId × 1000 + line
- TimerKey = $"{mapCfgId}_{line}_{cfgId}"

## System 详解

### MonsterSystem

#### 生命周期

| 方法 | 签名 | 说明 |
|------|------|------|
| Start | `Start(this MonsterComponent)` | 启动 1 秒 AI 更新定时器 |
| Stop | `Stop(this MonsterComponent)` | 停止定时器，清除所有怪物 |

#### 初始化与生成

| 方法 | 签名 | 说明 |
|------|------|------|
| InitMapMonsters | `InitMapMonsters(this MonsterComponent, int mapCfgId, int line)` | 按配置初始化地图怪物 |
| SpawnMonster | `SpawnMonster(this MonsterComponent, int mapId, int line, MonsterConfig cfg)` | 随机位置生成怪物 |
| SpawnMonsterAt | `SpawnMonsterAt(this MonsterComponent, int mapId, int line, MonsterConfig cfg, int x, int y)` | 指定位置生成 |

生成流程：
```
1. 分配 ObjectId（NextMonsterId++）
2. 在配置的刷新区域随机坐标
3. 创建 MonsterUnit
4. 创建 FightUnit（注册到 FightComponent）
5. 创建 MapNpc（加入地图 AOI）
6. 广播怪物出现
7. 加入 MapMonsters 索引
```

#### AI 状态机

```
             ┌──────────────────────────────┐
             │                              │
             ▼                              │
          Idle ──(主动/守卫AI)──→ Patrol    │
           │                        │       │
           │ (被动AI发现目标)        │ (发现目标)
           │                        │       │
           ▼                        ▼       │
         Chase ←───────────────────┘       │
           │                                │
           │ (进入攻击范围)                   │ (目标丢失)
           ▼                                │
        Attack ─────────────────────────────┘
```

AI 类型（MonsterConfig.AiType）：
- **被动(1)**：Idle 状态，不巡逻，玩家进入视野才 Chase
- **主动(2)**：Idle → Patrol 巡逻，发现玩家 Chase
- **守卫(3)**：同主动，巡逻 + 主动追击

#### 攻击机制

- 技能ID=1（普通攻击）
- 攻击间隔：2秒
- 通过 `Context.Fight.UseSkill()` 执行攻击

#### 死亡与掉落

| 方法 | 签名 | 说明 |
|------|------|------|
| OnMonsterKilled | `OnMonsterKilled(this MonsterComponent, long monsterId, long killerId)` | 处理怪物死亡 |

死亡掉落流程：
```
1. 从 MapMonsters 移除
2. 注销 FightUnit
3. 移除 MapNpc
4. 广播怪物死亡
5. 计算并发放奖励：
   ├── 经验 → Context.Players.RoleAttribute.Exp + LevelUpNtf
   ├── 金币 → Context.Players.RoleAttribute.Gold + GoldChangedNtf
   └── 物品 → Context.Players.RoleBag.AddItem + ItemGainNtf
6. 设置刷新定时器（按 SpawnInterval）
7. 广播 MonsterDropNtf
```

#### 刷新机制

AI 更新循环（1秒/次）同时检查刷新定时器：
- 每个 timerKey 有独立的刷新间隔
- 到时间后按 MaxCount 限制补充怪物

## Handler 详解

### 事件订阅

| 事件 | 方法 | 说明 |
|------|------|------|
| MonsterKilledEvent | OnMonsterKilled | 处理怪物死亡（掉落+刷新） |
| PlayerEnterMapEvent | OnPlayerEnterMap | 初始化地图怪物 |
| PlayerOfflineEvent | OnPlayerOffline | 清除以该玩家为追击目标的仇恨 |

## MonsterConfig 配置

| 字段 | 类型 | 说明 |
|------|------|------|
| Id | int | 配置ID |
| Name | string | 怪物名称 |
| MapId | int | 所属地图 |
| MaxHp | int | 最大HP |
| Attack | int | 攻击力 |
| Defense | int | 防御力 |
| Exp | int | 经验奖励 |
| GoldMin/GoldMax | int | 金币奖励范围 |
| DropItems | string | 掉落物品（逗号分隔 ConfigId） |
| DropRate | int | 掉落率（0-10000） |
| SpawnInterval | int | 刷新间隔（秒） |
| MaxCount | int | 地图最大同时存在数 |
| SpawnX1/X2/Y1/Y2 | int | 刷新区域 |
| SightRange | int | 视野范围（格） |
| AttackRange | int | 攻击范围（格） |
| AiType | int | AI类型（1=被动, 2=主动, 3=守卫） |

## 模块依赖

```
Monster 模块
  ├── 依赖 → Config（MonsterConfig 配置表）
  ├── 依赖 → Map（GameMap.AddNpc/RemoveNpc、AOI 广播）
  ├── 依赖 → Fight（FightUnit 注册/注销、UseSkill 攻击）
  ├── 依赖 → Player（奖励发放：Exp/Gold/Item）
  ├── 依赖 → Bag（掉落物品 → AddItem）
  ├── 依赖 → GameTimer（1秒 AI 更新定时器）
  ├── 依赖 → EventBus（MonsterKilledEvent/PlayerEnterMapEvent/PlayerOfflineEvent 订阅）
  │
  └── 被依赖 ←
       └── GameTask（MonsterKilledEvent 驱动 KillMonster 任务进度）
```

## 已知待办

1. **巡逻路径**：PatrolTarget 为随机点，无固定巡逻路径
2. **仇恨系统**：ChaseTarget 仅记录一个目标，FightUnit.ThreatMap 未被 AI 使用
3. **AOI 跨塔移动**：已知限制，移动可能不触发正确的视野更新
4. **掉落独立**：ExpChangedNtf/GoldChangedNtf/ItemGainNtf 定义在 MonsterProtos.cs 中，但语义上属于通用通知
