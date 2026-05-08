---
paths: ["client/**/*.cs"]
---

# Unity 客户端 csproj 是自动生成的

`client/Assembly-CSharp.csproj` 由 Unity Editor 自动生成，包含所有 `Assets/Scripts/` 下的 `.cs` 文件引用。

## 影响

- **新增 shared 文件后**，`dotnet build client/Assembly-CSharp.csproj` 会报 CS0234（找不到命名空间），需要在 Unity Editor 中刷新（打开项目或 Assets → Refresh）让 Unity 重新生成 csproj
- **服务端不受影响**，因为 `Game.csproj` 使用 `<Compile Include="..\..\client\Assets\Scripts\Shared\**\*.cs" />` 通配符，新增文件自动包含
- **验证策略**：新增 shared 文件后，先 `dotnet build server/Game` 验证编译，客户端编译需等 Unity 刷新
