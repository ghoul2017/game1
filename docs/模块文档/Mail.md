# Mail 模块

目标读者：人类。

## 定位

Mail 管理在线玩家邮件的查询、已读、附件领取和删除。`RoleMailComponent` 持久化到 `role_mail_component`。

## 数据结构与保存

邮件运行时使用 `Dictionary<long, RoleMail>`，MongoDB 使用 `ArrayOfDocuments` 表示 long 类型邮件 ID，避免非字符串字典键的序列化问题。

在线邮件业务同步修改内存：`SendMail`、已读、附件领取和删除成功后标记邮件组件为脏；附件进入背包时由 `AddItems` 标记背包组件。两个集合由 Player 批量持久化系统分别保存，不提供跨集合原子性。

上线时由 `PlayerOnlineLoadMailEventHandler` 加载或首次创建组件。当前没有被业务调用的离线系统邮件投递入口；原先未被调用且直接等待 MongoDB 的 `SendSystemMailAsync` 已移除，后续需要离线邮件时应单独设计离线队列或后台持久化流程。

## 状态机

```text
State=0 未读
  ├── 读取邮件 → State=1 已读
  └── 领取附件 → State=2 已领取附件

已领取附件或无附件邮件 → 删除邮件
```

附件仍按请求中的 MailId 逐封处理。每封邮件先完整验证附件配置、正数量和表格 MaxStack；当前背包已经满足 `Items.Count > Capacity` 时返回“背包已满”，该封邮件和后续邮件保持未领取。未满时先标记该邮件已领取并标脏，最后调用 `AddItems` 完整加入本封全部附件；即使本封使栈数超过 Capacity，也不会中途停止。下一封会重新判断 IsFull。

邮件附件当前统一以 `Bind=false, ExpireTime=0` 加入。背包变化、标脏和通知由 AddItems 内部完成，邮件调用方不重复推送；预检后的意外失败直接暴露并保留已写权威状态，不回滚。

## 已知局限

1. 没有离线系统邮件发送和全服邮件队列。
2. 过期邮件只在查询时过滤，没有定时物理清理。
3. 附件仅支持物品。
4. 邮件领取与背包保存不保证跨集合原子一致。
5. 一次请求前部邮件成功、后部邮件满包失败时，响应同时包含已成功的 `ClaimedMailIds` 和 ErrorMessage；当前客户端优先展示错误，邮件列表可能暂时落后，重新查询后恢复。
