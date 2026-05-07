using Game.Components.Bag;
using Game.Components.Player;
using System.Linq;
using System.Threading.Tasks;
using Game.Core.Network;
using Game.Network;
using Shared.Core.Log;
using Shared.Core.Network;
using Shared.Common.Protos;

namespace Game.Components.Bag;

public static class BagHandlers
{
    private static readonly ILog Log = LogManager.GetLogger(nameof(BagHandlers));

    [MessageHandler(typeof(RpcMethodHandler))]
    public static async Task<IMessage> QueryBagHandler(TcpSession session, QueryBagReq req, QueryBagAck res)
    {
        var player = Context.Players.GetByRoleId(session.RoleId);
        if (player == null)
        {
            res.Code = ErrorCode.InvalidParam;
            return res;
        }

        var bag = await player.Bag.LoadBagAsync(player.Id);
        res.Code = ErrorCode.Ok;
        res.Capacity = bag.Capacity;
        res.Items = bag.Items.Select(ToInfo).ToList();

        return res;
    }

    [MessageHandler(typeof(RpcMethodHandler))]
    public static async Task<IMessage> BagTidyHandler(TcpSession session, BagTidyReq req, BagTidyAck res)
    {
        var player = Context.Players.GetByRoleId(session.RoleId);
        if (player == null)
        {
            res.Code = ErrorCode.InvalidParam;
            return res;
        }

        var bag = await player.Bag.LoadBagAsync(player.Id);
        player.Bag.Tidy(bag);
        await player.Bag.SaveBagAsync(bag);

        res.Code = ErrorCode.Ok;
        res.Items = bag.Items.Select(ToInfo).ToList();

        return res;
    }

    [MessageHandler(typeof(RpcMethodHandler))]
    public static async Task<IMessage> BagSwapHandler(TcpSession session, BagSwapReq req, BagSwapAck res)
    {
        var player = Context.Players.GetByRoleId(session.RoleId);
        if (player == null)
        {
            res.Code = ErrorCode.InvalidParam;
            return res;
        }

        var bag = await player.Bag.LoadBagAsync(player.Id);
        player.Bag.Swap(bag, req.Index1, req.Index2);
        await player.Bag.SaveBagAsync(bag);

        res.Code = ErrorCode.Ok;
        return res;
    }

    [MessageHandler(typeof(RpcMethodHandler))]
    public static async Task<IMessage> BagUseItemHandler(TcpSession session, BagUseItemReq req, BagUseItemAck res)
    {
        var player = Context.Players.GetByRoleId(session.RoleId);
        if (player == null)
        {
            res.Code = ErrorCode.InvalidParam;
            return res;
        }

        var bag = await player.Bag.LoadBagAsync(player.Id);

        var bagItem = bag.GetByIndex(req.Index);
        if (bagItem == null)
        {
            res.Code = ErrorCode.ItemNotFound;
            return res;
        }

        if (bagItem.Count < req.Count)
        {
            res.Code = ErrorCode.ItemNotEnough;
            return res;
        }

        var configId = bagItem.ConfigId;
        if (configId < 2001 || configId > 2999)
        {
            res.Code = ErrorCode.ItemUseFail;
            Log.Warn($"非消耗品不能直接使用 RoleId:{session.RoleId} ConfigId:{configId}");
            return res;
        }

        var useResult = player.Bag.UseItem(bag, req.Index, req.Count);
        if (!useResult.Success)
        {
            res.Code = useResult.ErrorCode;
            return res;
        }

        await player.Bag.SaveBagAsync(bag);

        var effectResult = await ItemEffectSystem.ApplyItemEffectAsync(session.RoleId, configId);

        res.Code = ErrorCode.Ok;
        res.ConfigId = configId;

        Log.Info($"使用物品 RoleId:{session.RoleId} ConfigId:{configId} Count:{req.Count} EffectType:{effectResult.EffectType}");
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
}
