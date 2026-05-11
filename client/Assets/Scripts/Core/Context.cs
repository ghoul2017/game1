using Shared.Core.Entities;
using Shared.Core.Network;

namespace Client
{
    public static class Context
    {
        public static Module Client = new();
        public static NetworkComponent Network => Client.GetComponent<NetworkComponent>();
    }
}
