# Monster 模块文档

## 概述

Monster 模块实现怪物系统，包含怪物生成、刷新、AI 状态机、战斗、掉落。AI 采用有限状态机（Idle/Patrol/Chase/Attack），每秒更新一次。怪物为纯运行时对象，不持久化到数据库。ECS 重构后，怪物为 MonsterEntity（继承 MapObjectEntity），AI/信息/战斗数据分散到独立 Component。

## 文件清单

| 文件 | 行数 | 职责 |
|------|------|------|
| `server/Game/Components/Monster/MonsterEntity.cs` | 45 | 怪物实体（继承 MapObjectEntity） |
| `server/Game/Components/Monster/MonsterComponent.cs` | 19 | Module 级 Component，怪物集合+刷新计时 |
| `server/Game/Components/Monster/MonsterSystem.cs` | 667 | 怪物系统核心（生成/AI/掉落） |
| `server/Game/Components/Monster/MonsterHandlers.cs` | 43 | 事件订阅 |
| `server/Game/Components/Monster/MonsterInfoComponent.cs` | 20 | 怪物信息 Component |
| `server/Game/Components/Monster/MonsterAiComponent.cs` | 18 | 怪物AI Component |
| `server/Game/Components/Monster/AiState.cs` | 13 | AI 状态枚举 |

## 实体体系

### MonsterEntity

```csharp
public class MonsterEntity : MapObjectEntity
{
    public override int ObjectType => MapObjectType.Monster;  // 值=3
    public override string Name => Info.Name;

    // 组件访问
    private MonsterInfoComponent? _info;
    public MonsterInfoComponent Info => _info ??= GetComponent<MonsterInfoComponent>();

    private MonsterAiComponent? _ai;
    public MonsterAiComponent Ai => _ai ??= GetComponent<MonsterAiComponent>();

    private FightUnitComponent? _fight;
    public FightUnitComponent Fight => _fight ??= GetComponent<FightUnitComponent>();

    public int CfgId => Info.CfgId;

    public static MonsterEntity Create(long monsterId, MonsterConfig config, int mapId, int line, int x, int y);
}
```

### AiState（AI 状态枚举）

```csharp
public enum AiState
{
    Idle = 0,      // 待机
    Patrol = 1,    // 巡逻
    Chase = 2,     // 追击
    Attack = 3,    // 攻击
}
```

## Component

### MonsterComponent（Module 级）

```csharp
[ComponentOf(typeof(Module))]
public class MonsterComponent : Component
{
    public readonly Dictionary<long, Dictionary<long, MonsterEntity>> MapMonsters = new();  // MapKey → 怪物列表
    public readonly Dictionary<string, long> SpawnTimers = new();  // timerKey → 下次刷新时间
    public long NextMonsterId = 100000;    // 怪物ID生成器（简单递增）
    public GameTimerHandle UpdateTimer;    // AI更新定时器
}
```

- 挂载目标：`Context.Server`
- Context 便利属性：`Context.Monster`
- MapKey = mapCfgId × 1000 + line
- TimerKey = $"{mapCfgId}_{line}_{cfgId}"

### MonsterInfoComponent

```csharp
[ComponentOf(typeof(MonsterEntity))]
public class MonsterInfoComponent : Component
{
    public MonsterConfig? Config;
    public string Name = "";
    public int CfgId => Config?.Id ?? 0;
}
```

### MonsterAiComponent

```csharp
[ComponentOf(typeof(MonsterEntity))]
public class MonsterAiComponent : Component
{
    public AiState AiState = AiState.Idle;
    public (int X, int Y)? PatrolTarget;
    public long? ChaseTarget;
    public long LastAttackTime;
    public long LastUpdateTime;
}
```

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
3. MonsterEntity.Create() 创建实体
4. 注册 FightUnitComponent（RegisterUnit）
5. 加入 GameMap（AddObject，自动注册 AOI）
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
- 通过 `FightSystem.UseSkill(monster.ObjectId, 1, ...)` 执行攻击

#### 死亡与掉落

| 方法 | 签名 | 说明 |
|------|------|------|
| OnMonsterKilled | `OnMonsterKilled(this MonsterComponent, long monsterId, long killerId)` | 处理怪物死亡 |

死亡掉落流程：
```
1. 从 MapMonsters 移除
2. 注销 FightUnitComponent（UnregisterUnit）
3. 从 GameMap 移除（RemoveObject，自动移除 AOI）
4. 广播怪物死亡
5. 计算并发放奖励：
   ├── 经验 → RoleAttribute.Exp + LevelUpNtf
   ├── 金币 → RoleAttribute.Gold + GoldChangedNtf
   └── 物品 → RoleBag.AddItem + ItemGainNtf
6. 设置刷新定时器（按 SpawnInterval）
```

#### 刷新机制

AI 更新循环（1秒/次）同时检查刷新计时器：
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
  ├── 依赖 → Map（GameMap.AddObject/RemoveObject、AOI 广播、FindMapObject）
  ├── 依赖 → Fight（FightUnitComponent 注册/注销、FightSystem.UseSkill 攻击、FightSystem.GetFightUnit）
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
2. **仇恨系统**：ChaseTarget 仅记录一个目标，FightUnitComponent.ThreatMap 未被 AI 使用
3. **掉落独立**：ExpChangedNtf/GoldChangedNtf/ItemGainNtf 定义在 MonsterProtos.cs 中，但语义上属于通用通知
