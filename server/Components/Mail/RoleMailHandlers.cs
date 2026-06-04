using Game.Components.Player;
using System.Linq;
using System.Threading.Tasks;
using Game.Database;
using Shared.Core.Event;
using Shared.Core.Log;
using Shared.Core.Network;
using Shared.Common.Protos;

namespace Game.Components.Mail;

public static class RoleMailHandlers
{
    private static readonly ILog Log = LogManager.GetLogger(nameof(RoleMailHandlers));

    #region 事件订阅

    [EventSubscriber]
    public static async Task OnPlayerOnline(EventType.PlayerOnlineEvent e)
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

    #endregion
}
