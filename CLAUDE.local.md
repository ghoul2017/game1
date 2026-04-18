# CLAUDE.local.md — 本地配置（第二层）

本文件定义记忆规则、用户偏好、编码规则。每次对话自动加载，所有内容内联不拆文件。

---

## 记忆架构

项目使用三层记忆模型：

| 层级 | 文件 | 内容 | 加载频率 | 谁写 |
|------|------|------|----------|------|
| 第一层：项目总纲 | `CLAUDE.md` | 项目简介、总体架构、公共约定 | 每次对话 | 人工维护为主 |
| 第二层：本地配置 | `CLAUDE.local.md`（本文件） | 记忆规则、用户偏好、编码规则 | 每次对话 | mem-writer 可追加 |
| 第三层：项目细则 | `.claude/memory/*.md` | 对第一层的补充，多而杂 | 按需查阅 | mem-writer 主要写这里 |

### 记忆规则

**记什么：**
- 踩过的坑（以后会再遇到的）
- 非直觉的业务规则
- 复杂的代码路径（下次找起来费时的）
- 重要的设计决策及其理由
- 用户明确要求记住的内容

**怎么记：**
- 用户偏好和编码规则 → 直接写在本文件（第二层），不单独建文件
- 项目细则 → 写入 `.claude/memory/` 目录（第三层），用命名前缀分类
- 前缀规范：`arch-`(架构)、`debug-`(调试)、`biz-`(业务)、`rule-`(规则)、`workflow-`(工作流)、`feedback-`(反馈)、`project-`(项目)、`ref-`(引用)

**记哪里：**
- 第二层内容写在本文件对应章节
- 第三层内容写在 `.claude/memory/` 目录

**哪里读：**
- 第二层：本文件每次自动加载
- 第三层：通过本文件的「项目细则索引」按需查阅

---

## 用户偏好

- 保持专业性，不要迎合用户
- 回复使用中文
- 代码注释使用中文
- commit message: 中文，格式 `feat: [F001] 功能描述`
- 提交前确认编译通过
- 允许在分析阶段提问，实现阶段全自动
- 实现代码后自动 commit，无需额外确认
- 记忆文件必须实际写入文件，口头答应不算数

## 编码规则

- 文件编码：UTF-8 无 BOM，行尾 CRLF（Windows 下 git core.autocrlf=true 使本地文件为 CRLF，写 LF 会造成 diff 全篇飘红；.cmd 脚本必须 CRLF；VS/Unity/.NET SDK 默认写 CRLF）
- Windows .cmd 脚本中调用其他 .cmd 必须使用 `call` 关键字
- 记忆文件必须写在项目目录 `.claude/memory/` 下，不要写在用户全局目录

## 自动化流水线

```
/pp        → 需求拆解（交互式，产品经理角色）
/dev       → 单功能开发（协调器：准备→开发→验证→记录→提交→报告）
/devloop   → 循环开发（配额守卫：8轮/session，3连失败停止）
/qdev      → 临时任务（交互式分析 + 自动流水线）
/status    → 进度报告
/rc        → review + commit
/init      → 首次初始化
/agent     → 创建/优化 agent
```

### 流水线架构

```
coding-agent (协调器)
  ├── dev-worker      (代码实现)
  ├── verify-feature  (功能验证)  ─┐
  ├── verify-quality  (质量验证)  ─┤ 并行
  └── mem-writer      (知识归档)
```

### 项目细则索引（第三层）

> 以下文件在 `.claude/memory/` 目录，按需查阅。完整索引见 `.claude/memory/MEMORY.md`

- [架构决策](.claude/memory/arch-decisions.md) — ADR 记录 + 架构分层
- [排障指南](.claude/memory/debug-guide.md) — 问题排查表 + 调试技巧
- [进化历史](.claude/memory/project-evolution.md) — 进化日志 + 自我进化规则 + 工具链
- [工作流规则](.claude/memory/workflow-rules.md) — 开发流水线最佳实践
- [记忆规则](.claude/memory/memory-rules.md) — 记忆文件管理规则
- [Git 语法](.claude/memory/feedback-git-cmd.md) — git -C 语法偏好
- [devloop.cmd 修复](.claude/memory/feedback-devloop-cmd.md) — .cmd 调用注意事项
- [protobuf-net 依赖](.claude/memory/ref-protobuf-net-unity.md) — Unity protobuf-net DLL 列表
- [Redis 降级](.claude/memory/project-redis-bypass.md) — RedisManager 内存模式
