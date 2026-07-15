# Unity 手工测试过程记录 2026-06-08

目标读者：AI。

任务目标：为后续 AI 提供 2026-06-08 测试现场、已完成用例、失败证据和剩余 shard 的续测入口；本文是历史过程记录，不自动授权重跑 PlayMode、写测试数据或修复代码。

## AI 续测规则

- 开始新 shard 前先读取当前 Git 状态、`docs/test-manual.md` 和 `docs/test-bugs-20260608-unity.md`，并复核 Unity、uLoop、服务端、MongoDB 与目标测试数据是否仍可用。
- 不重复执行已有证据充分且未受代码、配置或环境变化影响的用例；受影响用例按当前事实重新执行并另记日期化增量，不改写本文件中的历史结果。
- 进入 PlayMode、自动登录、复用或创建账号、写 MongoDB、预置邮件/物品/任务数据前，必须取得当前任务授权；数据来源或清理边界不明时停止并询问用户。
- 每个 shard 独立记录命令、账号/角色、进程、实际结果、日志、截图、数据库副作用和停止原因；失败不得包装成通过。
- 完成后报告本次 shard、与历史记录的差异、新增或关闭的 Bug、未完成项及下一次可直接进入的入口。

> 本记录用于承接 `docs/test-manual.md` 的长流程测试。本文只记录本轮实际执行、证据和分布测试切分，不替代测试手册。
>
> 后续决定：2026-07-15 已确认项目正式保持 `runInBackground=1`；下文关于运行时临时开启的内容只描述 2026-06-08 当时的测试现场。

## 1. 本轮范围

- 执行时间：2026-06-08 21:31-21:51（Asia/Shanghai）。
- 执行机器：`/Users/gejiayi/UnityProjects/game1` 本地工作区。
- Unity：2022.3.62f3，项目 `client/`。
- 服务端：从 `server/` 目录执行 `dotnet run --project Server.csproj`。
- MongoDB：本机 `mongod` 已运行。
- 测试账号：`qa_0608_codex_2135`，密码 `123456`。
- 测试角色：`测06082142`，职业战士，RoleId `322370565182590976`。

## 2. 环境和冒烟

| 步骤 | 命令/操作 | 结果 |
|------|-----------|------|
| Robot 构建 | `dotnet build robot/Robot.csproj` | 通过，0 Warning，0 Error |
| 服务端启动 | `cd server && dotnet run --project Server.csproj` | 通过，配置表、MongoDB、事件、地图、网络初始化完成，监听 `0.0.0.0:9000` |
| Robot 登录冒烟 | `dotnet run --project robot/Robot.csproj -- --scenario login_test` | 通过，`PASS: login_test`，汇总 `1 通过, 0 失败` |
| Unity uLoop 连接 | `tools/unity/uloop.sh get-version` | 通过，UnityVersion `2022.3.62f3`，uLoop `2.1.4` |
| Unity 编译 | `tools/unity/uloop.sh compile` | 通过，ErrorCount 0，WarningCount 0 |
| Unity 错误日志 | `tools/unity/uloop.sh get-logs --log-type Error --max-count 20` | 初始为 0；后台暂停导致一次 socket 错误，见 bug 文档 |

## 3. Unity 登录和角色流程

| 用例 | 实测结果 | 证据 |
|------|----------|------|
| ENV-02 客户端启动 | 通过，进入 LoginPanel | 截图 `client/.uloop/outputs/Screenshots/Game_20260608_213441_989.png` |
| USER-01 正常登录和自动注册 | 通过，新账号自动注册，进入 RoleSelectPanel，角色数 0 | 服务端日志：`自动注册账号 User:qa_0608_codex_2135`、`登录成功 ... 角色数:0`；Unity 日志：`[LoginPanel] 登录成功 PlayerId=322369654062321664 角色数=0` |
| ROLE-01 角色列表 | 通过，显示 4 个空角色槽，进入按钮未选中时不可用 | 截图 `Game_20260608_214226_973.png` |
| ROLE-02 创建角色成功 | 通过，创建 `测06082142` 并自动选中 | 服务端日志：`CreateRoleReqHandler 创建角色 ... RoleId:322370565182590976 Name:测06082142`；截图 `Game_20260608_214600_146.png` |
| ROLE-04 进入游戏 | 通过，进入游戏场景并显示 MainPanel | 服务端日志：首次创建角色数据、属性、邮件、任务、背包；`进入游戏 ... Name:测06082142` |
| MAP-01 初始进图 | 通过，进入新手村 `MapId:1 Line:1 Pos:(100,100)` | 服务端日志：`玩家进入地图 RoleId:322370565182590976 MapId:1 Line:1 Pos:(100,100)`；Unity 日志：`[RoleSelectPanel] 进图成功`；截图 `Game_20260608_214952_195.png` |
| BAG-01 打开和查询背包 | 功能数据通过，UI 布局有问题 | 控制器数据：`bag capacity=100 items=0`；截图 `Game_20260608_215127_865.png`；UI 问题见 bug 文档 |
| TASK-01 查询任务 | 通过，控制器任务数 0 | 动态读取：`tasks=0` |
| MAIL-01 空邮件列表 | 续测通过 | 续测见“Shard E 续测记录”；控制器邮件数 0，标题栏 `0 封`，Mongo `Mails:{}` |

## 4. 本轮临时测试条件

Unity PlayMode 初次登录时，项目当前本地脏改动把 `client/ProjectSettings/ProjectSettings.asset` 的 `runInBackground` 改为 `0`。Codex/终端拿焦点后 Unity 虽显示 `isPlaying=True`、`isPaused=False`，但 `Time.time` 和 `frameCount` 不增长，导致登录只建立 TCP Session、不发送 `HandshakeReq`。本轮未修改 ProjectSettings，只在运行时执行：

```csharp
Application.runInBackground = true;
```

之后 Unity 帧恢复推进，登录、创建角色和进图继续通过。该问题已记录到 bug 文档。

## 5. Shard E 续测记录

- 续测时间：2026-06-08 23:04-23:24（Asia/Shanghai）。
- Unity Editor 初始未运行；通过 `tools/unity/uloop.sh launch /Users/gejiayi/UnityProjects/game1/client` 启动，uLoop 检测到 stale `Temp/UnityLockfile` 并清理 `Temp/` 后打开项目。
- 第一次从仓库根目录启动服务端失败：`Could not find file '/Users/gejiayi/UnityProjects/game1/appsettings.json'`；随后按既有记录从 `server/` 目录执行 `dotnet run --project Server.csproj`，监听 `0.0.0.0:9000`。
- Unity 编译：`tools/unity/uloop.sh compile` 通过，ErrorCount 0，WarningCount 0。
- Unity Error 日志：续测前后 `tools/unity/uloop.sh get-logs --log-type Error --max-count 20` 均为 0。
- PlayMode 续测仍运行时设置 `Application.runInBackground = true`，不写回 ProjectSettings。
- 复用账号 `qa_0608_codex_2135` 登录成功，服务端日志出现 `HandshakeReq`、`LoginReq`、`登录成功 ... 角色数:1`；进入角色 `测06082142` 后服务端记录 `玩家进入地图 RoleId:322370565182590976 MapId:1 Line:1 Pos:(100,100)`。

| 用例 | 实测结果 | 证据 |
|------|----------|------|
| MAIL-01 空邮件列表 | 通过 | `MailPanel` 显示；动态读取：`controllerMails=0 selected=-1 countText=0 封 listChildren=0 detailTitle=选择一封邮件查看详情`；Mongo：`role_mail_component` 为 `{ _id: 322370565182590976, Mails: {} }`；截图 `client/.uloop/outputs/Screenshots/Rendering_20260608_231754_477.png` |
| MAIL-02 无邮件操作 | 功能逻辑通过，手工可见点击路径失败 | 通过按钮对象触发：`全部已读` Toast `没有未读邮件`，`领取附件` Toast `没有可领取的附件`，`删除已读` Toast `没有可删除的邮件`；但按钮坐标在画面下方不可见，见 bug 文档 |
| MAIL-03 正向邮件流程 | 未执行，缺少测试数据前置 | 测试手册已说明当前无玩家可点击的系统邮件生成入口；本角色 Mongo `Mails:{}`，需要预置邮件或补 Robot/测试工具场景 |
| TASK-02 接取任务 | 失败 | 输入 `1` 接取任务返回 Toast：`接取任务失败: An error occurred while serializing the Tasks property of class Game.Components.GameTask.RoleTaskComponent: When using DictionaryRepresentation.Document key values must serialize as strings.`；客户端任务数仍为 0；再次接取返回 `任务已接取`，说明服务端内存已变更；见 bug 文档 |
| TASK-03 接取输入校验 | 部分通过 | 空输入 Toast `请输入任务ID`；非数字输入 Toast `无效的任务ID`；不存在任务 `9999` Toast `任务不存在`，服务端日志 `任务配置不存在 TaskId:9999`；任务列表满未测 |
| TASK-05 提交和放弃 | 部分通过 | 因 TASK-02 持久化失败，完整 UI 流程不可判定；直接提交任务 1 返回 `任务未完成`；放弃确认弹窗出现，确认后 Toast `已放弃任务`，Mongo `role_task_component` 恢复 `{ Tasks:{} }` |
| TASK-04 任务进度 | 未执行 | TASK-02 持久化失败阻断完整任务链路；击杀野猪进度属于战斗长流程，后续需在修复后重跑 |

补充观察：

- `MailPanel` 底部按钮 `ReadBtn/ClaimBtn/DeleteBtn/CloseBtn` 的 uLoop 标注坐标为 `SimY=1110`，超出 1920x1080 游戏画面；动态读取按钮世界 Y 为 `(-49,-11)`。
- `TaskPanel` 底部输入和按钮 `AcceptInput/AcceptBtn/SubmitBtn/GiveUpBtn/CloseBtn` 的 uLoop 标注坐标为 `SimY=1090`，超出 1920x1080 游戏画面；动态读取世界 Y 为 `(-29,9)`。
- 续测再次复现出生点附近怪物持续攻击玩家：2026-06-08 23:13:49 起服务端持续输出 `FightSystem 技能使用 ... Target:322370565182590976 Hits:1`。

## 6. 分布测试切分

后续长流程不要一次塞满上下文，按以下 shard 分布执行；每个 shard 独立写过程和 bug 增量。

| Shard | 范围 | 前置 | 建议记录 |
|-------|------|------|----------|
| A 登录和角色 | ENV-01/02、NET-01、USER-01/02/03、ROLE-01/02/03/04 | 服务端、MongoDB、Unity PlayMode | 登录账号、角色名、截图、服务端日志、Unity 错误日志 |
| B 地图和移动 | MAP-01/02/03/04 | 已进入游戏 | 坐标、传送前后 MapId、移动纠正日志、截图 |
| C 双客户端/AOI | USER-03、MAP-05、聊天广播 | 两个账号或 Unity + Robot | 两端账号、靠近/远离截图、AOI Ntf/服务端日志 |
| D 战斗和怪物 | FIGHT-01/02/03/04/05、UI-02 | 进入有怪物地图 | 目标选择、技能请求、HP 变化、掉落、怪物刷新 |
| E 背包/邮件/任务 | BAG-01/02/03/04、MAIL-01/02/03、TASK-01/02/03/04/05 | 需要测试物品/邮件/任务数据 | 控制器数据、协议返回、Toast、截图；正向邮件需预置数据或 Robot |
| F 聊天和断线重连 | CHAT-01/02/03/04、NET-02/03 | 至少一个角色在线，私聊需双端 | 聊天 Ntf、冷却错误、断线时间线、重连结果 |

## 7. 交接入口

- 测试手册：`docs/test-manual.md`
- 本轮过程：`docs/test-process-20260608-unity.md`
- 本轮 bug：`docs/test-bugs-20260608-unity.md`
- uLoop 入口：`tools/unity/uloop.sh`
- Robot 手册：`docs/Robot使用手册.md`

## 8. 当前交接状态

- 当前 Unity PlayMode 已停止；本轮启动的 `dotnet run --project Server.csproj` 服务端进程已结束。
- Shard E 从 MAIL-01 开始已续测到 TASK-05 的可执行分支；MAIL-03 因缺少邮件数据未执行，TASK-04 因 TASK-02 持久化失败未执行。
- 后续优先修复 `BUG-20260608-004` 和 `BUG-20260608-005`，再重跑 MAIL-02 的可见点击路径、TASK-02、TASK-04、TASK-05。
- `server/log/` 是本轮服务端运行生成日志，按项目规则不要提交。
- 2026-06-08 本轮测试文档空白检查通过；当时全仓检查被 `client/ProjectSettings/ProjectSettings.asset` 的尾随空白阻断。2026-07-15 现场收口已消除该 Editor 自动改写，旧 `docs/line-kb/` 也已迁入中枢并淘汰。
