using System.Reflection;
using Server.Core.Log;

namespace Server.Network;

public class MessageRouter
{
    private static readonly ILog Log = LogManager.GetLogger<MessageRouter>();
    private readonly Dictionary<ushort, IMessageHandler> _handlers = new();

    public void Register(IMessageHandler handler)
    {
        _handlers[handler.ReqMsgId] = handler;
    }

    /// <summary>自动扫描程序集中所有 IMessageHandler 实现（需有无参构造函数）</summary>
    public void AutoRegister()
    {
        var handlerType = typeof(IMessageHandler);
        var types = Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => !t.IsAbstract && !t.IsInterface && handlerType.IsAssignableFrom(t)
                        && t.GetConstructor(Type.EmptyTypes) != null);

        foreach (var type in types)
        {
            var handler = (IMessageHandler)Activator.CreateInstance(type)!;
            Register(handler);
            Log.Info($"自动注册Handler: {type.Name} -> MsgId:{handler.ReqMsgId}");
        }

        Log.Info($"Handler 自动注册完成，共 {_handlers.Count} 个");
    }

    public async Task RouteAsync(TcpSession session, ushort msgId, byte[] payload)
    {
        if (!_handlers.TryGetValue(msgId, out var handler))
        {
            Log.Warn($"未注册的消息ID: {msgId}");
            return;
        }

        try
        {
            var resp = await handler.HandleAsync(session, payload);
            if (resp != null)
                session.Send(resp);
        }
        catch (Exception ex)
        {
            Log.Error($"处理消息 {msgId} 异常: {ex.Message}");
        }
    }
}
