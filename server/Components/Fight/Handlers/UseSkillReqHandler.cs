using System.Threading.Tasks;
using Game.Components.Player;
using Shared.Common.Protos;
using Shared.Core.Network;

namespace Game.Components.Fight;

public sealed class UseSkillReqHandler : PlayerRpcMessageHandler<UseSkillReq, UseSkillAck>
{
    protected override Task ExecuteAsync(PlayerEntity player, UseSkillReq req, UseSkillAck res)
    {
        var result = FightSystem.UseSkill(player.Id, req.SkillId, req.X, req.Y, req.TargetId);
        res.ErrorMessage = result.ErrorMessage;

        return Task.CompletedTask;
    }
}
