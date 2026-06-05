using Game.Components.Player;
using System.Linq;
using System.Threading.Tasks;
using Game.Database;
using Shared.Core.Event;
using Shared.Core.Log;
using Shared.Core.Network;
using Shared.Common.Protos;

namespace Game.Components.Bag;

public static class RoleBagHandlers
{
    private static readonly ILog Log = LogManager.GetLogger(nameof(RoleBagHandlers));

    internal static BagItemInfo ToInfo(BagItem item) => new()
    {
        Uid = item.Uid,
        ConfigId = item.ConfigId,
        Count = item.Count,
        Index = item.Index,
        Bind = item.Bind,
    };


    [EventSubscriber]
    public static async Task OnPlayerOnline(EventType.PlayerOnlineEvent e)
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
