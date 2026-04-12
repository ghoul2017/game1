输出当前项目进度报告。

## 工作流程

### 1. 读取状态
- 读取 `progress.txt`
- 读取 `features.json`
- 运行 `git -C E:/worku/game1 log --oneline -10`

### 2. 输出报告

```
---
项目进度报告

总功能: X 条
已完成: X 条 (XX%)
失败: X 条
待开发: X 条

按分类统计:
  network: X/X
  shared: X/X
  login: X/X
  map: X/X
  bag: X/X
  mail: X/X
  chat: X/X
  task: X/X
  fight: X/X
  misc: X/X

最近 5 条提交:
  <git log>

下一个待开发: [功能ID] 功能描述
---
```

全自动执行，不提问。

$ARGUMENTS
