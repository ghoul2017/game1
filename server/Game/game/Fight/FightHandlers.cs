using System.Threading.Tasks;
using Game.Core.Network;
using Game.Network;
using Shared.Core.Log;
using Shared.Core.Network;
using Shared.Common.Protos;

namespace Game.Game.Fight;

public static class FightHandlers
{
    private static readonly ILog Log = LogManager.GetLogger(nameof(FightHandlers));

    [MessageHandler(typeof(RpcMethodHandler))]
    public static Task<IMessage> UseSkillHandler(TcpSession session, UseSkillReq req, UseSkillAck res)
    {
        if (session.RoleId == 0)
        {
            res.Code = FightErrorCode.NotInFight;
            return Task.FromResult<IMessage>(res);
        }

        var result = FightManager.Instance.UseSkill(session.RoleId, req.SkillId, req.X, req.Y, req.TargetId);
        res.Code = result.Code;

        return Task.FromResult<IMessage>(res);
    }

    [MessageHandler(typeof(RpcMethodHandler))]
    public static Task<IMessage> SetPkModeHandler(TcpSession session, SetPkModeReq req, PkModeChangedNtf res)
    {
        if (session.RoleId == 0)
            return Task.FromResult<IMessage>(res);

        if (req.PkMode < PkMode.Peace || req.PkMode > PkMode.All)
            return Task.FromResult<IMessage>(res);

        res.PkMode = req.PkMode;

        return Task.FromResult<IMessage>(res);
    }
}
