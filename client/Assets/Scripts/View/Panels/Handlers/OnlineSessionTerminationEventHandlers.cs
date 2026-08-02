using Client.Logic;
using Client.View.OnlineTechnical;
using Shared.Core.Event;
using EventType = Shared.Core.Event.EventType;

namespace Client.View
{
    public sealed class OnlineReconnectFailedEventHandler : ISyncEventHandler<EventType.NetReconnectFailedEvent>
    {
        public void Handle(EventType.NetReconnectFailedEvent value)
        {
            OnlineTechnicalEntry.TryReturnToLogin($"重连失败：{value.Message}");
        }
    }

    public sealed class OnlineKickedEventHandler : ISyncEventHandler<EventType.NetKickedEvent>
    {
        public void Handle(EventType.NetKickedEvent value)
        {
            OnlineTechnicalEntry.TryReturnToLogin(MapController.GetKickReasonText(value.Reason));
        }
    }

    public sealed class OnlineDisconnectedEventHandler : ISyncEventHandler<EventType.NetDisconnectedEvent>
    {
        public void Handle(EventType.NetDisconnectedEvent value)
        {
            OnlineTechnicalEntry.TryReturnToLogin("网络连接已断开，请重新登录");
        }
    }

    public sealed class OnlineNetworkErrorEventHandler : ISyncEventHandler<EventType.NetErrorEvent>
    {
        public void Handle(EventType.NetErrorEvent value)
        {
            OnlineTechnicalEntry.TryReturnToLogin(LoginController.GetConnectionErrorText(value.Reason));
        }
    }
}
