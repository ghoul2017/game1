# 背包 UID 实例栈重构计划

目标读者：AI。

## 状态与执行边界

- 任务 ID：`GAME1-BAG-04`。
- 状态：2026-07-27 已完成代码、协议、客户端、Robot 和文档实施；真实服务端上的 Robot 任务/背包/保存重登链路已通过，完整容量边界、邮件及 Unity Editor/PlayMode 验证仍待对应测试条件。
- 本文件保留为背包重构的权威决策与验收矩阵；当前代码行为说明见 `docs/模块文档/Bag.md`。
- 本次用户已明确选择 `GAME1-BAG-04` 并授权修改代码、协议、配置、客户端、Robot、测试与文档；该文件写入授权已经用于本轮实施。
- 本计划不授权启动服务、进入 Unity PlayMode、创建账号、写入或删除 MongoDB 数据。
- 删除旧背包数据前，必须明确目标为可丢弃数据的 Demo 数据库，并再次核对连接串、数据库名和集合名；目标不明确时停止。

## 2026-07-27 实施与验证结果

- 服务端权威结构、表驱动堆叠、容量准入、内部通知、扣除和 long 任务进度已按本计划实现。
- Bag、Fight、Task 协议已破坏式硬切；Tidy、Swap、Index 及旧 Handler 已删除。
- 客户端已改为 Uid 缓存、角色生命周期保护和动态滚动 UI；Robot 已同步字典快照、单次通知和未实现使用错误断言。
- 背包开发验证统一使用 Robot 进入真实 TCP、服务端业务和 MongoDB 保存链路，不保留独立旁路测试入口。
- `task_persistence_test` 已在本地 `127.0.0.1:9000` 与 `game` 数据库通过，验证 Uid 快照、单次变化通知、任务奖励、不扣物品的稳定使用错误，以及多次断线保存和重登恢复。
- 规定的五个项目构建、客户端边界和协议检查均为 0 Error；本机缺少项目指定的 Unity 2022.3.62f3c1，因此没有用其他 Editor 版本冒充 Unity 编译或 PlayMode 验收。

## 目标

把当前“格子 Index → BagItem”背包替换为没有 Index 的 UID 实例栈：

```text
Uid → BagItem
```

每个 BagItem 表示一个可堆叠实例栈。堆叠上限来自 `ItemConfig.MaxStack`；背包保留容量值，但容量只向业务提供可选的满包判断，不是 `AddItems` 的写入限制。

## 已确认决策

1. `RoleBagComponent.Items` 使用 `Dictionary<long, BagItem>`，Key 为 Uid。
2. `BagItem` 保留 Uid、ConfigId、long Count、Bind、ExpireTime，只删除 Index。
3. 字典 Key 与 `BagItem.Uid` 重复保存，并必须始终相等。
4. 堆叠上限只读取 `ItemConfig.MaxStack`，不保留 `DefaultMaxStack` 或调用方传入的临时上限。
5. 只有 ConfigId、Bind、ExpireTime 三者完全相同的 BagItem 才能互相堆叠。
6. 添加时按 Uid 升序填充已有未满栈，再按需要创建新 Uid。
7. `RoleBagComponent.Capacity` 保留，默认值为 100。
8. 满包定义固定为 `Items.Count > Capacity`，不是大于等于。
9. 当前恰好有 Capacity 个栈时仍不算满，可以接收一个完整批次；本批次结束后允许超过 Capacity。
10. 背包模块永不因容量拒绝添加；业务模块自行决定是否在调用前检查 `IsFull()`。
11. `AddItems` 一旦开始就完整加入本批全部合法物品，不能因中途超过 Capacity 停止或部分完成。
12. 删除 `CanAddItems`，改为只表达当前状态的 `IsFull`。
13. 对外只暴露批量 `AddItems`，不公开单项 `AddItem`；单项添加只作为 `AddItems` 内部实现细节。
14. `AddItems` 内部完成背包变化推送，并返回用于获得展示的归一化道具数组。
15. 背包删除或扣减同样由背包模块内部推送最终变化；调用方不重复发送背包变化通知。
16. 当前 ExpireTime 只是堆叠元数据。无论数值是否已早于当前时间，物品都按普通物品处理。
17. 当前装备继续作为普通 BagItem；表中 `MaxStack=1` 使其保持独立 Uid。
18. 未来装备属性直接扩展 BagItem，但强化、耐久、词条、镶嵌和穿戴槽不属于本任务。
19. 删除整理、交换和所有持久化展示顺序；客户端展示顺序由 ConfigId、Uid 派生。
20. 不新增 `BagMutationPlan`、`StorageKind` 或同类中间抽象。
21. 客户端与服务端同步破坏式切换，不保留旧客户端、旧协议或滚动升级兼容层。
22. 旧 `role_bag_component` 数据直接删除，不实现旧 BSON 迁移、双读、SchemaVersion 或懒转换。

## 不在本任务范围

1. 背包扩格、容量配置表、负重或不同物品类型的独立容量。
2. 过期作废、过期清理、自动删除或过期补偿。
3. 出售、兑换、玩家主动丢弃和物品回收。
4. Bind 对交易、出售、使用或消耗优先级的影响。
5. 装备养成属性和穿戴系统。
6. 玩家手工排序、获得时间排序或服务端持久化展示顺序。
7. 旧客户端兼容、灰度发布和旧背包数据迁移。
8. `GAME1-BAG-01` 中物品效果表关联、复活卷轴 2005 效果等未决功能。
9. 改变“收集物品”任务的来源口径。
10. 改变一次领取多个邮件时现有的逐封处理语义。

## 目标数据结构

```csharp
[ComponentOf(typeof(PlayerEntity))]
public class RoleBagComponent : Component, IEntity
{
    [BsonId]
    public long Id { get; set; }

    public int Capacity { get; set; } = 100;

    [BsonDictionaryOptions(DictionaryRepresentation.ArrayOfDocuments)]
    public Dictionary<long, BagItem> Items { get; set; } = new();
}

public class BagItem
{
    public long Uid { get; set; }
    public int ConfigId { get; set; }
    public long Count { get; set; }
    public bool Bind { get; set; }
    public long ExpireTime { get; set; }
}
```

目标 BSON 逻辑形态：

```text
{
  _id: <RoleId>,
  Capacity: 100,
  Items: [
    {
      k: NumberLong(<Uid>),
      v: {
        Uid: NumberLong(<Uid>),
        ConfigId: <ConfigId>,
        Count: NumberLong(<Count>),
        Bind: <bool>,
        ExpireTime: NumberLong(<UnixMillisecondsOrOpaqueValue>)
      }
    }
  ]
}
```

不新增 Index、SchemaVersion 或展示顺序字段。

## 数据不变量

1. Uid 为正数，并在单个角色背包内唯一。
2. `Items` 的 Key 必须等于对应 `BagItem.Uid`。
3. ConfigId 为正数，并存在对应 `ItemConfig`。
4. `ItemConfig.MaxStack > 0`。
5. 每个持久化栈满足 `0 < Count <= MaxStack`。
6. 同一个 Uid 只对应一个 BagItem。
7. 不存在 Index、空格编号或服务端持久化顺序。
8. `Items.Count` 可以大于 Capacity；这不是数据损坏。
9. ExpireTime 不参与当前有效性判断，只参与堆叠身份比较。
10. 业务代码不得直接修改 Items 或 BagItem.Count；统一通过 `RoleBagSystem`。
11. 单个批次发生实际变化时只调用一次 `MarkDirty()`。
12. 非法输入必须在修改任何栈前失败，不能产生部分写入或网络通知。
13. 配置把 MaxStack 降到已有栈 Count 以下时，不自动拆栈或截断；实施或运行时发现后停止并报告数据修复需求。

## 容量与业务准入

背包只提供状态查询：

```text
IsFull = Items.Count > Capacity
```

精确语义：

1. Capacity 为 100、当前 99 栈：`IsFull == false`。
2. Capacity 为 100、当前 100 栈：`IsFull == false`。
3. Capacity 为 100、当前 101 栈：`IsFull == true`。
4. `AddItems` 不读取 `IsFull`，在任意栈数下都能被直接调用。
5. 业务在批次开始前检查且结果为未满时，整个批次获得准入；即使第一项就让栈数超过 Capacity，后续项仍必须继续完整添加。
6. 业务选择不检查时，即使当前已经超限也完整添加。
7. 超限只影响以后主动选择检查的业务，不冻结使用、扣除、查询或其他背包操作。
8. 物品被扣除后栈数重新降到 Capacity 或以下，`IsFull` 自动恢复为 false。

Capacity 是业务准入阈值，不是 `RoleBagComponent.Items` 的硬约束。禁止在 `AddItems` 内按剩余栈数裁切、失败、回退或补偿。

## 堆叠规则

堆叠身份：

```text
(ConfigId, Bind, ExpireTime)
```

添加一个归一化分组时：

1. 从 ItemConfig 读取该 ConfigId 的 MaxStack。
2. 找出三项堆叠身份完全相同且 Count 小于 MaxStack 的已有栈。
3. 按 Uid 升序逐栈填充。
4. 剩余数量按 MaxStack 拆成新栈，每栈生成新 Uid。
5. 最后一个新栈可以小于 MaxStack。
6. Bind 不同不能堆叠。
7. ExpireTime 不同不能堆叠，包括两个不同的非零时间。
8. ExpireTime 已早于当前时间也不触发特殊处理；数值相同仍可堆叠。
9. 不因配置名称为装备而改变规则；装备依靠 MaxStack=1 自然形成独立栈。

## 添加输入与目标 API

添加输入使用承担真实业务含义的简单值对象：

```text
BagItemAddData
  int ConfigId
  long Count
  bool Bind
  long ExpireTime
```

目标公开能力：

```text
bool IsFull()
BagItemAddData[] AddItems(IReadOnlyList<BagItemAddData> items)
bool RemoveItemsByUid(IReadOnlyDictionary<long, long> uidCounts)
bool RemoveItemsByConfigId(int configId, long count)
BagItem? GetItem(long uid)
long GetItemCount(int configId)
```

约束：

1. 不公开 `AddItem`。
2. `BagItemAddData` 是添加输入和获得展示数据，不是 mutation plan，也不保存预计算后的栈变更。
3. `AddItems` 不接收容量策略参数；是否调用 `IsFull` 完全由业务决定。
4. 空输入返回空数组，不标脏、不推送。
5. 非空输入先完整物化并验证 ConfigId、Count、MaxStack 和数组结构，再修改权威状态。
6. 重复输入按 ConfigId、Bind、ExpireTime 聚合；聚合使用 checked long 运算，溢出属于配置或程序不变量错误。
7. 合法批次始终完整添加，返回的展示数组 Count 总和与请求一致。
8. 返回数组按每个堆叠身份在输入中的首次出现顺序排列。
9. 所有当前来源默认传 `Bind=false, ExpireTime=0`；未来来源需要其他元数据时必须显式传入。
10. `AddItems` 内部最多调用一次 `MarkDirty()`，随后发送一次聚合的 `BagItemChangedNtf`。
11. `AddItems` 返回前已经完成背包变化通知；调用方不得再次发送同一背包通知。
12. 调用方使用返回数组生成获得提示、任务响应或日志；展示数组表示本次获得量，不表示拆栈后的最终 Count。

`AddItems` 的私有算法可以拆出单分组辅助方法，但该方法不能成为其他模块可调用的公共 `AddItem`。

内部实现分三段完成：先验证并归一化全部输入，再用局部集合计算已有栈最终值和待创建 BagItem，最后一次性写回权威 Items。局部集合只是方法内计算状态，不定义 `BagMutationPlan` 类型，也不跨调用保存。完成写回后只标脏一次，并用同一份最终变化集合构造通知。

## 添加通知与展示返回

一次 AddItems 同时产生两种不同数据：

### 背包变化通知

由背包模块内部构造，内容是本批实际修改或新建栈的最终绝对快照：

```text
Dictionary<Uid, BagItemInfo>
```

例如已有一栈 Count=50、MaxStack=99，本次增加 150，最终通知包含三个栈，Count 分别为 99、99、2。

### 获得展示返回

返回调用方的是按堆叠身份聚合的本次获得量：

```text
[{ ConfigId, Count=150, Bind, ExpireTime }]
```

它不暴露拆栈数量，也不把最终栈 Count 误当成本次获得量。调用方可以把每项转换为 `ItemGainNtf`、任务奖励响应或日志。当前展示协议只需要 ConfigId 和 long Count；Bind、ExpireTime 保留在返回数据中供未来表现扩展。

## 扣除、查询与通知

### 按 Uid 扣除

`RemoveItemsByUid`：

1. 先验证全部 Uid 存在、请求数量为正且余额足够。
2. 任一项失败时完全不修改。
3. 扣后 Count 大于 0 的栈保留并发送最终绝对快照。
4. 扣后 Count 等于 0 的栈从字典删除，并把 Uid 放入 `RemovedUids`。
5. 成功批次只标脏一次、只发送一次变化通知。

### 按 ConfigId 扣除

`RemoveItemsByConfigId`：

1. 当前 Bind、ExpireTime 都不改变可消耗性。
2. 按 Uid 升序选择同 ConfigId 栈。
3. 先汇总验证总量足够，再执行扣除。
4. 通知规则与按 Uid 扣除相同。

### 查询

1. `GetItem(uid)` 按 Uid 查询；调用方不得直接修改返回对象。
2. `GetItemCount(configId)` 汇总该 ConfigId 的全部栈，包括任意 Bind、ExpireTime 和已经早于当前时间的 ExpireTime。
3. 汇总使用 checked long 运算；若权威数据无法用 long 表示，按数据不变量错误暴露，不做静默饱和。

未来出售、兑换或真正的过期功能若需要不同选择规则，应由对应业务先选择 Uid，再调用按 Uid 扣除；不能在当前背包核心中预埋未确认策略。

## 当前业务容量策略

### 任务奖励

1. 校验任务存在、已完成且未提交。
2. 解析完整奖励，并验证 ConfigId、正数量和 ItemConfig.MaxStack。
3. 调用 `IsFull`；只有当前 `Items.Count > Capacity` 时返回“背包已满”，任务状态不变。
4. 未满时先写任务已提交状态并标脏。
5. 调用 `AddItems` 完整发放；即使本批导致超限也不能中断。
6. 背包变化由 `AddItems` 内部推送。
7. `TaskSubmitAck.Rewards` 使用 AddItems 返回的展示数组，并把 Count 改为 long。

### 邮件附件

保持按 MailId 逐封处理：

1. 每封邮件先验证状态和全部附件。
2. 在处理当前邮件前调用 `IsFull`。
3. 当前已超限时，不领取该邮件并停止处理后续邮件；此前已经领取成功的邮件保持成功。
4. 当前未满时，先写该邮件已领取状态并标脏，再调用 `AddItems`。
5. 一个邮件批次获得准入后必须完整加入全部附件，即使结果超过 Capacity。
6. 如果第一封邮件使背包从 100 栈变为 105 栈，同一请求中的下一封邮件会因 `IsFull == true` 保持未领取。
7. 当前邮件来源使用 `Bind=false, ExpireTime=0`。

### 怪物掉落与通用 GrantItem

1. 怪物死亡是不可挂起来源，不调用 `IsFull`。
2. 当前已经超限也直接调用 `AddItems` 完整添加。
3. 使用返回的展示数组发送获得提示、记录日志和发布既有 `PlayerItemGainedEvent`。
4. `PlayerItemGainedEvent.Count` 改为 long。
5. 不转邮件、不落地、不重试、不补偿。

以后新增发放业务必须在调用点明确记录是否检查 `IsFull`；不得把容量策略重新塞回 AddItems，也不得给 AddItems 增加含义不清的 bool 参数。

## 收集任务的 long 转换

任务目标的 CurrentCount 和 TargetCount 继续使用 int，获得物品事件 Count 使用 long。`RoleTaskSystem.UpdateProgress` 的数量入口改为 long，并按目标剩余量裁切：

```text
remaining = (long)goal.TargetCount - goal.CurrentCount
increment = min(gainedCount, remaining)
goal.CurrentCount += (int)increment
```

只有已经证明处于 0 到 int.MaxValue 范围内的 increment 可以转换为 int。不得先把 gainedCount 转为 int。

## 使用物品

1. `BagUseItemReq` 只携带 Uid，不携带 Index 或 Count。
2. 当前协议固定表达“使用该 Uid 的一件物品”。
3. 在 `GAME1-BAG-01` 完成前，Handler 对所有请求返回稳定错误“道具使用功能未完成”。
4. 返回该错误时不扣物品、不修改属性、不发送背包变化通知。
5. 未来实现效果链时，通过 Uid 校验并调用 `RemoveItemsByUid({ uid: 1 })`。
6. 当前 ExpireTime 即使早于当前时间也不阻止使用；真正过期规则由未来任务另行修改。

## 协议硬切

直接修改 `client/Assets/Scripts/shared/Common/Protos/BagProtos.cs`。

目标协议：

```text
QueryBagAck
  tag 1: string ErrorMessage
  tag 2: Dictionary<long, BagItemInfo> Items
  tag 3: int Capacity

BagUseItemReq
  tag 1: long Uid
  删除 tag 2 Count

BagUseItemAck
  tag 1: string Error
  删除 tag 2 ConfigId

BagItemChangedNtf
  tag 1: Dictionary<long, BagItemInfo> Items
  tag 2: List<long> RemovedUids

BagItemInfo
  tag 1: long Uid
  tag 2: int ConfigId
  tag 3: long Count
  删除 tag 4 Index
  tag 5: bool Bind
  tag 6: long ExpireTime
```

同时删除：

```text
BagTidyReq
BagTidyAck
BagSwapReq
BagSwapAck
```

协议语义：

1. `QueryBagAck.Items` 是完整快照。
2. 字典 Key 必须等于 `BagItemInfo.Uid`。
3. `BagItemChangedNtf.Items` 是变化栈的最终绝对快照，不是 delta。
4. `RemovedUids` 只表示已经从权威字典删除的栈。
5. 同一通知中一个 Uid 不能同时出现在 Items 和 RemovedUids。
6. Capacity 可以小于当前 Items.Count，客户端必须接受并显示超限状态。
7. 本次硬切有意破坏旧客户端；服务端、客户端和 Robot 必须作为同一切换单元。

保留 `ItemGainNtf` 作为获得展示，并把 Count 改为 long。`TaskRewardInfo.Count` 和 `PlayerItemGainedEvent.Count` 同步改为 long。

## 客户端目标

`BagController` 维护：

```text
Dictionary<long, BagItemInfo> Items
int Capacity
```

Controller 负责：

```text
ReplaceSnapshot
ApplyChangedItems
RemoveUids
GetItem(uid)
UseItem(uid)
ResetForRole
```

生命周期：

1. 收到 `EnterGameNtf` 并确定 RoleId 后重置旧角色缓存。
2. 查询一次完整背包快照并原子替换 Items 和 Capacity。
3. 运行中只应用 `BagItemChangedNtf` 的绝对栈快照与 RemovedUids。
4. 快照返回时再次核对 RoleId，丢弃旧角色迟到响应。
5. 瞬时断线期间可以保留只读快照，但禁止发送背包操作。
6. 重连重新进入角色后再次全量查询。
7. 被踢、永久重连失败、退出角色或切换角色时清空缓存。
8. 进角时查询失败不阻止进图，标记未加载；打开背包时允许重试。

UI：

1. 一个 Uid 显示一个实例栈。
2. 按 ConfigId、Uid 升序生成展示列表。
3. 使用动态滚动容器，不再固定创建 64 个 Index 格子。
4. 显示 `Items.Count / Capacity`；超限时允许显示如 `105 / 100`。
5. 删除整理按钮和交换入口。
6. 点击条目时捕获 Uid，不把显示序号回传服务端。
7. Count 使用 long 显示，第一版直接显示完整十进制数。
8. Bind、ExpireTime 随快照缓存；当前不因它们改变显示或可用状态。
9. 使用确认框固定显示 `x1`。

## 配置变更

保留 ItemConfig.csv 的 MaxStack 列，并把共享配置属性改为 long：

```text
public long MaxStack { get; set; }
```

规则：

1. MaxStack 必须大于 0。
2. 所有添加只使用对应 ConfigId 的表格 MaxStack。
3. 删除 `RoleBagSystem.DefaultMaxStack`。
4. 调用方不能覆盖 MaxStack。
5. 木剑、铁剑、皮甲等当前装备配置继续使用 MaxStack=1。
6. 本任务不新增 StorageKind 或 ItemType。

## ExpireTime 当前语义

ExpireTime 在本任务中是普通 long 元数据：

1. 0 和非 0 都是合法值。
2. 不与当前时间比较。
3. 不自动作废、不清理、不隐藏。
4. 计入 Items.Count 和 IsFull 判断。
5. 可被查询、使用和按 ConfigId 扣除。
6. 只有数值完全一致时才能堆叠。

未来出售、兑换、过期作废或清理功能必须重新定义相应业务边界，本计划不预写规则。

## 持久化与 Demo 数据重建

新旧 Items 使用相同字段名，但 BSON Key 从 Index 变为 Uid，BagItem 删除 Index 且 Count 改为 Int64，不能混用。

本任务不增加兼容读取。切换步骤：

1. 停止目标 Demo 服务，确认没有继续写入玩家数据的进程。
2. 核对并记录 MongoDB 连接串标识、数据库名和集合名。
3. 查询并记录删除前 `role_bag_component` 的文档数。
4. 明确取得该 Demo 数据库 `role_bag_component` 可删除授权。
5. 对精确目标执行 drop collection，只删除 `role_bag_component`。
6. 在新服务启动前重新读取集合列表和文档数，确认目标集合不存在或为 0。
7. 同步切换服务端和客户端协议构建。
8. 启动新服务；玩家首次登录时创建 Capacity=100 的空 UID 背包。
9. 完成添加、扣除、超限、断线保存和重登验证。

任一目标核对、drop 或删除后复查失败时停止实施，不得启动新服务。不得临时增加双读，也不得把反序列化失败当作空背包覆盖旧数据。

## 实施文件清单

### 服务端

- `server/Components/Bag/RoleBagComponent.cs`
- `server/Components/Bag/BagItem.cs`
- `server/Components/Bag/BagItemAddData.cs`（新增）
- `server/Components/Bag/RoleBagSystem.cs`
- `server/Components/Bag/RoleBagHelper.cs`
- `server/Components/Bag/Handlers/QueryBagReqHandler.cs`
- `server/Components/Bag/Handlers/PlayerOnlineLoadBagEventHandler.cs`
- `server/Components/Bag/Handlers/BagUseItemReqHandler.cs`
- `server/Components/Bag/Handlers/BagTidyReqHandler.cs`（删除）
- `server/Components/Bag/Handlers/BagSwapReqHandler.cs`（删除）
- `server/Components/Player/PlayerSystem.cs`
- `server/Components/Mail/Handlers/MailGetItemReqHandler.cs`
- `server/Components/GameTask/Handlers/TaskSubmitReqHandler.cs`
- `server/Components/GameTask/RoleTaskHelper.cs`
- `server/Core/Event/EventType.cs`
- `server/Components/GameTask/Handlers/PlayerItemGainedTaskProgressEventHandler.cs`
- `server/Components/GameTask/RoleTaskSystem.cs`

### 共享协议与配置

- `client/Assets/Scripts/shared/Common/Protos/BagProtos.cs`
- `client/Assets/Scripts/shared/Common/Protos/FightProtos.cs`
- `client/Assets/Scripts/shared/Common/Protos/TaskProtos.cs`
- `client/Assets/Scripts/shared/Common/Config/ItemConfig.cs`
- `client/Assets/StreamingAssets/Tables/ItemConfig.csv`（保留 MaxStack 列和值）

### 客户端

- `client/Assets/Scripts/Logic/Bag/BagController.cs`
- `client/Assets/Scripts/Logic/Bag/Handlers/BagItemChangedNtfHandler.cs`
- `client/Assets/Scripts/Logic/RoleSelect/Handlers/EnterGameNtfHandler.cs`
- `client/Assets/Scripts/Logic/Core/Handlers/KickNtfHandler.cs`
- `client/Assets/Scripts/Logic/Core/NetClient.cs`
- `client/Assets/Scripts/View/Panels/BagPanel.cs`
- 现有角色退出和切换入口

### Robot、测试和文档

- `robot/TaskPersistenceTest.cs`
- `docs/test-manual.md`
- `docs/测试列表.md`
- `docs/模块文档/Bag.md`
- `docs/模块文档/Mail.md`
- `docs/计划任务.md`
- `docs/缺陷列表.md`
- `docs/工作记录/YYYY-MM.md`
- 中枢 `projects/game1/line-kb/` 中实际受影响条目

## 实施顺序

1. 重新检查项目路由、Git 分支、远端、完整工作树和计划文件归属。
2. 修改 BagItemInfo、Bag 协议和 long Count，使编译错误暴露旧 Index、Tidy、Swap 消费点。
3. 修改 ItemConfig.MaxStack 类型并保留 CSV 权威列。
4. 重写 RoleBagComponent 和 RoleBagSystem，建立 Uid Key、不变量、堆叠与内部通知。
5. 修改任务、邮件和怪物掉落的容量策略与 AddItems 调用顺序。
6. 把使用物品 Handler 收口为按 Uid 的未完成功能错误。
7. 重写客户端 UID 缓存、角色生命周期和动态背包 UI。
8. 修改 Robot 和测试入口。
9. 完成静态检查和各目标构建。
10. 获得运行授权后，停止目标 Demo 服务并删除指定背包集合。
11. 同步运行新客户端与服务端，完成真实运行、持久化和 UI 验证。
12. 更新当前模块文档、测试列表、缺陷/计划条目、工作记录和 line-kb。

## 静态与构建验证

在仓库根目录执行：

```text
dotnet build shared/Shared.csproj
dotnet build server/Server.csproj
dotnet build client-logic/ClientLogic.csproj
dotnet build robot/Robot.csproj
dotnet build tools/tools.csproj
bash scripts/check-client-boundary
bash scripts/check-protocol
```

只有 Unity 生成项目存在时，才补充：

```text
dotnet build game1.sln
```

客户端 Logic、View、资源和 UI 变更还必须使用项目规定版本的 Unity Editor 编译并检查 Error 日志；.NET 构建不能替代 Unity 编译。

静态检索必须确认背包相关业务代码中不再残留：

```text
BagItem.Index
BagItemInfo.Index
GetByIndex
RemoveByIndex
FindEmptyIndex
DefaultMaxStack
CanAddItems
BagTidy
BagSwap
RemovedIndices
```

同时确认：

```text
RoleBagComponent.Items 的 Key 为 long
BagItem.Count 为 long
ItemConfig.MaxStack 为 long
IsFull 使用 Items.Count > Capacity
AddItems 内没有容量判断
所有外部添加入口只调用 AddItems
```

历史工作记录和本计划中的文字命中不属于代码残留。

## 序列化验证

`scripts/check-protocol` 不能替代真实运行。开发阶段通过 Robot 连接构建后的服务端，并经 MongoDB 保存/重登覆盖 protobuf 与 BSON 链路；需要预置边界数据的场景仍按以下矩阵补充：

1. `QueryBagAck` tag 2 的 `Dictionary<long, BagItemInfo>` 往返。
2. Uid、Count、ExpireTime 大于 int.MaxValue 时不截断。
3. 字典 Key 与 BagItemInfo.Uid 保持一致。
4. `QueryBagAck.Capacity` 可以小于 Items.Count。
5. `BagItemChangedNtf.Items` 最终快照往返。
6. `RemovedUids` 的 long Uid 往返。
7. `BagUseItemReq` tag 1 正确落到 Uid。
8. `TaskRewardInfo.Count` 和 `ItemGainNtf.Count` 的 long 值往返。
9. 删除的 Tidy、Swap 消息和 Handler 不再注册。
10. MongoDB `Dictionary<long, BagItem>` 使用 ArrayOfDocuments 保存并恢复。

## 行为验证矩阵

### 堆叠与 Uid

1. 空背包增加单项，生成正 Uid 且 Key 等于 BagItem.Uid。
2. 同 ConfigId、Bind、ExpireTime 填充已有栈。
3. 超过 MaxStack 后拆成多个新 Uid。
4. Bind 不同产生不同栈。
5. ExpireTime 不同产生不同栈。
6. 相同且已经早于当前时间的 ExpireTime 仍按普通物品堆叠。
7. 多个可填充栈按 Uid 升序处理。
8. MaxStack=1 的装备每件生成独立 Uid。
9. 批量重复输入按三项堆叠身份聚合。
10. 非法 ConfigId、非正 Count、缺失配置和非正 MaxStack 在修改前失败。
11. 配置错误批次不标脏、不通知、不产生部分栈。
12. AddItems 返回获得展示量，通知发送最终栈 Count。

### 容量

1. 99/100 栈时 IsFull 为 false。
2. 100/100 栈时 IsFull 仍为 false。
3. 101/100 栈时 IsFull 为 true。
4. 100 栈时一个批次需要多个新栈，整批成功并允许远超 100。
5. 当前已经超限时直接调用 AddItems 仍完整成功。
6. AddItems 在批次中途超过 Capacity 后继续处理剩余项。
7. 扣除整栈使栈数降到 100 后 IsFull 恢复为 false。
8. ExpireTime 已经过时的栈仍计入 Items.Count。

### 业务链

1. 任务在 101/100 时拒绝提交且背包、任务状态不变。
2. 任务在 100/100 时提交成功，奖励可以使背包超限。
3. 邮件在 101/100 时保持未领取。
4. 同一请求第一封邮件使背包超限后，下一封邮件停止领取。
5. 获得准入的单封邮件附件必须全部添加。
6. 怪物掉落在超限状态下仍完整添加。
7. 任务、邮件和掉落的背包通知都只由 AddItems 发送一次。
8. 获得提示、任务响应和日志使用 AddItems 返回的展示数组。
9. 收集任务使用 long 获得量但不会溢出 int 目标。
10. 使用请求按 Uid 稳定返回“道具使用功能未完成”，不改变状态。

### 扣除与通知

1. 按 Uid 部分扣除发送最终 Count。
2. 按 Uid 扣到 0 发送 RemovedUids。
3. 多 Uid 批量扣除全有全无。
4. 按 ConfigId 扣除按 Uid 升序。
5. Bind 和 ExpireTime 不改变当前按 ConfigId 扣除资格。
6. 同一 Uid 不同时出现在变化字典与 RemovedUids。
7. 调用方不重复发送背包通知。

### 客户端与持久化

1. 空快照和多 Uid 完整快照。
2. 变化栈绝对数量覆盖。
3. RemovedUids 删除缓存。
4. ConfigId、Uid 升序稳定。
5. 超过 64 个栈仍可通过滚动 UI 访问。
6. 超限容量显示正确。
7. 点击使用发送 Uid，不发送显示序号。
8. 被踢、退出角色、切换角色和永久重连失败后缓存清空。
9. 瞬时重连后全量快照覆盖旧缓存。
10. 旧角色迟到快照不能覆盖当前角色。
11. 定时保存、断线保存和正常停服 Flush 后重登恢复 Uid、long Count、Bind、ExpireTime 和 Capacity。
12. 旧客户端不能与新服务端混用。

## 停止条件

出现以下任一情况时停止实施并报告：

1. 真实项目路径、Git 根或 origin 与项目登记不一致。
2. 计划文件或目标代码存在归属不明的用户改动。
3. 发现当前业务需要 Index 或服务端持久化手工顺序。
4. 新增物品来源没有明确选择“检查 IsFull”还是“无条件添加”。
5. 调用方在 AddItems 之后仍要执行可能失败的权威状态修改；内部即时通知会导致顺序错误。
6. 发现 MaxStack 将被降低到已有持久化栈 Count 以下。
7. 需要决定 Bind、ExpireTime、出售、兑换或装备养成的未来业务语义。
8. 目标 Demo MongoDB 连接串、数据库名或集合删除授权不明确。
9. 不能同步切换客户端与服务端。
10. 测试需要启动服务、进入 PlayMode、创建账号或写库，但当前未获得相应环境与数据授权。
11. 构建、生成或测试产生计划外 Git 跟踪文件变化或非忽略新文件。

## 完成定义

只有同时满足以下条件才可把 `GAME1-BAG-04` 记为完成：

1. 服务端权威数据为 `Uid → BagItem`，且不存在 Index。
2. Uid Key、BagItem.Uid、long Count 和表驱动 MaxStack 不变量成立。
3. AddItems 是唯一公开添加入口，内部不检查容量并完整添加合法批次。
4. IsFull 精确使用 `Items.Count > Capacity`，当前业务容量策略符合本计划。
5. Bind、ExpireTime 一致性堆叠和当前普通物品语义正确。
6. 背包模块内部发送一次最终变化通知，返回获得展示数组。
7. 协议按原消息完成破坏式替换，客户端与 Robot 已同步。
8. 客户端以 Uid 缓存和展示，动态 UI、容量和角色生命周期正确。
9. 受影响构建、边界检查、协议检查和 Robot 真实链路验证通过。
10. MongoDB 保存/重登和 Unity UI 验证通过；环境缺失项明确记录为未验证。
11. 当前模块文档、测试列表、缺陷/计划任务、工作记录和 line-kb 已按实际结果收口。
