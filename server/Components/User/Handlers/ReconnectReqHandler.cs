using System.Threading.Tasks;
using Shared.Common.Protos;
using Shared.Common.Enums;
using Shared.Core.Log;
using Shared.Core.Network;

namespace Game.Components.User;

public sealed class ReconnectReqHandler : RpcMessageHandler<ReconnectReq, ReconnectRes>
{
    private static readonly ILog Log = LogManager.GetLogger(nameof(ReconnectReqHandler));

    protected override async Task ExecuteAsync(TcpSession session, ReconnectReq req, ReconnectRes res)
    {
        if (session.RoleId != 0L)
        {
            res.ErrorMessage = "当前会话已完成认证，不能重复重连";
            return;
        }

        UserComponent userComponent = Context.Server.GetComponent<UserComponent>();
        User? authenticatedUser = null;
        if (session.UserId != 0L)
        {
            if (!userComponent.Users.TryGetValue(session.UserId, out authenticatedUser) ||
                !ReferenceEquals(authenticatedUser.Session, session) ||
                authenticatedUser.PendingReconnectRoleId != req.RoleId)
            {
                res.ErrorMessage = "当前会话已完成认证，不能重复重连";
                return;
            }
        }

        if (!ReconnectTokenRegistry.Instance.TryConsumeAndRotate(
                req.Token,
                req.RoleId,
                out long userId,
                out long roleId,
                out string newToken,
                out bool replayed,
                out string errorMessage))
        {
            res.ErrorMessage = errorMessage;
            return;
        }

        if (!userComponent.Users.TryGetValue(userId, out User? user))
        {
            ReconnectTokenRegistry.Instance.Invalidate(userId, roleId);
            res.ErrorMessage = "登录状态已失效";
            return;
        }

        if (authenticatedUser != null)
        {
            if (!replayed || userId != session.UserId ||
                !string.Equals(session.ReconnectToken, newToken, StringComparison.Ordinal))
            {
                res.ErrorMessage = "当前会话已完成认证，不能重复重连";
                return;
            }

            res.Token = newToken;
            await Task.CompletedTask;
            return;
        }

        if (replayed && user.Session != null && user.Session != session && user.Session.IsConnected)
        {
            res.Token = newToken;
            res.ErrorMessage = "重连已在其他会话完成";
            return;
        }

        if (user.Session != null && user.Session != session && user.Session.IsConnected)
        {
            UserHelper.KickUser(user.Session, LogoutReason.AnotherSession);
        }

        user.Session = session;
        user.PendingReconnectRoleId = roleId;
        session.UserId = userId;
        session.RoleId = 0L;
        session.ReconnectToken = newToken;
        res.Token = newToken;

        Log.Info($"重连成功 UserId:{userId} RoleId:{roleId}");
        await Task.CompletedTask;
    }
}
