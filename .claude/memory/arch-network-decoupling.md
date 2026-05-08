---
name: NetworkComponent 解耦重构
description: SessionManager 合并到 NetworkComponent + session 引用跟随所属对象
type: project
---

## 重构背景

SessionManager（手动单例）维护 userId/roleId → TcpSession 映射，NetworkComponent（ECS Component）维护 sessionId → TcpSession 映射。两者互不感知：

- SessionManager._userSessionMap 从未被填充（GetByUserId 永远返回 null）
- disconnect 时 SessionManager.Remove 从未被调用（映射泄漏）
- NetworkComponent.OnSessionDisconnected 直接耦合业务逻辑（Context.Players.SaveRoleAsync、EventBus.Publish）

## 重构结果

1. **删除 SessionManager.cs**，映射合并到 NetworkComponent
2. **NetworkComponent 只管连接生命周期**：sessionId → TcpSession + Kick + KickAll + 心跳，用 `Action<TcpSession>` 回调通知上层（不用 EventBus）
3. **User 持有 Session 引用**：`user.Session = session`，disconnect 时 `user.Session = null`
4. **PlayerOnlineComponent 持有 Session 引用**：`player.Online.Session = session`，disconnect 时 `player.Online.Session = null`
5. **上层通过对象引用访问 session**：`player.Online.Session?.Send(msg)`，不再通过中心化查找

## 踩坑

- 删除 SessionManager.cs 是 `Game.Network` 命名空间唯一的类 → 14 个文件 `using Game.Network;` 编译报错，需批量清理
- 清理 `using Game.Network;` 时不能误删 `using Game.Core.Network;`（RpcMethodHandler 等类型仍在用）

## 设计原则

- 底层模块（NetworkComponent）用回调解耦，不用 EventBus
- Session 引用跟着所属对象（User、PlayerOnlineComponent），不在中心化位置维护业务级映射
- 两套独立映射不同步是常见 bug 模式，合并到一处或让索引跟随数据所有者