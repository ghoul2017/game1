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
        private readonly Dictionary<string, IMessageHandler> _handlers = new();


        /// <summary>
        /// 自动扫描程序集中所有带 [MessageHandler] 特性的扩展方法。
        /// </summary>
        public void AutoRegister()
        {
            MessageRegistry.EnsureInitialized();
            AutoRegister(AppDomain.CurrentDomain.GetAssemblies());
        }

        public void AutoRegister(params Assembly[] assemblies)
        {
            var handlerAttrType = typeof(MessageHandlerAttribute);
            var types = assemblies.Distinct().SelectMany(GetLoadableTypes).ToArray();

            foreach (var method in types
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

                var messageName = MessageRegistry.GetName(reqType);
                var handler = (AMethodHandler)Activator.CreateInstance(handlerType)!;
                handler.Init(method);
                RegisterHandler(messageName, handler, method.Name);
                Log.Info($"自动注册Handler({handlerType.Name}): {method.Name} -> MessageName:{messageName}");
            }

            foreach (var type in types.Where(IsClassHandlerType))
            {
                if (type.GetConstructor(Type.EmptyTypes) == null)
                {
                    Log.Warn($"继承式 Handler 必须有无参构造: {type.FullName}");
                    continue;
                }

                var handler = (IClassMessageHandler)Activator.CreateInstance(type)!;
                var messageType = handler.MessageType;
                if (!typeof(IMessage).IsAssignableFrom(messageType))
                {
                    Log.Warn($"继承式 Handler 的 MessageType 必须实现 IMessage: {type.FullName}");
                    continue;
                }

                var messageName = MessageRegistry.GetName(messageType);
                RegisterHandler(messageName, handler, type.FullName ?? type.Name);
                Log.Info($"自动注册Handler({type.Name}) -> MessageName:{messageName}");
            }

            Log.Info($"Handler 自动注册完成，共 {_handlers.Count} 个");
        }

        private static bool IsClassHandlerType(Type type)
        {
            return !type.IsAbstract
                   && !type.IsInterface
                   && !type.ContainsGenericParameters
                   && typeof(IClassMessageHandler).IsAssignableFrom(type);
        }

        private void RegisterHandler(string messageName, IMessageHandler handler, string source)
        {
            if (_handlers.ContainsKey(messageName))
                throw new InvalidOperationException($"重复注册消息 Handler: {messageName}, {source}");

            _handlers[messageName] = handler;
        }

        private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
        {
            if (assembly.IsDynamic)
                yield break;

            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(t => t != null).Cast<Type>().ToArray();
            }

            foreach (var type in types)
                yield return type;
        }

        public bool HasHandler(string messageName)
        {
            return _handlers.ContainsKey(messageName);
        }

        public async Task<IMessage?> HandleAsync(TcpSession session, string messageName, byte[] payload)
        {
            var handler = _handlers[messageName];
            return await handler.HandleAsync(session, payload);
        }

        public async Task RouteAsync(TcpSession session, string messageName, byte[] payload)
        {
            if (!HasHandler(messageName))
            {
                Log.Warn($"未注册的消息: {messageName}");
                return;
            }

            try
            {
                await HandleAsync(session, messageName, payload);
            }
            catch (Exception ex)
            {
                Log.Error($"处理消息 {messageName} 异常", ex);
            }
        }
    }
}
