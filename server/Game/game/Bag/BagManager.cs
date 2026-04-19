using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Game.Core.Entity;
using Shared.Core.Log;

namespace Game.Game.Bag;

/// <summary>
/// 背包管理器。提供物品增删改查、整理、使用、容量检查等功能。
/// 参考: Java 项目的 BagManager。
/// </summary>
public class BagManager
{
    private static readonly ILog Log = LogManager.GetLogger<BagManager>();

    public static BagManager Instance { get; } = new();

    private static long _nextItemUid;

    private BagManager() { }

    /// <summary>加载角色背包（从实体缓存或数据库）</summary>
    public async Task<RoleBag> LoadBagAsync(long roleId)
    {
        var bag = await EntityFactory.Instance.GetAsync<RoleBag>(roleId);
        if (bag == null)
        {
            bag = new RoleBag { Id = roleId, Capacity = 100 };
            await EntityFactory.Instance.InsertAsync(bag);
            Log.Info($"创建新背包 RoleId:{roleId}");
        }
        return bag;
    }

    /// <summary>保存背包（Write-Behind）</summary>
    public async Task SaveBagAsync(RoleBag bag)
    {
        await EntityFactory.Instance.UpdateAsync(bag);
    }

    /// <summary>添加物品（自动找空格或叠加）</summary>
    public AddItemResult AddItem(RoleBag bag, int configId, int count, int maxStack = 999)
    {
        if (count <= 0)
            return AddItemResult.Fail(ErrorCode.InvalidParam);

        var remaining = count;

        // 先尝试叠加到已有同类物品
        foreach (var item in bag.Items)
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
            var emptyIndex = FindEmptyIndex(bag);
            if (emptyIndex < 0)
                return AddItemResult.Partial(count - remaining, ErrorCode.BagFull);

            var addCount = Math.Min(remaining, maxStack);
            var newItem = new BagItem
            {
                Uid = Interlocked.Increment(ref _nextItemUid),
                ConfigId = configId,
                Count = addCount,
                Index = emptyIndex,
                Bind = false,
                ExpireTime = 0,
            };
            bag.Items.Add(newItem);
            bag.GetIndexMap()[emptyIndex] = newItem;
            remaining -= addCount;
        }

        return AddItemResult.Ok(count);
    }

    /// <summary>移除物品（按配置ID）</summary>
    public bool RemoveItem(RoleBag bag, int configId, int count)
    {
        if (!HasEnough(bag, configId, count))
            return false;

        var remaining = count;
        for (int i = bag.Items.Count - 1; i >= 0; i--)
        {
            var item = bag.Items[i];
            if (item.ConfigId != configId) continue;

            if (item.Count <= remaining)
            {
                remaining -= item.Count;
                bag.RemoveFromIndex(item);
                bag.Items.RemoveAt(i);
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
    public bool RemoveByIndex(RoleBag bag, int index, int count)
    {
        var item = bag.GetByIndex(index);
        if (item == null) return false;
        if (item.Count < count) return false;

        if (item.Count == count)
        {
            bag.RemoveFromIndex(item);
            bag.Items.Remove(item);
        }
        else
        {
            item.Count -= count;
        }

        return true;
    }

    /// <summary>检查物品数量是否足够</summary>
    public bool HasEnough(RoleBag bag, int configId, int count)
    {
        var total = bag.Items.Where(i => i.ConfigId == configId).Sum(i => i.Count);
        return total >= count;
    }

    /// <summary>获取物品总数</summary>
    public int GetItemCount(RoleBag bag, int configId)
    {
        return bag.Items.Where(i => i.ConfigId == configId).Sum(i => i.Count);
    }

    /// <summary>检查是否有空格子</summary>
    public bool HasSpace(RoleBag bag, int need = 1)
    {
        return FindEmptyIndex(bag) >= 0;
    }

    /// <summary>整理背包（按配置ID排序，紧凑排列）</summary>
    public void Tidy(RoleBag bag)
    {
        // 按配置ID分组并合并
        var grouped = bag.Items
            .GroupBy(i => i.ConfigId)
            .OrderBy(g => g.Key)
            .ToList();

        bag.Items.Clear();
        bag.GetIndexMap().Clear();

        var index = 0;
        foreach (var group in grouped)
        {
            var totalCount = group.Sum(i => i.Count);
            // 简化：每个配置ID放一个格子（不拆分堆叠）
            bag.Items.Add(new BagItem
            {
                Uid = Interlocked.Increment(ref _nextItemUid),
                ConfigId = group.Key,
                Count = totalCount,
                Index = index,
                Bind = group.Any(i => i.Bind),
            });
            bag.GetIndexMap()[index] = bag.Items[^1];
            index++;
        }
    }

    /// <summary>交换两个格子的物品</summary>
    public bool Swap(RoleBag bag, int index1, int index2)
    {
        if (index1 == index2) return true;

        var item1 = bag.GetByIndex(index1);
        var item2 = bag.GetByIndex(index2);

        var map = bag.GetIndexMap();
        map.Remove(index1);
        map.Remove(index2);

        if (item1 != null) { item1.Index = index2; map[index2] = item1; }
        if (item2 != null) { item2.Index = index1; map[index1] = item2; }

        return true;
    }

    /// <summary>使用物品</summary>
    public UseItemResult UseItem(RoleBag bag, int index, int count)
    {
        if (count <= 0)
            return UseItemResult.Fail(ErrorCode.InvalidParam);

        var item = bag.GetByIndex(index);
        if (item == null)
            return UseItemResult.Fail(ErrorCode.ItemNotFound);

        if (item.Count < count)
            return UseItemResult.Fail(ErrorCode.ItemNotEnough);

        // 扣除物品
        if (item.Count == count)
        {
            bag.RemoveFromIndex(item);
            bag.Items.Remove(item);
        }
        else
        {
            item.Count -= count;
        }

        return UseItemResult.Ok(item.ConfigId, count);
    }

    /// <summary>获取背包信息（所有物品快照）</summary>
    public List<BagItem> GetItems(RoleBag bag)
    {
        return bag.Items.ToList();
    }

    private int FindEmptyIndex(RoleBag bag)
    {
        var map = bag.GetIndexMap();
        for (int i = 0; i < bag.Capacity; i++)
        {
            if (!map.ContainsKey(i))
                return i;
        }
        return -1;
    }
}

/// <summary>添加物品结果</summary>
public record AddItemResult(int AddedCount, bool Success, int ErrorCode)
{
    public static AddItemResult Ok(int count) => new(count, true, 0);
    public static AddItemResult Partial(int count, int error) => new(count, false, error);
    public static AddItemResult Fail(int error) => new(0, false, error);
}

/// <summary>使用物品结果</summary>
public record UseItemResult(int ConfigId, int UsedCount, bool Success, int ErrorCode)
{
    public static UseItemResult Ok(int configId, int count) => new(configId, count, true, 0);
    public static UseItemResult Fail(int error) => new(0, 0, false, error);
}

/// <summary>通用错误码</summary>
public static class ErrorCode
{
    public const int Ok = 0;
    public const int InvalidParam = 1001;
    public const int BagFull = 1002;
    public const int ItemNotFound = 1003;
    public const int ItemNotEnough = 1004;
    public const int ItemUseFail = 1005;
}
