---
name: Git Command Syntax
description: git 命令使用 git -C <path> 语法而非 cd + git 组合
type: feedback
---

git 命令用 `git -C <path>` 语法，不用 `cd ... && git ...` 组合命令
**Why**: 权限配置按命令前缀匹配，`cd` 开头匹配不到 `git:*`
**How to apply**: 所有 git 操作使用 `git -C E:/worku/game1 status` 格式
