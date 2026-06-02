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

        var reqType = parameters[1].ParameterType;
        var respType = parameters[2].ParameterType;
        if (!typeof(IMessage).IsAssignableFrom(reqType))
        {
            throw new Exception($"MethodHandler 参数2必须实现IMessage：{method.Name}");
        }

        if (!typeof(IMessage).IsAssignableFrom(respType))
        {
            throw new Exception($"MethodHandler 参数3必须实现IMessage：{method.Name}");
        }

        if (method.ReturnType != typeof(Task<IMessage>))
        {
            throw new Exception($"MethodHandler 返回值必须是Task<IMessage>：{method.Name}");
        }

        _method = method;
        _reqType = reqType;
        _respType = respType;
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
            // 尝试设置 ErrorMessage
            var errMsgProp = res?.GetType().GetProperty("ErrorMessage");
            if (errMsgProp != null && errMsgProp.CanWrite)
                errMsgProp.SetValue(res, "玩家不在线");
            return res;
        }

        // 调用扩展方法
        var task = (Task<IMessage?>)_method.Invoke(null, new object[] { player, req, res! })!;
        return await task;
    }
}
