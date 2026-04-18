---
name: workflow-rules
description: 开发流水线核心规则和错误处理策略
type: project
---

## 核心规则

1. **每 session 一个功能** — 严禁批量实现
2. **features.json 只改 passes 字段** — 禁止删除或修改条目
3. **验证驱动** — 编译通过 + 双验证通过 才能标记 passes: true
4. **干净环境** — session 结束时代码必须可编译
5. **回退机制** — 修复失败时 `git checkout -- <file>` 回退
6. **配额守卫** — devloop 最多 8 轮，连续 3 次失败停止

## 修复循环

- **编译失败**：反馈给 dev-worker 修复，最多 3 次
- **验证失败**：收集 issues → dev-worker 修复 → 重新编译+验证，最多 3 轮
- **3 轮后仍失败**：回退变更，标记 FAILED

## 错误处理优先级

1. 编译错误 → 自动修复
2. 验证失败 → 修复循环
3. 回归 bug → 先修复回归，commit 后再继续当前功能
