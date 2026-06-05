# Mail 模块

## 定位

Mail 模块管理玩家邮件——查询、已读标记、附件领取、删除、系统邮件发送、实时推送。邮件是异步物品传递的通道：系统奖励、补偿发放都通过邮件附件到达玩家背包。

## 核心设计

### 邮件是物品的中转站

邮件的核心价值在于附件——纯文本邮件只是通知，带附件的邮件才是物品传递。`ClaimAttachmentsAsync` 将附件物品转入背包，完成"邮件→背包"的物品流转。

### Component 与持久化合一

`RoleMailComponent` 同时是 Component 和 IEntity，持久化到 `role_mail_list` 集合。直接使用 `Dictionary<long, RoleMail>` 作为 MongoDB 持久化结构（key=mailId），提供 O(1) 邮件查找。

### 系统邮件 + 实时推送

`SendSystemMailAsync` 供其他模块调用（如战斗奖励、补偿发放），写入邮件后立即推送 `MailNewNtf`。在线玩家实时收到通知，离线玩家下次上线加载时看到。

### 保留的文件

| 文件 | 职责 |
|------|------|
| `RoleMailComponent.cs` | 邮件 Component（IEntity 持久化，Dictionary 主结构） |
| `RoleMailSystem.cs` | 邮件操作扩展方法 |
| `Handlers/PlayerOnlineLoadMailEventHandler.cs` | PlayerOnline → 加载邮件 |
| `Handlers/QueryMailListReqHandler.cs` | 查询邮件列表消息处理 |
| `Handlers/MailReadReqHandler.cs` | 标记已读消息处理 |
| `Handlers/MailGetItemReqHandler.cs` | 领取附件消息处理 |
| `Handlers/MailDeleteReqHandler.cs` | 删除邮件消息处理 |
| `RoleMail.cs` | 邮件/附件数据类 |

## 邮件操作

### 领取附件

```
1. 遍历指定 mailId 列表
2. 校验邮件存在且有未领附件 (State < 2, Attachments.Count > 0)
3. 标记 State = 2（已领附件）
4. 调用 player.RoleBag.AddItem() 添加物品到背包
5. 返回领取结果（邮件ID列表 + 物品列表）
```

### 删除邮件

只能删除已领附件的邮件（State=2）或无附件的已读邮件，防止物品丢失。

### 邮件状态机

```
[新邮件] → State=0 (未读)
    │
    ├── MarkRead → State=1 (已读)
    │
    ├── ClaimAttachments → State=2 (已领附件)
    │
    └── DeleteMails → 删除（需已领附件或无附件）
```

## 上线加载

```
PlayerOnlineEvent → PlayerOnlineLoadMailEventHandler
  ├── LoadAsync<RoleMailComponent>(roleId)
  ├── 不存在 → 创建默认空邮件列表 + InsertAsync
  └── player.AddComponent(mailComp)
```

## 跨模块协作

| 交互方 | 方向 | 内容 |
|--------|------|------|
| Bag | 单向 | `ClaimAttachmentsAsync` → `AddItem` 添加物品到背包 |
| Player | 单向 | `SendToPlayer` 推送 MailNewNtf；`player.RoleMail` 访问 |
| Network | 单向 | 推送实时邮件通知 |

任何模块都可以通过 `SendSystemMailAsync` 发系统邮件给玩家，这是 Mail 的主要被依赖方式。

## 已知局限

1. **无邮件过期清理**：RoleMail 有 ExpireTime 字段但无定时清理
2. **无邮件批量发送**：全服邮件需逐个玩家发送
3. **附件仅物品**：不支持金币/经验附件
