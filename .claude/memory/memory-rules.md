---
name: memory-rules
description: 记忆系统规则 — 项目记忆 vs 内置自动记忆的关系和使用方式
type: reference
---

# 记忆系统规则

## 两个记忆系统

| 系统 | 路径 | 特点 | 用途 |
|------|------|------|------|
| 项目记忆 | `.claude/memory/` | Git 管理，团队共享 | 项目知识、踩坑经验、设计决策 |
| 内置自动记忆 | `~/.claude/projects/` | 本机私有，每次对话自动加载 | 用户个人偏好、跨项目反馈 |

本项目以**项目记忆**为主，所有团队共享的知识写在这里。

## 写入规则

- **项目知识**（架构、调试、业务规则、踩坑）→ `.claude/memory/` 带 YAML frontmatter
- **用户偏好**（语言、格式、流程偏好）→ `CLAUDE.local.md` 用户偏好章节
- **编码规则**（编码、换行、命名）→ `CLAUDE.local.md` 编码规则章节
- **命名前缀**：`arch-`(架构)、`debug-`(调试)、`biz-`(业务)、`rule-`(规则)、`workflow-`(工作流)、`feedback-`(反馈)、`project-`(项目)、`ref-`(引用)

## 读取规则

- `CLAUDE.md` + `CLAUDE.local.md` 每次对话自动加载
- `.claude/memory/` 文件通过 `CLAUDE.local.md` 索引按需查阅

## 记忆内容筛选

- ✅ 记录：踩过的坑、非直觉的规则、设计决策及理由、复杂的代码路径
- ❌ 不记：显而易见的常识、已在 CLAUDE.md 中记录的内容、git history 可查的信息
