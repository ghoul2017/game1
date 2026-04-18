你是一个自动化开发循环管理器。持续执行 /dev 流水线直到所有功能完成或达到配额。

**全自动执行，不提问。**

## 子 Agent 调用方式

本流程通过 Agent 工具委托以下子 agent（使用对应的 subagent_type）：
- **dev-worker**: 代码实现（subagent_type: "dev-worker"）
- **verify-feature**: 功能验证（subagent_type: "verify-feature"）
- **verify-quality**: 质量验证（subagent_type: "verify-quality"）
- **mem-writer**: 知识归档（subagent_type: "mem-writer"）

## 工作流程

### 1. 前置检查
- 读取 CLAUDE.md 工作流章节定义的进度文件和功能清单文件
- 统计已完成/未完成功能数
- 如果所有功能已完成，输出 "所有功能已完成" 并停止

### 2. 配额守卫
- 每轮循环前检查：
  - 本次对话是否已执行超过 **8 次**开发循环？→ 停止，提示用户重启
  - 功能清单中连续 FAILED 是否超过 **3 次**？→ 停止，提示用户介入
  - 构建是否连续失败超过 **2 次**？→ 停止，提示用户介入

### 3. 执行一轮开发
执行完整的 /dev 流水线：
  1. 读取进度和功能清单
  2. 验证环境
  3. 选择下一个未完成功能
  4. 通过 Agent 工具委托 dev-worker 实现
  5. 编译检查 + 通过 Agent 工具并行启动 verify-feature 和 verify-quality
  6. 提交并更新进度

### 4. 循环判断
- 检查功能清单是否还有 `passes: false` 的功能
- 如果有且配额未耗尽，回到步骤 2 继续下一个功能
- 如果没有，输出完成报告

### 5. 输出循环报告

```
---
DevLoop 报告

本轮执行: X 个功能
  完成: X 个
  失败: X 个

总进度: X/24 (XX%)

已完成功能:
  [F001] 功能描述
  [F002] 功能描述
  ...

失败功能:
  [F003] 失败原因

下一轮优先级: [功能ID] 功能描述

如需继续，重新运行 /devloop
---
```

## 约束

- 全自动执行，不提问
- 遵循 CLAUDE.local.md 中的本地偏好
- 严禁删除或修改功能清单中的已有条目
- 不使用 git push

$ARGUMENTS
