using System.Threading.Tasks;
using Game.Components.Player;
using Shared.Common.Protos;
using Shared.Core.Network;

namespace Game.Components.Fight;

public sealed class UseSkillReqHandler : PlayerRpcMessageHandler<UseSkillReq, UseSkillAck>
{
    protected override async Task ExecuteAsync(PlayerEntity player, UseSkillReq req, UseSkillAck res)
    {
        res.ErrorMessage = FightHelper.UseSkill(player.Id, req.SkillId, req.X, req.Y, req.TargetId);

        await Task.CompletedTask;
    }
}
