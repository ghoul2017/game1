# Arena 模块

目标读者：人类。

## 定位

Arena 是 T5 的最小异步 PvP 技术证明：读取进攻方当前战斗快照和一个固定防守副本，由服务端一次性运行
`GameplayCore` Arena 规则并保存内存结果。战斗过程中客户端不能操作，也不能提交属性、Seed、胜负或奖励。

## 权威流程

```text
ArenaBattleStartReq(roleId + RequestId)
  → 服务端读取进攻方 Player 快照和固定防守副本
  → 分配 BattleId 与 Seed
  → ArenaBattleRunner 批量运行到唯一终态
  → 保存胜负摘要、关键帧和少量语义事件
  → ArenaBattleStartAck
  → ArenaReplayReq/Ack
  → ArenaReplayPlayer 只播放既有结果
```

同一角色的同一 `RequestId` 只运行一次；重复请求返回原 `BattleId`、Seed、胜负、终止原因、FinalTick 和事件哈希。
幂等键包含角色 ID，因此不同角色可以使用相同 RequestId。失败记录也进入稳定终态，不能通过重试重复运行。

## 回放与客户端

`ArenaReplayAck` 保存初始/终态等有限关键帧及攻击、伤害等语义事件。`ArenaReplayPlayer` 从最近关键帧重建可见
生命值；1 倍、4 倍和跳过只改变播放游标，永远不重跑规则或改变 `Outcome`。10,000 次哈希测试只证明当前同一
构建、配置与输入下可重复，不承诺跨版本、跨运行时或跨平台逐位确定性。

## 内存容量

- 完整回放最多 128 场，保留 5 分钟；达到上限时优先释放最旧已完成回放。
- 幂等回执最多全局 4096 条、每角色 64 条，保留 10 分钟；单角色不能耗尽全局索引。
- 完整回放淘汰后，幂等窗口内仍可返回缓存的胜负摘要；读取已淘汰或过期回放会得到明确错误。
- 所有 Arena 数据都是进程内数据。服务重启后丢失，不写 MongoDB，也没有赛季、排行、奖励、匹配池或长期录像。

## 验证边界

规则终态、重复 RequestId、回放哈希、1x/4x/跳过、淘汰、TTL 和容量公平性已有自动化；
`arena_idempotency_test` 已在真实本地 Server/MongoDB 通过。实际 Unity 人工联机播放仍由
`TEST-T5-UNITY-01` 跟踪。
