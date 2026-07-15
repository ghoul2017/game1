# Monster 模块

目标读者：人类。

## 定位

Monster 模块负责管理游戏中所有怪物的生命周期——从生成到战斗到死亡再到刷新。它是服务端核心战斗循环的一环：玩家击杀怪物 → 获得奖励 → 怪物定时重生，形成持续的游戏节奏。

怪物是**纯运行时对象**，不持久化到数据库。服务重启后，怪物随地图创建重新生成。

## 核心设计

### 怪物是地图对象

怪物继承 `MapObjectEntity`，天然拥有坐标、AOI 可见性、地图广播等能力。进入地图时自动通知周围玩家，离开时自动移除——这些全部由 Map 模块的 AOI 系统处理。

### 刷怪和 AI 属于地图

刷怪、AI 更新、死亡处理、掉落发放、刷新计时——这些逻辑全部由 `GameMap` 承载。每张地图独立管理自己的怪物和 AI 定时器，无需全局 MonsterComponent 做中转。

```
GameMap
├── _monsters          // 怪物字典
├── _spawnTimers       // 刷新计时器
├── _random            // 共享随机数
├── _aiTimer           // 1秒 AI 定时器
├── InitMonsters()     // 初始化刷怪
├── SpawnMonster()     // 生成怪物
├── OnMonsterKilled()  // 死亡+掉落+刷新
└── UpdateAllMonsters()// AI 状态机
```

### 三层 Component 分离

一只怪物的数据分散在三个 Component 中，各司其职：

| Component | 职责 | 生命周期 |
|-----------|------|---------|
| MonsterInfoComponent | 静态配置（名称、CfgId） | 创建时赋值，不再变化 |
| MonsterAiComponent | 运行时 AI 状态（当前状态、追击目标、攻击冷却） | 每秒更新 |
| FightUnitComponent | 战斗属性和技能（常驻，创建时挂载） | 受击/攻击时变化 |

FightUnitComponent 在 `MonsterEntity.Create` 时直接创建，不再需要 RegisterUnit/UnregisterUnit。怪物死亡后整个实体废弃，无需逐个拆 Component。

### 保留的文件

| 文件 | 职责 |
|------|------|
| `MonsterEntity.cs` | 怪物实体（Create 中创建所有 Component） |
| `MonsterInfoComponent.cs` | 怪物静态信息 |
| `MonsterAiComponent.cs` | AI 运行时状态 |
| `AiState.cs` | AI 状态枚举 |

已删除：`MonsterComponent.cs`、`MonsterSystem.cs`、`MonsterHandlers.cs`（逻辑迁入 GameMap）。

## 怪物生命周期

### 1. 地图创建时刷怪

怪物不再由玩家进入地图触发，而是在**地图创建时自动初始化**：

```
AppStart.RunServerAsync → MapSystem.InitDefaultMaps
→ 每张地图 CreateMap → map.InitMonsters() + map.StartAiTimer()
→ 读取该地图所有 MonsterConfig → 按 MaxCount 生成
```

### 2. 创建实例

生成一只怪物的核心步骤：

```
分配雪花ID → MonsterEntity.Create（含 FightUnitComponent）
→ map.AddObject → AOI 注册 + 广播进入视野
```

`MonsterEntity.Create` 接收 `FightAttribute` 参数，内部完成所有 Component 的创建，外部不需要再调 RegisterUnit。

### 3. AI 行为

每张地图有独立的 1 秒 GameTimer，驱动本地图所有怪物的 AI 更新。

#### 状态机

```
             ┌──────────────────────────────────┐
             │                                  │
             ▼                                  │
          Idle ──(主动/守卫AI)──→ Patrol         │
           │                        │           │
           │ (被动AI发现目标)        │ (发现目标) │
           │                        │           │
           ▼                        ▼           │
         Chase ←───────────────────┘           │
           │                                    │
           │ (进入攻击范围)            (目标丢失) │
           ▼                                    │
        Attack ─────────────────────────────────┘
```

#### AI 类型决定初始行为

| AiType | 名称 | Idle 行为 | 巡逻 | 说明 |
|--------|------|-----------|------|------|
| 1 | 被动 | 原地等待，视野内发现玩家才追击 | 不巡逻 | 适合鹿、兔子等无害生物 |
| 2 | 主动 | 立即开始巡逻 | 刷新区域内随机点 | 适合野狼、哥布林等 |
| 3 | 守卫 | 立即开始巡逻 | 刷新区域内随机点 | 同主动，数值上区分用途 |

#### 追击与攻击

- **发现目标**：遍历地图所有玩家，切比雪夫距离 ≤ `SightRange` 则锁定
- **追击**：每步向目标移动 1 格（曼哈顿方向），超出视野则丢失目标回到 Idle
- **攻击**：进入 `AttackRange` 后切换到 Attack 状态，2 秒冷却，使用技能 ID=1（普通攻击）
- **攻击后**：当前实现中，攻击完立即回到 Idle（重新评估目标）
- **目标丢失**：玩家下线后 GetPlayer 返回 null，AI 自动丢失目标回到 Idle，无需显式清除仇恨

### 4. 死亡与奖励

怪物死亡由 FightSystem 检测（HP ≤ 0），直接调用 `map.OnMonsterKilled()`：

```
从地图移除 → map.RemoveObject（AOI 移除 + 广播离开视野）
→ 计算并发放奖励（经验/金币/物品）
→ 设置刷新定时器
```

奖励计算：

- **经验**：固定值，`Config.Exp`
- **金币**：`Random.Next(GoldMin, GoldMax + 1)` 范围随机
- **物品掉落**：遍历 `DropItems`（逗号分隔的 ConfigId 列表），每件独立判定 `Random.Next(1, 10001) ≤ DropRate`

所有奖励通过 `PlayerSystem.GrantXxxAsync` 发放，GameMap 不直接操作背包或玩家数据。

### 5. 定时刷新

每种怪物有独立的刷新间隔（`SpawnInterval` 秒）。死亡时设置定时器，下次 AI 更新循环检查是否到期，到期则补充一只（不超过 `MaxCount`）。

## 跨模块协作

```
                    ┌─────────────┐
                    │   Config    │ 提供怪物配置表
                    └──────┬──────┘
                           │
┌──────────┐    ┌──────────▼──────────┐    ┌──────────┐
│   Map    │◄──►│      GameMap        │◄──►│  Fight   │
│ AOI/移动  │    │  刷怪/AI/掉落/刷新   │    │ 伤害/死亡 │
└──────────┘    └──────────┬──────────┘    └──────────┘
                           │
                    ┌──────▼──────┐
                    │   Player    │ 经验/金币/物品发放
                    └─────────────┘
```

| 交互方 | 方向 | 内容 |
|--------|------|------|
| Map | 双向 | GameMap 本身属于 Map 模块，内部使用 AOI 广播和玩家查询 |
| Fight | 双向 | 怪物 FightUnitComponent 常驻；FightSystem 检测死亡后调用 `map.OnMonsterKilled()` |
| Player | 单向 | 怪物死亡后通过同步的 `GrantExp/GrantGold/GrantItem` 修改内存并标脏 |
| Config | 单向 | 读取 `MonsterConfig` 驱动所有行为参数 |
| GameTask | 被动 | `MonsterKilledEvent` 驱动击杀任务进度 |

**关键设计决策**：Fight → GameMap 的死亡通知走直接方法调用而非事件。原因：FightSystem 明确知道怪物死亡后的处理逻辑，事件只用于需要通知多个模块的场景（如任务进度）。

## 配置驱动

怪物的一切行为参数都在 `MonsterConfig` 中定义，策划通过配置表控制：

| 配置项 | 控制什么 |
|--------|---------|
| MapId | 怪物出现在哪张地图 |
| SpawnX1/X2/Y1/Y2 | 刷新区域范围（怪物只在此区域内生成和巡逻） |
| MaxCount | 地图同种怪物上限，控制密度 |
| SpawnInterval | 死亡后多少秒刷新，控制节奏感 |
| SightRange / AttackRange | AI 视野和攻击距离，决定追击距离和攻击触发 |
| AiType | 行为模式：被动/主动/守卫 |
| MaxHp / Attack / Defense | 战斗属性，传入 FightAttribute |
| Exp / GoldMin/GoldMax | 死亡奖励 |
| DropItems / DropRate | 物品掉落列表和掉率（万分比） |

## 已知局限

1. **单目标仇恨**：AI 只追踪一个目标，`ThreatMap` 未接入，无法实现仇恨转移
2. **巡逻无路径**：`PatrolTarget` 为随机点，无固定巡逻路线
3. **攻击后回 Idle**：攻击完立即重置状态而非持续攻击，行为略显机械
4. **无脱战回血**：怪物脱离战斗后不会回血，下次被攻击时仍是残血
