---
name: workflow-rules
description: 增量开发工作流最佳实践（基于 Anthropic 长时间运行 Agent 指南）
type: project
---

# 工作流规则

基于 [Anthropic "Effective harnesses for long-running agents"](https://www.anthropic.com/engineering/effective-harnesses-for-long-running-agents) 最佳实践。

## 核心原则

1. **增量开发** — 每个 session 只实现一个功能，不做批量
2. **JSON 功能清单** — `features.json` 跟踪所有功能状态，只允许修改 `passes` 字段
3. **干净环境** — session 结束时代码必须可编译
4. **标准启动序列** — pwd → 读 progress → 读 features → git log → build check → 选下一个
5. **验证驱动** — 编译通过 + 验证通过 才能标记 passes: true
6. **回退机制** — 构建失败无法修复时 `git checkout -- <file>` 回退
7. **配额守卫** — devloop 最多 8 轮，连续 3 次失败停止

## 流水线流程

```
Prepare → Develop (dev-worker) → Verify (verify-feature + verify-quality 并行)
        → Fix Loop (max 3) → Record (mem-writer) → Commit → Report
```

## 修复循环

- 验证失败后进入修复循环：dev-worker 修复 → 重新验证
- 最多 3 轮
- 3 轮后仍失败：回退变更，标记 FAILED

## 知识归档

- dev-worker 输出 KNOWLEDGE（BIZ/DECISION/EDGE/PATH 标签）
- mem-writer 自动过滤、分类、写入三层记忆系统
- 非显而易见的知识才记录，避免冗余

## 错误处理优先级

1. 编译错误 → 自动修复，最多 3 次
2. 验证失败 → 修复循环，最多 3 轮
3. 回归 bug → 先修复回归，再继续当前功能
