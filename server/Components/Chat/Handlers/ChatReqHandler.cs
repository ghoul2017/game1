using System.Threading.Tasks;
using Game.Components.Player;
using Shared.Common.Protos;
using Shared.Core.Log;
using Shared.Core.Network;

namespace Game.Components.Chat;

public sealed class ChatReqHandler : PlayerRpcMessageHandler<ChatReq, ChatAck>
{
    private static readonly ILog Log = LogManager.GetLogger(nameof(ChatReqHandler));

    protected override async Task ExecuteAsync(PlayerEntity player, ChatReq req, ChatAck res)
    {
        var (errorMessage, ntf, targetRoleId) = Context.Chat.HandleChat(
            player.Id, player.RoleData.Name, req.Channel, req.Content, req.TargetName);

        res.ErrorMessage = errorMessage;

        if (string.IsNullOrEmpty(errorMessage) && ntf != null)
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

        await Task.CompletedTask;
    }
}
