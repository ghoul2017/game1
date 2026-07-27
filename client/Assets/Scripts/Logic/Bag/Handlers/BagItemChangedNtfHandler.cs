using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Shared.Common.Protos;
using Shared.Core.Event;
using Shared.Core.Network;

namespace Client.Logic
{
    public sealed class BagItemChangedNtfHandler : NtfMessageHandler<BagItemChangedNtf>
    {
        protected override async Task ExecuteAsync(TcpSession session, BagItemChangedNtf ntf)
        {
            BagController controller = BagController.Instance;
            if (controller.RoleId <= 0)
            {
                await Task.CompletedTask;
                return;
            }

            if (ntf.Items != null && ntf.Items.Count != 0)
            {
                foreach (var item in ntf.Items)
                    controller.Items[item.Uid] = item;
            }
            if (ntf.RemovedUids != null)
            {
                foreach (long uid in ntf.RemovedUids)
                    controller.Items.Remove(uid);
            }
            EventBus.Instance.Publish(new EventType.BagDataChangedEvent());
            await Task.CompletedTask;
        }
    }
}
