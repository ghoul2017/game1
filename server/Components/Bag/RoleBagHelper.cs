using System;
using System.Collections.Generic;
using Game.Components.Player;
using Shared.Common.Config;
using Shared.Common.Protos;
using Shared.Core.Config;

namespace Game.Components.Bag;

public static class RoleBagHelper
{
    internal static bool TryValidateAddItems(
        IReadOnlyList<BagItemAddData> items,
        out string errorMessage)
    {
        errorMessage = "";
        var totals = new Dictionary<(int ConfigId, bool Bind, long ExpireTime), long>();
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            if (item == null)
            {
                errorMessage = "道具数据错误";
                return false;
            }

            if (item.ConfigId <= 0)
            {
                errorMessage = "道具配置错误";
                return false;
            }

            if (item.Count <= 0)
            {
                errorMessage = "道具数量错误";
                return false;
            }

            var config = ConfigManager.Get<ItemConfig>(item.ConfigId);
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

            var key = (item.ConfigId, item.Bind, item.ExpireTime);
            try
            {
                totals[key] = checked(totals.GetValueOrDefault(key) + item.Count);
            }
            catch (OverflowException)
            {
                errorMessage = "道具数量溢出";
                return false;
            }
        }

        return true;
    }

    internal static void EnsureValidState(RoleBagComponent self)
    {
        if (self.Items == null)
            throw new InvalidOperationException($"背包 Items 为空 RoleId:{self.Id}");

        foreach (var (uid, item) in self.Items)
        {
            if (item == null)
                throw new InvalidOperationException($"背包存在空物品 RoleId:{self.Id} Uid:{uid}");
            if (uid <= 0 || item.Uid != uid)
                throw new InvalidOperationException(
                    $"背包 Uid 不一致 RoleId:{self.Id} Key:{uid} ItemUid:{item.Uid}");
            if (item.ConfigId <= 0)
                throw new InvalidOperationException(
                    $"背包物品配置错误 RoleId:{self.Id} Uid:{uid} ConfigId:{item.ConfigId}");

            var config = ConfigManager.Get<ItemConfig>(item.ConfigId);
            if (config == null)
                throw new InvalidOperationException(
                    $"背包物品配置不存在 RoleId:{self.Id} Uid:{uid} ConfigId:{item.ConfigId}");
            if (config.MaxStack <= 0)
                throw new InvalidOperationException(
                    $"背包物品堆叠上限错误 RoleId:{self.Id} Uid:{uid} ConfigId:{item.ConfigId}");
            if (item.Count <= 0 || item.Count > config.MaxStack)
                throw new InvalidOperationException(
                    $"背包物品数量违反堆叠上限 RoleId:{self.Id} Uid:{uid} Count:{item.Count} MaxStack:{config.MaxStack}");
        }
    }

    internal static BagItemInfo ToInfo(BagItem item) => new()
    {
        Uid = item.Uid,
        ConfigId = item.ConfigId,
        Count = item.Count,
        Bind = item.Bind,
        ExpireTime = item.ExpireTime,
    };

    internal static void NotifyChanged(
        RoleBagComponent self,
        Dictionary<long, BagItemInfo> items,
        List<long> removedUids)
    {
        if (items.Count == 0 && removedUids.Count == 0)
            return;
        if (self.Parent is not PlayerEntity player)
            throw new InvalidOperationException($"背包未挂载到玩家 RoleId:{self.Id}");

        player.GetComponent<PlayerOnlineComponent>().Session?.Send(new BagItemChangedNtf
        {
            Items = items,
            RemovedUids = removedUids,
        });
    }
}
