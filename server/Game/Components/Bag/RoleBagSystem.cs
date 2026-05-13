using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Game.Database;
using Shared.Core;
using Shared.Core.Log;

namespace Game.Components.Bag;

/// <summary>
/// 背包系统。提供物品增删改查、整理、使用、容量检查等功能。
/// </summary>
public static class RoleBagSystem
{
    private static readonly ILog Log = LogManager.GetLogger(nameof(RoleBagSystem));

    #region 物品效果类型

    public const int EffectHp = 1;
    public const int EffectMp = 2;
    public const int EffectBuff = 3;
    public const int EffectTeleport = 4;
    public const int EffectRevive = 5;

    #endregion

    /// <summary>保存背包</summary>
    public static async Task SaveBagAsync(this RoleBagComponent self)
    {
        await MongoDb.Instance.UpdateAsync(self);
    }

    /// <summary>添加物品（自动找空格或叠加）</summary>
    public static AddItemResult AddItem(this RoleBagComponent self, int configId, int count, int maxStack = 999)
    {
        if (count <= 0)
            return AddItemResult.Fail("参数错误");

        var remaining = count;

        // 先尝试叠加到已有同类物品
        foreach (var item in self.Items)
        {
            if (item.ConfigId == configId && item.Count < maxStack)
            {
                var canAdd = Math.Min(remaining, maxStack - item.Count);
                item.Count += canAdd;
                remaining -= canAdd;
                if (remaining <= 0) break;
            }
        }

        // 剩余的放到新格子
        while (remaining > 0)
        {
            var emptyIndex = FindEmptyIndex(self);
            if (emptyIndex < 0)
                return AddItemResult.Partial(count - remaining, "背包已满");

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
            self.Items.Add(newItem);
            self.GetIndexMap()[emptyIndex] = newItem;
            remaining -= addCount;
        }

        return AddItemResult.Ok(count);
    }

    /// <summary>移除物品（按配置ID）</summary>
    public static bool RemoveItem(this RoleBagComponent self, int configId, int count)
    {
        if (!HasEnough(self, configId, count))
            return false;

        var remaining = count;
        for (int i = self.Items.Count - 1; i >= 0; i--)
        {
            var item = self.Items[i];
            if (item.ConfigId != configId) continue;

            if (item.Count <= remaining)
            {
                remaining -= item.Count;
                self.RemoveFromIndex(item);
                self.Items.RemoveAt(i);
            }
            else
            {
                item.Count -= remaining;
                remaining = 0;
            }

            if (remaining <= 0) break;
        }

        return true;
    }

    /// <summary>移除物品（按格子索引）</summary>
    public static bool RemoveByIndex(this RoleBagComponent self, int index, int count)
    {
        var item = self.GetByIndex(index);
        if (item == null) return false;
        if (item.Count < count) return false;

        if (item.Count == count)
        {
            self.RemoveFromIndex(item);
            self.Items.Remove(item);
        }
        else
        {
            item.Count -= count;
        }

        return true;
    }

    /// <summary>检查物品数量是否足够</summary>
    public static bool HasEnough(this RoleBagComponent self, int configId, int count)
    {
        var total = self.Items.Where(i => i.ConfigId == configId).Sum(i => i.Count);
        return total >= count;
    }

    /// <summary>获取物品总数</summary>
    public static int GetItemCount(this RoleBagComponent self, int configId)
    {
        return self.Items.Where(i => i.ConfigId == configId).Sum(i => i.Count);
    }

    /// <summary>检查是否有空格子</summary>
    public static bool HasSpace(this RoleBagComponent self, int need = 1)
    {
        return FindEmptyIndex(self) >= 0;
    }

    /// <summary>整理背包（按配置ID排序，紧凑排列）</summary>
    public static void Tidy(this RoleBagComponent self)
    {
        var grouped = self.Items
            .GroupBy(i => i.ConfigId)
            .OrderBy(g => g.Key)
            .ToList();

        self.Items.Clear();
        self.GetIndexMap().Clear();

        var index = 0;
        foreach (var group in grouped)
        {
            var totalCount = group.Sum(i => i.Count);
            self.Items.Add(new BagItem
            {
                Uid = IdGenerator.Instance.NextId(),
                ConfigId = group.Key,
                Count = totalCount,
                Index = index,
                Bind = group.Any(i => i.Bind),
            });
            self.GetIndexMap()[index] = self.Items[^1];
            index++;
        }
    }

    /// <summary>交换两个格子的物品</summary>
    public static bool Swap(this RoleBagComponent self, int index1, int index2)
    {
        if (index1 == index2) return true;

        var item1 = self.GetByIndex(index1);
        var item2 = self.GetByIndex(index2);

        var map = self.GetIndexMap();
        map.Remove(index1);
        map.Remove(index2);

        if (item1 != null) { item1.Index = index2; map[index2] = item1; }
        if (item2 != null) { item2.Index = index1; map[index1] = item2; }

        return true;
    }

    /// <summary>使用物品</summary>
    public static UseItemResult UseItem(this RoleBagComponent self, int index, int count)
    {
        if (count <= 0)
            return UseItemResult.Fail("参数错误");

        var item = self.GetByIndex(index);
        if (item == null)
            return UseItemResult.Fail("物品不存在");

        if (item.Count < count)
            return UseItemResult.Fail("物品数量不足");

        // 扣除物品
        if (item.Count == count)
        {
            self.RemoveFromIndex(item);
            self.Items.Remove(item);
        }
        else
        {
            item.Count -= count;
        }

        return UseItemResult.Ok(item.ConfigId, count);
    }

    /// <summary>获取背包信息（所有物品快照）</summary>
    public static List<BagItem> GetItems(this RoleBagComponent self)
    {
        return self.Items.ToList();
    }

    private static int FindEmptyIndex(RoleBagComponent self)
    {
        var map = self.GetIndexMap();
        for (int i = 0; i < self.Capacity; i++)
        {
            if (!map.ContainsKey(i))
                return i;
        }
        return -1;
    }
}

/// <summary>添加物品结果</summary>
public record AddItemResult(int AddedCount, bool Success, string ErrorMessage = "")
{
    public static AddItemResult Ok(int count) => new(count, true);
    public static AddItemResult Partial(int count, string message) => new(count, false, message);
    public static AddItemResult Fail(string message) => new(0, false, message);
}

/// <summary>使用物品结果</summary>
public record UseItemResult(int ConfigId, int UsedCount, bool Success, string ErrorMessage = "")
{
    public static UseItemResult Ok(int configId, int count) => new(configId, count, true);
    public static UseItemResult Fail(string message) => new(0, 0, false, message);
}
