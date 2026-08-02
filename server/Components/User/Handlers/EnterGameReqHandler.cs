using System.Threading.Tasks;
using Game.Database;
using Game.Components.Player;
using Shared.Common.Protos;
using Shared.Core.Network;

namespace Game.Components.User;

public sealed class EnterGameReqHandler : RpcMessageHandler<EnterGameReq, EnterGameRes>
{
    protected override async Task ExecuteAsync(TcpSession session, EnterGameReq req, EnterGameRes res)
    {
        long userId = session.UserId;
        if (userId == 0L)
        {
            res.ErrorMessage = "未登录";
            return;
        }

        UserComponent userComponent = Context.Server.GetComponent<UserComponent>();
        if (!UserHelper.IsCurrentSession(session, userId) ||
            !userComponent.Users.TryGetValue(userId, out User? user))
        {
            res.ErrorMessage = "登录会话已失效";
            return;
        }

        bool enteredGame = false;
        try
        {
            if (user.PendingReconnectRoleId != 0L && user.PendingReconnectRoleId != req.RoleId)
            {
                res.ErrorMessage = "重连角色与进入角色不一致";
                return;
            }

            if (!Context.Players.CanEnterRole(session, req.RoleId))
            {
                res.ErrorMessage = "当前会话已绑定其他角色";
                return;
            }

            UserRole? userRole = await MongoDb.Instance.QueryFirstOrDefaultAsync<UserRole>(
                r => r.RoleId == req.RoleId && r.UserId == userId);
            if (!UserHelper.IsCurrentSession(session, userId))
            {
                res.ErrorMessage = "登录会话已失效";
                return;
            }

            if (userRole == null)
            {
                res.ErrorMessage = "角色不存在或不属于该账号";
                return;
            }

            (PlayerEntity player, string token)? result = await Context.Players.EnterGame(
                req.RoleId,
                userId,
                session,
                userRole.Name,
                userRole.Level,
                userRole.Job);
            if (result == null)
            {
                res.ErrorMessage = "进入游戏失败";
                return;
            }

            res.Token = result.Value.token;
            enteredGame = true;
        }
        finally
        {
            if (enteredGame)
            {
                UserHelper.ClearPendingReconnectRole(userComponent, session);
            }
        }
    }
}
