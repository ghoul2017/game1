using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Shared.Common.Config;
using Shared.Common.Protos;
using Shared.Core.Config;
using Shared.Core.Event;

namespace Client.Logic
{
    public class BagController
    {
        public static BagController Instance { get; } = new BagController();

        public Dictionary<long, BagItemInfo> Items { get; private set; } = new Dictionary<long, BagItemInfo>();
        public int Capacity { get; private set; }
        public bool IsLoaded { get; private set; }
        public long RoleId { get; private set; }

        private int _roleGeneration;
        private bool _queryInProgress;

        public async Task QueryBag()
        {
            NetClient netClient = NetClient.Instance;
            if (netClient == null)
                return;

            long expectedRoleId = GameData.CurrentRole != null ? GameData.CurrentRole.RoleId : 0;
            if (expectedRoleId <= 0 || RoleId != expectedRoleId)
            {
                ClientUiEventPublisher.ShowToast("尚未进入角色");
                return;
            }
            if (_queryInProgress)
                return;

            int expectedGeneration = _roleGeneration;
            _queryInProgress = true;

            try
            {
                QueryBagAck ack = await netClient.CallAsync<QueryBagAck>(new QueryBagReq());
                if (!string.IsNullOrEmpty(ack.ErrorMessage))
                {
                    ClientUiEventPublisher.ShowToast(ack.ErrorMessage);
                    return;
                }

                if (_roleGeneration != expectedGeneration)
                {
                    return;
                }

                if (RoleId != expectedRoleId)
                {
                    return;
                }

                if (GameData.CurrentRole == null)
                {
                    return;
                }

                if (GameData.CurrentRole.RoleId != expectedRoleId)
                {
                    return;
                }

                IReadOnlyList<BagItemInfo> source = ack.Items ?? new List<BagItemInfo>();
                Dictionary<long, BagItemInfo> snapshot = new Dictionary<long, BagItemInfo>();
                foreach (var item in source)
                    snapshot.Add(item.Uid, item);
                Items = snapshot;
                Capacity = ack.Capacity;
                IsLoaded = true;
                EventBus.Instance.Publish(new EventType.BagDataChangedEvent());
            }
            catch (Exception ex)
            {
                ClientUiEventPublisher.ShowToast("查询背包失败: " + ex.Message);
            }
            finally
            {
                if (_roleGeneration == expectedGeneration)
                    _queryInProgress = false;
            }
        }


        public BagItemInfo GetItem(long uid)
        {
            Items.TryGetValue(uid, out BagItemInfo item);
            return item;
        }

        public async Task UseItem(long uid)
        {
            NetClient netClient = NetClient.Instance;
            if (netClient == null
                || RoleId <= 0
                || GameData.CurrentRole == null
                || GameData.CurrentRole.RoleId != RoleId
                || !netClient.IsConnected)
            {
                ClientUiEventPublisher.ShowToast("当前无法操作背包");
                return;
            }

            BagItemInfo item = GetItem(uid);
            if (item == null)
            {
                ClientUiEventPublisher.ShowToast("物品不存在");
                return;
            }

            try
            {
                BagUseItemAck ack = await netClient.CallAsync<BagUseItemAck>(
                    new BagUseItemReq { Uid = uid });

                if (!string.IsNullOrEmpty(ack.Error))
                {
                    ClientUiEventPublisher.ShowToast(ack.Error);
                    return;
                }

                ItemConfig config = ConfigManager.Get<ItemConfig>(item.ConfigId);
                string itemName = config != null ? config.Name : "物品";
                ClientUiEventPublisher.ShowToast("使用了 " + itemName);
            }
            catch (Exception ex)
            {
                ClientUiEventPublisher.ShowToast("使用物品失败: " + ex.Message);
            }
        }

        public void ResetForRole(long roleId)
        {
            long nextRoleId = roleId > 0 ? roleId : 0;
            bool changed = RoleId != nextRoleId || Items.Count > 0 || Capacity != 0 || IsLoaded;
            RoleId = nextRoleId;
            Items = new Dictionary<long, BagItemInfo>();
            Capacity = 0;
            IsLoaded = false;
            _roleGeneration++;
            _queryInProgress = false;
            if (changed)
                EventBus.Instance.Publish(new EventType.BagDataChangedEvent());
        }
    }
}
