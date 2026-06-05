using System.Threading.Tasks;
using Shared.Common.Protos;
using Shared.Core.Event;
using Shared.Core.Network;

namespace Client.Logic
{
    public sealed class BagItemChangedNtfHandler : NtfMessageHandler<BagItemChangedNtf>
    {
        protected override Task ExecuteAsync(TcpSession session, BagItemChangedNtf ntf)
        {
            var controller = BagController.Instance;
            if (ntf.RemovedIndices != null)
            {
                foreach (int idx in ntf.RemovedIndices)
                {
                    controller.ItemMap.Remove(idx);
                }
                controller.Items.RemoveAll(it => ntf.RemovedIndices.Contains(it.Index));
            }

            if (ntf.ChangedItems != null)
            {
                foreach (var item in ntf.ChangedItems)
                {
                    controller.ItemMap[item.Index] = item;
                    int existIdx = controller.Items.FindIndex(it => it.Index == item.Index);
                    if (existIdx >= 0)
                        controller.Items[existIdx] = item;
                    else
                        controller.Items.Add(item);
                }
            }

            EventBus.Instance.Publish(new EventType.BagDataChangedEvent());
            return Task.CompletedTask;
        }
    }
}
