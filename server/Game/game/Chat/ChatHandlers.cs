using System.Threading.Tasks;
using Game.Core.Network;
using Game.Game.Player;
using Game.Network;
using Shared.Core.Log;
using Shared.Core.Network;
using Shared.Common.Protos;

namespace Game.Game.Chat;

public static class ChatHandlers
{
    private static readonly ILog Log = LogManager.GetLogger(nameof(ChatHandlers));

    [MessageHandler(typeof(RpcMethodHandler))]
    public static Task<IMessage> ChatHandler(TcpSession session, ChatReq req, ChatAck res)
    {
        if (session.RoleId == 0)
        {
            res.Code = ChatErrorCode.NotLoggedIn;
            return Task.FromResult<IMessage>(res);
        }

        var player = PlayerManager.Instance.GetByRoleId(session.RoleId);
        if (player == null)
        {
            res.Code = ChatErrorCode.NotLoggedIn;
            return Task.FromResult<IMessage>(res);
        }

        var (code, ntf, targetRoleId) = ChatManager.Instance.HandleChat(
            player.Id, player.RoleData.Role?.Name ?? "", req.Channel, req.Content, req.TargetName);

        res.Code = code;

        if (code == ChatErrorCode.Ok && ntf != null)
        {
            switch (req.Channel)
            {
                case ChatChannel.World:
                    PlayerManager.Instance.Broadcast(ntf);
                    break;
                case ChatChannel.Private:
                    PlayerManager.Instance.SendToPlayer(targetRoleId, ntf);
                    break;
            }

            Log.Debug($"聊天 RoleId:{session.RoleId} Channel:{req.Channel}");
        }

        return Task.FromResult<IMessage>(res);
    }
}
