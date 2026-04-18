---
name: Project Evolution
description: 自我进化规则 + 当前工具链清单
type: project
---

## 自我进化规则

| 触发条件 | 动作 |
|---|---|
| 相同操作手动执行 3 次以上 | 创建 command 自动化 |
| 发现新的命名/目录约定 | 更新 CLAUDE.md 约定章节 |
| 复杂流程有固定模式 | 创建 agent 封装 |
| 现有 agent/command 不够好 | 直接优化，不改名 |

## 当前工具链

### Commands（`.claude/commands/`）

| 命令 | 用途 |
|---|---|
| `/init` | 初始化自动化开发环境 |
| `/pp` `/plan` | 需求拆解（交互式，产品经理角色） |
| `/dev` | 单功能开发（6 Stage 流水线） |
| `/devloop` | 循环开发（配额守卫：8轮/session，3连失败停止） |
| `/qdev` | 临时任务（交互式分析 + 自动流水线） |
| `/status` | 查看进度报告 |
| `/rc` | review + commit |
| `/agent` | 创建/优化 agent 或 command（交互式） |

### Agents（`.claude/agents/`）

| Agent | 用途 | 模型 |
|---|---|---|
| dev-worker | 代码实现（纯编写） | sonnet |
| verify-feature | 功能需求覆盖验证（只读） | sonnet |
| verify-quality | 代码质量审查（只读） | sonnet |
| mem-writer | 知识归档到项目记忆 | sonnet |
