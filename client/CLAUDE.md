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
    ├── Logic/        #   客户端纯逻辑层（零 UnityEngine 依赖）
    │   ├── Controllers/ #  业务控制器（Login/Bag/Mail/Task/Chat/Fight/RoleSelect/Map 共 8 个）
    │   ├── Handlers/ #     消息 Handler（LoginHandlers, BagHandlers 等 8 个，含 MapHandlers）
    │   ├── Data/     #     GameData, MapObjectType, MapData（坐标转换元组）
    │   ├── NetClient.cs    #  网络管理器（纯 C# 类，非 MonoBehaviour）
    │   ├── Context.cs      #  客户端上下文（Module + NetworkComponent）
    │   ├── IClientUI.cs    #  UI 桥接接口 + INetworkSender + ClientContext
    │   └── IPanels.cs      #  面板空标记接口
    └── View/         #   Unity 表现层
        ├── Core/     #     核心框架（UIManager, UIPanel, UIBuilder, GameEntry 等）
        ├── Panels/   #     面板（LoginPanel, BagPanel, MainPanel 等，纯渲染）
        ├── Scene/    #     场景对象（PlayerController, MapGround, MonsterView 等）
        └── Editor/   #     编辑器工具（SceneCreator）
```

`Library/`、`Temp/`、`obj/` 为 Unity 自动生成，不要修改。

## 网络层

- **Context** — 客户端上下文（`Client.Logic.Context`），类似于服务端 `Context`，挂载 `NetworkComponent`
- **NetClient** — 纯 C# 网络管理器（已从 MonoBehaviour 重构），底层基于 `Context.Network.ConnectAsync()` + `TcpSession`。GameEntry.Update 驱动 `NetClient.Instance.Update(deltaTime)`
- **INetworkSender** — Logic 层定义的网络发送接口，NetClient 实现。Logic 通过 `ClientContext.Network?.Send(msg)` 发消息
- **NetworkComponent** — 共享网络组件，客户端复用其 `ConnectAsync` 连接远端服务器
- **TcpSession** — 连接会话，通过 `PostToMainThread` 回调将消息投递到主线程队列
- **MessageRouter** — 自动扫描 `[MessageHandler]` 标记的静态方法（在 `NetworkComponent` 构造函数中自动注册），客户端所有消息处理统一使用此机制

## Controller 模式

- **Controller 单例** — 每个子系统一个 Controller（Logic/Controllers/），持有业务状态 + 处理网络响应 + 触发事件
- **数据流**：`Message → Handler → Controller.HandleXxx() → 更新状态 + 触发事件 → Panel 订阅刷新`
- **用户操作流**：`Panel 按钮 → Controller.Action() → ClientContext.Network.Send(msg)`
- **Panel 纯渲染**：读 Controller 状态，不持有业务数据，不直接发网络消息
- **事件订阅**：Panel 在 `OnShow` 中 `Controller.Instance.OnDataChanged += Refresh`，`OnHide` 中 `-=`
- **ConfigManager 查询**：物品品质颜色等显示格式化保留在 View

## UI 框架约定

- **UIPanel 基类** — 所有面板继承 `Client.View.UIPanel`，重写 `OnInit()`/`OnShow()`/`OnHide()`
- **UIManager** — 通过 `UIManager.Instance.ShowPanel<T>()` / `ReplacePanel<T>()` 管理面板生命周期
- **UIBuilder** — `Client.View.UIBuilder` 提供代码构建 UI 元素的静态工具方法
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

## 配置表

- **ConfigManager** — 共享 `Shared.Core.Config.ConfigManager`，双端统一 API：`ConfigManager.Get<T>(id)` / `GetAll<T>()` / `GetDict<T>()`
- **初始化** — `GameEntry.Start()` 中调用 `ConfigManager.Instance.LoadAll(tablesDir)`，开发环境指向 `server/Game/Data/Tables/`
- **CSV 读取** — 使用 `SimpleCsvReader`（纯 .NET，无外部依赖），不用 CsvHelper
- **配置类** — `shared/Common/Config/` 下标注 `[ConfigTable]` 的类，双端共享
- **热更新** — 客户端不启用，仅服务端通过 `FileSystemWatcher` 热更新
