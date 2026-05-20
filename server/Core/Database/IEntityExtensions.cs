using Shared.Core.Database;

namespace Game.Database;

/// <summary>
/// IEntity 扩展方法。提供 SaveAsync 便利方法。
/// </summary>
public static class IEntityExtensions
{
    public static async Task SaveAsync(this IEntity entity)
    {
        await MongoDb.Instance.UpdateAsync(entity);
    }
}
