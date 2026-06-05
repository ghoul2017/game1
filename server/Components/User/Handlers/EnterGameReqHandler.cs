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
        if (session.UserId == 0)
        {
            res.ErrorMessage = "未登录";
            return;
        }

        var userRole = await MongoDb.Instance.QueryFirstOrDefaultAsync<UserRole>(
            r => r.RoleId == req.RoleId && r.UserId == session.UserId);
        if (userRole == null)
        {
            res.ErrorMessage = "角色不存在或不属于该账号";
            return;
        }

        var result = await Context.Players.EnterGame(req.RoleId, session.UserId, session, userRole.Name, userRole.Level, userRole.Job);
        if (result == null)
        {
            res.ErrorMessage = "进入游戏失败";
            return;
        }

        var (_, token) = result.Value;
        res.Token = token;
    }
}
