using System.Threading.Tasks;
using Game.Database;
using Shared.Core.Event;
using Shared.Core.Log;

namespace Game.Components.Player;

public sealed class PlayerOnlineLoadAttributeEventHandler : IAsyncEventHandler<EventType.PlayerOnlineEvent>
{
    private static readonly ILog Log = LogManager.GetLogger(nameof(PlayerOnlineLoadAttributeEventHandler));

    public async Task HandleAsync(EventType.PlayerOnlineEvent e)
    {
        var player = e.Player;

        var attr = await MongoDb.Instance.LoadAsync<RoleAttributeComponent>(player.Id);
        if (attr == null)
        {
            attr = new RoleAttributeComponent { Id = player.Id };
            await MongoDb.Instance.InsertAsync(attr);
            Log.Info($"创建新角色属性 RoleId:{player.Id}");
        }
        player.AddComponent(attr);
    }
}
