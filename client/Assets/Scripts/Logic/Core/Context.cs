using Shared.Core.Entities;
using Shared.Core.Event;
using Shared.Core.Network;

namespace Client.Logic
{
    public static class Context
    {
        public static Module Client = new();
        public static NetClient NetClient = new();
        public static NetworkComponent Network => Client.GetComponent<NetworkComponent>();

        private static bool _eventHandlersRegistered;

        public static void Start()
        {
            if (!_eventHandlersRegistered)
            {
                EventBus.Instance.AutoRegister();
                _eventHandlersRegistered = true;
            }

            NetClient.Initialize();
        }

        public static void Update(float deltaTime)
        {
            NetClient.Update(deltaTime);
        }

        public static void OnDestroy()
        {
            NetClient.Dispose();
        }
    }

    public static class ClientUiEventPublisher
    {
        public static void ShowToast(string message)
        {
            if (string.IsNullOrEmpty(message))
                return;
            EventBus.Instance.Publish(new EventType.ClientToastRequestedEvent { Message = message });
        }
    }
}
