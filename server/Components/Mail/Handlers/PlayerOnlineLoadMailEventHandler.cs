using System.Threading.Tasks;
using Game.Database;
using Shared.Core.Event;
using Shared.Core.Log;

namespace Game.Components.Mail;

public sealed class PlayerOnlineLoadMailEventHandler : IAsyncEventHandler<EventType.PlayerOnlineEvent>
{
    private static readonly ILog Log = LogManager.GetLogger(nameof(PlayerOnlineLoadMailEventHandler));

    public async Task HandleAsync(EventType.PlayerOnlineEvent e)
    {
        var player = e.Player;

        var mail = await MongoDb.Instance.LoadAsync<RoleMailComponent>(player.Id);
        if (mail == null)
        {
            mail = new RoleMailComponent { Id = player.Id };
            await MongoDb.Instance.InsertAsync(mail);
            Log.Info($"创建新邮件 RoleId:{player.Id}");
        }
        player.AddComponent(mail);
    }
}
