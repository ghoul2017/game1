using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Game;
using Game.Components.Player;
using Game.Core;
using ProtoBuf;
using Shared.Core.Network;

namespace Game.Core.Network;

/// <summary>
/// Player 通知风格消息处理器适配器（无响应）。
/// 将扩展方法风格 Handler 适配到 IMessageHandler 接口。
/// </summary>
public class PlayerNtfMethodHandler : AMethodHandler
{
    private MethodInfo _method = null!;
    private Type _ntfType = null!;

    public override void Init(MethodInfo method)
    {
        ParameterInfo[] parameters = method.GetParameters();
        if (parameters.Length != 2)
        {
            throw new Exception($"MethodHandler Handler与Method的参数必须一致{method.Name}");
        }

        var playerType = parameters[0].ParameterType;
        if (playerType != typeof(PlayerEntity))
        {
            throw new Exception($"MethodHandler 参数1必须是PlayerEntity：{method.Name}");
        }

        _method = method;
        _ntfType = parameters[1].ParameterType;
    }

    public override async Task<IMessage?> HandleAsync(TcpSession session, byte[] payload)
    {
        // 反序列化通知
        object ntf;
        using (var ms = new MemoryStream(payload))
            ntf = Serializer.NonGeneric.Deserialize(_ntfType, ms);

        var player = Context.Players.GetByRoleId(session.RoleId);
        if (player == null)
        {
            return null;
        }

        // 调用扩展方法
        var task = (Task)_method.Invoke(null, new object[] { player, ntf })!;
        await task;

        // 无响应
        return null;
    }
}
