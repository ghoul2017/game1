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
        var bagItem = player.RoleBag.GetByIndex(req.Index);
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

        var useResult = player.RoleBag.UseItem(req.Index, req.Count);
        if (!useResult.Success)
        {
            res.ErrorMessage = useResult.ErrorMessage;
            return;
        }

        await player.RoleBag.SaveBagAsync();

        var effectResult = await ItemEffectSystem.ApplyItemEffectAsync(player.Id, configId);
        res.ConfigId = configId;

        Log.Info($"使用物品 RoleId:{player.Id} ConfigId:{configId} Count:{req.Count} EffectType:{effectResult.EffectType}");
    }
}
