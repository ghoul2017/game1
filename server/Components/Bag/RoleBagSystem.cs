using System;
using System.Collections.Generic;
using System.Linq;
using Game.Components.Player;
using Shared.Core;

namespace Game.Components.Bag;

/// <summary>
/// 背包系统。提供物品增删改查、整理、使用、容量检查等功能。
/// </summary>
public static class RoleBagSystem
{
    public const int DefaultMaxStack = 999;

    /// <summary>整批检测道具是否可以完整加入背包，不修改背包内容</summary>
    public static bool CanAddItems(this RoleBagComponent self,
        int[] configIds, int[] counts, out string errorMessage,
        int maxStack = DefaultMaxStack)
    {
        errorMessage = "";
        if (configIds.Length != counts.Length)
        {
            errorMessage = "道具配置和数量长度不一致";
            return false;
        }

        if (maxStack <= 0)
        {
            errorMessage = "堆叠上限错误";
            return false;
        }

        var addCounts = new Dictionary<int, long>();
        for (var index = 0; index < configIds.Length; index++)
        {
            var configId = configIds[index];
            var count = counts[index];
            if (count <= 0)
            {
                errorMessage = "道具数量错误";
                return false;
            }

            addCounts[configId] = addCounts.GetValueOrDefault(configId) + count;
        }

        var emptySlotCount = 0;
        for (var index = 0; index < self.Capacity; index++)
        {
            if (!self.Items.ContainsKey(index))
                emptySlotCount++;
        }

        long requiredSlotCount = 0;
        foreach (var (configId, addCount) in addCounts)
        {
            long stackSpace = 0;
            foreach (var item in self.Items.Values)
            {
                if (item.ConfigId == configId && item.Count < maxStack)
                    stackSpace += maxStack - item.Count;
            }

            var remaining = addCount - stackSpace;
            if (remaining <= 0)
                continue;

            requiredSlotCount += (remaining + maxStack - 1) / maxStack;
            if (requiredSlotCount > emptySlotCount)
            {
                errorMessage = "背包空间不足";
                return false;
            }
        }

        return true;
    }

    /// <summary>添加物品（自动找空格或叠加）</summary>
    public static int AddItem(this RoleBagComponent self, int configId, int count,
        int maxStack = DefaultMaxStack)
    {
        if (count <= 0 || maxStack <= 0)
            return 0;

        var remaining = count;
        var changed = false;

        // 先尝试叠加到已有同类物品
        foreach (var item in self.Items.Values)
        {
            if (item.ConfigId == configId && item.Count < maxStack)
            {
                var canAdd = Math.Min(remaining, maxStack - item.Count);
                item.Count += canAdd;
                remaining -= canAdd;
                changed = true;
                if (remaining <= 0)
                    break;
            }
        }

        // 剩余的放到新格子
        while (remaining > 0)
        {
            var emptyIndex = FindEmptyIndex(self);
            if (emptyIndex < 0)
            {
                if (changed)
                    self.MarkDirty();
                return count - remaining;
            }

            var addCount = Math.Min(remaining, maxStack);
            var newItem = new BagItem
            {
                Uid = IdGenerator.Instance.NextId(),
                ConfigId = configId,
                Count = addCount,
                Index = emptyIndex,
                Bind = false,
                ExpireTime = 0,
            };
            self.Items[emptyIndex] = newItem;
            remaining -= addCount;
            changed = true;
        }

        if (changed)
            self.MarkDirty();
        return count;
    }

    /// <summary>批量添加道具，不回退已经完成的添加</summary>
    /// <returns>成功返回空字符串，失败返回错误信息</returns>
    public static string AddItems(this RoleBagComponent self, int[] configIds, int[] counts)
    {
        if (configIds.Length != counts.Length)
            return "道具配置和数量长度不一致";

        for (var index = 0; index < configIds.Length; index++)
        {
            var addedCount = self.AddItem(configIds[index], counts[index]);
            if (addedCount != counts[index])
            {
                return
                    $"添加道具失败 ConfigId:{configIds[index]} Count:{counts[index]} AddedCount:{addedCount}";
            }
        }

        return "";
    }

    /// <summary>移除物品（按配置ID）</summary>
    public static bool RemoveItem(this RoleBagComponent self, int configId, int count)
    {
        if (!HasEnough(self, configId, count))
            return false;

        var remaining = count;
        var keysToRemove = new List<int>();

        foreach (var kvp in self.Items)
        {
            if (kvp.Value.ConfigId != configId)
                continue;

            if (kvp.Value.Count <= remaining)
            {
                remaining -= kvp.Value.Count;
                keysToRemove.Add(kvp.Key);
            }
            else
            {
                kvp.Value.Count -= remaining;
                remaining = 0;
            }

            if (remaining <= 0)
                break;
        }

        foreach (var key in keysToRemove)
        {
            self.Items.Remove(key);
        }

        self.MarkDirty();
        return true;
    }

    /// <summary>移除物品（按格子索引）</summary>
    public static bool RemoveByIndex(this RoleBagComponent self, int index, int count)
    {
        if (!self.Items.TryGetValue(index, out var item))
            return false;
        if (item.Count < count)
            return false;

        if (item.Count == count)
        {
            self.Items.Remove(index);
        }
        else
        {
            item.Count -= count;
        }

        self.MarkDirty();
        return true;
    }

    /// <summary>检查物品数量是否足够</summary>
    public static bool HasEnough(this RoleBagComponent self, int configId, int count)
    {
        var total = self.Items.Values.Where(i => i.ConfigId == configId).Sum(i => i.Count);
        return total >= count;
    }

    /// <summary>获取物品总数</summary>
    public static int GetItemCount(this RoleBagComponent self, int configId)
    {
        return self.Items.Values.Where(i => i.ConfigId == configId).Sum(i => i.Count);
    }

    /// <summary>检查是否有空格子</summary>
    public static bool HasSpace(this RoleBagComponent self, int need = 1)
    {
        return FindEmptyIndex(self) >= 0;
    }

    /// <summary>整理背包（按配置ID排序，紧凑排列）</summary>
    public static void Tidy(this RoleBagComponent self)
    {
        if (self.Items.Count == 0)
            return;

        var grouped = self.Items.Values
            .GroupBy(i => i.ConfigId)
            .OrderBy(g => g.Key)
            .ToList();

        self.Items.Clear();

        var index = 0;
        foreach (var group in grouped)
        {
            var totalCount = group.Sum(i => i.Count);
            self.Items[index] = new BagItem
            {
                Uid = IdGenerator.Instance.NextId(),
                ConfigId = group.Key,
                Count = totalCount,
                Index = index,
                Bind = group.Any(i => i.Bind),
            };
            index++;
        }

        self.MarkDirty();
    }

    /// <summary>交换两个格子的物品</summary>
    public static void Swap(this RoleBagComponent self, int index1, int index2)
    {
        if (index1 == index2)
            return;

        self.Items.Remove(index1, out var item1);
        self.Items.Remove(index2, out var item2);

        if (item1 != null)
        {
            item1.Index = index2;
            self.Items[index2] = item1;
        }

        if (item2 != null)
        {
            item2.Index = index1;
            self.Items[index1] = item2;
        }

        if (item1 != null || item2 != null)
            self.MarkDirty();
    }

    private static int FindEmptyIndex(RoleBagComponent self)
    {
        for (int i = 0; i < self.Capacity; i++)
        {
            if (!self.Items.ContainsKey(i))
                return i;
        }

        return -1;
    }
}
