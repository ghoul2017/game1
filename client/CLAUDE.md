# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 项目概览

Unity 客户端项目，配对 .NET 8 C# 游戏服务端（`../server/`）。通过 TCP 二进制 Protobuf 协议与服务端通信：`[MsgId:2B][Length:4B][Payload]`（小端序）。

## Unity 环境

- **编辑器**: Unity 2021.3.26f1c1
- **渲染管线**: Universal Render Pipeline (URP) 12.1.11
- **.csproj**: 由 Unity 自动生成，不要手动编辑 `Assembly-CSharp.csproj` / `Assembly-CSharp-Editor.csproj`
- **IDE**: Visual Studio + "Game Development with Unity" 工作负载（`.vsconfig`）

## 构建与运行

- 通过 Unity Editor 打开项目（无 CLI 构建流程）
- Build via File > Build Settings
- `client.sln` 仅用于 IDE 智能提示

## 目录结构

```
Assets/
├── Scenes/           # Unity 场景
├── Settings/         # URP 渲染管线资源（Balanced/HighFidelity/Performant）
├── TutorialInfo/     # Unity 模板默认文件（可删除）
├── Plugins/          # 第三方插件（protobuf-net）
├── Prefabs/          # 通用预制体
├── Resources/
│   └── UI/           # UI 面板预制体（可选，无预制体时代码动态构建）
└── Scripts/
    ├── Core/         # 核心框架（UIManager, UIPanel, UIBuilder, GameEntry, UIToast, UIMessageBox）
    ├── Editor/       # 编辑器工具（SceneCreator）
    ├── Loading/      # 加载界面（LoadingPanel）
    ├── Login/        # 登录 & 角色选择（LoginPanel, RoleSelectPanel）
    ├── Game/         # 主界面 & 游戏场景（MainPanel, GameData, GameSceneEntry, PlayerController, PlayerView, CameraFollow, MapData, MapGround, VirtualJoystick）
    ├── Network/      # 网络层（待实现）
    └── Shared/
        └── Messages/ # 共享 Protobuf 消息定义
```

`Library/`、`Temp/`、`obj/` 为 Unity 自动生成，不要修改。

## 服务端协议集成

客户端与服务端通信的相关约定：
- 消息定义使用 protobuf-net（`[ProtoContract]`/`[ProtoMember]`）
- 服务端消息定义和 MsgId 在 `../server/server/Network/Messages/`
- 协议格式：`[MsgId:UInt16LE][Length:Int32LE][ProtobufPayload]`（小端序）
- 消息命名：`*Req`（请求）、`*Ack`（响应）、`*Ntf`（通知）
- MsgId 分段：101=user, 103=bag, 105=fight 等

## 核心编码原则

1. **服务端权威，客户端只负责表现** — 所有游戏逻辑（数值计算、状态校验、规则判定）由服务端完成，客户端仅做 UI 展示、动画表现、音效特效
2. **可引用服务端基础代码，禁止引用业务代码** — 客户端可直接引用 `../server/server/` 下的基础模块（Network/Messages、Config/Tables、Core/ 等），但不得引用业务系统代码（System/ 下的 Player、Bag、Mail、Chat、Task、Aoi 等）

## 约定

- `Assets/` 下的 C# 脚本由 Unity 运行时编译（非 .NET CLI）
- Unity 使用 `.meta` 文件跟踪资源，不要删除它们
- 保持 Unity 2021.3 API 兼容性
- 命名空间：`Client.Core`（框架）、`Client.Loading`、`Client.Login`、`Client.Game`

### UI 框架约定

- **UIPanel 基类** — 所有面板继承 `Client.Core.UIPanel`，重写 `OnInit()`/`OnShow()`/`OnHide()`
- **UIManager** — 通过 `UIManager.Instance.ShowPanel<T>()` / `ReplacePanel<T>()` 管理面板生命周期
- **UIBuilder** — `Client.Core.UIBuilder` 提供代码构建 UI 元素的静态工具方法
- **面板构建方式** — UI 在 `OnInit()` 中通过 `UIBuilder` 代码动态构建，无需预制体（也可放 Resources/UI/ 下）
- **面板切换流程** — `LoadingPanel → LoginPanel → RoleSelectPanel` → 加载 GameScene → `MainPanel`
- **RPG 配色** — 深蓝/紫底色 + 金色强调色（常量在 UIBuilder 中）

### 2.5D 场景约定

- **相机** — 正交投影，45度俯视角（rotation X=45），Size=8，跟随玩家
- **坐标映射** — 1格=1世界单位，服务端Y → Unity Z轴（`MapData.GridToWorld`）
- **角色表现** — `PlayerView` 负责几何体模型+名字板，`PlayerController` 负责移动逻辑
- **地面生成** — `MapGround.Generate()` 根据地图配置程序化生成地面+边界+装饰物
- **场景入口** — `GameSceneEntry` 初始化场景，提供静态引用 `PlayerController`/`MapGround`
- **场景切换** — `RoleSelectPanel.OnEnterGame()` → `SceneManager.LoadScene("GameScene")`
- **UI叠加** — `UIManager` Canvas（ScreenSpaceOverlay + DontDestroyOnLoad）自然叠加在3D场景上
- **所有程序化材质** 必须使用 `Shader.Find("Universal Render Pipeline/Lit")`

## 自我进化约定

项目工具链（agents、commands、scripts）在开发过程中持续自我优化。

### 触发规则

| 触发条件 | 动作 | 举例 |
|---|---|---|
| 相同操作手动执行 3 次以上 | 创建 command 自动化 | 多次手动创建消息+Handler → `/newmsg` |
| 发现新的命名/目录约定 | 更新 CLAUDE.md 约定章节 | 发现表名需要统一前缀 → 补充到命名约定 |
| 复杂流程有固定模式 | 创建 agent 封装 | review+提交模式 → `review-commit` agent |
| 现有 agent/command 不够好 | 直接优化，不改名 | devloop 发现遗漏步骤 → 补充到 devloop.md |
| 从 Java/服务端项目学到新模式 | 添加到约定章节 | 发现新的设计模式 → 补充到约定 |

### 现有工具链清单

#### Agents（`.claude/agents/`）

| Agent | 用途 | 触发方式 |
|---|---|---|
| build-check | 代码修改后自动编译检查并修复错误 | 代码修改完成后自动运行 |

#### Commands（`.claude/commands/`）

| 命令 | 用途 |
|---|---|
| （待建立） | |
