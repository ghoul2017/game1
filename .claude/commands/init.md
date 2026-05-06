项目首次初始化。全自动执行。

## 流程

1. 检查 `progress.txt` 是否存在（已存在则跳过）
2. 验证构建（编译服务端+客户端，失败则修复最多 3 次）
3. 验证 CLAUDE.md 中提到的核心目录存在
4. 创建 `progress.txt` 初始记录
5. 读取 `features.json` 确认功能总数
6. 提交初始化结果

$ARGUMENTS
