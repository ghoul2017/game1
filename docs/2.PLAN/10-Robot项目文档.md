# Robot 项目文档

## 概述

编写两份 Robot 使用手册：人类使用手册（`docs/`）和 AI 使用手册（`rules/`）。

## 1. 人类使用手册 — docs/Robot使用手册.md

### 内容大纲

```
# Robot 使用手册

## 1. 概述
- Robot 是什么、能做什么
- 与 Unity 客户端的关系

## 2. 环境搭建
- 前置依赖（.NET 8 SDK）
- 编译 Robot 项目
- 配置服务端连接

## 3. 快速开始
- 启动服务端
- 运行第一个测试场景
- 理解输出结果

## 4. 编写测试场景
- 继承 TestScenario
- 使用 RobotSession 收发消息
- 使用 Assertions 验证
- 完整示例（登录测试）

## 5. 内置命令与选项
- 命令行参数
- 场景选择
- 输出格式（详细/简洁）

## 6. 调试
- 如何查看原始消息
- 如何设置断点
- 常见错误排查

## 7. CI 集成
- 退出码规范
- 输出格式
- 示例 CI 配置

## 8. 常见问题
```

### 新建文件
- `docs/Robot使用手册.md`

---

## 2. AI 使用手册 — rules/robot-ai-manual.md

### 内容大纲

```
# Robot AI 使用手册

## 1. Robot 项目定位
- Robot 是薄壳项目，自身只有基础设施 + 控制代码
- 90% 能力来自引用 Shared + Logic 层
- AI 修改 Robot 代码的范围边界

## 2. 项目结构
- robot/ 目录结构
- robot/Robot.csproj 依赖声明
- 与 Shared/Logic/View 的引用关系

## 3. 编写测试场景规范
- TestScenario 子类模板
- 命名约定
- 断言风格
- 场景注册方式

## 4. 扩展 Robot 基础设施
- 何时需要扩展 RobotSession
- 何时需要扩展 Assertions
- 禁止往 Robot 塞业务逻辑

## 5. 代码规范
- 命名空间：Robot.*
- 类名：PascalCase
- 测试场景命名：{模块}Test
- 使用 shared 日志工具（ILog + LogManager）

## 6. 编译与验证
- `dotnet build robot/` 编译
- 编译前确认 Logic 层变更已同步
- 常见编译错误处理

## 7. 与 Logic 层协作
- Logic 层变更时 Robot 侧的处理
- IClientUI 接口扩展规则
- 新增 Handler 时同步添加测试场景
```

### 新建文件
- `rules/robot-ai-manual.md`

---

## 执行顺序

1. 等待 Robot 项目和 Logic/View 分离完成后编写（文档基于实际代码）
2. 先写 AI 使用手册（约束 AI 行为），再写人类手册（面向用户）
3. 人类手册在 Robot 可用后编写，确保示例可运行

## 验证

- [ ] `docs/Robot使用手册.md` 存在且覆盖全部大纲
- [ ] `rules/robot-ai-manual.md` 存在且覆盖全部大纲
- [ ] Human 手册中的示例代码可在 Robot 项目中编译运行
- [ ] AI 手册中的规范与其他 rules 文件不冲突
