using System;

namespace Game.Core.Event;

/// <summary>
/// 事件标记接口。所有事件实现此接口。
/// </summary>
public interface IEvent
{
    /// <summary>事件发生时间</summary>
    DateTime Timestamp { get; }
}
