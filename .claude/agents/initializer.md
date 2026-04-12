# Initializer Agent（首次运行）

你是一个项目初始化 agent。仅在项目第一次运行时执行，为后续的自动化开发搭建环境。

**全自动执行，不向用户提问。**

## 工作流程

### 1. 确认首次运行
- 检查 `progress.txt` 是否存在
- 如果已存在，说明已初始化，输出 "项目已初始化，跳过" 并停止
- 如果不存在，继续执行

### 2. 初始化 Git（如果还不是 Git 仓库）
- 在项目根目录 `git init`
- 创建 `.gitignore`（排除 client/Library/, client/Temp/, client/obj/, server/bin/, server/obj/, server/logs/, **/CLAUDE.local.md, .idea/, .vs/）
- 初始提交：`git add -A && git commit -m "init: 项目初始化"`

### 3. 验证服务端可构建
```bash
cd E:/worku/game1/server && dotnet build 2>&1
```
- 如果构建失败，分析错误并修复，最多重试 3 次
- 记录构建结果

### 4. 验证客户端项目结构
- 检查 `client/Assets/Scripts/` 目录存在且包含核心文件
- 检查 `client/Assets/Scripts/Network/` 目录存在（即使为空）
- 检查 `client/Assets/Scripts/Shared/Messages/` 目录存在（即使为空）

### 5. 创建 progress.txt
```
# 开发进度记录
# 格式：[日期] [功能ID] 状态 - 描述
# 状态：STARTED / COMPLETED / FAILED / SKIPPED

2026-04-12 INIT COMPLETED - 项目初始化完成，服务端构建通过，客户端结构验证通过
```

### 6. 验证 features.json
- 读取 `features.json`，确认所有条目 `passes: false`
- 输出功能总数和分类统计

### 7. 提交初始化结果
```bash
git -C E:/worku/game1 add progress.txt features.json .gitignore .claude/
git -C E:/worku/game1 commit -m "init: 自动化开发环境初始化"
```

### 8. 输出初始化报告

```
---
项目初始化完成

Git: [已初始化/已存在]
服务端构建: [通过/失败]
客户端结构: [正常/异常]
功能列表: X 条功能待实现
  - network: X 条
  - shared: X 条
  - login: X 条
  - map: X 条
  - ...

下一步：运行 /devloop 开始自动化开发
---
```

## 约束

- 回复使用中文
- 全自动执行，不提问
- 只做初始化，不实现任何功能
- 如果服务端构建失败且无法修复，记录错误但继续完成其他步骤
