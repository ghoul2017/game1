using System.Collections.Generic;
using Game.Entities;
using Shared.Core.ECS;

namespace Game.Components.Chat;

/// <summary>
/// 聊天组件。挂载到根实体，管理聊天冷却记录。
/// </summary>
[ComponentOf(typeof(Module))]
public class ChatComponent : Component
{
    public readonly Dictionary<long, long> LastWorldChatTime = new();  // roleId → 时间戳
}
