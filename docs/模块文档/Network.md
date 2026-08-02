# Network 模块

目标读者：人类。

## 定位

Network 模块为 Client、Server 和 Robot 提供同一 `PacketCodec`、Protobuf 消息路由、RPC 关联与连接生命周期。
2026-08 的正式平台改造把字节传输从会话逻辑中拆开，使微信 WebGL 不依赖 `.NET System.Net.Sockets`。

## 传输边界

| 类型 | 职责 |
| --- | --- |
| `IByteTransport` | 连接状态、异步读取、发送和关闭的最小字节契约 |
| `SocketByteTransport` | Server、Robot、Editor 和原生客户端的 Socket 实现 |
| `WeChatClientTransport` | 微信 WebGL 的 `WX.CreateTCPSocket()` 适配及回调/UTF-8 桥 |
| `TcpSession` | 封包、路由、选择可靠/最新状态发送语义和一次性断开完成 |
| `NetworkComponent` | 监听/连接、Session 集合与断开通知 |

`TcpSession` 选择 Reliable 或 Latest 发送语义；`SocketByteTransport` 与 `WeChatClientTransport` 各自持有
1 MiB 有界可靠队列、最新状态槽和发送循环。可靠队列溢出会暴露失败并断开，不能无限积压；普通高频状态通过
`SendLatest` 通过每个 Transport 的单一 latest 槽替换尚未发送的上一普通状态，关键结果继续可靠有序。当前每条
Session 只把远征完整快照送入该槽；增加第二类独立 latest 状态前必须先扩展槽键。Transport 发送循环拥有队列和传输的收尾，
`TcpSession` 的断开完成只发布一次，避免接收循环在缓冲已释放后继续写入。

“可靠”只表示当前 Transport/TCP 生命周期内的有界有序发送；当前没有应用层 ACK，也不跨断线重放表现事件。

客户端 `NetClient` 为每次连接尝试分配代次。旧异步连接即使稍后完成，也不能替换新 Session 或发布陈旧成功事件；
主动断开会取消当前尝试。玩家业务还必须通过 RoleId 与 `PlayerOnline.Session` 双向绑定校验，陈旧连接不能仅凭旧
RoleId 继续操作当前玩家。

## 正式配置启动门禁

`GameEntry` 在创建业务 `Context`、UI 和网络前，使用 `UnityWebRequest` 异步取得 `ConfigManager` 列出的全部 CSV。
`SimpleCsvReader` 严格校验表头、列数、主键、类型和重复项；`ConfigManager` 先在临时快照解析全部表，全部成功后
才原子切换。缺表、空表、格式错误或请求失败会阻断启动，不留下半加载全局状态。Server 继续读取构建输出中的
本地表文件，并复用同一解析逻辑。

## 平台边界

微信 SDK 条件分支已通过当前 SDK 程序集的补充 API 形状编译，但当前精确 Unity 安装缺少 WebGL Build Support，
没有得到本轮 WebGL/IL2CPP 产物。正式微信登录、RPC、推送、主动/异常断开和重连仍须在开发者工具与目标设备验证。
平台原生 TCP 是明文传输；公网域名、证书、WSS 或网关方案由独立外联任务决定。

## 本地 Editor 启动器

`Tools/Online Technical/Build, Start Server and Play` 从 `global.json` 解析精确 SDK，构建后以仓库 `bin/` 为
工作目录运行 `Server.dll`。本地模式只监听 `127.0.0.1:9000`；启动前检测 MongoDB 27017 和 Server 9000，
拒绝接管未知监听者。

所有权由 Unity PID、Server PID/启动时间、随机 token 和控制文件共同确认。停止或退出 Unity 时先请求优雅停服，
最多等待 20 秒；超时也只终止完全匹配的自有进程。状态和日志入口位于 `client/Library/Game1LocalServer`，不提交。
该工具是永久 Editor 测试能力，不进入 Player。当前只验证 Windows；自动 MongoDB、跨平台编排和测试 Player 伴随
服务由 `GAME1-ENG-10` 跟踪。
