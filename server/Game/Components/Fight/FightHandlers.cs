using System.Threading.Tasks;
using Game.Components.Fight;
using Game.Core.Network;
using Shared.Core.Event;
using Shared.Core.Log;
using Shared.Core.Network;
using Shared.Common.Protos;

namespace Game.Components.Fight;

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

        var result = Context.Fight.UseSkill(session.RoleId, req.SkillId, req.X, req.Y, req.TargetId);
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

    #region 事件订阅

    [EventSubscriber]
    public static void OnPlayerOffline(EventType.PlayerOfflineEvent e)
    {
        Context.Fight.UnregisterUnit(e.RoleId);
        Log.Debug($"玩家下线注销战斗单元 RoleId:{e.RoleId}");
    }

    [EventSubscriber]
    public static void OnPlayerLeaveMap(EventType.PlayerLeaveMapEvent e)
    {
        Context.Fight.UnregisterUnit(e.RoleId);
        Log.Debug($"玩家离开地图注销战斗单元 RoleId:{e.RoleId}");
    }

    #endregion
}
