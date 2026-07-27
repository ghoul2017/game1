using System;
using System.Collections.Generic;
using Shared.Common.Config;
using Shared.Core.Config;

namespace Game.Components.Bag;

/// <summary>
/// 在业务写入背包前验证并归一化待入包道具。
/// </summary>
public static class BagItemAddDataGenerator
{
    public static List<BagItemAddData> Generate(int[] rewardConfigs, int[] rewardCounts)
    {
        if (!TryGenerate(rewardConfigs, rewardCounts, out List<BagItemAddData> normalizedItems, out string errorMessage))
            throw new ArgumentException(errorMessage, nameof(rewardConfigs));

        return normalizedItems;
    }

    public static List<BagItemAddData> Generate(int configId, long count)
    {
        List<BagItemAddData> normalized = [];
        Dictionary<(int ConfigId, bool Bind, long ExpireTime), int> groupIndices = [];
        if (!TryAddNormalized(normalized, groupIndices, configId, count, false, 0, out string errorMessage))
            throw new ArgumentException(errorMessage, nameof(configId));

        return normalized;
    }

    public static bool TryGenerate(int[] rewardConfigs, int[] rewardCounts, out List<BagItemAddData> normalizedItems, out string errorMessage)
    {
        normalizedItems = [];
        errorMessage = "";
        if (rewardConfigs.Length != rewardCounts.Length)
        {
            errorMessage = "奖励配置和数量长度不一致";
            return false;
        }

        Dictionary<(int ConfigId, bool Bind, long ExpireTime), int> groupIndices = [];
        for (int index = 0; index < rewardConfigs.Length; index++)
        {
            if (!TryAddNormalized(normalizedItems, groupIndices, rewardConfigs[index], rewardCounts[index], false, 0, out errorMessage))
            {
                normalizedItems = [];
                return false;
            }
        }

        return true;
    }

    private static bool TryAddNormalized(List<BagItemAddData> normalizedItems, Dictionary<(int ConfigId, bool Bind, long ExpireTime), int> groupIndices, int configId, long count, bool bind, long expireTime, out string errorMessage)
    {
        errorMessage = "";
        if (configId <= 0)
        {
            errorMessage = "道具配置错误";
            return false;
        }

        if (count <= 0)
        {
            errorMessage = "道具数量错误";
            return false;
        }

        ItemConfig? config = ConfigManager.Get<ItemConfig>(configId);
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

        (int ConfigId, bool Bind, long ExpireTime) key = (configId, bind, expireTime);
        if (!groupIndices.TryGetValue(key, out int groupIndex))
        {
            groupIndices[key] = normalizedItems.Count;
            normalizedItems.Add(new BagItemAddData
            {
                ConfigId = configId,
                Count = count,
                Bind = bind,
                ExpireTime = expireTime,
            });
            return true;
        }

        try
        {
            normalizedItems[groupIndex].Count = checked(normalizedItems[groupIndex].Count + count);
        }
        catch (OverflowException)
        {
            errorMessage = "道具数量溢出";
            return false;
        }

        return true;
    }
}
