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

    public static BagItemAddData[] AddItems(
        this RoleBagComponent self,
        IReadOnlyList<BagItemAddData> items)
    {
        if (items == null)
            throw new ArgumentNullException(nameof(items));
        if (items.Count == 0)
            return Array.Empty<BagItemAddData>();
        if (!RoleBagHelper.TryValidateAddItems(items, out var errorMessage))
            throw new ArgumentException(errorMessage, nameof(items));

        RoleBagHelper.EnsureValidState(self);
        var normalized = Normalize(items);
        var finalCounts = new Dictionary<long, long>();
        var createdItems = new List<BagItem>();
        var plannedUids = new HashSet<long>();

        foreach (var addData in normalized)
        {
            var config = ConfigManager.Get<ItemConfig>(addData.ConfigId)!;
            var remaining = addData.Count;
            var matchingItems = self.Items.Values
                .Where(item => item.ConfigId == addData.ConfigId
                               && item.Bind == addData.Bind
                               && item.ExpireTime == addData.ExpireTime
                               && item.Count < config.MaxStack)
                .OrderBy(item => item.Uid);

            foreach (var item in matchingItems)
            {
                var currentCount = finalCounts.GetValueOrDefault(item.Uid, item.Count);
                var addedCount = Math.Min(remaining, config.MaxStack - currentCount);
                if (addedCount <= 0)
                    continue;

                finalCounts[item.Uid] = currentCount + addedCount;
                remaining -= addedCount;
                if (remaining == 0)
                    break;
            }

            while (remaining > 0)
            {
                var uid = IdGenerator.Instance.NextId();
                if (uid <= 0 || self.Items.ContainsKey(uid) || !plannedUids.Add(uid))
                    throw new InvalidOperationException($"生成了非法或重复的背包 Uid:{uid}");

                var stackCount = Math.Min(remaining, config.MaxStack);
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

        var changedItems = new Dictionary<long, BagItemInfo>();
        foreach (var (uid, count) in finalCounts.OrderBy(pair => pair.Key))
        {
            var item = self.Items[uid];
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
        return normalized;
    }

    public static bool RemoveItemsByUid(
        this RoleBagComponent self,
        IReadOnlyDictionary<long, long> uidCounts)
    {
        if (uidCounts == null)
            return false;

        RoleBagHelper.EnsureValidState(self);
        if (uidCounts.Count == 0)
            return true;

        foreach (var (uid, count) in uidCounts)
        {
            if (uid <= 0 || count <= 0
                         || !self.Items.TryGetValue(uid, out var item)
                         || item.Count < count)
            {
                return false;
            }
        }

        var changedItems = new Dictionary<long, BagItemInfo>();
        var removedUids = new List<long>();
        foreach (var (uid, count) in uidCounts.OrderBy(pair => pair.Key))
        {
            var item = self.Items[uid];
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

        RoleBagHelper.EnsureValidState(self);
        var matchingItems = self.Items.Values
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

        var remaining = count;
        var uidCounts = new Dictionary<long, long>();
        foreach (var item in matchingItems)
        {
            var removeCount = Math.Min(remaining, item.Count);
            uidCounts[item.Uid] = removeCount;
            remaining -= removeCount;
            if (remaining == 0)
                break;
        }

        return self.RemoveItemsByUid(uidCounts);
    }

    public static BagItem? GetItem(this RoleBagComponent self, long uid)
    {
        RoleBagHelper.EnsureValidState(self);
        return self.Items.TryGetValue(uid, out var item) ? item : null;
    }

    public static long GetItemCount(this RoleBagComponent self, int configId)
    {
        RoleBagHelper.EnsureValidState(self);

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

    private static BagItemAddData[] Normalize(IReadOnlyList<BagItemAddData> items)
    {
        var normalized = new List<BagItemAddData>();
        var groupIndices = new Dictionary<(int ConfigId, bool Bind, long ExpireTime), int>();

        foreach (var item in items)
        {
            var key = (item.ConfigId, item.Bind, item.ExpireTime);
            if (!groupIndices.TryGetValue(key, out var groupIndex))
            {
                groupIndices[key] = normalized.Count;
                normalized.Add(new BagItemAddData
                {
                    ConfigId = item.ConfigId,
                    Count = item.Count,
                    Bind = item.Bind,
                    ExpireTime = item.ExpireTime,
                });
                continue;
            }

            try
            {
                normalized[groupIndex].Count = checked(normalized[groupIndex].Count + item.Count);
            }
            catch (OverflowException ex)
            {
                throw new InvalidOperationException(
                    $"批量添加道具数量溢出 ConfigId:{item.ConfigId}", ex);
            }
        }

        return normalized.ToArray();
    }
}
