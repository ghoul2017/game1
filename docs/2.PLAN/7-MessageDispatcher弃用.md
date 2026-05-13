# MessageDispatcher 弃用

## 概述

客户端两套消息路由并存：MessageDispatcher（面板手动 Register）和 [MessageHandler] 属性（MessageRouter 自动扫描）。服务端已全面使用 [MessageHandler]，客户端面板仍用 MessageDispatcher。迁移面板消息处理到 [MessageHandler]，最终删除 MessageDispatcher。

## 方案

### 核心设计

1. 为每个模块创建客户端 Handler 类（static + `[MessageHandler(typeof(NtfMethodHandler))]`），与面板解耦
2. Handler 通过 `UIManager.GetPanel<T>()` 获取目标面板，调用公开更新方法
3. 面板保留 UI 更新逻辑和请求发送，只移除 Register/Unregister
4. 两套系统可并存，逐面板迁移

### 交互模式

```csharp
// Handler（static，MessageRouter 自动扫描注册）
[MessageHandler(typeof(NtfMethodHandler))]
public static void HandleBagUseItemAck(TcpSession session, BagUseItemAck ack)
{
    var panel = UIManager.Instance.GetPanel<BagPanel>();
    if (panel != null) panel.OnUseItemResult(ack);
}
```

### 消息分发机制

当前双路分发并存（TcpSession 同时走 MessageRouter + OnMessageReceived 回调）：
- 路径1: TcpSession → MessageRouter → `[MessageHandler]` 静态方法（已用于心跳/握手等）
- 路径2: TcpSession → OnMessageReceived → MessageDispatcher → 面板回调（待迁移）

迁移完成后移除路径2，只保留路径1。

## 新增文件

### Client.Login.LoginHandlers — client/Assets/Scripts/Login/LoginHandlers.cs
- 用途：登录模块消息处理
- 方法：
  - `static void HandleLoginRes(TcpSession session, LoginRes res)`

### Client.Login.RoleSelectHandlers — client/Assets/Scripts/Login/RoleSelectHandlers.cs
- 用途：角色选择模块消息处理
- 方法：
  - `static void HandleCreateRoleRes(TcpSession session, CreateRoleRes res)`
  - `static void HandleEnterGameRes(TcpSession session, EnterGameRes res)`
  - `static void HandleEnterGameNtf(TcpSession session, EnterGameNtf ntf)`
  - `static void HandleEnterMapAck(TcpSession session, EnterMapAck ack)`

### Client.Game.BagHandlers — client/Assets/Scripts/Game/BagHandlers.cs
- 用途：背包模块消息处理
- 方法：
  - `static void HandleQueryBagAck(TcpSession session, QueryBagAck ack)`
  - `static void HandleBagTidyAck(TcpSession session, BagTidyAck ack)`
  - `static void HandleBagUseItemAck(TcpSession session, BagUseItemAck ack)`
  - `static void HandleBagItemChangedNtf(TcpSession session, BagItemChangedNtf ntf)`

### Client.Game.MailHandlers — client/Assets/Scripts/Game/MailHandlers.cs
- 用途：邮件模块消息处理
- 方法：
  - `static void HandleQueryMailListAck(TcpSession session, QueryMailListAck ack)`
  - `static void HandleMailReadAck(TcpSession session, MailReadAck ack)`
  - `static void HandleMailGetItemAck(TcpSession session, MailGetItemAck ack)`
  - `static void HandleMailDeleteAck(TcpSession session, MailDeleteAck ack)`
  - `static void HandleMailNewNtf(TcpSession session, MailNewNtf ntf)`

### Client.Game.TaskHandlers — client/Assets/Scripts/Game/TaskHandlers.cs
- 用途：任务模块消息处理
- 方法：
  - `static void HandleQueryTaskListAck(TcpSession session, QueryTaskListAck ack)`
  - `static void HandleTaskAcceptAck(TcpSession session, TaskAcceptAck ack)`
  - `static void HandleTaskSubmitAck(TcpSession session, TaskSubmitAck ack)`
  - `static void HandleTaskGiveUpAck(TcpSession session, TaskGiveUpAck ack)`
  - `static void HandleTaskChangedNtf(TcpSession session, TaskChangedNtf ntf)`

### Client.Game.ChatHandlers — client/Assets/Scripts/Game/ChatHandlers.cs
- 用途：聊天模块消息处理
- 方法：
  - `static void HandleChatAck(TcpSession session, ChatAck ack)`
  - `static void HandleChatNtf(TcpSession session, ChatNtf ntf)`
  - `static void HandleSystemChatNtf(TcpSession session, SystemChatNtf ntf)`

### Client.Game.FightHandlers — client/Assets/Scripts/Game/FightHandlers.cs
- 用途：战斗模块消息处理
- 方法：
  - `static void HandleUseSkillAck(TcpSession session, UseSkillAck ack)`
  - `static void HandleHpChangedNtf(TcpSession session, HpChangedNtf ntf)`
  - `static void HandleKilledByNtf(TcpSession session, KilledByNtf ntf)`
  - `static void HandlePkModeChangedNtf(TcpSession session, PkModeChangedNtf ntf)`

## 修改文件

### client/Assets/Scripts/Core/UIManager.cs
- 新增方法：`public T GetPanel<T>() where T : UIPanel` — 栈中查找面板，未找到返回 null

### client/Assets/Scripts/Login/LoginPanel.cs
- 删除：OnShow 中的 `Register<LoginRes>` + OnHide 中的 `Unregister`
- 新增公开方法：`public void OnLoginResult(LoginRes res)`（从原 private handler 提取）

### client/Assets/Scripts/Login/RoleSelectPanel.cs
- 删除：OnShow 中的 4 个 Register + OnHide 中的 Unregister
- 新增公开方法：`OnCreateRoleResult`, `OnEnterGameResult`, `OnEnterGameNotify`, `OnEnterMapResult`

### client/Assets/Scripts/Game/BagPanel.cs
- 删除：OnShow 中的 4 个 Register + OnHide 中的 Unregister
- 新增公开方法：`OnQueryBagResult`, `OnBagTidyResult`, `OnUseItemResult`, `OnBagItemChanged`

### client/Assets/Scripts/Game/MailPanel.cs
- 删除：OnShow 中的 5 个 Register + OnHide 中的 Unregister
- 新增公开方法：`OnQueryMailListResult`, `OnMailReadResult`, `OnMailGetItemResult`, `OnMailDeleteResult`, `OnMailNew`

### client/Assets/Scripts/Game/TaskPanel.cs
- 删除：OnShow 中的 5 个 Register + OnHide 中的 Unregister
- 新增公开方法：`OnQueryTaskListResult`, `OnTaskAcceptResult`, `OnTaskSubmitResult`, `OnTaskGiveUpResult`, `OnTaskChanged`

### client/Assets/Scripts/Game/MainPanel.cs
- 删除：OnShow 中的 7 个 Register（Chat 3个 + Fight 4个）+ OnHide 中的 Unregister
- 新增公开方法：`OnChatResult`, `OnChatNotify`, `OnSystemChat`, `OnUseSkillResult`, `OnHpChanged`, `OnKilledBy`, `OnPkModeChanged`

### client/Assets/Scripts/Network/NetClient.cs
- 删除：`_dispatcher` 字段、`Dispatcher` 属性、MessageDispatcher 实例创建
- 删除：`_session.OnMessageReceived` 回调（迁移后不再需要）

## 删除文件

### client/Assets/Scripts/Network/MessageDispatcher.cs
- 原因：迁移完成，不再需要

## 执行顺序

1. 确认/新增 UIManager.GetPanel\<T\>() 方法
2. 创建 7 个 Handler 类（7 个新文件）
3. 逐面板迁移：Register/Unregister → 公开方法 + Handler 调用
4. 清理 NetClient 中 MessageDispatcher 引用
5. 删除 MessageDispatcher.cs
6. 编译验证

## 验证

- [ ] 编译通过（dotnet build game1.sln）
- [ ] 登录流程正常（LoginPanel → RoleSelectPanel → 进入游戏）
- [ ] 背包/邮件/任务面板消息处理正常
- [ ] 聊天和战斗通知正常（MainPanel）
- [ ] 面板关闭时 Handler 不崩溃（null 检查）
- [ ] MessageDispatcher 无残留引用
