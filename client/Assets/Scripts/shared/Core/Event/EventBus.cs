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
            AutoRegister(AppDomain.CurrentDomain.GetAssemblies());
        }

        public void AutoRegister(Assembly assembly)
        {
            AutoRegister(new[] { assembly });
        }

        public void AutoRegister(params Assembly[] assemblies)
        {
            int syncCount = 0;
            int asyncCount = 0;
            var types = assemblies.Distinct().SelectMany(GetLoadableTypes).ToArray();

            foreach (var type in types.Where(IsClassHandlerType))
            {
                if (type.GetConstructor(Type.EmptyTypes) == null)
                {
                    Log.Warn($"继承式 EventHandler 必须有无参构造: {type.FullName}");
                    continue;
                }

                var handler = Activator.CreateInstance(type)!;

                foreach (var handlerInterface in GetHandlerInterfaces(type, typeof(ISyncEventHandler<>)))
                {
                    var eventType = handlerInterface.GetGenericArguments()[0];
                    RegisterSyncHandler(handler, eventType);
                    syncCount++;
                    Log.Info($"自动注册EventHandler({type.Name}) -> Event:{eventType.Name}");
                }

                foreach (var handlerInterface in GetHandlerInterfaces(type, typeof(IAsyncEventHandler<>)))
                {
                    var eventType = handlerInterface.GetGenericArguments()[0];
                    RegisterAsyncHandler(handler, eventType);
                    asyncCount++;
                    Log.Info($"自动注册AsyncEventHandler({type.Name}) -> Event:{eventType.Name}");
                }
            }

            Log.Info($"EventHandler 自动注册完成，同步 {syncCount} 个，异步 {asyncCount} 个");
        }

        private static bool IsClassHandlerType(Type type)
        {
            return !type.IsAbstract
                   && !type.IsInterface
                   && !type.ContainsGenericParameters
                   && (GetHandlerInterfaces(type, typeof(ISyncEventHandler<>)).Any()
                       || GetHandlerInterfaces(type, typeof(IAsyncEventHandler<>)).Any());
        }

        private static IEnumerable<Type> GetHandlerInterfaces(Type type, Type genericHandlerType)
        {
            return type.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == genericHandlerType);
        }

        private void RegisterSyncHandler(object handler, Type eventType)
        {
            var method = typeof(EventBus)
                .GetMethod(nameof(RegisterSyncHandlerGeneric), BindingFlags.Instance | BindingFlags.NonPublic)!
                .MakeGenericMethod(eventType);
            method.Invoke(this, new[] { handler });
        }

        private void RegisterSyncHandlerGeneric<TEvent>(object handler) where TEvent : IEvent
        {
            var typedHandler = (ISyncEventHandler<TEvent>)handler;
            Action<IEvent> wrapper = e => typedHandler.Handle((TEvent)e);
            var list = _syncHandlers.GetOrAdd(typeof(TEvent), _ => new List<Action<IEvent>>());
            lock (list)
            {
                list.Add(wrapper);
            }
        }

        private void RegisterAsyncHandler(object handler, Type eventType)
        {
            var method = typeof(EventBus)
                .GetMethod(nameof(RegisterAsyncHandlerGeneric), BindingFlags.Instance | BindingFlags.NonPublic)!
                .MakeGenericMethod(eventType);
            method.Invoke(this, new[] { handler });
        }

        private void RegisterAsyncHandlerGeneric<TEvent>(object handler) where TEvent : IEvent
        {
            var typedHandler = (IAsyncEventHandler<TEvent>)handler;
            Func<IEvent, Task> wrapper = e => typedHandler.HandleAsync((TEvent)e);
            var list = _asyncHandlers.GetOrAdd(typeof(TEvent), _ => new List<Func<IEvent, Task>>());
            lock (list)
            {
                list.Add(wrapper);
            }
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

        public void Publish<TEvent>(TEvent e) where TEvent : IEvent
        {
            if (ReferenceEquals(e, null))
                throw new ArgumentNullException(nameof(e));

            var eventType = e.GetType();
            if (_syncHandlers.TryGetValue(eventType, out var syncList))
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
                        Log.Error($"事件处理异常 [{eventType.Name}]: {ex.Message}");
                    }
                }
            }
        }

        public async Task PublishAsync<TEvent>(TEvent e) where TEvent : IEvent
        {
            if (ReferenceEquals(e, null))
                throw new ArgumentNullException(nameof(e));

            Publish(e);

            var eventType = e.GetType();
            if (!_asyncHandlers.TryGetValue(eventType, out var asyncList)) return;

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
                    Log.Error($"异步事件处理异常 [{eventType.Name}]: {ex.Message}");
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
