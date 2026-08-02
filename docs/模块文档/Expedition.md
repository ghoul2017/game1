# Expedition 模块

目标读者：人类。

## 定位

Expedition 是百炼 Like 技术竖切：T1/T2 用本地宿主快速验证灰盒手感和“结算 → 升级 → 第二局”，T3/T4
用服务端宿主复用同一份 `GameplayCore` 规则并接入玩家永久进度。两种宿主共享规则语义，但只有服务端在线模式
能发放正式玩家聚合内的收益。

## 本地切片

- `ExpeditionHandfeelScene` + `LocalExpeditionHost`：固定短路线，覆盖队伍移动、稳定目标、自动战斗/采集、
  Boss 警告/躲避、出口和唯一终态。
- `ExpeditionVerticalSliceScene` + `LocalCampaignHost`：6–10 房间确定性图、成功/失败带回、一个设施升级和第二局。
- `ProductExpeditionFactory` 是本地与服务端共同消费的产品组合入口；`T2ExpeditionCampaignFactory` 只保留为
  T2 调用点的稳定兼容门面。本地 Campaign 不写 MongoDB，也不是反作弊权威。
- Build Settings 默认入口是 `OnlineTechnicalScene`；T2/T1 人工回归分别直接打开对应本地场景。

## 在线权威链

```text
Unity 输入
  → ExpeditionController（量化方向、单调序号、根节点预测）
  → ExpeditionInputNtf
  → ExpeditionRunHost（单写者、10 Hz）
  → ProductExpeditionFactory / ProductExpeditionRun
  → GameplayCoreExpeditionSimulationFactory / ExpeditionSimulation
  → ExpeditionSnapshotNtf（每 2 Tick，5 Hz，SendLatest）
  → 客户端纠正与表现
```

客户端不能提交位置、伤害、掉落、资源或结果。服务端每秒最多接受 30 条输入；方向必须有限且在协议量化范围内，
RunId、所有者、单调序号、状态和交互目标都在权威边界校验。最近有效输入超过 500 ms 后归零。显式交互目标只有
在存在、已揭示且可达时才生效；附近敌人威胁优先于请求的资源目标。

普通完整快照通过 `TcpSession.SendLatest` 只保留尚未发送的最新一份；关键表现事件通过
`ExpeditionEventBatchNtf` 走有界可靠有序队列。这里的可靠性只覆盖当前 TCP 连接，不承诺断线后的 ACK/重放；
权威状态和收益仍由快照、结果与结算响应恢复。
客户端只预测队伍根移动：房间/阶段变化或偏差达到 2 个单位时硬纠正，小偏差按 0.35 比例平滑收敛。

独立 `OnlineTechnicalScene` 含 `GameEntry`、`OnlineTechnicalEntry` 和在线世界。登录、创建/选择角色后直接进入
远征，不再请求或显示旧 `GameScene`；Arena 收入诊断抽屉，不遮挡主玩法。该场景位于 Build Settings index 0，
T2、T6、T1 场景依次为 index 1、2、3，旧 `GameScene` 与 `SampleScene` 已禁用。

## 协议

| 协议 | 职责 |
| --- | --- |
| `ExpeditionStartReq/Res` | 活动/保留宿主内按 RequestId 幂等开始；冲突 ID 拒绝，返回永久进度和初始快照 |
| `ExpeditionInputNtf` | 方向、交互目标与单调输入序号 |
| `ExpeditionSnapshotNtf` | 当前房间完整小快照，以及 Tick、移动、边界和 Boss 警告半径 |
| `ExpeditionEventBatchNtf` | 当前 TCP 连接内可靠有序的战斗、采集、房间与危险表现事件 |
| `ExpeditionResultNtf` | 权威终态与待结算提示 |
| `ExpeditionResumeReq/Res` | 同进程恢复活动局或查询最近回执 |
| `ExpeditionSettlementReq/Res` | 消费结算权并返回不可变回执 |
| `ExpeditionQueryProgressReq/Res` | 查询带回资源、设施、升级价/上限、代表伤害与最近结算标识 |
| `ExpeditionUpgradeReq/Res` | 预检后先扣资源，再提升唯一设施并返回新旧代表伤害 |

## 断线、结算与保存

- 每账号最多一个活动局；断线后宿主保留 5 分钟，并以零输入继续推进。
- 同进程重连 token 在线不计时且不可消费，权威断线后才开启 5 分钟窗口；首次消费轮换，响应丢失重放只返回
  同一新 token，pending EnterGame 再断线仍可恢复。恢复后继续同一 RunId/Seed；空恢复响应会清掉客户端旧快照、
  预测和待确认输入。
- 终态只能产生一次。结算先消费本局结算权，再把带回资源和回执写入 `PlayerExpeditionComponent`，最后交给现有
  `PlayerEntity` 合批保存；不插入单组件即时 MongoDB 保存。
- 回执保存结算当时的设施等级，升级后重复同一 RunId 仍返回原字段；旧保留局不能回滚同进程中更晚的结算或升级。
- 活动局不写数据库、不跨服务进程恢复；服务重启后只保留最后成功保存的永久进度。
- `ExpeditionStartReq.RequestId` 的幂等边界是当前服务进程和宿主保留期；跨重启强幂等尚未承诺。

## 本机测试入口

Unity Editor 菜单 `Tools/Online Technical/Build, Start Server and Play` 会使用 `global.json` 指定的 SDK 构建，
从仓库 `bin/` 启动仅监听 `127.0.0.1:9000` 的本地服务端，打开在线场景并进入 Play。工具先检查 MongoDB
`127.0.0.1:27017` 和未知 9000 监听者，以 PID、Unity 父进程、启动时间、token 与控制文件确认所有权；停止时
先等待最多 20 秒优雅保存，只会终止完全匹配的自有进程。正式 Player 不包含该 Editor 工具，也不会启动服务端。

## 容量边界

进程内确定性探针覆盖 1/10/50 个逻辑局，最大序列化快照 328 B、最大完整包 356 B，三档均没有 100 ms Tick
超预算。该结果只支持当前规则探针继续单进程，不代表真实 TCP、客户端、MongoDB、Linux 或生产 CCU 容量。

## 验证边界

纯规则、协议映射、宿主、幂等、重连和容量已有自动化；本地真实 Server/MongoDB/Robot 已走通远征完整闭环。
尚待执行：Unity 人工在线试玩、T1/T2 人工手感与目标设备、正式微信完整一局、MongoDB 故障/重启、弱网、
前后台和 Linux/生产环境容量。入口见 `docs/测试列表.md`。
