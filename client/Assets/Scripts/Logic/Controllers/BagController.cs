using System;
using System.Collections.Generic;
using Shared.Common.Protos;

namespace Client.Logic
{
    public class BagController
    {
        public static BagController Instance { get; } = new();

        public List<BagItemInfo> Items { get; private set; } = new();
        public Dictionary<int, BagItemInfo> ItemMap { get; private set; } = new();
        public int Capacity { get; private set; }

        public event Action OnDataChanged;

        public void QueryBag()
        {
            NetClient.Instance.Send(new QueryBagReq());
        }

        public void UseItem(int index, int count)
        {
            NetClient.Instance.Send(new BagUseItemReq { Index = index, Count = count });
        }

        public void TidyBag()
        {
            NetClient.Instance.Send(new BagTidyReq());
        }

        public void HandleQueryBagAck(QueryBagAck ack)
        {
            if (!string.IsNullOrEmpty(ack.ErrorMessage))
            {
                Context.UI?.ShowToast(ack.ErrorMessage);
                return;
            }
            Items = ack.Items ?? new List<BagItemInfo>();
            Capacity = ack.Capacity;
            RebuildMap();
        }

        public void HandleBagTidyAck(BagTidyAck ack)
        {
            if (!string.IsNullOrEmpty(ack.ErrorMessage))
            {
                Context.UI?.ShowToast(ack.ErrorMessage);
                return;
            }
            Items = ack.Items ?? new List<BagItemInfo>();
            RebuildMap();
            Context.UI?.ShowToast("整理完成");
        }

        public void HandleBagUseItemAck(BagUseItemAck ack)
        {
            if (!string.IsNullOrEmpty(ack.ErrorMessage))
            {
                Context.UI?.ShowToast(ack.ErrorMessage);
                return;
            }
            var cfg = Shared.Core.Config.ConfigManager.Get<Shared.Common.Config.ItemConfig>(ack.ConfigId);
            string itemName = cfg != null ? cfg.Name : "物品";
            Context.UI?.ShowToast("使用了 " + itemName);
        }

        public void HandleBagItemChangedNtf(BagItemChangedNtf ntf)
        {
            if (ntf.RemovedIndices != null)
            {
                foreach (int idx in ntf.RemovedIndices)
                {
                    ItemMap.Remove(idx);
                }
                Items.RemoveAll(it => ntf.RemovedIndices.Contains(it.Index));
            }
            if (ntf.ChangedItems != null)
            {
                foreach (var item in ntf.ChangedItems)
                {
                    ItemMap[item.Index] = item;
                    int existIdx = Items.FindIndex(it => it.Index == item.Index);
                    if (existIdx >= 0)
                        Items[existIdx] = item;
                    else
                        Items.Add(item);
                }
            }
            OnDataChanged?.Invoke();
        }

        private void RebuildMap()
        {
            ItemMap.Clear();
            foreach (var item in Items)
                ItemMap[item.Index] = item;
            OnDataChanged?.Invoke();
        }
    }
}
