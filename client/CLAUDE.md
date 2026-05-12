# CLAUDE.md — 客户端

本文件为 Claude Code 在 `client/` 目录下工作时提供指导。项目级约定见根目录 `CLAUDE.md`。

## Unity 环境

- **编辑器**: Unity 2021.3.26f1c1
- **渲染管线**: Universal Render Pipeline (URP) 12.1.11
- **.csproj**: 由 Unity 自动生成，不要手动编辑
- **IDE**: Visual Studio + "Game Development with Unity" 工作负载

## 目录结构

```
Assets/
├── Scenes/           # Unity 场景
├── Settings/         # URP 渲染管线资源
├── Plugins/          # 第三方插件（protobuf-net）
└── Scripts/
    ├── shared/       #   双端共享代码（服务端通过 csproj Compile Include 引用）
    │   ├── Core/     #     ECS, Network, Config, Log, IdGenerator
    │   └── Common/   #     Protos（消息定义）, Config（配置表定义）, MsgIdConst
    ├── Core/         #   核心框架（Context, UIManager, UIPanel, UIBuilder, GameEntry, UIToast, UIMessageBox）
    ├── Config/       #   ClientConfigManager
    ├── Editor/       #   编辑器工具（SceneCreator）
    ├── Loading/      #   加载界面（LoadingPanel）
    ├── Login/        #   登录 & 角色选择（LoginPanel, RoleSelectPanel）
    ├── Game/         #   主界面 & 游戏场景（MainPanel, PlayerController, MapGround, BagPanel, MailPanel, TaskPanel, ChangeMapPanel, MonsterView, OtherPlayerManager, VirtualJoystick, CameraFollow, GameData, GameSceneEntry）
    └── Network/      #   网络层（NetClient, MessageDispatcher, NetworkStatusPanel, NtfMethodHandler）
```

`Library/`、`Temp/`、`obj/` 为 Unity 自动生成，不要修改。

## 网络层

- **Context** — 客户端上下文（`Client.Context`），类似于服务端 `Context`，挂载 `NetworkComponent`
- **NetClient** — MonoBehaviour 单例，底层基于 `Context.Network.ConnectAsync()` + `TcpSession`
- **NetworkComponent** — 共享网络组件，客户端复用其 `ConnectAsync` 连接远端服务器
- **TcpSession** — 连接会话，通过 `PostToMainThread` 回调将消息投递到 Unity 主线程
- **MessageRouter** — 自动扫描 `[MessageHandler]` 标记的静态方法（在 `NetworkComponent` 构造函数中自动注册）
- **MessageDispatcher** — 手动注册的按 MsgId 分发器（`TcpSession.OnMessageReceived` 转发，后续弃用）

## UI 框架约定

- **UIPanel 基类** — 所有面板继承 `Client.Core.UIPanel`，重写 `OnInit()`/`OnShow()`/`OnHide()`
- **UIManager** — 通过 `UIManager.Instance.ShowPanel<T>()` / `ReplacePanel<T>()` 管理面板生命周期
- **UIBuilder** — `Client.Core.UIBuilder` 提供代码构建 UI 元素的静态工具方法
- **面板构建** — UI 在 `OnInit()` 中通过 `UIBuilder` 代码动态构建，无需预制体
- **面板流程** — `LoadingPanel → LoginPanel → RoleSelectPanel` → GameScene → `MainPanel`
- **RPG 配色** — 深蓝/紫底色 + 金色强调色（常量在 UIBuilder 中）

## 2.5D 场景约定

- **相机** — 正交投影，45度俯视角（rotation X=45），Size=8，跟随玩家
- **坐标映射** — 1格=1世界单位，服务端Y → Unity Z轴（`MapData.GridToWorld`）
- **角色表现** — `PlayerView` 负责几何体模型+名字板，`PlayerController` 负责移动逻辑
- **怪物表现** — `MonsterView` 管理怪物/NPC视觉（通过 ObjectType 区分），`OtherPlayerManager` 管理其他玩家
- **地面生成** — `MapGround.Generate()` 根据地图配置程序化生成地面+边界+装饰物
- **场景入口** — `GameSceneEntry` 初始化场景，提供静态引用 `PlayerController`/`MapGround`
- **UI叠加** — `UIManager` Canvas（ScreenSpaceOverlay + DontDestroyOnLoad）自然叠加在3D场景上
- **所有程序化材质** 必须使用 `Shader.Find("Universal Render Pipeline/Lit")`
