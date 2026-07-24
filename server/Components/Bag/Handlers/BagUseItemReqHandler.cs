using System.Threading.Tasks;
using Game.Components.Player;
using Shared.Common.Protos;
using Shared.Core.Log;
using Shared.Core.Network;

namespace Game.Components.Bag;

public sealed class BagUseItemReqHandler : PlayerRpcMessageHandler<BagUseItemReq, BagUseItemAck>
{
    private static readonly ILog Log = LogManager.GetLogger(nameof(BagUseItemReqHandler));

    protected override async Task ExecuteAsync(PlayerEntity player, BagUseItemReq req, BagUseItemAck res)
    {
        var bagComponent = player.GetComponent<RoleBagComponent>();
        var attributeComponent = player.GetComponent<RoleAttributeComponent>();

        if (req.Count <= 0)
        {
            res.ErrorMessage = "物品数量错误";
            return;
        }

        var bagItem = bagComponent.GetByIndex(req.Index);
        if (bagItem == null)
        {
            res.ErrorMessage = "物品不存在";
            return;
        }

        if (bagItem.Count < req.Count)
        {
            res.ErrorMessage = "物品数量不足";
            return;
        }

        var configId = bagItem.ConfigId;
        if (configId < 2001 || configId > 2999)
        {
            res.ErrorMessage = "物品使用失败";
            Log.Warn($"非消耗品不能直接使用 RoleId:{player.Id} ConfigId:{configId}");
            return;
        }

        if (!bagComponent.RemoveByIndex(req.Index, req.Count))
        {
            var message = $"物品校验通过后扣除失败 RoleId:{player.Id} Index:{req.Index} Count:{req.Count}";
            Log.Error(message);
            throw new InvalidOperationException(message);
        }

        var effectType = ItemEffectHelper.ApplyItemEffect(player.Id, attributeComponent, configId);
        res.ConfigId = configId;

        Log.Info($"使用物品 RoleId:{player.Id} ConfigId:{configId} Count:{req.Count} EffectType:{effectType}");
        await Task.CompletedTask;
    }
}
