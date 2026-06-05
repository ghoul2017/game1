using System;
using Client.Logic;
using Shared.Core.Event;

namespace Robot.Handlers
{
    /// <summary>
    /// Robot 的客户端 UI 事件处理器。客户端表现事件转为控制台输出。
    /// </summary>
    public class RobotClientUIEventHandler : ISyncEventHandler<EventType.ClientToastRequestedEvent>
    {
        public void Handle(EventType.ClientToastRequestedEvent e)
        {
            if (string.IsNullOrEmpty(e.Message)) return;
            Console.WriteLine($"[UI] Toast: {e.Message}");
        }
    }
}
