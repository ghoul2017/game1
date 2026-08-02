using System;
using Game.Components.Player;
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

    /// <summary>检查会话在异步数据库等待后仍是该用户唯一有效的在线会话。</summary>
    public static bool IsCurrentSession(TcpSession session, long expectedUserId)
    {
        if (expectedUserId <= 0L || !session.IsConnected || session.UserId != expectedUserId)
        {
            return false;
        }

        UserComponent userComponent = Context.Server.GetComponent<UserComponent>();
        return userComponent.Users.TryGetValue(expectedUserId, out User? user) &&
            ReferenceEquals(user.Session, session);
    }

    /// <summary>仅 User 与 Player 都指回该会话时，才允许断线回调启动恢复窗口。</summary>
    public static bool TryBeginRecoveryWindow(
        UserComponent users,
        PlayerComponent players,
        TcpSession session,
        ReconnectTokenRegistry registry)
    {
        if (session.UserId <= 0L ||
            !users.Users.TryGetValue(session.UserId, out User? user) ||
            !ReferenceEquals(user.Session, session))
        {
            return false;
        }

        if (session.RoleId == 0L)
        {
            return user.PendingReconnectRoleId > 0L &&
                registry.BeginRecoveryWindow(
                    session.UserId,
                    user.PendingReconnectRoleId,
                    session.ReconnectToken);
        }

        PlayerEntity? player = players.GetByRoleId(session.RoleId);
        if (player == null)
        {
            return false;
        }

        PlayerOnlineComponent online = player.GetComponent<PlayerOnlineComponent>();
        if (online.UserId != session.UserId || !ReferenceEquals(online.Session, session))
        {
            return false;
        }

        return registry.BeginRecoveryWindow(session.UserId, session.RoleId, session.ReconnectToken);
    }

    /// <summary>只清理仍由该会话持有的待 EnterGame 角色，避免陈旧异步回调覆盖新会话。</summary>
    public static void ClearPendingReconnectRole(UserComponent users, TcpSession session)
    {
        if (session.UserId > 0L &&
            users.Users.TryGetValue(session.UserId, out User? user) &&
            ReferenceEquals(user.Session, session))
        {
            user.PendingReconnectRoleId = 0L;
        }
    }
}
