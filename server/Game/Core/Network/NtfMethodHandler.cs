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
/// 通知风格消息处理器适配器（无响应）。
/// 将扩展方法风格 Handler 适配到 IMessageHandler 接口。
/// </summary>
public class NtfMethodHandler : IMessageHandler
{
    private readonly MethodInfo _method;
    private readonly Type _componentType;
    private readonly Type _reqType;

    public ushort ReqMsgId { get; }

    public NtfMethodHandler(MethodInfo method, Type componentType, Type reqType)
    {
        _method = method;
        _componentType = componentType;
        _reqType = reqType;

        // 从 TReq 实例获取 MsgId（MsgId 是实例属性）
        var reqInstance = (IMessage?)System.Activator.CreateInstance(reqType);
        ReqMsgId = reqInstance?.MsgId ?? 0;
    }

    public async Task<IMessage?> HandleAsync(TcpSession session, byte[] payload)
    {
        // 反序列化请求
        object req;
        using (var ms = new MemoryStream(payload))
            req = Serializer.NonGeneric.Deserialize(_reqType, ms);

        // 获取组件实例
        var component = Context.Server.GetComponent(_componentType);

        // 调用扩展方法
        var task = (Task)_method.Invoke(null, new object[] { component!, session, req })!;
        await task;

        // 无响应
        return null;
    }
}
