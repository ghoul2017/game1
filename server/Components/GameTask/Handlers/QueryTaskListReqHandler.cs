using System.Linq;
using System.Threading.Tasks;
using Game.Components.Player;
using Shared.Common.Protos;
using Shared.Core.Network;

namespace Game.Components.GameTask;

public sealed class QueryTaskListReqHandler : PlayerRpcMessageHandler<QueryTaskListReq, QueryTaskListAck>
{
    protected override async Task ExecuteAsync(PlayerEntity player, QueryTaskListReq req, QueryTaskListAck res)
    {
        var taskComponent = player.GetComponent<RoleTaskComponent>();
        res.Tasks = taskComponent.Tasks.Select(RoleTaskHelper.ToInfo).ToList();
        await Task.CompletedTask;
    }
}
