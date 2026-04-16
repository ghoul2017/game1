# Game1 项目架构思维指南

> 本文档结合 `programming-architecture` skill 的设计模式，与项目实际实现对照，
> 用于指导代码分析、功能修改和架构优化。

---

## 1. 项目设计模式映射

### 1.1 已实现的设计模式

| 设计模式 | 项目实现 | 位置 |
|---------|---------|------|
| **单例模式** | Manager 类静态 Instance | `*Manager.cs` |
| **观察者模式** | EventBus 发布订阅 | `Core/Event/EventBus.cs` |
| **命令模式** | MessageHandler 消息处理 | `IMessageHandler` |
| **工厂模式** | EntityFactory 实体创建 | `Core/Entity/EntityFactory.cs` |
| **模块模式** | IModule 生命周期 | `Core/IModule.cs` |

### 1.2 设计模式详解

#### 1.2.1 状态机（适合扩展）

当前项目**未实现**标准状态机，可参考 skill 扩展：

```csharp
// 可用于：玩家状态（Idle/Move/Attack/Dead）
//        NPC AI 状态（Patrol/Chase/Attack/Flee）
//        UI 面板状态

public abstract class State<T> where T : class
{
    protected T Context { get; private set; }
    public void setContext(T context) => Context = context;
    public virtual void Enter() { }
    public virtual void Update() { }
    public virtual void Exit() { }
}

public class StateMachine<T> where T : class
{
    private State<T> _current;
    public void ChangeState(State<T> newState)
    {
        _current?.Exit();
        _current = newState;
        _current.setContext(Context);
        _current.Enter();
    }
}
```

#### 1.2.2 对象池（适合扩展）

当前项目**未实现**对象池，可参考 skill 扩展：

```csharp
// 可用于：子弹、特效、怪物刷新
// 收益：减少 Instantiate/Destroy 开销
public class ObjectPool<T> where T : Component
{
    private readonly Queue<T> _pool = new();
    private readonly T _prefab;

    public T Get() => _pool.Count > 0 ? _pool.Dequeue() : Create();
    public void Return(T obj) { obj.gameObject.SetActive(false); _pool.Enqueue(obj); }
}
```

---

## 2. 架构分层指导

### 2.1 当前分层

```
┌─────────────────────────────────────────────────────────────┐
│  Game Layer                                                  │
│  ├─ Managers (单例): PlayerManager, BagManager, FightManager │
│  ├─ Handlers: 消息处理                                       │
│  └─ Models: 数据模型                                         │
├─────────────────────────────────────────────────────────────┤
│  Core Layer                                                  │
│  ├─ EventBus, EntityFactory, LogManager                      │
│  ├─ MessageRouter, SessionManager                            │
│  └─ ConfigManager                                            │
├─────────────────────────────────────────────────────────────┤
│  Network Layer                                               │
│  ├─ TcpServer, TcpSession, PacketCodec                       │
│  └─ NetClient                                               │
├─────────────────────────────────────────────────────────────┤
│  Engine Layer (Unity/.NET)                                   │
│  Unity Physics/Rendering │ .NET Networking                  │
└─────────────────────────────────────────────────────────────┘
```

### 2.2 修改代码时的分层原则

| 操作 | 应该在哪个层修改 |
|------|-----------------|
| 添加新消息 | `shared/Protos/` + 对应 Handler |
| 修改 UI | `client/Assets/Scripts/Game/` |
| 修改业务逻辑 | `server/server/game/<module>/` |
| 修改网络协议 | `shared/Network/` |
| 修改框架 | `server/server/Core/` |

---

## 3. 依赖注入实践

### 3.1 当前实现

项目使用**单例 + 静态方法**的依赖方式：

```csharp
// 当前方式（单例）
BagManager.Instance.AddItem(bag, configId, count);

// 建议改为（依赖注入，更易测试）
public class BagService
{
    private readonly IEntityFactory _entityFactory;
    private readonly IConfigManager _configManager;

    public BagService(IEntityFactory entityFactory, IConfigManager configManager)
    {
        _entityFactory = entityFactory;
        _configManager = configManager;
    }
}
```

### 3.2 扩展建议

未来可引入 DI 容器（如 `Microsoft.Extensions.DependencyInjection`）：

```csharp
services.AddSingleton<BagManager>();
services.AddSingleton<IMessageHandler, BagHandler>();
```

---

## 4. 松耦合实践

### 4.1 通过事件解耦

**当前**：Handler 直接调用 Manager

```csharp
// 当前耦合方式
public class BagHandler : IMessageHandler
{
    public async Task<IMessage> HandleAsync(...)
    {
        var bag = await BagManager.Instance.LoadBagAsync(roleId);
        // ...
    }
}
```

**建议**：通过 EventBus 解耦

```csharp
// 解耦方式
public class BagHandler : IMessageHandler
{
    public async Task<IMessage> HandleAsync(...)
    {
        await EventBus.Instance.PublishAsync(new BagQueryRequested { RoleId = roleId });
    }
}

public class BagEventHandler : IEventListener<BagQueryRequested>
{
    public async Task HandleAsync(BagQueryRequested e)
    {
        var bag = await BagManager.Instance.LoadBagAsync(e.RoleId);
        await EventBus.Instance.PublishAsync(new BagQueryResult { Bag = bag });
    }
}
```

### 4.2 组件化示例

参考 ECS 思想，将复杂系统拆分为组件：

```csharp
// 当前：大型 FightManager
public class FightManager
{
    // 1000+ 行代码处理所有战斗逻辑
}

// 建议：按职责拆分
public class FightSystem
{
    private readonly DamageCalculator _damageCalc;
    private readonly SkillCooldownManager _cdManager;
    private readonly BuffSystem _buffSystem;
    private readonly ThreatManager _threatManager;
}
```

---

## 5. 可测试性改进

### 5.1 当前问题

- Manager 依赖单例，难以 mock
- Handler 直接使用 `Serializer.Deserialize`，难以注入

### 5.2 改进建议

```csharp
// 接口抽象
public interface ISerializer
{
    T Deserialize<T>(byte[] data);
    byte[] Serialize<T>(T obj);
}

// 注入实现
public class MessageRouter
{
    private readonly ISerializer _serializer;
    public MessageRouter(ISerializer serializer) => _serializer = serializer;
}
```

---

## 6. 常见修改场景

### 6.1 添加新消息

```
1. shared/Protos/MsgId.cs          添加 MsgId 常量
2. shared/Protos/XxxProtos.cs      添加消息类
3. server/game/Xxx/XxxHandler.cs   添加 Handler
4. client/Network/Dispatcher.cs     注册 Handler（客户端）
```

### 6.2 添加新子系统

```
1. server/game/NewSystem/
   ├── NewSystemManager.cs     单例
   ├── NewSystemModel.cs       IModule 实现
   ├── NewSystemHandlers.cs    消息处理
   └── NewSystemConfig.cs      配置（可选）
2. ModuleRegistry.cs           注册模块
3. shared/Protos/              添加消息
```

### 6.3 修改现有功能

| 场景 | 步骤 |
|------|------|
| 修改业务逻辑 | 找到对应 Manager → 修改方法 → 测试 |
| 修改消息格式 | 同步修改 shared/ + Handler + Client |
| 修改 UI | 找到对应 Panel → 修改 OnShow/OnHide |

---

## 7. 性能优化方向

### 7.1 当前性能特性

- **Write-Behind 缓存**：减少数据库写入
- **Per-User 线程队列**：减少锁竞争
- **AOI 视野裁剪**：减少广播量

### 7.2 可优化点

| 优化点 | 当前实现 | 优化建议 |
|--------|---------|---------|
| 对象池 | 无 | 子弹/特效/怪物 |
| 脏矩形渲染 | 无 | UI/地图更新 |
| 对象类型 | Dictionary | 可考虑更高效结构 |
| GC | 普通 | 对象池 + 结构体 |

---

## 8. 架构决策记录 (ADR)

### ADR-001: 使用 TCP 而非 UDP

**决定**：选择 TCP 作为通信协议
**原因**：游戏逻辑需要可靠传输，简化开发
**后果**：实时性略差，但可靠性好

### ADR-002: 服务端权威

**决定**：所有游戏逻辑在服务端计算
**原因**：防止作弊，保证公平性
**后果**：网络延迟影响体验

### ADR-003: 共享消息定义

**决定**：消息定义放在 shared/ 目录
**原因**：避免双端不同步
**后果**：shared/ 代码受限于 Unity .NET 版本

### ADR-004: Write-Behind 缓存

**决定**：数据先写内存，定时批量持久化
**原因**：减少数据库压力
**后果**：服务器崩溃可能丢失少量数据

---

## 9. 未来扩展方向

### 9.1 可扩展功能

| 功能 | 当前状态 | 扩展难度 |
|------|---------|---------|
| 状态机系统 | 无 | 中 |
| 对象池 | 无 | 低 |
| 成就系统 | 无 | 低 |
| 排行榜 | 无 | 低 |
| 公会系统 | 无 | 高 |
| 跨服战 | 无 | 高 |

### 9.2 技术升级

| 方向 | 当前 | 升级目标 |
|------|------|---------|
| 协议 | Protobuf | gRPC |
| DI | 手写单例 | 依赖注入框架 |
| 测试 | 手动 | 单元测试 + CI |
| 热更新 | 无 | IL2CPP + Lua |

---

---

## 10. 踩坑记录（来自 claudecode 开发经验）

### 10.1 必须注意的陷阱

| 陷阱 | 原因 | 解决方案 |
|------|------|----------|
| MsgId 引用冲突 | MsgId 常量名与其他类型冲突 | 客户端用 `global::GameShared.Protos.MsgId.Xxx` |
| Task 命名冲突 | `Server.Game.Task` vs `System.Threading.Tasks.Task` | 任务系统改用 `TaskSystem` |
| .cmd 脚本只跑一轮 | 直接调用 .cmd 相当于 exec | 必须用 `call` 调用其他 .cmd |
| protobuf-net Unity 依赖 | Unity 2021.3 缺少 netstandard2.0 DLL | 补齐 6 个 DLL 到 Plugins/ |
| 默认接口实现 | netstandard2.0 不支持 C# 8 默认实现 | shared/ 禁止使用 |
| Redis 开发环境 | 不想启动 Redis | RedisManager 自动降级内存模式 |

### 10.2 架构进化历史

```
2026-04-10  初始化 Agent/Command 体系
2026-04-10  MsgId 引用冲突 → global:: 前缀约定
2026-04-11  Task 命名冲突 → TaskSystem 命名
2026-04-11  消息系统重构 → IMessage + Handler + Router 自动扫描
2026-04-11  Config 系统重构 → [ConfigTable] 注解自动扫描
2026-04-11  目录结构重构 → Handler/Proto 按模块合并
2026-04-12  共享代码迁移 → shared/ (GameShared.*)
2026-04-13  战斗表现完善 → 伤害飘字/受击闪烁/死亡渐隐
2026-04-13  移除业务层 Redis → IdGenerator + TokenStore
```

### 10.3 自我进化机制

| 触发条件 | 动作 |
|----------|------|
| 相同操作 3 次以上 | 创建 command 自动化 |
| 发现新命名/目录约定 | 更新项目约定 |
| 复杂流程有固定模式 | 创建 agent 封装 |
| 从参考项目学到新模式 | 添加到约定 |

---

*本文档结合 `programming-architecture` skill + claudecode 开发经验编写，用于架构思维指导*
