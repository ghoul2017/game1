using Game.Entities;
using Shared.Core.ECS;

namespace Game.Components.Chat;

/// <summary>
/// 聊天组件。挂载到根实体。
/// </summary>
[ComponentOf(typeof(Module))]
public class ChatComponent : Component
{
}
