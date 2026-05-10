using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Shared.Core.Log;

#nullable enable
namespace Shared.Core.Network
{
    public class MessageRouter
    {
        private static readonly ILog Log = LogManager.GetLogger<MessageRouter>();
        private readonly Dictionary<ushort, IMessageHandler> _handlers = new();


        /// <summary>
        /// 自动扫描程序集中所有带 [MessageHandler] 特性的扩展方法。
        /// </summary>
        public void AutoRegister()
        {
            var handlerAttrType = typeof(MessageHandlerAttribute);

            foreach (var method in Assembly.GetExecutingAssembly().GetTypes()
                         .SelectMany(t => t.GetMethods(BindingFlags.Static | BindingFlags.Public))
                         .Where(m => m.GetCustomAttribute(handlerAttrType) != null))
            {
                MessageHandlerAttribute? attr = (MessageHandlerAttribute?)method.GetCustomAttribute(handlerAttrType);
                if (attr == null)
                    continue;
                var handlerType = attr.type;
                if (!typeof(AMethodHandler).IsAssignableFrom(handlerType))
                {
                    Log.Warn($"Handler 必须实现 IMessageHandler: {handlerType}");
                    continue;
                }

                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length < 2)
                {
                    Log.Warn($"Handler 方法参数不足: {method.Name}");
                    continue;
                }

                var reqType = parameters[1].ParameterType;

                // 验证 TReq 实现 IMessage
                if (!typeof(IMessage).IsAssignableFrom(reqType))
                {
                    Log.Warn($"Handler 方法的第2个参数必须实现 IMessage: {method.Name}");
                    continue;
                }

                // 获取 MsgId（需要创建实例，因为 MsgId 是实例属性）
                var reqInstance = (IMessage?)Activator.CreateInstance(reqType);
                var msgId = reqInstance?.MsgId ?? 0;


                var handler = (AMethodHandler)Activator.CreateInstance(handlerType)!;
                handler.Init(method);
                _handlers[msgId] = handler;
                Log.Info($"自动注册Handler({handlerType.Name}): {method.Name} -> MsgId:{msgId}");
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
                var res = await handler.HandleAsync(session, payload);
                if (res != null)
                    session.Send(res);
            }
            catch (Exception ex)
            {
                Log.Error($"处理消息 {msgId} 异常",ex);
            }
        }
    }
}