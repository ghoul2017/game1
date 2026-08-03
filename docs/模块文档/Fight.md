# Fight 模块

目标读者：人类。

## 定位

Fight 模块负责旧地图链路中的服务端运行时技能、命中、暴击、伤害、HP、死亡和复活。`FightUnitComponent` 挂载在 `MapPlayerEntity` 或 `MonsterEntity` 上，随地图对象创建和移除，不单独持久化。

## 运行时结构

| 文件 | 当前职责 |
| --- | --- |
| `FightUnitComponent.cs` | 战斗属性、已装备技能、冷却字典、仇恨和击杀者信息 |
| `FightCore.cs` | 随机命中/暴击、伤害计算，并在 UseSkill 中直接扣 HP、写仇恨和击杀信息 |
| `FightHelper.cs` | 全局查找、技能校验、目标收集、死亡、复活和广播编排 |
| `FightSystem.cs` | 伤害、治疗、仇恨、技能和状态重置扩展 |
| `Handlers/UseSkillReqHandler.cs` | 玩家技能 RPC；响应对象只填写 ErrorMessage |
| `Handlers/SetPkModeReqHandler.cs` | 校验并回显 PK 模式，当前不保存服务端状态 |

`FightCore` 不是无副作用纯计算：它使用进程内 `Random`，`UseSkill` 会通过 `FightSystem` 直接修改目标 HP、攻击者仇恨和击杀信息。当前没有全局 `FightComponent` 或 RegisterUnit/UnregisterUnit 链路。

地图玩家和怪物创建时都只装备技能 ID=1。`Skill.LastUseTime` 是实际冷却来源；`FightUnitComponent.SkillCoolDown` 当前只在重置时清空，没有读写消费者。

## 技能流程

```text
UseSkill(attackerId, skillId, x, y, targetId)
  ├── 校验攻击者战斗单元、存活、已装备技能和 SkillConfig
  ├── 用 Skill.LastUseTime + CdTime 校验冷却
  ├── 定位攻击者地图；单体技能校验目标存在且存活
  ├── 有 targetObj 时按切比雪夫距离校验 CastRange
  ├── 写入 Skill.LastUseTime
  ├── 按 EffectArea 收集目标
  ├── FightCore 执行命中、暴击、扣血和击杀标记
  ├── 对死亡目标执行 OnUnitKilled
  └── 向攻击者及其 AOI 周围玩家发送完整 UseSkillAck
```

冷却时间在目标集合完成前写入；非单体技能没有有效目标时仍会消耗本次冷却。

### 范围目标

| EffectArea | 当前行为 |
| --- | --- |
| 1 单体 | 使用 targetId 指定的一个存活目标 |
| 2 圆形 | 以目标坐标或请求坐标为中心，按范围取最多 MaxTarget 个对象 |
| 3 扇形 | 当前与圆形相同，没有方向或夹角判定 |
| 4 自身周围 | 以施法者为中心，但排除施法者自身 |

非单体选目标只排除攻击者和死亡对象，不检查对象类型、阵营、PK 模式或技能是伤害/治疗；没有 targetObj 时也不会校验请求坐标与施法者之间的 CastRange。

### 伤害公式

当前只使用物理攻击和物理防御：

```text
attack  = Random(PhyAttMin..PhyAttMax)
defence = Random(PhyDefMin..PhyDefMax)
base    = attack * (HurtPoint/1000 + DamagePct/10000) + ExHurt - defence
reduced = base * (10000-DamageReducePct) / 10000
damage  = max(reduced, Random(1..max(1, attack*5%)))
暴击时  damage += damage * (5000+CriticalDamageRate) / 10000
最终    max(0, damage)
```

技能等级固定按 1 级计算；`AttackType`、魔法攻防、`CostMp`、Buff 和 `HatePoint` 尚未进入流程。

## 响应与广播

`UseSkillReqHandler` 返回的 RPC `UseSkillAck` 只包含错误信息。技能成功后，`FightHelper` 另外构造一份包含命中、伤害和剩余 HP 的完整 `UseSkillAck`，作为普通消息发送给攻击者并广播给攻击者 AOI 周围玩家；客户端 `UseSkillAckHandler` 处理这份推送。

普通命中不发送独立 `HpChangedNtf`。死亡和复活路径才使用该通知。

`MapSystem.BroadcastToAoiWatchers` 通过玩家索引查找对象所在地图。攻击者或被广播对象是怪物时查找失败，因此怪物主动攻击的完整技能推送、怪物死亡 HP 广播等不会到达周围客户端。

客户端技能栏显示 1–4 四个技能，但服务端玩家只装备技能 1，技能 2–4 的请求当前都会返回“技能不存在”。

## 死亡与复活

```text
OnUnitKilled(victimId, killerId)
  ├── 向被击杀玩家发送 KilledByNtf
  ├── 尝试向被击杀对象周围广播死亡 HpChangedNtf
  ├── 怪物：map.OnMonsterKilled → 移除、掉落、刷新计时
  │          再发布 MonsterKilledEvent 驱动任务
  └── 玩家：发布 PlayerDeathEvent
```

死亡通知存在两个字段问题：`HpChangedNtf.DeltaHp` 在 HP 已经归零后取 `-victimFight.Attr.Hp`，所以恒为 0；`KilledByNtf.IsMonster` 按“被击杀者是否怪物”赋值，但客户端把它解释为“击杀者是否怪物”。实际能收到该通知的玩家受害者因此总得到 false。

`FightHelper.Revive` 只接受已死亡战斗单元，会恢复 FightAttribute HP、清仇恨/击杀信息并广播。当前物品复活没有调用这个方法，而是直接改 FightAttribute HP 并只通知本人；客户端死亡后的“回城复活”则重新发送 `EnterMapReq`，玩家仍在地图时服务端只重发地图快照，不会恢复 HP。

## Player 数据桥梁

`RoleAttributeComponent.ToFightAttribute()` 在创建地图玩家投影时复制 HP、攻防和暴击字段。之后两套对象没有同步：

- 战斗伤害和 `FightHelper.Revive` 修改地图实体的 FightAttribute。
- HP/MP 药品和升级修改持久化 RoleAttributeComponent。
- 物品复活直接修改 FightAttribute，但不更新 RoleAttributeComponent。

离图后重新创建投影会再次从 RoleAttributeComponent 复制，可能覆盖之前的战斗 HP 状态。

## 已知问题与边界

1. **战斗与持久化属性分裂**：伤害、药品、升级、复活和重新进图可能读写不同 HP/属性对象。
2. **玩家复活未闭环**：客户端复活请求只重发当前地图，服务端战斗单元仍为死亡状态。
3. **怪物战斗广播失败**：AOI 广播入口只能按玩家 ID 找地图，怪物主动攻击的中间伤害没有客户端推送。
4. **PK 模式仅客户端回显**：服务端不保存模式，目标选择也不检查和平/队伍/帮派/全体规则。
5. **范围技能缺少阵营和几何语义**：扇形按圆形处理，自身范围排除自己，所有存活对象都可能成为目标。
6. **技能字段未消费**：魔法攻击、MP 消耗、治疗/Buff、技能等级和 HatePoint 未实现。
7. **无目标也消耗冷却**：LastUseTime 在范围目标收集成功前更新。
8. **仇恨未接 AI**：伤害把目标仇恨写在攻击者 ThreatMap，Monster AI 也不读取该表。
9. **死亡通知字段错误**：Killed 的 DeltaHp 恒为 0，KilledByNtf.IsMonster 的服务端赋值语义与客户端读取语义相反。
10. **技能栏与装备不一致**：客户端暴露技能 2–4，服务端当前只给玩家技能 1。
