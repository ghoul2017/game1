using System;
using System.Threading.Tasks;
using Game.Components.Player;
using Shared.Common.Protos;
using Shared.Core.Log;
using Shared.Core.Network;

namespace Game.Components.Chat;

public sealed class ChatReqHandler : PlayerRpcMessageHandler<ChatReq, ChatAck>
{
    private static readonly ILog Log = LogManager.GetLogger(nameof(ChatReqHandler));
    private const long WorldChatCooldownMs = 3000;
    private const int MaxContentLength = 500;

    protected override async Task ExecuteAsync(PlayerEntity player, ChatReq req, ChatAck res)
    {
        var roleDataComponent = player.GetComponent<RoleDataComponent>();
        var chatComponent = Context.Chat;

        if (string.IsNullOrWhiteSpace(req.Content))
        {
            res.ErrorMessage = "聊天内容不能为空";
            return;
        }

        var content = req.Content.Length > MaxContentLength
            ? req.Content[..MaxContentLength]
            : req.Content;
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        switch (req.Channel)
        {
            case ChatChannel.World:
                if (chatComponent.LastWorldChatTime.TryGetValue(player.Id, out var lastTime) &&
                    now - lastTime < WorldChatCooldownMs)
                {
                    res.ErrorMessage = "发言太快，请稍后再试";
                    return;
                }

                chatComponent.LastWorldChatTime[player.Id] = now;
                Context.Players.Broadcast(CreateChatNtf(
                    player.Id, roleDataComponent.Name, ChatChannel.World, content, now));
                break;

            case ChatChannel.Private:
                if (string.IsNullOrEmpty(req.TargetName) || req.TargetName == roleDataComponent.Name)
                {
                    res.ErrorMessage = "无效的私聊目标";
                    return;
                }

                var targetPlayer = Context.Players.GetByRoleName(req.TargetName);
                if (targetPlayer == null)
                {
                    res.ErrorMessage = "目标玩家不在线";
                    return;
                }

                Context.Players.SendToPlayer(targetPlayer.Id, CreateChatNtf(
                    player.Id, roleDataComponent.Name, ChatChannel.Private, content, now));
                break;

            default:
                res.ErrorMessage = "无效的聊天频道";
                return;
        }

        Log.Debug($"聊天 RoleId:{player.Id} Channel:{req.Channel}");
        await Task.CompletedTask;
    }

    private static ChatNtf CreateChatNtf(
        long senderId, string senderName, int channel, string content, long sendTime)
    {
        return new ChatNtf
        {
            Channel = channel,
            SenderId = senderId,
            SenderName = senderName,
            Content = content,
            SendTime = sendTime,
        };
    }
}
