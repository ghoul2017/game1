using Shared.Core.Network;
using Shared.Common.Protos;

namespace Client.Logic
{
    public static class BagHandlers
    {
        [MessageHandler(typeof(NtfMethodHandler))]
        public static void HandleBagItemChangedNtf(TcpSession session, BagItemChangedNtf ntf)
        {
            BagController.Instance.HandleBagItemChangedNtf(ntf);
        }
    }
}
