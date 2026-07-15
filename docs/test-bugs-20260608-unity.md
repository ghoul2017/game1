# Unity 手工测试 Bug 记录 2026-06-08

目标读者：AI。

任务目标：把 2026-06-08 的已验证 Bug、风险和未完成测试作为后续复现、修复与回归的证据入口；使用前必须用当前代码、配置和运行现场复核，不把历史建议直接当成修改授权。

## AI 使用规则

- 先读取当前 Git 状态、对应代码和 `docs/test-manual.md`，再选择本任务授权范围内的 Bug；不得因为本文标记“未修复”就自动修改代码。
- 历史截图、日志、账号、RoleId 和绝对路径只说明当时现场。文件不存在或当前行为不同不等于问题已修复，必须按同层级用例重新验证。
- 已有产品决定优先于历史“建议”：`runInBackground=1` 已确认；新手出生点安全语义仍需用户确认；上线前测试数据无需迁移兼容，可按验证范围清理并报告。
- 修复任务只能更新已实际复现和验证的条目状态；无法复现时记录环境、命令、实际现象和再次处理条件。
- 最终报告必须列出处理的 Bug ID、复现结果、修改范围、验证闭环、未处理条目和仍需用户决定的行为分歧。

> 本记录只收本轮实测发现的问题和风险。未复现或未完成项保持待确认，不包装成通过。

## BUG-20260608-001 PlayMode 失焦后帧不推进导致登录卡在 Connecting

- 严重级别：P1 自动化阻塞 / P2 功能风险
- 状态：已决策；2026-07-15 确认项目正式保持 `runInBackground=1`，待批次 2 基线复核
- 影响范围：Unity PlayMode 手工/自动化登录、uLoop 后台测试、网络握手和重连判断
- 历史复现条件：2026-06-08 本地 `client/ProjectSettings/ProjectSettings.asset` 中 `runInBackground=0`；Unity 进入 PlayMode 后焦点在 Codex/终端。
- 复现步骤：
  1. 启动服务端。
  2. Unity 进入 PlayMode。
  3. 通过 uLoop/动态代码点击 LoginPanel 登录按钮。
  4. 焦点保持在 Codex/终端，读取 `Time.time`、`Time.frameCount` 和 `NetClient.State`。
- 实际结果：
  - `EditorApplication.isPlaying=True`、`isPaused=False`，但 `Time.time`、`frameCount` 长时间不增长。
  - `NetClient.State=Connecting`，服务端只出现 `会话添加`，没有 `HandshakeReq` / `LoginReq`。
  - 60 秒后服务端心跳超时断开 UserId 0 的 Session。
  - Unity 错误日志出现：`TcpSession 发送失败: The socket has been shut down`。
- 预期结果：Unity PlayMode 测试期间网络帧持续推进，连接成功后立即发送 `HandshakeReq`，然后继续登录。
- 本轮规避：运行时执行 `Application.runInBackground = true`，不写回 ProjectSettings。
- 证据：
  - 服务端：`心跳超时 SessionId:322368207820165120 UserId:0`。
  - 动态读取：设置前 `state=Connecting currentTime=77.05042` 多次不变；设置后 `state=Connected currentTime=82.79514`。
  - 本地脏改动：`ProjectSettings.asset` 中 `runInBackground: 1 -> 0`。
- 处理决定：项目配置保持 `runInBackground=1`，不再把运行时临时开启作为常规测试前置；批次 2 仍需在 Unity 失焦时复核帧推进和登录握手。

## BUG-20260608-002 BagPanel 网格布局溢出面板

- 严重级别：P2
- 状态：已复现，未修复
- 影响范围：背包 UI 可读性、点击格子准确性
- 复现步骤：
  1. 新角色进入游戏。
  2. 打开背包面板。
- 实际结果：
  - 控制器数据正确：`bag capacity=100 items=0`。
  - UI 上背包卡片显示 `0/100`，但 8x8 格子区域明显偏右下，部分超出/贴出卡片可视区域。
- 预期结果：背包格子应完整落在 `BagCard` 内容区域内，和标题、容量、整理/关闭按钮保持一致布局。
- 证据：截图 `client/.uloop/outputs/Screenshots/Game_20260608_215127_865.png`。
- 建议：检查 `BagPanel` 中格子根节点锚点、父节点、`RectTransform.anchoredPosition` 和卡片内容区域尺寸；先修 UI 布局，再继续 BAG-02/03。

## BUG-20260608-003 新角色出生点立即进入怪物攻击范围

- 严重级别：P2 待确认
- 状态：已观察到，需设计确认
- 影响范围：新手进图体验、MAP-01/FIGHT 测试稳定性
- 复现步骤：
  1. 新账号创建新角色。
  2. 点击进入游戏，等待进入新手村。
- 实际结果：
  - 玩家进入 `MapId:1 Line:1 Pos:(100,100)` 后，画面中多只野猪在角色附近。
  - 服务端很快出现多条怪物对玩家使用技能的日志：`FightSystem 技能使用 Attacker:... Target:322370565182590976 Hits:1`。
  - 截图中玩家附近有怪物和黄色场景块，角色处于被攻击环境。
- 预期结果：若新手出生点设计为安全区，应不应立即被怪物攻击；若设计允许被攻击，需要在测试手册中明确这是预期。
- 证据：
  - 服务端日志时间段：2026-06-08 21:49:13 起多条 `FightSystem 技能使用`。
  - 续测时间段：2026-06-08 23:13:49 起再次持续输出 `FightSystem 技能使用 ... Target:322370565182590976 Hits:1`。
  - 截图 `client/.uloop/outputs/Screenshots/Game_20260608_214952_195.png`。
- 建议：
  - 对齐设计：确认新手村出生点是否应为安全区。
  - 若应安全，调整怪物出生/巡逻/仇恨范围或玩家出生点。
  - 若非安全，更新 `docs/test-manual.md` 的 MAP-01/FIGHT 前置说明，避免测试误判。

## BUG-20260608-004 MailPanel 和 TaskPanel 底部控件超出 1080p 游戏画面

- 严重级别：P2
- 状态：已复现，未修复
- 影响范围：MAIL-02、TaskPanel 的接取/提交/放弃/关闭等手工 UI 操作；按钮和输入框不可见或难以点击。
- 复现步骤：
  1. 进入游戏，打开邮件面板。
  2. 观察面板底部操作按钮。
  3. 打开任务面板，观察任务 ID 输入框和底部按钮。
- 实际结果：
  - `MailPanel` 显示空列表和 `0 封`，但 `全部已读`、`领取附件`、`删除已读`、`关闭` 不在可视区域内。
  - `TaskPanel` 显示 `0 个`，但 `AcceptInput`、`接取`、`提交`、`放弃`、`关闭` 位于画面底部外侧。
  - uLoop 标注：邮件按钮 `SimY=1110`；任务底部控件 `SimY=1090`；游戏画面高度为 1080。
  - 动态读取：邮件按钮世界 Y `(-49,-11)`；任务底部控件世界 Y `(-29,9)`。
- 预期结果：底部控件应完整位于面板内和 1920x1080 游戏画面内，能够被玩家看到并点击。
- 证据：
  - 截图 `client/.uloop/outputs/Screenshots/Rendering_20260608_231754_477.png`。
  - `client/Assets/Scripts/View/Panels/MailPanel.cs:167` 使用 `bottomY = bodyTop - bodyH - 10f`，按钮锚点放到画面下方。
  - `client/Assets/Scripts/View/Panels/TaskPanel.cs:153` 使用同类 `bottomY`，输入和按钮同样越界。
- 建议：
  - 调整面板内部坐标系，底部按钮应相对 `MailCard/TaskCard` 内容区域底部向上留边，而不是继续向负 Y 推出内容区域。
  - 修复后重跑 MAIL-02、TASK-02/03/05 的可见点击路径。

## BUG-20260608-005 接取任务后保存 RoleTaskComponent 失败导致客户端/服务端状态分叉

- 严重级别：P1 任务主流程阻塞
- 状态：2026-07-15 已修复并通过 Robot/MongoDB 回归
- 影响范围：TASK-02 接取任务、TASK-04 任务进度、TASK-05 提交领奖；任务持久化和客户端列表刷新。
- 复现步骤：
  1. 进入游戏并打开任务面板。
  2. 通过任务面板输入 `1` 并触发接取。
  3. 再次接取任务 `1`。
- 实际结果：
  - 第一次接取返回 Toast：`接取任务失败: An error occurred while serializing the Tasks property of class Game.Components.GameTask.RoleTaskComponent: When using DictionaryRepresentation.Document key values must serialize as strings.`。
  - 客户端 `TaskController.Instance.Tasks.Count` 仍为 0，任务列表没有新增任务。
  - 第二次接取任务 `1` 返回 `任务已接取`，说明服务端内存中的 `RoleTaskComponent.Tasks` 已经添加任务。
  - MongoDB 查询 `role_task_component` 仍为 `{ _id: 322370565182590976, Tasks: {} }`。
  - 后续直接提交任务 1 返回 `任务未完成`，放弃任务 1 成功并清理服务端内存状态。
- 预期结果：接取任务 1 成功返回 `TaskInfo`，客户端列表新增任务，MongoDB 持久化任务数据；若保存失败，应回滚内存变更并返回一致错误。
- 证据：
  - 服务端日志：2026-06-08 23:22:14 `RoleTaskSystem 接取任务 TaskId:1 GoalCount:1` 后，`TcpSession 处理RPC请求 Task.TaskAcceptReq 异常`，异常为 `DictionaryRepresentation.Document key values must serialize as strings`。
  - `server/Components/GameTask/RoleTaskComponent.cs:19`：`Tasks` 类型为 `Dictionary<int, RoleTask>`。
  - `server/Components/GameTask/RoleTaskSystem.cs:46`：`SaveTasksAsync()` 直接 `MongoDb.Instance.UpdateAsync(self)`。
  - `server/Components/GameTask/Handlers/TaskAcceptReqHandler.cs:15` 先 `AcceptTask()` 修改内存，`server/Components/GameTask/Handlers/TaskAcceptReqHandler.cs:19` 再保存，保存异常没有回滚内存。
- 2026-07-15 修复与验证：
  - `RoleTaskComponent.Tasks` 已直接替换为 `List<RoleTask>`；本地 `game.role_task_component` 的 4 条旧空字典文档按授权清理。
  - 任务、奖励、战斗和相关事件业务保持同步：只修改游戏主线程内存并标脏，不在业务链路等待 MongoDB。
  - Player 批量持久化按 60 秒、断线和正常停服触发，按组件集合分别 `BulkWrite`；脏版本只在对应快照成功后清除，失败数据留待后续批次重试。
  - 任务提交先在内存中完整解析奖励并尝试加入背包，中途失败恢复背包快照；成功后把任务设为 State=2，并分别标脏任务与背包。当前明确不要求跨集合原子性，也不保留 RewardState 或奖励回执。
  - `RoleBagComponent.Items` 改为 MongoDB `ArrayOfDocuments` 字典表示；本地 `game.role_bag_component` 的 4 条旧空字典文档按授权清理。
  - 新增 Robot `task_persistence_test`。修复后验证重复接取错误、断线保存后恢复相同 `AcceptTime`、5 次击杀完成、进度恢复、任务奖励增量 `1001:1`、重复提交错误和领奖后恢复。野猪会随机掉落 `1001`，因此按提交前后增量断言。
  - MongoDB 实测 `Tasks` 为数组、任务状态为 2、进度为 5；背包为整数键数组。在线脏玩家 Ctrl+C 停服时，最终批次保存了任务进度、经验和金币后正常退出。

## 未完成项

- MAIL-03 正向邮件流程：缺少系统邮件预置数据或 Robot/测试工具场景，未执行。
- TASK-03 任务列表满分支：未构造 20 个进行中任务，未执行。
- CHAT-01 世界频道：未执行。
- MAP-02 到 MAP-04、BAG-02/03、双客户端和重连：未执行，已拆入分布测试 shard。
