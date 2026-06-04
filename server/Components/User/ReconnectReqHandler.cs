using System.Threading.Tasks;
using Game.Components.Player;
using Shared.Common.Protos;
using Shared.Core;
using Shared.Core.Log;
using Shared.Core.Network;

namespace Game.Components.User;

public sealed class ReconnectReqHandler : RpcMessageHandler<ReconnectReq, ReconnectRes>
{
    private static readonly ILog Log = LogManager.GetLogger(nameof(ReconnectReqHandler));

    protected override async Task ExecuteAsync(TcpSession session, ReconnectReq req, ReconnectRes res)
    {
        var parsed = TokenSigner.Instance.Validate(req.Token);
        if (parsed == null)
        {
            res.ErrorMessage = "Token无效或已过期";
            return;
        }

        var (userId, roleId, _) = parsed.Value;

        RoleDataComponent? roleData = null;
        if (roleId > 0)
        {
            roleData = await Context.Players.LoadRoleDataAsync(roleId);
            if (roleData == null || roleData.LastToken != req.Token)
            {
                res.ErrorMessage = "Token已失效";
                return;
            }
        }

        session.UserId = userId;

        var newToken = TokenSigner.Instance.Generate(userId, roleId);
        session.ReconnectToken = newToken;

        if (roleData != null)
        {
            roleData.LastToken = newToken;
        }

        res.Token = newToken;

        Log.Info($"重连成功 UserId:{userId}");
    }
}
