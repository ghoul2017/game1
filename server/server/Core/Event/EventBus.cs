using System.Collections.Concurrent;
using Server.Core.Log;

namespace Server.Core.Event;

/// <summary>
/// 事件总线。类型安全的发布-订阅机制，支持跨模块解耦。
/// <para>用法：</para>
/// <para>  EventBus.Instance.Subscribe&lt;MyEvent&gt;(e => { ... });</para>
/// <para>  EventBus.Instance.Publish(new MyEvent());</para>
/// </summary>
public class EventBus
{
    private static readonly ILog Log = LogManager.GetLogger<EventBus>();

    public static EventBus Instance { get; } = new();

    /// <summary>事件类型 → 处理器列表</summary>
    private readonly ConcurrentDictionary<Type, List<Delegate>> _handlers = new();

    private EventBus() { }

    /// <summary>订阅事件（委托方式）</summary>
    public void Subscribe<TEvent>(Func<TEvent, Task> handler) where TEvent : IEvent
    {
        var type = typeof(TEvent);
        var list = _handlers.GetOrAdd(type, _ => []);
        lock (list)
        {
            list.Add(handler);
        }
    }

    /// <summary>订阅事件（同步方式）</summary>
    public void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : IEvent
    {
        Subscribe<TEvent>(e =>
        {
            handler(e);
            return Task.CompletedTask;
        });
    }

    /// <summary>订阅事件（IEventListener 实例方式）</summary>
    public void Subscribe<TEvent>(IEventListener<TEvent> listener) where TEvent : IEvent
    {
        Subscribe<TEvent>(listener.HandleAsync);
    }

    /// <summary>取消订阅</summary>
    public void Unsubscribe<TEvent>(Func<TEvent, Task> handler) where TEvent : IEvent
    {
        var type = typeof(TEvent);
        if (_handlers.TryGetValue(type, out var list))
        {
            lock (list)
            {
                list.Remove(handler);
            }
        }
    }

    /// <summary>发布事件，同步调用所有订阅者</summary>
    public async Task PublishAsync<TEvent>(TEvent e) where TEvent : IEvent
    {
        if (!_handlers.TryGetValue(typeof(TEvent), out var list)) return;

        List<Delegate> snapshot;
        lock (list)
        {
            snapshot = [.. list];
        }

        foreach (var del in snapshot)
        {
            try
            {
                if (del is Func<TEvent, Task> asyncHandler)
                    await asyncHandler(e);
            }
            catch (Exception ex)
            {
                Log.Error($"事件处理异常 [{typeof(TEvent).Name}]: {ex.Message}");
            }
        }
    }

    /// <summary>清空所有订阅</summary>
    public void Clear()
    {
        _handlers.Clear();
    }
}
