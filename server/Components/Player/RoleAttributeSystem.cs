using System.Threading.Tasks;
using Game.Database;
using Shared.Core.Log;

namespace Game.Components.Player;

/// <summary>
/// 角色属性系统。提供属性加载、保存功能。
/// </summary>
public static class RoleAttributeSystem
{
    private static readonly ILog Log = LogManager.GetLogger(nameof(RoleAttributeSystem));

    /// <summary>保存角色属性</summary>
    public static async Task SaveAttributeAsync(this RoleAttributeComponent self)
    {
        await MongoDb.Instance.UpdateAsync(self);
    }
}
