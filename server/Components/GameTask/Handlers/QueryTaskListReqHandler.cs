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
        var tasks = player.RoleTask.GetTaskList();
        res.Tasks = tasks.Select(player.RoleTask.ToTaskInfo).ToList();
        await Task.CompletedTask;
    }
}
