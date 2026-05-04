using System.Threading.Tasks;
using Game.Game.Player;
using Game.Network;
using Shared.Core.Log;
using Shared.Core.Network;
using Shared.Common.Protos;

namespace Game.Game.Chat;

/// <summary>
/// 聊天请求 Handler
/// </summary>
public class ChatHandler : MessageHandler<ChatReq>
{
    private static readonly ILog Log = LogManager.GetLogger<ChatHandler>();

    protected override Task<IMessage?> ExecuteAsync(TcpSession session, ChatReq req)
    {
        var res = new ChatAck();

        // 必须已选角
        if (session.RoleId == 0)
        {
            res.Code = ChatErrorCode.NotLoggedIn;
            return Task.FromResult<IMessage?>(res);
        }

        var player = PlayerManager.Instance.GetByRoleId(session.RoleId);
        if (player == null)
        {
            res.Code = ChatErrorCode.NotLoggedIn;
            return Task.FromResult<IMessage?>(res);
        }

        var (code, ntf, targetRoleId) = ChatManager.Instance.HandleChat(
            player.RoleId, player.RoleName, req.Channel, req.Content, req.TargetName);

        res.Code = code;

        if (code == ChatErrorCode.Ok && ntf != null)
        {
            switch (req.Channel)
            {
                case ChatChannel.World:
                    // 广播给所有人（包括发送者）
                    PlayerManager.Instance.Broadcast(ntf);
                    break;
                case ChatChannel.Private:
                    // 发送给目标
                    PlayerManager.Instance.SendToPlayer(targetRoleId, ntf);
                    break;
            }

            Log.Debug($"聊天 RoleId:{session.RoleId} Channel:{req.Channel}");
        }

        return Task.FromResult<IMessage?>(res);
    }
}
