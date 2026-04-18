---
name: Project Evolution
description: 项目进化历史、自我进化规则、参考项目、工具链清单
type: project
---

## 自我进化规则

| 触发条件 | 动作 |
|---|---|
| 相同操作手动执行 3 次以上 | 创建 command 自动化 |
| 发现新的命名/目录约定 | 更新 CLAUDE.md 约定章节 |
| 复杂流程有固定模式 | 创建 agent 封装 |
| 现有 agent/command 不够好 | 直接优化，不改名 |
| 从参考项目学到新模式 | 添加到约定章节 |

## 参考项目

- **E:\work\sgserver** — 成熟三国游戏服务器（Java 8, Maven 多模块）
- **E:\work\server** — 游戏服务器雏形（Java 21, Maven 多模块）

## 进化日志

- `2026-04-10` 初始化：review-commit agent, /rc 命令, /dev 命令, /devloop 命令, devloop.cmd 脚本
- `2026-04-10` M14 背包系统：MsgId 引用冲突，更新命名约定（使用 global:: 前缀）
- `2026-04-11` M17 任务系统：`Server.Game.Task` 与 `System.Threading.Tasks.Task` 冲突，改用 `TaskSystem`
- `2026-04-11` 消息系统重构：IMessage + MessageHandler\<TReq\> + Router 自动扫描
- `2026-04-11` Config 系统重构：`[ConfigTable]` 自动扫描 + ConfigManager API + xlsx2cs 工具
- `2026-04-11` 目录结构重构：Handler 按模块合并，Proto 按模块合并，MsgId 重编 10101 起步
- `2026-04-12` 共享代码迁移：消息/MsgId/IMessage/PacketCodec 迁移到 `shared/`（`GameShared.*`）
- `2026-04-13` F019 战斗表现：伤害飘字、受击闪烁、死亡渐隐
- `2026-04-13` F025 移除业务层Redis：IdGenerator雪花算法 + TokenStore内存存储
- `2026-04-18` 工作流优化（从 et-wuxia 移植）：多 Agent 协调架构 + 铁律 + 验证流水线 + 知识归档 + 三层记忆模型

## 工具链

### Commands（`.claude/commands/`）

| 命令 | 用途 |
|---|---|
| `/init` | 初始化自动化开发环境 |
| `/pp` `/plan` | 需求拆解（交互式，产品经理角色） |
| `/dev` | 实现一个功能（6 Stage 流水线） |
| `/devloop` | 循环开发（带配额守卫） |
| `/qdev` | 临时任务（交互式分析 + 自动流水线） |
| `/status` | 查看进度报告 |
| `/rc` | review + commit |
| `/agent` | 创建/优化 agent（交互式） |

### Agents（`.claude/agents/`）

| Agent | 用途 | 执行方式 |
|---|---|---|
| agent-designer | Agent 设计助手（铁律审查） | 交互式 |
| coding-agent | 开发协调器（主流水线） | 全自动 |
| dev-worker | 代码实现（纯编写） | 全自动 |
| verify-feature | 功能需求覆盖验证 | 全自动 |
| verify-quality | 代码质量审查 | 全自动 |
| quick-dev | 临时任务协调器 | 混合 |
| mem-writer | 知识归档到三层记忆 | 全自动 |
| planner-agent | 需求拆解（产品经理角色） | 交互式 |
| initializer | 首次运行环境初始化 | 全自动 |
