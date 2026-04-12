---
name: memory-rules
description: Memory system configuration — save to project dir, not ~/.claude/projects/
type: reference
---

# 记忆系统规则

- **所有记忆文件保存在项目目录** `E:/worku/game1/.claude/memory/` 下
- **不使用** Claude Code 内置记忆系统（`~/.claude/projects/` 下的 memory/），那些文件只有本机能看，不能 Git 管理
- **写**: 项目知识/约定/经验 → `CLAUDE.md`；本地/个人/工作记录 → `CLAUDE.local.md`；结构化记忆 → `.claude/memory/`
- **读**: 每次对话开头，先检查 `CLAUDE.local.md` 的工作记录和 `CLAUDE.md` 的相关章节
- **时机**: 用户说"记一下"/"记住" → 写 CLAUDE.md 或 .claude/memory/，不写 ~/.claude/projects/

## 为什么

用户要求记忆保存在项目目录，便于 Git 版本管理和团队共享。内置记忆系统路径 `~/.claude/projects/` 是本机私有路径，不能被 Git 管理。

## 如何应用

当需要保存记忆时，在 `E:/worku/game1/.claude/memory/` 下创建 `.md` 文件，并在 `.claude/memory/MEMORY.md` 索引中添加条目。
