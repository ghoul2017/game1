using System.Threading.Tasks;

namespace Game.Core.Event;

/// <summary>
/// 事件监听器。处理指定类型的事件。
/// </summary>
public interface IEventListener<in TEvent> where TEvent : IEvent
{
    Task HandleAsync(TEvent e);
}
