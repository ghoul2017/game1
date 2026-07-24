using Game.Components.Player;
using Shared.Common.Protos;
using Shared.Core.Log;
using System;

namespace Game.Components.Chat;

/// <summary>
/// 聊天系统。支持世界频道、私聊、系统广播。
/// </summary>
public static class ChatSystem
{
    private static readonly ILog Log = LogManager.GetLogger(nameof(ChatSystem));

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
