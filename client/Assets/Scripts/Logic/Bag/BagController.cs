using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Shared.Common.Protos;
using Shared.Core.Event;

namespace Client.Logic
{
    public class BagController
    {
        public static BagController Instance { get; } = new();

        public List<BagItemInfo> Items { get; private set; } = new();
        public Dictionary<int, BagItemInfo> ItemMap { get; private set; } = new();
        public int Capacity { get; private set; }

        public async Task QueryBag()
        {
            try
            {
                QueryBagAck ack = await NetClient.Instance.CallAsync<QueryBagAck>(new QueryBagReq());

                if (!string.IsNullOrEmpty(ack.ErrorMessage))
                {
                    ClientUiEventPublisher.ShowToast(ack.ErrorMessage);
                    return;
                }
                Items = ack.Items ?? new List<BagItemInfo>();
                Capacity = ack.Capacity;
                RebuildMap();
            }
            catch (Exception ex)
            {
                ClientUiEventPublisher.ShowToast("查询背包失败: " + ex.Message);
            }
        }

        public async Task UseItem(int index, int count)
        {
            try
            {
                BagUseItemAck ack = await NetClient.Instance.CallAsync<BagUseItemAck>(
                    new BagUseItemReq { Index = index, Count = count });

                if (!string.IsNullOrEmpty(ack.ErrorMessage))
                {
                    ClientUiEventPublisher.ShowToast(ack.ErrorMessage);
                    return;
                }
                var cfg = Shared.Core.Config.ConfigManager.Get<Shared.Common.Config.ItemConfig>(ack.ConfigId);
                string itemName = cfg != null ? cfg.Name : "物品";
                ClientUiEventPublisher.ShowToast("使用了 " + itemName);
            }
            catch (Exception ex)
            {
                ClientUiEventPublisher.ShowToast("使用物品失败: " + ex.Message);
            }
        }

        public async Task TidyBag()
        {
            try
            {
                BagTidyAck ack = await NetClient.Instance.CallAsync<BagTidyAck>(new BagTidyReq());

                if (!string.IsNullOrEmpty(ack.ErrorMessage))
                {
                    ClientUiEventPublisher.ShowToast(ack.ErrorMessage);
                    return;
                }
                Items = ack.Items ?? new List<BagItemInfo>();
                RebuildMap();
                ClientUiEventPublisher.ShowToast("整理完成");
            }
            catch (Exception ex)
            {
                ClientUiEventPublisher.ShowToast("整理背包失败: " + ex.Message);
            }
        }

        private void RebuildMap()
        {
            ItemMap.Clear();
            foreach (var item in Items)
                ItemMap[item.Index] = item;
            PublishDataChanged();
        }

        private static void PublishDataChanged()
        {
            EventBus.Instance.Publish(new EventType.BagDataChangedEvent());
        }
    }

}
