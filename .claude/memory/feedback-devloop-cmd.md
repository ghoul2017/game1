---
name: devloop-cmd-fix
description: devloop.cmd 必须用 call 调用 claude.cmd，否则只跑一轮就退出
type: feedback
---

Windows batch 中调用另一个 .cmd/.bat 文件时，必须用 `call` 前缀。否则控制权不会返回给调用者（相当于 exec 而非 spawn）。

**Why:** `claude` 本身是个 .cmd 包装脚本，直接调用会导致 devloop.cmd 在第一轮结束后就终止。

**How to apply:** 在 .cmd 脚本中调用任何 .cmd/.bat 外部程序时，始终使用 `call` 前缀。
