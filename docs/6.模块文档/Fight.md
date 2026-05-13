# Fight 模块文档

## 概述

Fight 模块实现战斗系统，包含技能使用、伤害计算、命中/暴击/闪避判定、HP管理、死亡/复活、PK模式管理。战斗单元（FightUnitComponent）挂载在 MapObjectEntity 上，进入地图时注册，离开地图时注销。无数据库持久化，纯内存运算。

## 文件清单

| 文件 | 行数 | 职责 |
|------|------|------|
| `server/Game/Components/Fight/FightSystem.cs` | 403 | 战斗系统（FightUnitComponent扩展方法 + 技能使用 + 死亡处理） |
| `server/Game/Components/Fight/FightHandlers.cs` | 54 | 消息处理 + 事件订阅 |
| `server/Game/Components/Fight/FightCore.cs` | 158 | 伤害计算核心（纯函数） |
| `server/Game/Components/Fight/FightUnitComponent.cs` | 30 | 战斗单元 Component |
| `server/Game/Components/Fight/FightAttribute.cs` | 76 | 战斗属性（ProtoContract） |
| `server/Game/Components/Fight/Skill.cs` | — | 技能数据类 |
| `client/Assets/Scripts/shared/Common/Protos/FightProtos.cs` | 108 | 消息定义 + PK模式 + HP变化原因 |
| `client/Assets/Scripts/shared/Common/MsgIdConst.cs` | — | MsgId 10801-10900 段 |

## 数据模型

### FightUnitComponent（战斗单元 Component）

```csharp
[ComponentOf(typeof(MapObjectEntity))]
public class FightUnitComponent : Component
{
    public FightAttribute Attr = new();
    public Dictionary<int, Skill> Skills = new();
    public Dictionary<int, long> SkillCoolDown = new();
    public Dictionary<long, int> ThreatMap = new();
    public bool IsDead => Attr.Hp <= 0;
    public long KillerId;
    public int KillerSkillId;
}
```

- 挂载目标：MapObjectEntity（MapPlayerEntity / MonsterEntity）
- 不持久化，运行时数据

### FightAttribute（战斗属性）

```csharp
[ProtoContract]
public class FightAttribute
{
    [ProtoMember(1)]  public int PhyAttMin { get; set; }       // 物攻最小
    [ProtoMember(2)]  public int PhyAttMax { get; set; }       // 物攻最大
    [ProtoMember(3)]  public int MagicAttMin { get; set; }     // 魔攻最小
    [ProtoMember(4)]  public int MagicAttMax { get; set; }     // 魔攻最大
    [ProtoMember(5)]  public int PhyDefMin { get; set; }       // 物防最小
    [ProtoMember(6)]  public int PhyDefMax { get; set; }       // 物防最大
    [ProtoMember(7)]  public int MagicDefMin { get; set; }     // 魔防最小
    [ProtoMember(8)]  public int MagicDefMax { get; set; }     // 魔防最大
    [ProtoMember(9)]  public int MaxHp { get; set; }           // 最大HP
    [ProtoMember(10)] public int CriticalRate { get; set; }    // 暴击率（万分比）
    [ProtoMember(11)] public int CriticalDamageRate { get; set; } // 暴击伤害加成（万分比）
    [ProtoMember(12)] public int DodgeRate { get; set; }       // 闪避率（万分比）
    [ProtoMember(13)] public int HitRate { get; set; }         // 命中率（万分比）
    [ProtoMember(14)] public int DamagePct { get; set; }       // 伤害加成（万分比）
    [ProtoMember(15)] public int DamageReducePct { get; set; } // 伤害减免（万分比）
    [ProtoMember(16)] public int Hp { get; set; }              // 当前HP
}
```

- 兼容 Protobuf 序列化（用于战斗通知）
- RoleAttributeComponent.ToFightAttribute() 转换生成

## System 详解

### FightSystem

FightSystem 不再依赖 FightComponent，所有操作通过 MapObjectEntity 上的 FightUnitComponent。

#### FightUnitComponent 扩展方法

| 方法 | 签名 | 说明 |
|------|------|------|
| TakeDamage | `TakeDamage(this FightUnitComponent, int damage) → int` | 受伤，返回实际扣血 |
| Heal | `Heal(this FightUnitComponent, int amount) → int` | 回血，返回实际回血 |
| AddThreat | `AddThreat(this FightUnitComponent, long targetId, int value)` | 添加仇恨 |
| GetMaxThreatTarget | `GetMaxThreatTarget(this FightUnitComponent) → long` | 获取仇恨最高目标 |
| AddSkill | `AddSkill(this FightUnitComponent, Skill skill)` | 添加技能 |
| GetSkill | `GetSkill(this FightUnitComponent, int skillId) → Skill?` | 获取技能 |
| ResetFightState | `ResetFightState(this FightUnitComponent)` | 重置战斗状态（满血复活） |

#### 战斗单元管理

| 方法 | 签名 | 说明 |
|------|------|------|
| RegisterUnit | `RegisterUnit(this MapObjectEntity, FightAttribute attr) → FightUnitComponent` | 注册战斗单元 |
| UnregisterUnit | `UnregisterUnit(this MapObjectEntity)` | 注销战斗单元 |
| GetFightUnit | `GetFightUnit(long objectId) → FightUnitComponent?` | 通过全局查找获取战斗单元 |

#### 技能使用

| 方法 | 签名 | 说明 |
|------|------|------|
| UseSkill | `UseSkill(long attackerId, int skillId, int x, int y, long targetId) → UseSkillResult` | 使用技能（静态方法） |
| Revive | `Revive(long objectId, int hpPercent=100) → bool` | 复活 |

#### UseSkill 流程

```
1. 校验攻击者存在且存活
2. 校验技能存在且冷却完毕
3. 校验目标存在且存活
4. 校验攻击距离
5. FightCore.UseSkill 计算伤害
6. 处理死亡
7. 广播技能效果
8. 返回 UseSkillResult
```

### FightCore（纯函数）

| 方法 | 签名 | 说明 |
|------|------|------|
| CalculateDamage | `CalculateDamage(FightUnitComponent attacker, FightUnitComponent target, SkillConfig, bool isCritical) → int` | 伤害计算 |
| IsHit | `IsHit(FightUnitComponent attacker, FightUnitComponent target) → bool` | 命中判定 |
| IsCritical | `IsCritical(FightUnitComponent attacker, FightUnitComponent target) → bool` | 暴击判定 |
| UseSkill | `UseSkill(FightUnitComponent attacker, long attackerId, FightUnitComponent target, long targetId, SkillConfig) → List<SkillHitResult>` | 单体技能执行 |
| UseSkillAoe | `UseSkillAoe(FightUnitComponent attacker, List<FightUnitComponent> targets, SkillConfig) → List<SkillHitResult>` | AOE技能执行 |

### 错误处理

UseSkillAck 使用 `ErrorMessage` 字段，成功时为空字符串，失败时为中文错误文本（如 "技能未学习"、"技能冷却中" 等）。客户端通过 `!string.IsNullOrEmpty(ack.ErrorMessage)` 判断失败。

## Handler 详解

### 消息处理

| Handler | MsgId | 请求 | 响应 | 说明 |
|---------|-------|------|------|------|
| UseSkillHandler | 10801→10802 | UseSkillReq(skillId,x,y,targetId) | UseSkillAck | 使用技能 |
| SetPkModeHandler | 10805→10806 | SetPkModeReq(pkMode) | PkModeChangedNtf | 设置PK模式 |

### 事件订阅

| 事件 | 方法 | 说明 |
|------|------|------|
| PlayerOfflineEvent | OnPlayerOffline | 注销战斗单元 |
| PlayerLeaveMapEvent | OnPlayerLeaveMap | 注销战斗单元 |

### 发布事件

| 事件 | 触发场景 |
|------|---------|
| MonsterKilledEvent | 怪物被击杀 |
| PlayerDeathEvent | 玩家被击杀 |

## 协议汇总

| MsgId | 消息类 | 方向 | 关键字段 |
|-------|--------|------|----------|
| 10801 | UseSkillReq | C→S | SkillId, X, Y, TargetId |
| 10802 | UseSkillAck | S→C | ErrorMessage, AttackerId, SkillId, MainTargetId, X, Y, MissedIds, IsCriticalIds, Targets(List\<SkillTargetInfo\>) |
| 10803 | KilledByNtf | S→C | KillerId, KillerName, IsMonster |
| 10804 | HpChangedNtf | S→C | ObjectId, DeltaHp, Hp, Reason |
| 10805 | SetPkModeReq | C→S | PkMode |
| 10806 | PkModeChangedNtf | S→C | PkMode |
| 10807 | ExpChangedNtf | S→C | Exp, Level |
| 10808 | GoldChangedNtf | S→C | Gold |
| 10809 | LevelUpNtf | S→C | Level, MaxHp, MaxMp |
| 10810 | ItemGainNtf | S→C | ConfigId, Count |
| 10811 | MpChangedNtf | S→C | ObjectId, DeltaMp, Mp, Reason |

辅助常量：
- `PkMode`：Peace=0, Team=1, Guild=2, All=3
- `HpChangeReason`：SkillDamage=1, Heal=2, Killed=3, Revive=4, Buff=5, Recover=6

共享结构 `SkillTargetInfo`：TargetId, DeltaHp, Hp, IsCritical

## 模块依赖

```
Fight 模块
  ├── 依赖 → Config（SkillConfig 配置表）
  ├── 依赖 → Map（FindMapObject 查找、AOI 广播、FightUnitComponent 注册/注销）
  ├── 依赖 → Player（Context.Players.SendToPlayer）
  ├── 依赖 → EventBus（MonsterKilledEvent/PlayerDeathEvent 发布、PlayerOffline/LeaveMap 订阅）
  │
  └── 被依赖 ←
       ├── Monster（AI攻击 → UseSkill、OnMonsterKilled 处理死亡）
       ├── Bag（ItemEffectSystem.ApplyReviveEffect → FightSystem.Revive）
       └── GameTask（MonsterKilledEvent 驱动任务进度）
```

## 已知待办

1. **魔法攻击**：FightAttribute 有 MagicAtt 字段但 FightCore 仅使用物攻计算
2. **BUFF/Debuff**：未实现
3. **AOE 技能**：UseSkillAoe 已定义但未在 UseSkill 流程中调用
4. **MP 消耗**：ErrorMessage "MP不足" 已定义但未校验
