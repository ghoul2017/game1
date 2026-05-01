using System.Linq;
using System.Threading.Tasks;
using Game.Game.Player;
using Game.Network;
using Shared.Core.Log;
using Shared.Core.Network;
using Shared.Common.Protos;

namespace Game.Game.Bag;

/// <summary>
/// 查询背包信息 Handler
/// </summary>
public class QueryBagHandler : MessageHandler<QueryBagReq>
{
    private static readonly ILog Log = LogManager.GetLogger<QueryBagHandler>();

    protected override async Task<IMessage?> ExecuteAsync(TcpSession session, QueryBagReq req)
    {
        var res = new QueryBagAck();

        var player = PlayerManager.Instance.GetByRoleId(session.RoleId);
        if (player == null)
        {
            res.Code = ErrorCode.InvalidParam;
            return res;
        }

        var bag = await BagManager.Instance.LoadBagAsync(player.RoleId);
        res.Code = ErrorCode.Ok;
        res.Capacity = bag.Capacity;
        res.Items = bag.Items.Select(ToInfo).ToList();

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

/// <summary>
/// 整理背包 Handler
/// </summary>
public class BagTidyHandler : MessageHandler<BagTidyReq>
{
    protected override async Task<IMessage?> ExecuteAsync(TcpSession session, BagTidyReq req)
    {
        var res = new BagTidyAck();

        var player = PlayerManager.Instance.GetByRoleId(session.RoleId);
        if (player == null)
        {
            res.Code = ErrorCode.InvalidParam;
            return res;
        }

        var bag = await BagManager.Instance.LoadBagAsync(player.RoleId);
        BagManager.Instance.Tidy(bag);
        await BagManager.Instance.SaveBagAsync(bag);

        res.Code = ErrorCode.Ok;
        res.Items = bag.Items.Select(QueryBagHandler.ToInfo).ToList();

        return res;
    }
}

/// <summary>
/// 交换物品格子 Handler
/// </summary>
public class BagSwapHandler : MessageHandler<BagSwapReq>
{
    protected override async Task<IMessage?> ExecuteAsync(TcpSession session, BagSwapReq req)
    {
        var res = new BagSwapAck();

        var player = PlayerManager.Instance.GetByRoleId(session.RoleId);
        if (player == null)
        {
            res.Code = ErrorCode.InvalidParam;
            return res;
        }

        var bag = await BagManager.Instance.LoadBagAsync(player.RoleId);
        BagManager.Instance.Swap(bag, req.Index1, req.Index2);
        await BagManager.Instance.SaveBagAsync(bag);

        res.Code = ErrorCode.Ok;
        return res;
    }
}

/// <summary>
/// 使用物品 Handler
/// </summary>
public class BagUseItemHandler : MessageHandler<BagUseItemReq>
{
    private static readonly ILog Log = LogManager.GetLogger<BagUseItemHandler>();

    protected override async Task<IMessage?> ExecuteAsync(TcpSession session, BagUseItemReq req)
    {
        var res = new BagUseItemAck();

        var player = PlayerManager.Instance.GetByRoleId(session.RoleId);
        if (player == null)
        {
            res.Code = ErrorCode.InvalidParam;
            return res;
        }

        // 加载背包
        var bag = await BagManager.Instance.LoadBagAsync(player.RoleId);

        // 获取物品信息（用于检查物品类型）
        var bagItem = bag.GetByIndex(req.Index);
        if (bagItem == null)
        {
            res.Code = ErrorCode.ItemNotFound;
            return res;
        }

        // 检查物品数量
        if (bagItem.Count < req.Count)
        {
            res.Code = ErrorCode.ItemNotEnough;
            return res;
        }

        // 检查物品是否可使用（消耗品ID范围: 2001-2999）
        var configId = bagItem.ConfigId;
        if (configId < 2001 || configId > 2999)
        {
            // 非消耗品不能直接使用
            res.Code = ErrorCode.ItemUseFail;
            Log.Warn($"非消耗品不能直接使用 RoleId:{session.RoleId} ConfigId:{configId}");
            return res;
        }

        // 使用物品
        var useResult = BagManager.Instance.UseItem(bag, req.Index, req.Count);
        if (!useResult.Success)
        {
            res.Code = useResult.ErrorCode;
            return res;
        }

        await BagManager.Instance.SaveBagAsync(bag);

        // 触发物品效果
        var effectResult = await ItemEffectManager.Instance.ApplyItemEffectAsync(session.RoleId, configId);

        res.Code = ErrorCode.Ok;
        res.ConfigId = configId;

        Log.Info($"使用物品 RoleId:{session.RoleId} ConfigId:{configId} Count:{req.Count} EffectType:{effectResult.EffectType}");
        return res;
    }
}
