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

    /// <summary>加载角色属性（从数据库）</summary>
    public static async Task<RoleAttributeComponent> LoadAttributeAsync(this RoleAttributeComponent self, long roleId)
    {
        var attr = await DatabaseManager.Instance.EntityRepo.LoadAsync<RoleAttributeComponent>(roleId);
        if (attr == null)
        {
            attr = new RoleAttributeComponent { Id = roleId };
            await DatabaseManager.Instance.EntityRepo.InsertAsync(attr);
            Log.Info($"创建新角色属性 RoleId:{roleId}");
        }
        // 把数据库加载的数据拷贝到当前 Component
        self.Id = attr.Id;
        self.Hp = attr.Hp;
        self.MaxHp = attr.MaxHp;
        self.Mp = attr.Mp;
        self.MaxMp = attr.MaxMp;
        self.PhyAttMin = attr.PhyAttMin;
        self.PhyAttMax = attr.PhyAttMax;
        self.MagicAttMin = attr.MagicAttMin;
        self.MagicAttMax = attr.MagicAttMax;
        self.PhyDef = attr.PhyDef;
        self.MagicDef = attr.MagicDef;
        self.MoveSpeed = attr.MoveSpeed;
        self.AttackSpeed = attr.AttackSpeed;
        self.CriticalRate = attr.CriticalRate;
        self.CriticalDamage = attr.CriticalDamage;
        self.Exp = attr.Exp;
        self.Level = attr.Level;
        self.Gold = attr.Gold;
        return self;
    }

    /// <summary>保存角色属性</summary>
    public static async Task SaveAttributeAsync(this RoleAttributeComponent self)
    {
        await DatabaseManager.Instance.EntityRepo.UpdateAsync(self);
    }
}
