# Fight 模块文档

## 概述

Fight 模块实现战斗系统，包含技能使用、伤害计算、命中/暴击/闪避判定、HP管理、死亡/复活、PK模式管理。战斗单元（FightUnit）在进入地图时注册，离开地图时注销。无数据库持久化，纯内存运算。

## 文件清单

| 文件 | 行数 | 职责 |
|------|------|------|
| `server/Game/Components/Fight/FightComponent.cs` | 16 | Module 级 Component，战斗单元集合 |
| `server/Game/Components/Fight/FightSystem.cs` | 345 | 战斗操作扩展方法 |
| `server/Game/Components/Fight/FightHandlers.cs` | 54 | 消息处理 + 事件订阅 |
| `server/Game/Components/Fight/FightCore.cs` | 158 | 伤害计算核心（纯函数） |
| `server/Game/Components/Fight/FightUnit.cs` | 116 | 战斗单元数据类 |
| `server/Game/Components/Fight/FightAttribute.cs` | 76 | 战斗属性（ProtoContract） |
| `server/Game/Components/Fight/FightErrorCode.cs` | 19 | 错误码常量 |
| `client/Assets/Scripts/shared/Common/Protos/FightProtos.cs` | 108 | 消息定义 + PK模式 + HP变化原因 |
| `client/Assets/Scripts/shared/Common/MsgIdConst.cs` | — | MsgId 10801-10900 段 |

## 数据模型

### FightUnit（战斗单元）

```csharp
namespace Game.Components.Fight;

public class FightUnit
{
    public long ObjectId { get; }              // 对象ID（RoleId/MonsterId）
    public int ObjectType { get; }             // MapObjectType: Player=1, Npc=2, Monster=3
    public string Name { get; }
    public int CfgId { get; }                  // 配置ID
    public FightAttribute Attr { get; }        // 战斗属性
    public Dictionary<int, Skill> Skills;      // 技能列表 skillId → Skill
    public Dictionary<int, long> SkillCoolDown; // 冷却 groupId → 可用时间戳(ms)
    public Dictionary<long, int> ThreatMap;    // 仇恨列表 targetId → 仇恨值
    public bool IsDead => Attr.Hp <= 0;
    public long KillerId { get; set; }
    public int KillerSkillId { get; set; }

    // 方法
    void AddSkill(Skill skill);
    Skill? GetSkill(int skillId);
    int TakeDamage(int damage);     // 返回实际伤害
    int Heal(int amount);           // 返回实际治疗
    void AddThreat(long targetId, int value);
    long GetMaxThreatTarget();      // 返回最大仇恨目标
    void Reset();                   // 重置满血
}
```

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

## Component

### FightComponent（Module 级）

```csharp
[ComponentOf(typeof(Module))]
public class FightComponent : Component
{
    public readonly Dictionary<long, FightUnit> Units;  // objectId → FightUnit
}
```

- 挂载目标：`Context.Server`
- Context 便利属性：`Context.Fight`
- 不持久化，运行时维护

## System 详解

### FightSystem

| 方法 | 签名 | 说明 |
|------|------|------|
| RegisterUnit | `RegisterUnit(this FightComponent, long objectId, int objectType, string name, int cfgId, FightAttribute attr) → FightUnit` | 注册战斗单元（进地图时） |
| UnregisterUnit | `UnregisterUnit(this FightComponent, long objectId)` | 注销战斗单元（离地图/下线时） |
| GetUnit | `GetUnit(this FightComponent, long objectId) → FightUnit?` | 获取战斗单元 |
| GetSkillConfig | `GetSkillConfig(int skillId) → SkillConfig?` | 获取技能配置 |
| UseSkill | `UseSkill(this FightComponent, long attackerId, int skillId, int x, int y, long targetId) → UseSkillResult` | 使用技能（核心入口） |
| Revive | `Revive(this FightComponent, long objectId, int hpPercent=100) → bool` | 复活 |
| RemoveAll | `RemoveAll(this FightComponent)` | 清除所有单元 |

#### UseSkill 流程

```
1. 校验攻击者存在且存活
2. 校验技能存在且冷却完毕
3. 校验目标存在且存活
4. 校验攻击距离
5. FightCore.UseSkill / UseSkillAoe 计算伤害
6. 处理死亡
7. 广播技能效果
8. 返回 UseSkillResult
```

### FightCore（纯函数）

| 方法 | 签名 | 说明 |
|------|------|------|
| CalculateDamage | `CalculateDamage(FightUnit attacker, FightUnit target, SkillConfig skillCfg, bool isCritical) → int` | 伤害计算 |
| IsHit | `IsHit(FightUnit attacker, FightUnit target) → bool` | 命中判定 |
| IsCritical | `IsCritical(FightUnit attacker, FightUnit target) → bool` | 暴击判定 |
| UseSkill | `UseSkill(FightUnit attacker, FightUnit target, SkillConfig) → List<SkillHitResult>` | 单体技能执行 |
| UseSkillAoe | `UseSkillAoe(FightUnit attacker, List<FightUnit> targets, SkillConfig) → List<SkillHitResult>` | AOE技能执行 |

#### 伤害公式

```
伤害 = MAX((攻击力 × 伤害系数 + 附加伤害 - 防御) × 伤害减免, 随机最小伤害) × 暴击系数
命中率 = 95 + (attacker.HitRate - target.DodgeRate) / 100，范围 [10%, 99%]
```

### 错误码

| Code | 常量 | 含义 |
|------|------|------|
| 0 | Ok | 成功 |
| 1 | NotInFight | 不在战斗中 |
| 2 | AlreadyDead | 已死亡 |
| 3 | SkillNotFound | 技能未学习 |
| 4 | SkillConfigNotFound | 技能配置不存在 |
| 5 | SkillInCd | 技能冷却中 |
| 6 | TargetNotFound | 目标不存在 |
| 7 | TargetDead | 目标已死亡 |
| 8 | OutOfRange | 超出范围 |
| 9 | NotEnoughMp | MP不足 |

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
| 10802 | UseSkillAck | S→C | Code, AttackerId, SkillId, MainTargetId, X, Y, MissedIds, IsCriticalIds, Targets(List\<SkillTargetInfo\>) |
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
  ├── 依赖 → Map（AOI 广播、FightUnit 注册/注销事件）
  ├── 依赖 → Player（Context.Players.SendToPlayer）
  ├── 依赖 → EventBus（MonsterKilledEvent/PlayerDeathEvent 发布、PlayerOffline/LeaveMap 订阅）
  │
  └── 被依赖 ←
       ├── Monster（AI攻击 → UseSkill、OnMonsterKilled 处理死亡）
       ├── Bag（ItemEffectSystem.ApplyReviveEffect → Fight.Revive）
       └── GameTask（MonsterKilledEvent 驱动任务进度）
```

## 已知待办

1. **魔法攻击**：FightAttribute 有 MagicAtt 字段但 FightCore 仅使用物攻计算
2. **BUFF/Debuff**：未实现
3. **AOE 技能**：UseSkillAoe 已定义但未在 UseSkill 流程中调用
4. **MP 消耗**：NotEnoughMp 错误码已定义但未校验
