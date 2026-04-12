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
├── Resources/UI/     # UI 面板预制体（可选，无预制体时代码动态构建）
└── Scripts/
    ├── GameShared/   #   directory junction → shared/（消息、IMessage、PacketCodec）
    ├── Core/         #   核心框架（UIManager, UIPanel, UIBuilder, GameEntry, UIToast, UIMessageBox）
    ├── Editor/       #   编辑器工具（SceneCreator）
    ├── Loading/      #   加载界面（LoadingPanel）
    ├── Login/        #   登录 & 角色选择（LoginPanel, RoleSelectPanel）
    ├── Game/         #   主界面 & 游戏场景（MainPanel, PlayerController, MapGround 等）
    └── Network/      #   网络层（MessageDispatcher, MsgTypeRegistry）
```

`Library/`、`Temp/`、`obj/` 为 Unity 自动生成，不要修改。

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
- **地面生成** — `MapGround.Generate()` 根据地图配置程序化生成地面+边界+装饰物
- **场景入口** — `GameSceneEntry` 初始化场景，提供静态引用 `PlayerController`/`MapGround`
- **UI叠加** — `UIManager` Canvas（ScreenSpaceOverlay + DontDestroyOnLoad）自然叠加在3D场景上
- **所有程序化材质** 必须使用 `Shader.Find("Universal Render Pipeline/Lit")`
