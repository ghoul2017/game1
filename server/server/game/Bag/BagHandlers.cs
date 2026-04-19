using System.Threading.Tasks;
using Server.Core.Log;
using Server.Game.Player;
using Server.Network;
using Shared.Core.Network;
using Shared.Common.Protos;

namespace Server.Game.Bag;

/// <summary>
/// 查询背包信息 Handler
/// </summary>
public class QueryBagHandler : MessageHandler<QueryBagReq>
{
    private static readonly ILog Log = LogManager.GetLogger<QueryBagHandler>();

    protected override async Task<IMessage?> ExecuteAsync(TcpSession session, QueryBagReq req)
    {
        var resp = new QueryBagAck();

        var player = PlayerManager.Instance.GetByRoleId(session.RoleId);
        if (player == null)
        {
            resp.Code = ErrorCode.InvalidParam;
            return resp;
        }

        var bag = await BagManager.Instance.LoadBagAsync(player.RoleId);
        resp.Code = ErrorCode.Ok;
        resp.Capacity = bag.Capacity;
        resp.Items = bag.Items.Select(ToInfo).ToList();

        return resp;
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
        var resp = new BagTidyAck();

        var player = PlayerManager.Instance.GetByRoleId(session.RoleId);
        if (player == null)
        {
            resp.Code = ErrorCode.InvalidParam;
            return resp;
        }

        var bag = await BagManager.Instance.LoadBagAsync(player.RoleId);
        BagManager.Instance.Tidy(bag);
        await BagManager.Instance.SaveBagAsync(bag);

        resp.Code = ErrorCode.Ok;
        resp.Items = bag.Items.Select(QueryBagHandler.ToInfo).ToList();

        return resp;
    }
}

/// <summary>
/// 交换物品格子 Handler
/// </summary>
public class BagSwapHandler : MessageHandler<BagSwapReq>
{
    protected override async Task<IMessage?> ExecuteAsync(TcpSession session, BagSwapReq req)
    {
        var resp = new BagSwapAck();

        var player = PlayerManager.Instance.GetByRoleId(session.RoleId);
        if (player == null)
        {
            resp.Code = ErrorCode.InvalidParam;
            return resp;
        }

        var bag = await BagManager.Instance.LoadBagAsync(player.RoleId);
        BagManager.Instance.Swap(bag, req.Index1, req.Index2);
        await BagManager.Instance.SaveBagAsync(bag);

        resp.Code = ErrorCode.Ok;
        return resp;
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
        var resp = new BagUseItemAck();

        var player = PlayerManager.Instance.GetByRoleId(session.RoleId);
        if (player == null)
        {
            resp.Code = ErrorCode.InvalidParam;
            return resp;
        }

        // 加载背包
        var bag = await BagManager.Instance.LoadBagAsync(player.RoleId);

        // 获取物品信息（用于检查物品类型）
        var bagItem = bag.GetByIndex(req.Index);
        if (bagItem == null)
        {
            resp.Code = ErrorCode.ItemNotFound;
            return resp;
        }

        // 检查物品数量
        if (bagItem.Count < req.Count)
        {
            resp.Code = ErrorCode.ItemNotEnough;
            return resp;
        }

        // 检查物品是否可使用（消耗品ID范围: 2001-2999）
        var configId = bagItem.ConfigId;
        if (configId < 2001 || configId > 2999)
        {
            // 非消耗品不能直接使用
            resp.Code = ErrorCode.ItemUseFail;
            Log.Warn($"非消耗品不能直接使用 RoleId:{session.RoleId} ConfigId:{configId}");
            return resp;
        }

        // 使用物品
        var useResult = BagManager.Instance.UseItem(bag, req.Index, req.Count);
        if (!useResult.Success)
        {
            resp.Code = useResult.ErrorCode;
            return resp;
        }

        await BagManager.Instance.SaveBagAsync(bag);

        // 触发物品效果
        var effectResult = await ItemEffectManager.Instance.ApplyItemEffectAsync(session.RoleId, configId);

        resp.Code = ErrorCode.Ok;
        resp.ConfigId = configId;

        Log.Info($"使用物品 RoleId:{session.RoleId} ConfigId:{configId} Count:{req.Count} EffectType:{effectResult.EffectType}");
        return resp;
    }
}
