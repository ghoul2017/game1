using Game.Components.Chat;
using Game.Components.Player;
using System.Threading.Tasks;
using Game.Core.Network;
using Game.Network;
using Shared.Core.Log;
using Shared.Core.Network;
using Shared.Common.Protos;

namespace Game.Components.Chat;

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

        var player = Context.Players.GetByRoleId(session.RoleId);
        if (player == null)
        {
            res.Code = ChatErrorCode.NotLoggedIn;
            return Task.FromResult<IMessage>(res);
        }

        var (code, ntf, targetRoleId) = Context.Chat.HandleChat(
            player.Id, player.RoleData.Role?.Name ?? "", req.Channel, req.Content, req.TargetName);

        res.Code = code;

        if (code == ChatErrorCode.Ok && ntf != null)
        {
            switch (req.Channel)
            {
                case ChatChannel.World:
                    Context.Players.Broadcast(ntf);
                    break;
                case ChatChannel.Private:
                    Context.Players.SendToPlayer(targetRoleId, ntf);
                    break;
            }

            Log.Debug($"聊天 RoleId:{session.RoleId} Channel:{req.Channel}");
        }

        return Task.FromResult<IMessage>(res);
    }
}
