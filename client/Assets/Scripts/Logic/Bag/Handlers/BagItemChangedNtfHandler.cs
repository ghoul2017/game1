using System;
using System.Threading.Tasks;
using Shared.Common.Protos;
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

            if (ntf.Items != null && ntf.RemovedUids != null)
            {
                foreach (long uid in ntf.RemovedUids)
                {
                    if (ntf.Items.ContainsKey(uid))
                        throw new InvalidOperationException("背包变化中同一 Uid 同时被更新和删除: " + uid);
                }
            }

            controller.ApplyChangedItems(ntf.Items);
            controller.RemoveUids(ntf.RemovedUids);
            BagController.PublishDataChanged();
            await Task.CompletedTask;
        }
    }
}
