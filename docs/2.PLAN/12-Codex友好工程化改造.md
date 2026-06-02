# Codex 友好工程化改造

## 目标

把当前项目从“文档约束为主”推进到“编译边界和脚本验证为主”，降低 Codex 后续新增功能时的误改、漏改和不可验证风险。

## 修改范围

- 根目录项目结构和 `.csproj`
- `client/Assets/Scripts/`
- `server/`
- `robot/`
- `scripts/`
- `docs/line-kb/`
- `docs/7.遗留问题.md`

## 实施步骤

1. 独立共享代码项目
   - 新建 `shared/Shared.csproj`，承载当前 `client/Assets/Scripts/shared`。
   - 新建 `client-logic/ClientLogic.csproj`，承载当前 `client/Assets/Scripts/Logic`。
   - `server/Server.csproj`、`robot/Robot.csproj` 从 `Compile Include` 改为 `ProjectReference`。
   - Unity 端保留源码可见路径或用 `.asmdef`/链接方式接入，迁移时必须同步 `.meta`。

2. 增加 Unity asmdef 编译边界
   - `client/Assets/Scripts/shared` 建立 `Game.Shared.asmdef`。
   - `client/Assets/Scripts/Logic` 建立 `Game.ClientLogic.asmdef`，引用 `Game.Shared`。
   - `client/Assets/Scripts/View` 建立 `Game.ClientView.asmdef`，引用 `Game.Shared` 和 `Game.ClientLogic`。
   - `client/Assets/Scripts/View/Editor` 建立 Editor asmdef。
   - 验证 `Logic` 和 `shared` 不引用 `UnityEngine`。

3. 增加固定验证脚本
   - 新增 `scripts/check-server`：运行 `dotnet build server/Server.csproj`。
   - 新增 `scripts/check-robot`：运行 `dotnet build robot/Robot.csproj`。
   - 新增 `scripts/check-all`：运行 `dotnet build game1.sln`，并串联协议和边界检查。
   - 新增 `scripts/check-client-boundary`：搜索 `Logic/shared` 的真实 `UnityEngine` 依赖。
   - 文档-only 改动继续使用 `git diff --check`。

4. 解决客户端配置路径硬编码
   - 开发环境可继续读取 `server/Data/Tables`。
   - 正式构建读取 `Application.streamingAssetsPath + "/Tables"`。
   - 将 CSV 同步到 `client/Assets/StreamingAssets/Tables/`，并更新相关 `.meta`。
   - 完成后从 `docs/7.遗留问题.md` 移除或归档该问题。

5. 处理 Unity csproj 刷新风险
   - 删除、移动或改名 `.cs` 后明确触发 Unity Refresh 或提供脚本提醒。
   - 记录可复用命令或 Editor 操作到文档。
   - 完成后更新 `docs/7.遗留问题.md`。

6. 增加协议一致性检查
   - 检查所有 `IMessage` 类型都有协议标记。
   - 检查消息名或旧 MsgId 不重复。
   - 检查 Handler 请求类型已注册。
   - 纳入 `scripts/check-all`。
   - 若先执行 `11-字符串消息名协议.md`，本步骤按字符串消息名实现。

7. 拆分超大 View 文件
   - 优先拆 `MainPanel.cs`、`OtherPlayerManager.cs`、`TaskPanel.cs`。
   - 只拆表现职责，不改业务语义。
   - 每次拆分保持一个入口类，避免场景和 Prefab 引用大范围断裂。
   - 拆分后用 Unity Editor 验证 UI 和场景引用。

## 推荐顺序

1. 验证脚本和边界检查
2. 协议一致性检查
3. 客户端配置路径
4. Unity csproj 刷新处理
5. 独立 `shared` / `client-logic` 项目
6. Unity asmdef
7. 大 View 文件拆分

## 验证

- `git diff --check`
- `dotnet build server/Server.csproj`
- `dotnet build robot/Robot.csproj`
- `dotnet build game1.sln`
- Unity Editor 验证 asmdef、StreamingAssets、Prefab/场景引用。

## 风险

- 独立项目和 asmdef 会影响 Unity 编译边界，必须小步迁移并保留 `.meta`。
- 大 View 拆分容易影响场景对象引用，应按文件逐个拆，不和协议迁移混做。
- 验证脚本应先落地，后续结构改动才有可靠反馈。
