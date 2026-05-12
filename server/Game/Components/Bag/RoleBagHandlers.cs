using Game.Components.Player;
using System.Linq;
using System.Threading.Tasks;
using Game.Database;
using Shared.Core.Event;
using Shared.Core.Log;
using Shared.Core.Network;
using Shared.Common.Protos;

namespace Game.Components.Bag;

public static class RoleBagHandlers
{
    private static readonly ILog Log = LogManager.GetLogger(nameof(RoleBagHandlers));

    [MessageHandler(typeof(PlayerRpcMethodHandler))]
    public static async Task<IMessage> QueryBagHandler(PlayerEntity player, QueryBagReq req, QueryBagAck res)
    {
        await Task.CompletedTask;
        res.Capacity = player.RoleBag.Capacity;
        res.Items = player.RoleBag.Items.Select(ToInfo).ToList();

        return res;
    }

    [MessageHandler(typeof(PlayerRpcMethodHandler))]
    public static async Task<IMessage> BagTidyHandler(PlayerEntity player, BagTidyReq req, BagTidyAck res)
    {
        player.RoleBag.Tidy();
        await player.RoleBag.SaveBagAsync();

        res.Items = player.RoleBag.Items.Select(ToInfo).ToList();

        return res;
    }

    [MessageHandler(typeof(PlayerRpcMethodHandler))]
    public static async Task<IMessage> BagSwapHandler(PlayerEntity player, BagSwapReq req, BagSwapAck res)
    {
        player.RoleBag.Swap(req.Index1, req.Index2);
        await player.RoleBag.SaveBagAsync();
        return res;
    }

    [MessageHandler(typeof(PlayerRpcMethodHandler))]
    public static async Task<IMessage> BagUseItemHandler(PlayerEntity player, BagUseItemReq req, BagUseItemAck res)
    {
        var bagItem = player.RoleBag.GetByIndex(req.Index);
        if (bagItem == null)
        {
            res.ErrorMessage = "物品不存在";
            return res;
        }

        if (bagItem.Count < req.Count)
        {
            res.ErrorMessage = "物品数量不足";
            return res;
        }

        var configId = bagItem.ConfigId;
        if (configId < 2001 || configId > 2999)
        {
            res.ErrorMessage = "物品使用失败";
            Log.Warn($"非消耗品不能直接使用 RoleId:{player.Id} ConfigId:{configId}");
            return res;
        }

        var useResult = player.RoleBag.UseItem(req.Index, req.Count);
        if (!useResult.Success)
        {
            res.ErrorMessage = useResult.ErrorMessage;
            return res;
        }

        await player.RoleBag.SaveBagAsync();

        var effectResult = await ItemEffectSystem.ApplyItemEffectAsync(player.Id, configId);
        res.ConfigId = configId;

        Log.Info($"使用物品 RoleId:{player.Id} ConfigId:{configId} Count:{req.Count} EffectType:{effectResult.EffectType}");
        return res;
    }

    internal static BagItemInfo ToInfo(BagItem item) => new()
    {
        Uid = item.Uid,
        ConfigId = item.ConfigId,
        Count = item.Count,
        Index = item.Index,
        Bind = item.Bind,
    };

    #region 事件订阅

    [EventSubscriber]
    public static async Task OnPlayerOnline(EventType.PlayerOnlineEvent e)
    {
        var player = e.Player;

        var bag = await MongoDb.Instance.LoadAsync<RoleBagComponent>(player.Id);
        if (bag == null)
        {
            bag = new RoleBagComponent { Id = player.Id, Capacity = 100 };
            await MongoDb.Instance.InsertAsync(bag);
            Log.Info($"创建新背包 RoleId:{player.Id}");
        }
        bag.RebuildIndex();
        player.AddComponent(bag);
    }

    #endregion
}
