using System.Threading.Tasks;
using Game.Database;
using Shared.Core.Event;
using Shared.Core.Log;

namespace Game.Components.GameTask;

public sealed class PlayerOnlineLoadTaskEventHandler : IAsyncEventHandler<EventType.PlayerOnlineEvent>
{
    private static readonly ILog Log = LogManager.GetLogger(nameof(PlayerOnlineLoadTaskEventHandler));

    public async Task HandleAsync(EventType.PlayerOnlineEvent e)
    {
        var player = e.Player;

        var task = await MongoDb.Instance.LoadAsync<RoleTaskComponent>(player.Id);
        if (task == null)
        {
            task = new RoleTaskComponent { Id = player.Id };
            await MongoDb.Instance.InsertAsync(task);
            Log.Info($"创建新任务列表 RoleId:{player.Id}");
        }
        player.AddComponent(task);
    }
}
