using System.Threading.Tasks;
using Game.Components.Player;
using Shared.Core.Log;
using Shared.Core.Network;
using Shared.Common.Protos;

namespace Game.Components.Fight;

public static class FightHandlers
{
    private static readonly ILog Log = LogManager.GetLogger(nameof(FightHandlers));

    [MessageHandler(typeof(PlayerRpcMethodHandler))]
    public static Task<IMessage> UseSkillHandler(PlayerEntity player, UseSkillReq req, UseSkillAck res)
    {
        var result = FightSystem.UseSkill(player.Id, req.SkillId, req.X, req.Y, req.TargetId);
        res.ErrorMessage = result.ErrorMessage;

        return Task.FromResult<IMessage>(res);
    }

    [MessageHandler(typeof(PlayerRpcMethodHandler))]
    public static Task<IMessage> SetPkModeHandler(PlayerEntity player, SetPkModeReq req, PkModeChangedNtf res)
    {
        if (req.PkMode < PkMode.Peace || req.PkMode > PkMode.All)
            return Task.FromResult<IMessage>(res);

        res.PkMode = req.PkMode;

        return Task.FromResult<IMessage>(res);
    }
}
