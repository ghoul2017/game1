using System.Threading.Tasks;
using Game.Database;
using Shared.Core.Event;
using Shared.Core.Log;

namespace Game.Components.Player;

public static class PlayerHandlers
{
    private static readonly ILog Log = LogManager.GetLogger(nameof(PlayerHandlers));

    [EventSubscriber]
    public static async Task OnPlayerOnline(EventType.PlayerOnlineEvent e)
    {
        var player = Context.Players.GetByRoleId(e.RoleId);
        if (player == null)
        {
            return;
        }

        var attr = await MongoDb.Instance.LoadAsync<RoleAttributeComponent>(e.RoleId);
        if (attr == null)
        {
            attr = new RoleAttributeComponent { Id = e.RoleId };
            await MongoDb.Instance.InsertAsync(attr);
            Log.Info($"创建新角色属性 RoleId:{e.RoleId}");
        }
        player.AddComponent(attr);
    }
}
