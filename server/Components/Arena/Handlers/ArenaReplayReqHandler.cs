using System.Threading.Tasks;
using Game.Components.Player;
using Shared.Common.Protos;
using Shared.Core;
using Shared.Core.Network;

namespace Game.Components.Arena;

public sealed class ArenaReplayReqHandler : PlayerRpcMessageHandler<ArenaReplayReq, ArenaReplayAck>
{
    protected override async Task ExecuteAsync(
        PlayerEntity player,
        ArenaReplayReq request,
        ArenaReplayAck response)
    {
        ArenaComponent arena = Context.Server.GetComponent<ArenaComponent>();
        if (request.BattleId <= 0 ||
            !arena.TryGetByBattleId(request.BattleId, out ArenaBattleRecord record) ||
            record.RoleId != player.Id)
        {
            response.ErrorMessage = "竞技场战斗不存在";
            return;
        }

        ArenaProtocolMapper.FillReplayAck(record, response);
        await Task.CompletedTask;
    }
}
