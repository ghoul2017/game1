using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Shared.Core.Log;

namespace Shared.Core.Event
{
    public class EventBus
    {
        private static readonly ILog Log = LogManager.GetLogger<EventBus>();

        public static EventBus Instance { get; } = new();

        private readonly ConcurrentDictionary<Type, List<Action<IEvent>>> _handlers = new();

        private EventBus()
        {
        }

        public void AutoRegister()
        {
            AutoRegister(Assembly.GetExecutingAssembly());
        }

        public void AutoRegister(Assembly assembly)
        {
            var attrType = typeof(EventSubscriberAttribute);
            int count = 0;

            foreach (var method in assembly.GetTypes()
                         .SelectMany(t => t.GetMethods(BindingFlags.Static | BindingFlags.Public))
                         .Where(m => m.GetCustomAttribute(attrType) != null))
            {
                var parameters = method.GetParameters();
                if (parameters.Length != 1)
                {
                    Log.Warn($"[EventSubscriber] 方法参数必须只有一个: {method.DeclaringType?.Name}.{method.Name}");
                    continue;
                }

                var eventType = parameters[0].ParameterType;
                if (!typeof(IEvent).IsAssignableFrom(eventType))
                {
                    Log.Warn($"[EventSubscriber] 参数必须实现 IEvent: {method.DeclaringType?.Name}.{method.Name}");
                    continue;
                }

                if (method.ReturnType != typeof(void))
                {
                    Log.Warn($"[EventSubscriber] 返回类型必须是 void: {method.DeclaringType?.Name}.{method.Name}");
                    continue;
                }

                var actionType = typeof(Action<>).MakeGenericType(eventType);
                var del = Delegate.CreateDelegate(actionType, method);

                Action<IEvent> wrapper = e => del.DynamicInvoke(e);

                var list = _handlers.GetOrAdd(eventType, _ => new List<Action<IEvent>>());
                lock (list)
                {
                    list.Add(wrapper);
                }

                Log.Info($"注册 EventSubscriber: {method.DeclaringType?.Name}.{method.Name} -> {eventType.Name}");
                count++;
            }

            Log.Info($"EventSubscriber 自动注册完成，共 {count} 个");
        }

        public void Publish<TEvent>(TEvent e) where TEvent : IEvent
        {
            if (!_handlers.TryGetValue(typeof(TEvent), out var list)) return;

            List<Action<IEvent>> snapshot;
            lock (list)
            {
                snapshot = new List<Action<IEvent>>(list);
            }

            foreach (var handler in snapshot)
            {
                try
                {
                    handler(e);
                }
                catch (Exception ex)
                {
                    Log.Error($"事件处理异常 [{typeof(TEvent).Name}]: {ex.Message}");
                }
            }
        }

        public void Clear()
        {
            _handlers.Clear();
        }
    }
}