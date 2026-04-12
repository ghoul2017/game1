using Server.Core.Log;
using Server.Game.Player;
using Server.Network;
using ProtoBuf;
using server.common.protos;

namespace Server.Game.Chat;

/// <summary>
/// 聊天管理器。支持世界频道、私聊、系统广播。
/// </summary>
public class ChatManager
{
    private static readonly ILog Log = LogManager.GetLogger<ChatManager>();

    public static ChatManager Instance { get; } = new();

    /// <summary>世界聊天冷却（毫秒）</summary>
    private const long WorldChatCooldownMs = 3000;

    /// <summary>聊天内容最大长度</summary>
    private const int MaxContentLength = 500;

    /// <summary>最近一次世界聊天时间（按角色记录）</summary>
    private readonly Dictionary<long, long> _lastWorldChatTime = new();

    private ChatManager() { }

    /// <summary>处理聊天请求</summary>
    public (int code, ChatNtf? ntf, long targetRoleId) HandleChat(long senderRoleId, string senderName, int channel, string content, string targetName)
    {
        // 参数校验
        if (string.IsNullOrWhiteSpace(content))
            return (ChatErrorCode.EmptyContent, null, 0);

        if (content.Length > MaxContentLength)
            content = content[..MaxContentLength];

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        switch (channel)
        {
            case ChatChannel.World:
                // 冷却检查
                if (_lastWorldChatTime.TryGetValue(senderRoleId, out var lastTime)
                    && now - lastTime < WorldChatCooldownMs)
                    return (ChatErrorCode.Cooldown, null, 0);

                _lastWorldChatTime[senderRoleId] = now;

                var worldNtf = new ChatNtf
                {
                    Channel = ChatChannel.World,
                    SenderId = senderRoleId,
                    SenderName = senderName,
                    Content = content,
                    SendTime = now,
                };
                return (ChatErrorCode.Ok, worldNtf, 0);

            case ChatChannel.Private:
                if (string.IsNullOrEmpty(targetName))
                    return (ChatErrorCode.InvalidTarget, null, 0);

                if (targetName == senderName)
                    return (ChatErrorCode.InvalidTarget, null, 0);

                // 查找目标角色
                var targetPlayer = PlayerManager.Instance.GetByRoleName(targetName);
                if (targetPlayer == null)
                    return (ChatErrorCode.TargetNotFound, null, 0);

                var privateNtf = new ChatNtf
                {
                    Channel = ChatChannel.Private,
                    SenderId = senderRoleId,
                    SenderName = senderName,
                    Content = content,
                    SendTime = now,
                };
                return (ChatErrorCode.Ok, privateNtf, targetPlayer.RoleId);

            default:
                return (ChatErrorCode.InvalidChannel, null, 0);
        }
    }

    /// <summary>发送系统广播</summary>
    public void SendSystemBroadcast(string content)
    {
        var ntf = new SystemChatNtf
        {
            Content = content,
            SendTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        };

        SessionManager.Instance.Broadcast(ntf);

        Log.Info($"系统广播: {content}");
    }

    /// <summary>清理离线玩家的冷却记录</summary>
    public void CleanCooldown(long roleId)
    {
        _lastWorldChatTime.Remove(roleId);
    }
}

public static class ChatErrorCode
{
    public const int Ok = 0;
    public const int EmptyContent = 3001;
    public const int Cooldown = 3002;
    public const int InvalidChannel = 3003;
    public const int InvalidTarget = 3004;
    public const int TargetNotFound = 3005;
    public const int NotLoggedIn = 3006;
}
