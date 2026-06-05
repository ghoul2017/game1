using Shared.Common.Protos;

namespace Game.Components.Bag;

public static class RoleBagHelper
{
    internal static BagItemInfo ToInfo(BagItem item) => new()
    {
        Uid = item.Uid,
        ConfigId = item.ConfigId,
        Count = item.Count,
        Index = item.Index,
        Bind = item.Bind,
    };
}
