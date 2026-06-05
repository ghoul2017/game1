using Shared.Core.Event;

namespace Game.Components.GameTask;

public sealed class PlayerGoldChangedTaskProgressEventHandler : ISyncEventHandler<EventType.PlayerGoldChangedEvent>
{
    public void Handle(EventType.PlayerGoldChangedEvent e)
    {
        // 金币变化供 "累计获得 N 金币" 类任务消费
    }
}
