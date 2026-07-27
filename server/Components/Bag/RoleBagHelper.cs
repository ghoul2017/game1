using System.Collections.Generic;
using Game.Components.Player;
using Shared.Common.Config;
using Shared.Common.Protos;
using Shared.Core.Config;

namespace Game.Components.Bag;

public static class RoleBagHelper
{
    internal static bool TryValidateNormalizedItems(IReadOnlyList<BagItemAddData> normalizedItems, out string errorMessage)
    {
        errorMessage = "";
        HashSet<(int ConfigId, bool Bind, long ExpireTime)> keys = [];
        foreach (var item in normalizedItems)
        {
            if (item == null || item.ConfigId <= 0)
            {
                errorMessage = "道具配置错误";
                return false;
            }

            if (item.Count <= 0)
            {
                errorMessage = "道具数量错误";
                return false;
            }

            ItemConfig? config = ConfigManager.Get<ItemConfig>(item.ConfigId);
            if (config == null)
            {
                errorMessage = "道具配置不存在";
                return false;
            }

            if (config.MaxStack <= 0)
            {
                errorMessage = "道具堆叠上限错误";
                return false;
            }

            if (!keys.Add((item.ConfigId, item.Bind, item.ExpireTime)))
            {
                errorMessage = "待入包道具未归一化";
                return false;
            }
        }

        return true;
    }

    internal static BagItemInfo ToInfo(BagItem item) => new()
    {
        Uid = item.Uid,
        ConfigId = item.ConfigId,
        Count = item.Count,
        Bind = item.Bind,
        ExpireTime = item.ExpireTime,
    };

    internal static void NotifyChanged(RoleBagComponent self, Dictionary<long, BagItemInfo> items, List<long> removedUids)
    {
        if (items.Count == 0 && removedUids.Count == 0)
            return;
        if (self.Parent is not PlayerEntity player)
            throw new InvalidOperationException($"背包未挂载到玩家 RoleId:{self.Id}");

        player.GetComponent<PlayerOnlineComponent>().Session?.Send(new BagItemChangedNtf
        {
            Items = new List<BagItemInfo>(items.Values),
            RemovedUids = removedUids,
        });
    }
}
