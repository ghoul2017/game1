using System;

namespace Shared.Core.Event
{
    [AttributeUsage(AttributeTargets.Method)]
    public class EventSubscriberAttribute : Attribute { }
}
