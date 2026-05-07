using Game.Entities;
using Shared.Core.ECS;

namespace Game.Components.Mail;

/// <summary>
/// 邮件组件。挂载到根实体。
/// </summary>
[ComponentOf(typeof(Module))]
public class MailComponent : Component
{
}
