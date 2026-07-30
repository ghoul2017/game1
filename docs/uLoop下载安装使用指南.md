# uLoop 下载安装与 AI 使用指南

目标读者：AI。

任务目标：在用户授权的验证范围内确认 uLoop 是否可用，并用它执行 Unity 编译、日志检查、PlayMode、截图或必要的 UI 自动化验证。

## 执行边界

- 默认只允许读取项目状态并运行不会改变游戏数据的编译、日志和查询命令。
- 进入 PlayMode、自动登录、操作 UI、写 Unity 资源或改变 MongoDB 测试数据前，必须确认当前任务已经明确授权对应副作用。
- 不手动编辑 `.meta`、Unity 生成的 csproj、`UserSettings/UnityMcpSettings.json` 或 uLoop 本地生成目录。
- 命令输出与本文冲突时，以当前 manifest、脚本、Unity 日志和实际命令输出为准；无法确认时停止并报告，不自行改包版本或全局环境。

## 权威入口

- Unity 项目路径：`client/`
- Unity 版本：`2022.3.62f3c1`，以 `client/ProjectSettings/ProjectVersion.txt` 为准
- Unity 包：`client/Packages/manifest.json` 中的 `io.github.hatayama.uloopmcp`，当前版本 `2.1.10`
- Unity 包来源：`client/Packages/manifest.json` 与 `client/ProjectSettings/PackageManagerSettings.asset` 中的 OpenUPM `https://package.openupm.com`
- CLI 入口：`tools/unity/uloop.sh`
- CLI 版本：脚本固定调用 `npx -y uloop-cli@2.1.10`
- 本地生成目录：`client/.uloop/`、`client/.codex/skills/`、`client/UserSettings/UnityMcpSettings.json`

如果本文与上述权威文件冲突，以上述文件和实际命令输出为准。

uLoop 是开发工具，不是游戏运行时依赖。任何 Player 的 BuildReport、WebGL `ManagedStripped` 或最终输出中出现
`uLoopMCP` 程序集、脚本或资源，都视为工具泄漏并阻断出包；2.1.10 的一次性验收见
`TEST-ULOOP-PLAYER-01`，持续检查任务见 `GAME1-ENG-08`。

## AI 执行卡片

在仓库根目录执行：

```bash
tools/unity/uloop.sh launch
tools/unity/uloop.sh get-version
tools/unity/uloop.sh compile
tools/unity/uloop.sh get-logs --log-type Error --max-count 20
```

成功标准：

- `get-version` 能返回 Unity 版本和 uLoop 版本。
- `compile` 返回 `Success=true`，并确认 `ErrorCount=0`。
- Error 日志没有新的阻断性错误。

常规验证优先使用编译、测试、日志和静态检查。除非用户明确要求“进入游戏”“登录到游戏里”“用 Unity/uLoop 操作界面”或等价指令，不主动用 uLoop 执行耗时的 PlayMode UI 流程。

## 下载安装

### 1. 准备环境

本项目的 uLoop 安装依赖以下本机条件：

- 已安装 Unity Hub。
- 已安装“权威入口”所列版本的 Unity Editor。
- 已安装 Node.js 22 或更高版本，并能执行 `npx`；uLoop 2.1.10 CLI 的 `engines.node` 要求为
  `>=22.0.0`。
- 本机可以访问 OpenUPM 和 npm registry。

首次使用或升级 Node.js 后先确认：

```bash
node --version
npx --version
```

### 2. 运行权限基线

- 在项目目录、Unity 用户缓存、npm 用户缓存和构建输出目录可写的前提下，Git、.NET、Node/`npx`、
  Unity Editor、uLoop CLI 和既有 WebGL 模块的日常运行都不需要 Windows 管理员权限。
- 安装或更新 Program Files 下的软件与 Unity 模块、修改 UAC/防火墙/系统证书/系统 PATH、控制 Windows
  服务时才可能需要管理员权限；这些系统操作仍需当前任务单独授权。
- 本项目不全局安装 uLoop CLI；`npx -y` 使用用户缓存。如果 npm 全局目录不可写，应改用用户可写 prefix，
  不以管理员权限启动 Unity 来绕过目录权限。
- uLoop 的动态代码等级和工具开关是工具内部授权，不是 Windows 管理员权限。默认保持
  `Restricted`，并关闭第三方工具；只有当前任务确实需要被 Restricted 阻止的 API 且负责人授权对应
  副作用时，才临时使用 `FullAccess`。

### 3. 下载 Unity 端 uLoop 包

本项目已经把 Unity 包配置提交到仓库，不需要 AI 手动编辑 `manifest.json`：

```json
"io.github.hatayama.uloopmcp": "2.1.10"
```

Unity 打开 `client/` 时会按 `manifest.json` 和 `packages-lock.json` 从 OpenUPM 下载并解析该包。若是新机器首次使用，先启动 Unity 项目：

```bash
tools/unity/uloop.sh launch
```

如果 Unity Package Manager 报包源问题，先检查：

- `client/Packages/manifest.json` 是否存在 OpenUPM scoped registry。
- `client/ProjectSettings/PackageManagerSettings.asset` 是否包含 `https://package.openupm.com` 和 `io.github.hatayama.uloopmcp`。
- 本机网络是否能访问 OpenUPM。

### 4. 下载 CLI

本项目不要求全局安装 `uloop`。统一使用包装脚本：

```bash
tools/unity/uloop.sh --help
```

脚本会通过 `npx -y uloop-cli@2.1.10` 下载并运行 CLI。对常用 Unity 工具命令，脚本会自动追加 `--project-path <仓库>/client`，因此 Agent 不需要在常规命令里手写项目路径。

升级时必须同步修改 Unity 包、`packages-lock.json` 和 CLI 固定版本，并按 `GAME1-ENG-09` 在仓库外任务副本完成验证；不得只升级其中一处。

### 5. 安装或刷新 AI 技能

如果当前 AI 环境需要本地 uLoop 技能说明，执行：

```bash
tools/unity/uloop.sh skills install --codex --flat
tools/unity/uloop.sh skills list --codex --flat
```

这些技能会写入本地生成目录，例如 `client/.codex/skills/`。该目录只服务本机 AI 使用，不是项目权威配置，不要提交。

## 常用命令

### 启动和连接

```bash
tools/unity/uloop.sh launch
tools/unity/uloop.sh get-version
tools/unity/uloop.sh list
```

`launch` 会按 `client/ProjectSettings/ProjectVersion.txt` 选择 Unity 版本并等待 Editor 可连接。若已有 Unity 实例但状态异常，并且已确认没有未保存 Unity 工作，可按需使用：

```bash
tools/unity/uloop.sh launch --restart
```

Unity 必须以普通用户权限运行。若窗口标题或日志提示以 Administrator 权限运行，停止该实例并报告环境条件；
Unity 会执行项目脚本、第三方 Editor 插件和构建钩子，高权限会把误操作或恶意代码的影响从项目目录扩大到
系统设置、注册表、其他进程及任意可写文件，也会掩盖普通权限环境才能暴露的访问控制问题。除非负责人针对
明确任务单独授权，不把高权限 Unity 的自动化或 Player 构建作为正式验证证据。

### 编译和日志

```bash
tools/unity/uloop.sh compile
tools/unity/uloop.sh compile --force-recompile true --wait-for-domain-reload true
tools/unity/uloop.sh get-logs --log-type Error --max-count 20
tools/unity/uloop.sh clear-console
```

修改客户端、Shared、整体结构或跨端协议后，优先按项目规则运行 `dotnet build game1.sln`。uLoop 编译只覆盖 Unity Editor 侧编译状态，不能替代所有 .NET 构建验证。

### PlayMode

```bash
tools/unity/uloop.sh control-play-mode --action Play
tools/unity/uloop.sh control-play-mode --action Stop
tools/unity/uloop.sh control-play-mode --action Pause
```

PlayMode UI 自动化较慢，只有用户明确要求时才进入游戏或操作界面。进入 PlayMode 后如果 Unity 失焦导致帧不推进，可先聚焦 Unity：

```bash
tools/unity/uloop.sh focus-window
```

必要时可以用动态代码临时打开运行时后台执行，但不要擅自写回 `ProjectSettings`：

```bash
tools/unity/uloop.sh execute-dynamic-code --code 'using UnityEngine; Application.runInBackground = true; return Application.runInBackground;'
```

### 截图和 UI 坐标

```bash
tools/unity/uloop.sh screenshot
tools/unity/uloop.sh screenshot --capture-mode rendering
tools/unity/uloop.sh screenshot --capture-mode rendering --annotate-elements
tools/unity/uloop.sh screenshot --capture-mode rendering --annotate-elements --elements-only
```

进行 UI 点击前，优先用 `--annotate-elements` 获取元素坐标和交互元数据。截图默认输出到 `client/.uloop/outputs/Screenshots/`，这是本地生成目录，不要提交。

### 层级和对象检查

```bash
tools/unity/uloop.sh get-hierarchy
tools/unity/uloop.sh find-game-objects --search-mode Contains --name-pattern LoginPanel
```

检查对象时先用内置查询命令。只有内置命令不能表达检查目标时，再使用动态代码。

### 动态代码

```bash
tools/unity/uloop.sh execute-dynamic-code --code 'using UnityEngine; return Time.frameCount;'
```

动态代码可以访问 Unity API，适合检查场景、Prefab、SerializedObject、PlayMode 状态和 UI 控件。使用约束：

- 先读 `client/.codex/skills/uloop-execute-dynamic-code/SKILL.md` 中的规则和示例。
- 代码写直接语句，不包 class、namespace、method。
- 不用动态代码创建或编辑 `.cs`、`.asmdef`、`.meta`。
- 不手写 `client/UserSettings/UnityMcpSettings.json`。
- 修改 Unity 资源、场景或 Prefab 后，按项目规则用 Unity/日志/测试验证。

### 测试

```bash
tools/unity/uloop.sh run-tests
```

如果测试命令失败，先读结构化输出和 Unity Error 日志，再判断是测试失败、编译失败、Unity 未连接还是锁文件问题。

## 故障处理

### CLI 连接失败

先确认 Unity 是否运行：

```bash
tools/unity/uloop.sh launch
tools/unity/uloop.sh get-version
```

如果返回“Unity Editor is running, but Unity CLI Loop server is not”，不要反复执行 `launch`。在对应 Unity Editor 中打开 `Window > Unity CLI Loop > Server`；状态为 `Stopped` 时点击 `Start Server`，再执行：

```bash
tools/unity/uloop.sh get-version
```

2026-07-15 基线验证确认：当 `client/UserSettings/UnityMcpSettings.json` 的本机状态为 `isServerRunning=false` 时，Unity 可正常完成项目加载，但 `launch` 会继续等待动态代码服务并在 180 秒后超时。该文件属于被忽略的本机状态，不手动改写；应通过 Unity 的 Server 窗口启动服务。

如果 Unity 正在运行但 CLI 仍认为 Unity 忙或连接失败，清理 stale lock：

```bash
tools/unity/uloop.sh fix
tools/unity/uloop.sh get-version
```

`fix` 只处理 stale lock，不能代替启动已停止的 Unity CLI Loop Server。

### 包未安装或命令不存在

检查 Unity 包配置：

```bash
rg -n "uloop|hatayama|openupm" client/Packages client/ProjectSettings
```

然后重新启动 Unity：

```bash
tools/unity/uloop.sh launch --restart
tools/unity/uloop.sh get-version
```

### 技能目录缺失

重新安装本地 AI 技能：

```bash
tools/unity/uloop.sh skills install --codex --flat
```

如果只是 CLI 工具定义缓存异常，可同步：

```bash
tools/unity/uloop.sh sync
```

### 编译失败

执行顺序：

```bash
tools/unity/uloop.sh compile
tools/unity/uloop.sh get-logs --log-type Error --max-count 50
```

结论必须来自命令输出、日志或已读取文件。未复现的问题标记为“未重现”，不要硬改代码、配置或全局环境。

## 不要做的事

- 不手动新增、编辑或补齐 `.meta`。
- 不手动修改 `client/Assembly-CSharp*.csproj`。
- 不提交 `client/.uloop/`、`client/.codex/skills/`、`client/UserSettings/`、`Library/`、`Temp/`、`Logs/`。
- 不把 uLoop 自动登录、进入游戏或 PlayMode UI 操作作为默认验证路径。
- 不在失败未定位时反复重试同一命令并包装成完成。

## 最终报告

完成当前授权范围后，向用户报告：执行过的命令、Unity/uLoop 版本、编译与 Error 日志结果、是否进入 PlayMode、是否改变测试数据、生成或刷新了哪些本地文件，以及仍未验证的风险。若任一步失败，附上原始错误摘要和再次处理条件，不把连接成功或命令返回当作目标行为已经通过。
