using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Game.Core.Network;
using Shared.Core.Log;
using Shared.Core.Network;

namespace Game.Network;

public class MessageRouter
{
    private static readonly ILog Log = LogManager.GetLogger<MessageRouter>();
    private readonly Dictionary<ushort, IMessageHandler> _handlers = new();

    public void Register(IMessageHandler handler)
    {
        _handlers[handler.ReqMsgId] = handler;
    }

    /// <summary>
    /// 自动扫描程序集中所有带 [MessageHandler] 特性的扩展方法。
    /// </summary>
    public void AutoRegister()
    {
        var handlerAttrType = typeof(MessageHandlerAttribute);
        var rpcHandlerType = typeof(MessageHandler<,>);
        var ntfHandlerType = typeof(MessageNtfHandler<>);

        foreach (var method in Assembly.GetExecutingAssembly().GetTypes()
            .SelectMany(t => t.GetMethods(BindingFlags.Static | BindingFlags.Public))
            .Where(m => m.GetCustomAttribute(handlerAttrType) != null))
        {
            var attr = (MessageHandlerAttribute?)method.GetCustomAttribute(handlerAttrType);
            if (attr == null) continue;

            var parameters = method.GetParameters();
            if (parameters.Length < 3)
            {
                Log.Warn($"Handler 方法参数不足: {method.Name}");
                continue;
            }

            var componentType = parameters[0].ParameterType;
            var reqType = parameters[2].ParameterType;

            // 验证 TReq 实现 IMessage
            if (!typeof(IMessage).IsAssignableFrom(reqType))
            {
                Log.Warn($"Handler 方法的第3个参数必须实现 IMessage: {method.Name}");
                continue;
            }

            // 获取 MsgId
            var msgIdProp = reqType.GetProperty("MsgId");
            var msgId = (ushort)(msgIdProp?.GetValue(null) ?? 0);

            // 判断是 RPC 还是通知风格
            var handlerType = attr.type;
            bool isRpc = false;

            if (handlerType != null && handlerType.IsGenericTypeDefinition)
            {
                var genericDef = handlerType.GetGenericTypeDefinition();
                isRpc = genericDef == rpcHandlerType;
            }

            if (isRpc && parameters.Length == 4)
            {
                // RPC 风格：4 个参数
                var respType = parameters[3].ParameterType;
                var handler = new RpcMethodHandler(method, componentType, reqType, respType);
                _handlers[msgId] = handler;
                Log.Info($"自动注册Handler(RPC): {method.Name} -> MsgId:{msgId}");
            }
            else if (!isRpc && parameters.Length == 3)
            {
                // 通知风格：3 个参数
                var handler = new NtfMethodHandler(method, componentType, reqType);
                _handlers[msgId] = handler;
                Log.Info($"自动注册Handler(Ntf): {method.Name} -> MsgId:{msgId}");
            }
            else
            {
                Log.Warn($"Handler 方法签名不匹配: {method.Name} (参数数:{parameters.Length}, isRpc:{isRpc})");
            }
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
