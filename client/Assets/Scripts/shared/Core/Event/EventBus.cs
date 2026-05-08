using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Shared.Core.Log;

namespace Shared.Core.Event
{
    public class EventBus
    {
        private static readonly ILog Log = LogManager.GetLogger<EventBus>();

        public static EventBus Instance { get; } = new();

        private readonly ConcurrentDictionary<Type, List<Action<IEvent>>> _syncHandlers = new();
        private readonly ConcurrentDictionary<Type, List<Func<IEvent, Task>>> _asyncHandlers = new();

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
            int syncCount = 0;
            int asyncCount = 0;

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

                if (method.ReturnType == typeof(void))
                {
                    var actionType = typeof(Action<>).MakeGenericType(eventType);
                    var del = Delegate.CreateDelegate(actionType, method);

                    Action<IEvent> wrapper = e => del.DynamicInvoke(e);

                    var list = _syncHandlers.GetOrAdd(eventType, _ => new List<Action<IEvent>>());
                    lock (list)
                    {
                        list.Add(wrapper);
                    }

                    syncCount++;
                }
                else if (method.ReturnType == typeof(Task))
                {
                    var funcType = typeof(Func<,>).MakeGenericType(eventType, typeof(Task));
                    var del = Delegate.CreateDelegate(funcType, method);

                    Func<IEvent, Task> wrapper = e => (Task)del.DynamicInvoke(e)!;

                    var list = _asyncHandlers.GetOrAdd(eventType, _ => new List<Func<IEvent, Task>>());
                    lock (list)
                    {
                        list.Add(wrapper);
                    }

                    asyncCount++;
                }
                else
                {
                    Log.Warn($"[EventSubscriber] 返回类型必须是 void 或 Task: {method.DeclaringType?.Name}.{method.Name}");
                    continue;
                }

                Log.Info($"注册 EventSubscriber: {method.DeclaringType?.Name}.{method.Name} -> {eventType.Name}");
            }

            Log.Info($"EventSubscriber 自动注册完成，同步 {syncCount} 个，异步 {asyncCount} 个");
        }

        public void Publish<TEvent>(TEvent e) where TEvent : IEvent
        {
            if (_syncHandlers.TryGetValue(typeof(TEvent), out var syncList))
            {
                List<Action<IEvent>> snapshot;
                lock (syncList)
                {
                    snapshot = new List<Action<IEvent>>(syncList);
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
        }

        public async Task PublishAsync<TEvent>(TEvent e) where TEvent : IEvent
        {
            Publish(e);

            if (!_asyncHandlers.TryGetValue(typeof(TEvent), out var asyncList)) return;

            List<Func<IEvent, Task>> snapshot;
            lock (asyncList)
            {
                snapshot = new List<Func<IEvent, Task>>(asyncList);
            }

            foreach (var handler in snapshot)
            {
                try
                {
                    await handler(e);
                }
                catch (Exception ex)
                {
                    Log.Error($"异步事件处理异常 [{typeof(TEvent).Name}]: {ex.Message}");
                }
            }
        }

        public void Clear()
        {
            _syncHandlers.Clear();
            _asyncHandlers.Clear();
        }
    }
}