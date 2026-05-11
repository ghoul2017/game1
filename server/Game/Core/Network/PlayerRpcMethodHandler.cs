using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Game;
using Game.Components.Player;
using Shared.Core;
using ProtoBuf;
using Shared.Core.Network;

namespace Shared.Core.Network;

/// <summary>
/// Player RPC 风格消息处理器适配器。
/// 将扩展方法风格 Handler 适配到 IMessageHandler 接口。
/// </summary>
public class PlayerRpcMethodHandler : AMethodHandler
{
    private MethodInfo _method = null!;
    private Type _reqType = null!;
    private Type _respType = null!;

    public override void Init(MethodInfo method)
    {
        ParameterInfo[] parameters = method.GetParameters();
        if (parameters.Length != 3)
        {
            throw new Exception($"MethodHandler Handler与Method的参数必须一致{method.Name}");
        }

        var playerType = parameters[0].ParameterType;
        if (playerType != typeof(PlayerEntity))
        {
            throw new Exception($"MethodHandler 参数1必须是PlayerEntity：{method.Name}");
        }

        _method = method;
        _reqType = parameters[1].ParameterType;
        _respType = parameters[2].ParameterType;
    }

    public override async Task<IMessage?> HandleAsync(TcpSession session, byte[] payload)
    {
        // 反序列化请求
        object req;
        using (var ms = new MemoryStream(payload))
            req = Serializer.NonGeneric.Deserialize(_reqType, ms);

        // 创建响应对象
        var res = (IMessage?)System.Activator.CreateInstance(_respType);
        var player = Context.Players.GetByRoleId(session.RoleId);
        if (player == null)
        {
            // res.Code = ErrorCode.InvalidParam;
            return res;
        }

        // 调用扩展方法
        var task = (Task<IMessage?>)_method.Invoke(null, new object[] { player, req, res! })!;
        return await task;
    }
}