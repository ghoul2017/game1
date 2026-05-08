---
paths: ["**/*.cs"]
---

# 反射调用泛型方法需要非泛型重载

通过反射调用 `Entity.GetComponent<T>()` 时，泛型版本无法直接使用 `Type` 参数。需提供非泛型重载：

```csharp
// 泛型版本（编译期类型）
public T GetComponent<T>() where T : Component

// 非泛型重载（运行时 Type）
public Component GetComponent(Type type)
```

反射场景：
```csharp
var component = entity.GetComponent(componentType);  // 使用非泛型重载
```
