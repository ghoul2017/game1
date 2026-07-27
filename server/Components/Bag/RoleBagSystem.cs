using System;
using System.Collections.Generic;
using System.Linq;
using Game.Components.Player;
using Shared.Common.Config;
using Shared.Common.Protos;
using Shared.Core;
using Shared.Core.Config;

namespace Game.Components.Bag;

/// <summary>
/// 背包组件的 UID 实例栈原子能力。容量准入由调用业务决定。
/// </summary>
public static class RoleBagSystem
{
    public static bool IsFull(this RoleBagComponent self)
    {
        return self.Items.Count > self.Capacity;
    }

    /// <summary>将 BagItemAddDataGenerator 产生的 normalized 道具填栈入包。</summary>
    public static List<BagItemAddData> AddItems(this RoleBagComponent self, IReadOnlyList<BagItemAddData> normalizedItems)
    {
        if (normalizedItems.Count == 0)
            return [];

        Dictionary<long, long> finalCounts = [];
        List<BagItem> createdItems = [];
        HashSet<long> plannedUids = [];

        foreach (var addData in normalizedItems)
        {
            ItemConfig config = ConfigManager.Get<ItemConfig>(addData.ConfigId)!;
            long remaining = addData.Count;
            IOrderedEnumerable<BagItem> matchingItems = self.Items.Values
                .Where(item => item.ConfigId == addData.ConfigId
                               && item.Bind == addData.Bind
                               && item.ExpireTime == addData.ExpireTime
                               && item.Count < config.MaxStack)
                .OrderBy(item => item.Uid);

            foreach (var item in matchingItems)
            {
                long currentCount = finalCounts.GetValueOrDefault(item.Uid, item.Count);
                long addedCount = Math.Min(remaining, config.MaxStack - currentCount);
                if (addedCount <= 0)
                    continue;

                finalCounts[item.Uid] = currentCount + addedCount;
                remaining -= addedCount;
                if (remaining == 0)
                    break;
            }

            while (remaining > 0)
            {
                long uid = IdGenerator.Instance.NextId();
                if (uid <= 0 || self.Items.ContainsKey(uid) || !plannedUids.Add(uid))
                    throw new InvalidOperationException($"生成了非法或重复的背包 Uid:{uid}");

                long stackCount = Math.Min(remaining, config.MaxStack);
                createdItems.Add(new BagItem
                {
                    Uid = uid,
                    ConfigId = addData.ConfigId,
                    Count = stackCount,
                    Bind = addData.Bind,
                    ExpireTime = addData.ExpireTime,
                });
                remaining -= stackCount;
            }
        }

        Dictionary<long, BagItemInfo> changedItems = [];
        foreach (var (uid, count) in finalCounts.OrderBy(pair => pair.Key))
        {
            BagItem item = self.Items[uid];
            item.Count = count;
            changedItems[uid] = RoleBagHelper.ToInfo(item);
        }

        foreach (var item in createdItems)
        {
            self.Items.Add(item.Uid, item);
            changedItems[item.Uid] = RoleBagHelper.ToInfo(item);
        }

        self.MarkDirty();
        RoleBagHelper.NotifyChanged(self, changedItems, []);
        return normalizedItems.ToList();
    }

    public static bool RemoveItemsByUid(this RoleBagComponent self, IReadOnlyDictionary<long, long> uidCounts)
    {
        if (uidCounts == null)
            return false;

        if (uidCounts.Count == 0)
            return true;

        foreach (var (uid, count) in uidCounts)
        {
            if (uid <= 0 || count <= 0
                         || !self.Items.TryGetValue(uid, out BagItem? item)
                         || item.Count < count)
            {
                return false;
            }
        }

        Dictionary<long, BagItemInfo> changedItems = [];
        List<long> removedUids = [];
        foreach (var (uid, count) in uidCounts.OrderBy(pair => pair.Key))
        {
            BagItem item = self.Items[uid];
            if (item.Count == count)
            {
                self.Items.Remove(uid);
                removedUids.Add(uid);
                continue;
            }

            item.Count -= count;
            changedItems[uid] = RoleBagHelper.ToInfo(item);
        }

        self.MarkDirty();
        RoleBagHelper.NotifyChanged(self, changedItems, removedUids);
        return true;
    }

    public static bool RemoveItemsByConfigId(this RoleBagComponent self, int configId, long count)
    {
        if (configId <= 0 || count <= 0)
            return false;

        List<BagItem> matchingItems = self.Items.Values
            .Where(item => item.ConfigId == configId)
            .OrderBy(item => item.Uid)
            .ToList();

        long total = 0;
        try
        {
            foreach (var item in matchingItems)
                total = checked(total + item.Count);
        }
        catch (OverflowException ex)
        {
            throw new InvalidOperationException($"背包道具总数溢出 ConfigId:{configId}", ex);
        }

        if (total < count)
            return false;

        long remaining = count;
        Dictionary<long, long> uidCounts = [];
        foreach (var item in matchingItems)
        {
            long removeCount = Math.Min(remaining, item.Count);
            uidCounts[item.Uid] = removeCount;
            remaining -= removeCount;
            if (remaining == 0)
                break;
        }

        return self.RemoveItemsByUid(uidCounts);
    }

    public static BagItem? GetItem(this RoleBagComponent self, long uid)
    {
        return self.Items.TryGetValue(uid, out BagItem? item) ? item : null;
    }

    public static long GetItemCount(this RoleBagComponent self, int configId)
    {
        long total = 0;
        try
        {
            foreach (var item in self.Items.Values.Where(item => item.ConfigId == configId))
                total = checked(total + item.Count);
        }
        catch (OverflowException ex)
        {
            throw new InvalidOperationException($"背包道具总数溢出 ConfigId:{configId}", ex);
        }

        return total;
    }
}
