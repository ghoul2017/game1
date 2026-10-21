# Mail 模块

目标读者：人类。

## 定位

Mail 管理在线玩家邮件的查询、已读、附件领取和删除。

## 数据结构

邮件运行时使用 `Dictionary<long, RoleMail>`，MongoDB 使用 `ArrayOfDocuments` 表示 long 类型邮件 ID，避免非字符串字典键的序列化问题。

`SendMail` 接收 `int[] RewardConfigs` 和 `int[] RewardCounts`，由 `BagItemAddDataGenerator` 生成 normalized `List<BagItemAddData>`；已读、附件领取和删除修改邮件状态，附件通过 `AddItems` 进入背包。

当前没有被业务调用的离线系统邮件投递入口；后续需要离线邮件时应单独设计离线队列或后台流程。

## 状态机

```text
State=0 未读
  ├── 读取邮件 → State=1 已读
  └── 领取附件 → State=2 已领取附件

已领取附件或无附件邮件 → 删除邮件
```

附件仍按请求中的 MailId 逐封处理。每封邮件先验证附件仍是配置存在、正数量、正 MaxStack 且无重复堆叠身份的 normalized 列表，不再次调用生成器。当前背包已经满足 `Items.Count > Capacity` 时返回“背包已满”，该封邮件和后续邮件保持未领取。未满时先标记该邮件已领取，最后调用 `AddItems` 完整加入本封全部附件；即使本封使栈数超过 Capacity，也不会中途停止。下一封会重新判断 IsFull。

邮件附件当前统一以 `Bind=false, ExpireTime=0` 加入。背包变化通知由 AddItems 内部完成，邮件调用方不重复推送。

## 过期与容量

- `QueryMailList` 和未读计数会过滤过期邮件。
- 已读、领取和删除接口按 MailId 直接访问字典，不检查 `IsExpired`；知道邮件 ID 的客户端仍可操作列表中不可见的过期邮件。
- 过期邮件没有物理清理，会继续保留在邮件字典和 `player_entity` BSON 中。`MaxVisibleMailCount=100` 只是查询结果上限，不是发送或存储容量限制。

## 已知局限

1. 没有离线系统邮件发送和全服邮件队列。
2. 过期邮件只在列表和未读统计中过滤，仍可按 ID 操作，同时继续增加内存字典和玩家 BSON 文档体积。
3. 附件仅支持物品。
4. 邮件物理清理和保留数量限制尚未实现。
5. 一次请求前部邮件成功、后部邮件满包失败时，响应同时包含已成功的 `ClaimedMailIds` 和 ErrorMessage；当前客户端优先展示错误，邮件列表可能暂时落后，重新查询后恢复。
