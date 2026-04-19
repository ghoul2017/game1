# 开发进度

## qdev-20260419: 登录界面记住密码 Toggle 勾选标记

- **状态**: SUCCESS
- **变更文件**: 1 个
  - `client/Assets/Scripts/Login/LoginPanel.cs` (修改)
- **问题**: RememberToggle 缺少 graphic（勾选标记），导致点击后无视觉反馈
- **修复**: 添加 Checkmark 子 GameObject（18x18 金色方块），赋值给 Toggle.graphic
- **验证**: 功能 PASS，质量 PASS（0 CRITICAL，3 WARNING 均为改进建议）

## qdev-20260419b: 点击技能/切换地图等按钮时主界面消失

- **状态**: SUCCESS
- **变更文件**: 1 个
  - `client/Assets/Scripts/Core/UIManager.cs` (修改)
- **问题**: UIManager.HidePanel<T>() 移除面板后不恢复栈顶面板可见性。UIToast/UIMessageBox 作为覆盖层使用 ShowPanel 时隐藏了底层面板，消失后底层面板无法恢复
- **修复**: 在 HidePanel<T>() 的 RemoveFromStack 之后添加栈顶面板恢复逻辑
- **验证**: 功能 PASS，质量 PASS
