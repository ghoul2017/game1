using Game.Components.Chat;
using Game.Components.Player;
using System.Threading.Tasks;
using Game.Core.Network;
using Shared.Core.Event;
using Shared.Core.Log;
using Shared.Core.Network;
using Shared.Common.Protos;

namespace Game.Components.Chat;

public static class ChatHandlers
{
    private static readonly ILog Log = LogManager.GetLogger(nameof(ChatHandlers));

    [MessageHandler(typeof(PlayerRpcMethodHandler))]
    public static Task<IMessage> ChatHandler(PlayerEntity player, ChatReq req, ChatAck res)
    {
        var (code, ntf, targetRoleId) = Context.Chat.HandleChat(
            player.Id, player.RoleData.Name, req.Channel, req.Content, req.TargetName);

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

            Log.Debug($"聊天 RoleId:{player.Id} Channel:{req.Channel}");
        }

        return Task.FromResult<IMessage>(res);
    }

    #region 事件订阅

    [EventSubscriber]
    public static void OnPlayerOffline(EventType.PlayerOfflineEvent e)
    {
        Context.Chat.CleanCooldown(e.RoleId);
    }

    #endregion
}
