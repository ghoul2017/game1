# Monster 模块

目标读者：人类。

## 定位

Monster 模块负责管理游戏中所有怪物的生命周期——从生成到战斗到死亡再到刷新。它是服务端核心战斗循环的一环：玩家击杀怪物 → 获得奖励 → 怪物定时重生，形成持续的游戏节奏。

怪物是**纯运行时对象**，不持久化到数据库。服务重启后，怪物随地图创建重新生成。

## 核心设计

### 怪物是地图对象

怪物继承 `MapObjectEntity`，天然拥有坐标、AOI 可见性、地图广播等能力。进入地图时自动通知周围玩家，离开时自动移除——这些全部由 Map 模块的 AOI 系统处理。

### GameMap 持有状态，MonsterHelper 承载行为

每张 `GameMap` 持有怪物字典、刷新计时、随机数和 AI 定时器；刷怪、AI、死亡、掉落和刷新行为由 `MonsterHelper` 的 `GameMap` 扩展方法实现。当前没有全局 MonsterComponent。

```
GameMap
├── _monsters          // 私有怪物字典
├── SpawnTimers        // map+line+cfgId → 下次刷新时间
├── Random             // 地图共享随机数
├── _aiTimer           // 1秒 AI 定时器
└── MonsterHelper      // Init/Spawn/Killed/AI 扩展方法
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
| `MonsterHelper.cs` | GameMap 扩展方法：刷怪、AI、死亡、掉落和刷新 |

已删除旧的全局 `MonsterComponent.cs` 和 `MonsterHandlers.cs`；当前怪物实体行为由 `MonsterEntity`、`MonsterAiComponent` 与 `MonsterHelper` 协作完成。

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
| 1 | 被动 | 原地等待，视野内发现玩家就追击 | 不巡逻 | “被动”只表示不巡逻，当前仍会主动攻击进入视野的玩家 |
| 2 | 主动 | 立即开始巡逻 | 刷新区域内随机点 | 适合野狼、哥布林等 |
| 3 | 守卫 | 立即开始巡逻 | 刷新区域内随机点 | 同主动，数值上区分用途 |

#### 追击与攻击

- **发现目标**：遍历地图所有玩家，切比雪夫距离 ≤ `SightRange` 则锁定
- **追击**：X/Y 每次各最多移动 1 格，允许斜向移动；超出视野则丢失目标回到 Idle
- **攻击**：进入 `AttackRange` 后切换到 Attack 状态，2 秒冷却，使用技能 ID=1（普通攻击）
- **攻击后**：当前实现中，攻击完立即回到 Idle（重新评估目标）
- **目标丢失**：玩家下线后 GetPlayer 返回 null，AI 自动丢失目标回到 Idle，无需显式清除仇恨

### 4. 死亡与奖励

怪物死亡由 FightHelper 检测（HP ≤ 0），直接调用 `map.OnMonsterKilled()`：

```
从地图移除 → map.RemoveObject（AOI 移除 + 广播离开视野）
→ 计算并发放奖励（经验/金币/物品）
→ 设置刷新定时器
```

奖励计算：

- **经验**：固定值，`Config.Exp`
- **金币**：`Random.Next(GoldMin, GoldMax + 1)` 范围随机
- **物品掉落**：遍历 `DropItems`（逗号分隔的 ConfigId 列表），每件独立判定 `Random.Next(1, 10001) ≤ DropRate`

所有奖励通过同步的 `PlayerSystem.GrantExp`、`GrantGold` 和 `GrantItem` 发放并标脏，GameMap 不直接操作背包或玩家数据。

### 5. 定时刷新

刷新键是 `mapId_line_cfgId`，同地图同配置只保存一个时间点。每次死亡都会用“当前时间 + SpawnInterval”覆盖该键；到期后每次 AI 更新都会尝试补充一只，`SpawnMonster` 仍限制 `MaxCount`。当前实现没有在刷新成功或达到上限后重置时间，因而到期后会每秒持续尝试生成。

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
| Fight | 双向 | 怪物 FightUnitComponent 常驻；FightHelper 检测死亡后调用 `map.OnMonsterKilled()` |
| Player | 单向 | 怪物死亡后通过同步的 `GrantExp/GrantGold/GrantItem` 发放奖励 |
| Config | 单向 | 读取 `MonsterConfig` 驱动所有行为参数 |
| GameTask | 被动 | `MonsterKilledEvent` 驱动击杀任务进度 |

**关键设计决策**：Fight → GameMap 的死亡通知走直接方法调用而非事件。原因：FightHelper 明确知道怪物死亡后的处理逻辑，事件只用于需要通知多个模块的场景（如任务进度）。

## 配置驱动

怪物的大部分行为参数都在 `MonsterConfig` 中定义；存在配置字段不等于当前运行链已经消费：

| 配置项 | 控制什么 |
|--------|---------|
| MapId | 怪物出现在哪张地图 |
| SpawnX1/X2/Y1/Y2 | 刷新区域范围（怪物只在此区域内生成和巡逻） |
| MaxCount | 地图同种怪物上限，控制密度 |
| SpawnInterval | 死亡后多少秒刷新，控制节奏感 |
| SightRange / AttackRange | AI 视野和攻击距离，决定追击距离和攻击触发 |
| AiType | 行为模式：被动/主动/守卫 |
| MoveSpeed | 配置模型存在，但当前 AI 未读取；实际固定每秒最多移动 X/Y 各 1 格 |
| MaxHp / Attack / Defense | 战斗属性，传入 FightAttribute |
| Exp / GoldMin/GoldMax | 死亡奖励 |
| DropItems / DropRate | 物品掉落列表和掉率（万分比） |

## 已知问题与边界

1. **单目标仇恨**：AI 只追踪一个目标，`ThreatMap` 未接入，无法实现仇恨转移
2. **巡逻无路径**：`PatrolTarget` 为随机点，无固定巡逻路线
3. **攻击后回 Idle**：攻击完立即重置状态而非持续攻击，行为略显机械
4. **无脱战回血**：怪物脱离战斗后不会回血，下次被攻击时仍是残血
5. **刷新计时不复位**：到期后每秒重复尝试刷怪；同配置多只怪物死亡只共享并覆盖一个时间点，不能逐只计时
6. **MoveSpeed 未消费**：不同怪物配置速度不会改变实际移动频率或步长
7. **怪物攻击广播缺失**：Fight 的 AOI 广播按玩家索引查地图，怪物作为攻击者时客户端收不到普通攻击过程
8. **掉落表现未闭环**：`MonsterDropNtf` 只有协议定义，没有服务端生产者；经验、金币和物品通知也缺少客户端 Handler
