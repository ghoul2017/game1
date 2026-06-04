using System;
using System.Linq;
using System.Threading.Tasks;
using Game.Components.Player;
using Shared.Core;
using Game.Database;
using Shared.Common.Enums;
using Shared.Common.Protos;
using Shared.Core.Event;
using Shared.Core.Log;
using Shared.Core.Network;

namespace Game.Components.User;

public static class UserHandler
{
    private static readonly ILog Log = LogManager.GetLogger<UserComponent>();

    [EventSubscriber]
    public static void ServerInitEvent(EventType.ServerInitEvent e)
    {
        Context.Server.AddComponent<UserComponent>();
    }

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
