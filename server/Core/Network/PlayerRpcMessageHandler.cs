using System;
using System.IO;
using System.Threading.Tasks;
using Game;
using Game.Components.Player;
using ProtoBuf;

#nullable enable
namespace Shared.Core.Network;

/// <summary>
/// Player RPC 消息处理器基类。根据 session.RoleId 查找在线玩家后执行业务逻辑。
/// </summary>
public abstract class PlayerRpcMessageHandler<TReq, TRes> : IClassMessageHandler
    where TReq : class, IMessage, new()
    where TRes : class, IMessage, new()
{
    public Type MessageType => typeof(TReq);

    protected abstract Task ExecuteAsync(PlayerEntity player, TReq req, TRes res);

    public async Task<IMessage?> HandleAsync(TcpSession session, byte[] payload)
    {
        TReq req;
        using (var ms = new MemoryStream(payload))
            req = Serializer.Deserialize<TReq>(ms);

        var res = new TRes();
        var player = Context.Players.GetByRoleId(session.RoleId);
        if (player == null)
        {
            var errorMessageProperty = typeof(TRes).GetProperty("ErrorMessage");
            if (errorMessageProperty != null && errorMessageProperty.CanWrite)
                errorMessageProperty.SetValue(res, "玩家不在线");
            return res;
        }

        player.RefreshOnlineActivity(session.LastActiveTime);
        await ExecuteAsync(player, req, res);
        return res;
    }
}
