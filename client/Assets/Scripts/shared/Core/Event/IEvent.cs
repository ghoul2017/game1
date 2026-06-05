using System.Threading.Tasks;

namespace Shared.Core.Event
{
    public interface IEvent
    {
    }

    public interface ISyncEventHandler<TEvent> where TEvent : IEvent
    {
        void Handle(TEvent e);
    }

    public interface IAsyncEventHandler<TEvent> where TEvent : IEvent
    {
        Task HandleAsync(TEvent e);
    }
}
