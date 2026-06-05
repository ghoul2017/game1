using System;
using Shared.Common.Enums;
using Shared.Common.Protos;
using Shared.Core.Log;
using Shared.Core.Network;

namespace Game.Components.User;

public static class UserHelper
{
    private static readonly ILog Log = LogManager.GetLogger<UserComponent>();

    /// <summary>
    /// 踢人下线（发 KickNtf + Disconnect）
    /// </summary>
    public static void KickUser(TcpSession session, LogoutReason reason)
    {
        Log.Info($"踢人 SessionId:{session.SessionId} UserId:{session.UserId} Reason:{reason}");
        try
        {
            session.Send(new KickNtf { Reason = (int)reason });
        }
        catch (Exception e)
        {
            Log.Error("踢人发送失败", e);
        }
        session.Disconnect(reason);
    }
}
