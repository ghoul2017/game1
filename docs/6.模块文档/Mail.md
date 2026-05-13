# Mail 模块文档

## 概述

Mail 模块实现玩家邮件系统，支持邮件查询、已读标记、附件领取、删除、系统邮件发送和实时推送。邮件数据持久化到 MongoDB，上线时加载。

## 文件清单

| 文件 | 行数 | 职责 |
|------|------|------|
| `server/Game/Components/Mail/RoleMail.cs` | 62 | 邮件数据类 + 附件类 |
| `server/Game/Components/Mail/RoleMailComponent.cs` | 46 | PlayerEntity 级 Component（IEntity 持久化） |
| `server/Game/Components/Mail/RoleMailSystem.cs` | 256 | 邮件操作扩展方法 |
| `server/Game/Components/Mail/RoleMailHandlers.cs` | 101 | 消息处理 + 事件订阅 |
| `client/Assets/Scripts/shared/Common/Protos/MailProtos.cs` | 118 | 消息定义 |
| `client/Assets/Scripts/shared/Common/MsgIdConst.cs` | — | MsgId 10401-10500 段 |

## 数据模型

### RoleMail（邮件）

```csharp
namespace Game.Components.Mail;

public class RoleMail
{
    public long Id;                                // 邮件ID
    public long RoleId;                            // 收件人角色ID
    public string Sender = "";                     // 发件人
    public string Title = "";                      // 标题
    public string Content = "";                    // 内容
    public List<MailAttachment> Attachments = [];  // 附件列表
    public long SendTime;                          // 发送时间（Unix毫秒）
    public long ExpireTime;                        // 过期时间（0=永不过期）
    public int State;                              // 0=未读, 1=已读, 2=已领附件

    // 计算属性
    public bool IsRead => State >= 1;
    public bool IsClaimed => State >= 2;
    public bool IsExpired => ExpireTime > 0 && DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() > ExpireTime;
    public bool HasUnclaimedAttachment => !IsClaimed && Attachments.Count > 0;
}
```

### MailAttachment（附件）

```csharp
public class MailAttachment
{
    public int ConfigId;    // 物品配置ID
    public int Count;       // 物品数量
}
```

## Component

### RoleMailComponent（PlayerEntity 级）

```csharp
[ComponentOf(typeof(PlayerEntity))]
public class RoleMailComponent : Component, IEntity
{
    [BsonId] public long Id { get; set; }                    // = RoleId
    public List<RoleMail> Mails = [];                        // 邮件列表

    [BsonIgnore] public long NextMailId = 1;                 // 邮件ID生成器
    [BsonIgnore] private Dictionary<long, RoleMail>? _mailMap; // 运行时索引

    internal void AfterDeserialize();
    internal void RebuildIndex();
    internal Dictionary<long, RoleMail> GetMailMap();
    internal RoleMail? GetByMailId(long mailId);  // 注意：实际方法名在 GetMailMap 内查找
}
```

- MongoDB 集合名：`role_mail_list`
- NextMailId 递增生成邮件 ID

## System 详解

### RoleMailSystem

| 方法 | 签名 | 说明 |
|------|------|------|
| LoadMailsAsync | `LoadMailsAsync(this RoleMailComponent, long roleId) → Task<RoleMailComponent>` | 从 MongoDB 加载 |
| SaveMailsAsync | `SaveMailsAsync(this RoleMailComponent) → Task` | 保存到 MongoDB |
| SendMail | `SendMail(this RoleMailComponent, string sender, string title, string content, List<MailAttachment>?, long expireTime) → MailResult` | 发送邮件 |
| SendSystemMailAsync | `SendSystemMailAsync(this RoleMailComponent, long roleId, string title, string content, List<MailAttachment>?, long expireTime) → Task` | 发送系统邮件+推送 |
| GetMailList | `GetMailList(this RoleMailComponent) → List<RoleMail>` | 获取未过期邮件（按时间倒序） |
| MarkRead | `MarkRead(this RoleMailComponent, List<long> mailIds) → MarkResult` | 标记已读 |
| ClaimAttachmentsAsync | `ClaimAttachmentsAsync(this RoleMailComponent, List<long> mailIds) → Task<ClaimResult>` | 领取附件（加入背包） |
| DeleteMails | `DeleteMails(this RoleMailComponent, List<long> mailIds) → DeleteResult` | 删除邮件（附件已领才能删） |
| GetUnreadCount | `GetUnreadCount(this RoleMailComponent) → int` | 未读邮件数 |
| ToMailInfo | `ToMailInfo(this RoleMailComponent, RoleMail) → MailInfo` | 转换为网络传输格式 |

常量：`MaxMailCount = 100`（每人最多100封邮件）

#### ClaimAttachmentsAsync 流程

```
1. 遍历指定 mailId 列表
2. 校验邮件存在且有未领附件
3. 标记 State = 2（已领附件）
4. 调用 player.RoleBag.AddItem() 添加物品到背包
5. 返回领取结果（邮件ID列表 + 物品列表）
```

### 错误处理

所有 Ack 使用 `ErrorMessage` 字段，成功时为空字符串，失败时为中文错误文本（如 "邮箱已满"、"邮件不存在" 等）。客户端通过 `!string.IsNullOrEmpty(ack.ErrorMessage)` 判断失败，直接显示 ErrorMessage 内容。

## Handler 详解

### 消息处理

| Handler | MsgId | 请求 | 响应 | 说明 |
|---------|-------|------|------|------|
| QueryMailListHandler | 10401→10402 | QueryMailListReq | QueryMailListAck | 查询邮件列表 |
| MailReadHandler | 10403→10404 | MailReadReq(mailIds) | MailReadAck(readMailIds) | 标记已读 |
| MailGetItemHandler | 10405→10406 | MailGetItemReq(mailIds) | MailGetItemAck(claimedMailIds, items) | 领取附件 |
| MailDeleteHandler | 10407→10408 | MailDeleteReq(mailIds) | MailDeleteAck(deletedMailIds) | 删除邮件 |

### 事件订阅

| 事件 | 方法 | 说明 |
|------|------|------|
| PlayerOnlineEvent | OnPlayerOnline | 挂载 RoleMailComponent + LoadMailsAsync |

## 协议汇总

| MsgId | 消息类 | 方向 | 关键字段 |
|-------|--------|------|----------|
| 10401 | QueryMailListReq | C→S | （无） |
| 10402 | QueryMailListAck | S→C | ErrorMessage, Mails(List\<MailInfo\>) |
| 10403 | MailReadReq | C→S | MailIds(List\<long\>) |
| 10404 | MailReadAck | S→C | ErrorMessage, ReadMailIds(List\<long\>) |
| 10405 | MailGetItemReq | C→S | MailIds(List\<long\>) |
| 10406 | MailGetItemAck | S→C | ErrorMessage, ClaimedMailIds, Items(List\<MailAttachmentInfo\>) |
| 10407 | MailDeleteReq | C→S | MailIds(List\<long\>) |
| 10408 | MailDeleteAck | S→C | ErrorMessage, DeletedMailIds(List\<long\>) |
| 10409 | MailNewNtf | S→C | Mail(MailInfo) |

共享结构：
- `MailInfo`：MailId, Sender, Title, Content, Attachments, SendTime, State, ExpireTime
- `MailAttachmentInfo`：ConfigId, Count

## 模块依赖

```
Mail 模块
  ├── 依赖 → MongoDB（RoleMailComponent 持久化）
  ├── 依赖 → Player（player.RoleMail 访问）
  ├── 依赖 → Bag（ClaimAttachmentsAsync → AddItem）
  ├── 依赖 → Network（SendToPlayer 推送 MailNewNtf）
  │
  └── 被依赖 ←
       └── 其他模块可通过 SendSystemMailAsync 发系统邮件
```
