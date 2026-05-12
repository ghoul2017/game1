using Game.Components.Player;
using Game.Components.Chat;
using Shared.Common.Protos;
using Shared.Core.Log;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Components.Chat;

/// <summary>
/// 聊天系统。支持世界频道、私聊、系统广播。
/// </summary>
public static class ChatSystem
{
    private static readonly ILog Log = LogManager.GetLogger(nameof(ChatSystem));

    /// <summary>世界聊天冷却（毫秒）</summary>
    public const long WorldChatCooldownMs = 3000;

    /// <summary>聊天内容最大长度</summary>
    public const int MaxContentLength = 500;

    /// <summary>处理聊天请求</summary>
    public static (string errorMessage, ChatNtf? ntf, long targetRoleId) HandleChat(this ChatComponent self, long senderRoleId, string senderName, int channel, string content, string targetName)
    {
        // 参数校验
        if (string.IsNullOrWhiteSpace(content))
            return ("聊天内容不能为空", null, 0);

        if (content.Length > MaxContentLength)
            content = content[..MaxContentLength];

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        switch (channel)
        {
            case ChatChannel.World:
                // 冷却检查
                if (self.LastWorldChatTime.TryGetValue(senderRoleId, out var lastTime)
                    && now - lastTime < WorldChatCooldownMs)
                    return ("发言太快，请稍后再试", null, 0);

                self.LastWorldChatTime[senderRoleId] = now;

                var worldNtf = new ChatNtf
                {
                    Channel = ChatChannel.World,
                    SenderId = senderRoleId,
                    SenderName = senderName,
                    Content = content,
                    SendTime = now,
                };
                return ("", worldNtf, 0);

            case ChatChannel.Private:
                if (string.IsNullOrEmpty(targetName))
                    return ("无效的私聊目标", null, 0);

                if (targetName == senderName)
                    return ("无效的私聊目标", null, 0);

                // 查找目标角色
                var targetPlayer = Context.Players.GetByRoleName(targetName);
                if (targetPlayer == null)
                    return ("目标玩家不在线", null, 0);

                var privateNtf = new ChatNtf
                {
                    Channel = ChatChannel.Private,
                    SenderId = senderRoleId,
                    SenderName = senderName,
                    Content = content,
                    SendTime = now,
                };
                return ("", privateNtf, targetPlayer.Id);

            default:
                return ("无效的聊天频道", null, 0);
        }
    }

    /// <summary>发送系统广播</summary>
    public static void SendSystemBroadcast(this ChatComponent self, string content)
    {
        var ntf = new SystemChatNtf
        {
            Content = content,
            SendTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        };

        Context.Players.Broadcast(ntf);

        Log.Info($"系统广播: {content}");
    }

    /// <summary>清理离线玩家的冷却记录</summary>
    public static void CleanCooldown(this ChatComponent self, long roleId)
    {
        self.LastWorldChatTime.Remove(roleId);
    }
}
