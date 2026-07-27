# Bag 模块

目标读者：人类。

## 定位

Bag 管理玩家的 UID 物品实例栈，是邮件附件、任务奖励和怪物掉落的最终入包入口。服务端不保存格子 Index 或手工展示顺序。

## 数据结构与不变量

数据结构为：

```text
Dictionary<long, BagItem> Items
  Key = BagItem.Uid

BagItem
  long Uid
  int ConfigId
  long Count
  bool Bind
  long ExpireTime
```

非字符串字典键使用 MongoDB `ArrayOfDocuments` 表示。Uid 必须为正数且 Key 必须等于 `BagItem.Uid`；ConfigId 必须存在，栈数量必须满足 `0 < Count <= ItemConfig.MaxStack`。

## 添加与堆叠

业务先通过 `BagItemAddDataGenerator` 将 `int[] RewardConfigs` 和 `int[] RewardCounts` 生成 `List<BagItemAddData>` normalized 数据，然后调用唯一添加入口 `AddItems(IReadOnlyList<BagItemAddData>)`：

1. 生成器验证两个数组等长、ConfigId、正数量、配置存在和正 MaxStack，并按 `(ConfigId, Bind, ExpireTime)` 聚合重复输入。
2. `AddItems` 只读取 normalized 列表，不再验证或重新归一化；只把三项堆叠身份完全一致的物品合并，按 Uid 升序填充已有未满栈。
3. 剩余数量严格按表格 `ItemConfig.MaxStack` 拆成新 Uid；装备通过 `MaxStack=1` 自然保持一件一栈。
4. 批次合法后完整写入，不读取容量，也不会因中途超限停止。
5. 实际变化后由背包内部发送一条 `BagItemChangedNtf`。通知中的 `Items` 是变化栈的最终绝对快照，不是增量。
6. 返值是 `List<BagItemAddData>` 本次获得展示量；其 Count 不是拆栈后的最终栈数量。`IReadOnlyList` 参数表达 `AddItems` 不得增删、替换或重排调用方的列表。

`ExpireTime` 当前只是普通 long 元数据：不与当前时间比较，不作废、不清理、不隐藏，仍计入容量、查询和扣除；只有数值完全相同的栈才能合并。

## 容量与业务准入

Capacity 默认是 100，但只是业务准入阈值：

```text
IsFull = Items.Count > Capacity
```

因此 100/100 仍不是满包，101/100 才是满包。一个在批次开始前取得准入的业务必须完整发放，即使结束后远超 100；直接调用 AddItems 时，即使当前已经超限也会完整添加。

| 来源 | 是否在调用 AddItems 前检查 IsFull |
| --- | --- |
| 任务提交奖励 | 检查；先写任务已提交状态，最后 AddItems |
| 邮件附件 | 按 MailId 逐封检查；先写该邮件已领取状态，最后 AddItems |
| 怪物掉落 / 通用 GrantItem | 不检查；不可挂起来源始终完整添加 |

容量不冻结查询、使用请求或扣除。整栈扣除使栈数降回 Capacity 或以下后，IsFull 自动恢复为 false。

## 扣除与查询

- `RemoveItemsByUid` 先验证整个 Uid/数量字典，任一项失败时完全不修改。部分扣除推送最终 Count，扣到零则删除栈并推送 `RemovedUids`。
- `RemoveItemsByConfigId` 汇总确认数量足够后按 Uid 升序扣除；当前 Bind 和 ExpireTime 不改变可消耗资格。
- `GetItem(uid)` 按 Uid 查询；调用方不得直接修改返回对象。
- `GetItemCount(configId)` 使用 checked long 汇总全部 Bind、ExpireTime 和过去时间的栈。

所有成功扣除批次只由背包内部推送一次；调用方不重复发送背包变化通知。

## 使用物品

协议固定为 `BagUseItemReq.Uid`，语义是使用该 Uid 的一件物品。`GAME1-BAG-01` 完成前，服务端对所有请求稳定返回 `BagUseItemAck.Error = "道具使用功能未完成"`，不会扣物品、改属性或发送背包变化通知。

## 协议与客户端

- `QueryBagAck.Items` 是 `List<BagItemInfo>` 完整快照，列表中的 Uid 必须为正数且唯一；Capacity 可以小于 Items.Count。
- `BagItemChangedNtf.Items` 是 `List<BagItemInfo>` 变化栈最终快照，`RemovedUids` 表示已删除栈；同一 Uid 不能同时出现在两者中。
- Tidy、Swap、Index、空格搜索及对应消息已经删除；协议不兼容旧客户端。
- `BagController` 按 Uid 缓存，进角时清空并查询全量快照，变化通知按绝对数量覆盖；迟到快照会再次核对 RoleId 与角色代次。
- 临时断线可以保留只读快照，但禁止发送背包操作；踢下线、永久重连失败、主动断开或切换角色会清空缓存。
- `BagPanel` 按 ConfigId、Uid 升序生成动态滚动条目，显示 `Items.Count / Capacity`，点击条目发送 Uid，确认框固定为 `x1`。
