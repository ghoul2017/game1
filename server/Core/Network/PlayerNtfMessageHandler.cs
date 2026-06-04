using System;
using System.IO;
using System.Threading.Tasks;
using Game;
using Game.Components.Player;
using ProtoBuf;

#nullable enable
namespace Shared.Core.Network;

/// <summary>
/// Player 通知消息处理器基类。根据 session.RoleId 查找在线玩家后执行业务逻辑。
/// </summary>
public abstract class PlayerNtfMessageHandler<TNtf> : IClassMessageHandler
    where TNtf : class, IMessage, new()
{
    public Type MessageType => typeof(TNtf);

    protected abstract Task ExecuteAsync(PlayerEntity player, TNtf ntf);

    public async Task<IMessage?> HandleAsync(TcpSession session, byte[] payload)
    {
        TNtf ntf;
        using (var ms = new MemoryStream(payload))
            ntf = Serializer.Deserialize<TNtf>(ms);

        var player = Context.Players.GetByRoleId(session.RoleId);
        if (player == null)
            return null;

        await ExecuteAsync(player, ntf);
        return null;
    }
}
