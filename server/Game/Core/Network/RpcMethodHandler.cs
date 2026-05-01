using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Game;
using Game.Core;
using Game.Network;
using ProtoBuf;
using Shared.Core.Network;

namespace Game.Core.Network;

/// <summary>
/// RPC 风格消息处理器适配器。
/// 将扩展方法风格 Handler 适配到 IMessageHandler 接口。
/// </summary>
public class RpcMethodHandler : IMessageHandler
{
    private readonly MethodInfo _method;
    private readonly Type _componentType;
    private readonly Type _reqType;
    private readonly Type _respType;

    public RpcMethodHandler(MethodInfo method, Type componentType, Type reqType, Type respType)
    {
        _method = method;
        _componentType = componentType;
        _reqType = reqType;
        _respType = respType;
    }

    public async Task<IMessage?> HandleAsync(TcpSession session, byte[] payload)
    {
        // 反序列化请求
        object req;
        using (var ms = new MemoryStream(payload))
            req = Serializer.NonGeneric.Deserialize(_reqType, ms);

        // 创建响应对象
        var res = System.Activator.CreateInstance(_respType);

        // 获取组件实例
        var component = Context.Server.GetComponent(_componentType);

        // 调用扩展方法
        var task = (Task<IMessage?>)_method.Invoke(null, new object[] { component!, session, req, res! })!;
        return await task;
    }
}
