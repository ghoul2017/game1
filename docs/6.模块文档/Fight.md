# Fight 模块

## 定位

Fight 模块是服务端战斗的运算核心——技能使用、伤害计算、命中/暴击/闪避判定、HP 管理、死亡/复活。所有战斗逻辑在 `FightCore`（纯函数）和 `FightSystem`（流程编排）中完成，战斗单元（`FightUnitComponent`）挂载在 `MapObjectEntity` 上随实体存亡。

战斗是**纯内存运算**，不持久化。玩家下线或离开地图时战斗单元随实体销毁。

## 核心设计

### 战斗单元属于地图对象

`FightUnitComponent` 挂载在 `MapObjectEntity`（`MapPlayerEntity` / `MonsterEntity`）上，在实体 Create 时直接创建，实体销毁时自动消失。不再有全局 `FightComponent` 做 RegisterUnit/UnregisterUnit 中转。

### 三层分离

| 层 | 文件 | 职责 |
|----|------|------|
| 纯函数 | `FightCore.cs` | 伤害计算、命中/暴击/闪避判定，无副作用 |
| 流程编排 | `FightSystem.cs` | UseSkill 流程、死亡检测、广播通知 |
| 数据容器 | `FightUnitComponent.cs` | 战斗属性、技能列表、冷却、仇恨 |

`FightCore` 不依赖任何 System 或 Component，只接收 `FightUnitComponent` 和配置参数，可独立测试。

### 死亡通知走直接调用

FightSystem 检测到怪物死亡后，直接调用 `map.OnMonsterKilled()` 处理后续（移除、掉落、刷新定时器），而非通过事件中转。事件只用于需要通知多个模块的场景（如 `MonsterKilledEvent` 驱动任务进度）。

### 保留的文件

| 文件 | 职责 |
|------|------|
| `FightSystem.cs` | 战斗流程编排（FightUnitComponent 扩展方法 + UseSkill + 死亡处理） |
| `FightCore.cs` | 伤害计算纯函数 |
| `FightUnitComponent.cs` | 战斗单元 Component（属性/技能/冷却/仇恨） |
| `FightAttribute.cs` | 战斗属性数据结构（ProtoContract，兼容网络序列化） |
| `Skill.cs` | 技能数据类 |
| `UseSkillReqHandler.cs` | UseSkillReq 消息处理 |
| `SetPkModeReqHandler.cs` | SetPkModeReq 消息处理 |

已删除：`FightComponent.cs`（逻辑迁入 FightSystem，FightUnitComponent 改为 MapObjectEntity 级）。

## 战斗流程

### UseSkill

```
1. 校验攻击者存在且存活
2. 校验技能存在且冷却完毕
3. 校验目标存在且存活
4. 校验攻击距离
5. FightCore.UseSkill 计算伤害（命中 → 暴击 → 伤害公式）
6. 处理死亡（HP ≤ 0 → OnUnitKilled）
7. 广播技能效果（UseSkillAck + HpChangedNtf）
8. 设置技能冷却
```

### 伤害计算

```
FightCore.CalculateDamage
  │
  ├── 物攻区间随机：Random(PhyAttMin, PhyAttMax + 1)
  ├── 物防区间随机：Random(PhyDefMin, PhyDefMax + 1)
  ├── 基础伤害 = max(物攻 - 物防, 1)
  ├── 暴击加成：基础伤害 * CriticalDamageRate / 10000
  ├── 伤害加成/减免：乘算 DamagePct / DamageReducePct
  └── 最终伤害 = max(结果, 1)
```

### 死亡处理

```
FightSystem.OnUnitKilled
  │
  ├── 记录 KillerId / KillerSkillId
  ├── 怪物 → map.OnMonsterKilled()（直接调用，不经过事件）
  │             ├── 移除地图对象
  │             ├── 计算并发放奖励
  │             └── 设置刷新定时器
  ├── 玩家 → PublishAsync(PlayerDeathEvent)
  └── 广播 KilledByNtf
```

## FightAttribute 转换

`RoleAttributeComponent.ToFightAttribute()` 将持久化属性转换为战斗属性。这是 Player → Fight 的唯一数据桥梁：

- 玩家的 PhyAttMin/PhyAttMax/PhyDef → FightAttribute
- 怪物的属性直接从 MonsterConfig 构造 FightAttribute 传入

## 跨模块协作

```
                    ┌─────────────┐
                    │   Config    │ SkillConfig 配置表
                    └──────┬──────┘
                           │
┌──────────┐    ┌──────────▼──────────┐    ┌──────────┐
│   Map    │◄──►│      Fight          │◄──►│ Player   │
│ 对象查找  │    │ 伤害/死亡/技能/复活   │    │ 属性转换  │
└──────────┘    └──────────┬──────────┘    └──────────┘
                           │
                    ┌──────▼──────┐
                    │  GameTask   │ MonsterKilledEvent 驱动任务
                    └─────────────┘
```

| 交互方 | 方向 | 内容 |
|--------|------|------|
| Map | 双向 | FindMapObject 查找、AOI 广播；FightUnitComponent 挂载在 MapObjectEntity 上 |
| Player | 单向 | `RoleAttributeComponent.ToFightAttribute()` 转换属性；`SendToPlayer` 推送通知 |
| Monster | 双向 | AI 攻击 → UseSkill；OnUnitKilled → `map.OnMonsterKilled()` |
| Bag | 单向（反向） | ItemEffectSystem 复活效果 → `FightSystem.Revive()` |
| GameTask | 被动 | `MonsterKilledEvent` 驱动击杀任务进度 |
| Config | 单向 | 读取 `SkillConfig` 驱动伤害参数 |

## 已知局限

1. **仅物攻计算**：FightAttribute 有 MagicAtt 字段但 FightCore 未使用
2. **MP 校验暂不实现**：ErrorMessage "MP不足" 已定义但无实际校验，短期不接入技能消耗
3. **单目标仇恨**：ThreatMap 存在但未在 AI 中实际使用（AI 用简单距离判定）
