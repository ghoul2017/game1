using System;

namespace Shared.Core.Event
{
    [AttributeUsage(AttributeTargets.Method)]
    [Obsolete("事件订阅已迁移到 ISyncEventHandler<TEvent>/IAsyncEventHandler<TEvent>。", true)]
    public class EventSubscriberAttribute : Attribute
    {
    }
}
