using Game.Components.Mail;
using Game.Components.Player;
using Shared.Core.ECS;

namespace Game.Components.Mail;

/// <summary>
/// 邮件组件。持有 RoleMailList 持久化数据。
/// </summary>
[ComponentOf(typeof(PlayerEntity))]
public class MailComponent : Component
{
    public RoleMailList? RoleMailList { get; set; }
    public long NextMailId;

    public MailComponent()
    {
        NextMailId = 1;
    }
}
