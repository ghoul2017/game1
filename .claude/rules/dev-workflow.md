---
always_apply: true
---

# 工作准则

你拥有完整的自主判断权。根据任务性质自行决定执行方式：小修复直接动手，大功能拆步实现，需求模糊就先问清楚。没有刚性分步，只有底线规则。

## 判断力

- 小任务（修 bug、调样式、加字段）→ 直接改，编译通过即可
- 中等任务（新增 Handler、新面板）→ 改完编译+验证
- 大任务（新子系统、跨端功能）→ 拆解为可独立完成的步骤，逐步实现
- 需求不清 → 先和用户讨论，确认范围后再动手

## 底线规则

1. **改完必须编译** — 服务端 `dotnet build server/Game`，客户端 `dotnet build client/Assembly-CSharp.csproj`。编译不过就修，修不了就回退
2. **验证不是摆设** — 代码变更后用 verify-feature + verify-quality 并行验证。验证失败就修，修不好就回退，最多 3 轮
3. **回退机制** — 修复循环耗尽后 `git checkout -- <files>` 回退，向用户说明原因。不硬撑
4. **知识归档** — 实现中发现有价值经验，委托 mem-writer 归档。没发现就跳过
5. **不 git push**

## 子 Agent

需要委托时使用 Agent 工具：

| 角色 | subagent_type | 用途 |
|------|--------------|------|
| dev-worker | dev-worker | 代码实现（大块任务委托给它） |
| verify-feature | verify-feature | 功能需求覆盖验证 |
| verify-quality | verify-quality | 代码质量审查 |
| mem-writer | mem-writer | 知识归档 |

并行验证时同一条消息发出两个 Agent 调用，都设 `run_in_background: true`。

## 需求拆解

按 `docs/` 工作流（`project-docs-workflow.md`）执行，不使用 features.json。
