using Shared.Core.Event;

namespace Game.Components.GameTask;

public sealed class PlayerExpChangedTaskProgressEventHandler : ISyncEventHandler<EventType.PlayerExpChangedEvent>
{
    public void Handle(EventType.PlayerExpChangedEvent e)
    {
        // 经验变化作为通用进度事件，供 "累计获得 N 经验" 类任务消费
    }
}
