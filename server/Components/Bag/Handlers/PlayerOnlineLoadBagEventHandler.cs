using System.Threading.Tasks;
using Game.Database;
using Shared.Core.Event;
using Shared.Core.Log;

namespace Game.Components.Bag;

public sealed class PlayerOnlineLoadBagEventHandler : IAsyncEventHandler<EventType.PlayerOnlineEvent>
{
    private static readonly ILog Log = LogManager.GetLogger(nameof(PlayerOnlineLoadBagEventHandler));

    public async Task HandleAsync(EventType.PlayerOnlineEvent e)
    {
        var player = e.Player;

        var bag = await MongoDb.Instance.LoadAsync<RoleBagComponent>(player.Id);
        if (bag == null)
        {
            bag = new RoleBagComponent { Id = player.Id, Capacity = 100 };
            await MongoDb.Instance.InsertAsync(bag);
            Log.Info($"创建新背包 RoleId:{player.Id}");
        }
        player.AddComponent(bag);
    }
}
