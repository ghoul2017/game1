using System.Threading.Tasks;
using Game.Database;
using Shared.Core.Event;
using Shared.Core.Log;

namespace Game.Components.Player;

public static class PlayerHandlers
{
    private static readonly ILog Log = LogManager.GetLogger(nameof(PlayerHandlers));

    [EventSubscriber]
    public static async Task OnPlayerOnline(EventType.PlayerOnlineEvent e)
    {
        var player = Context.Players.GetByRoleId(e.RoleId);
        if (player == null)
        {
            return;
        }

        var attrComp = player.AddComponent<RoleAttributeComponent>();
        var attr = await DatabaseManager.Instance.EntityRepo.LoadAsync<RoleAttributeComponent>(e.RoleId);
        if (attr == null)
        {
            attr = new RoleAttributeComponent { Id = e.RoleId };
            await DatabaseManager.Instance.EntityRepo.InsertAsync(attr);
            Log.Info($"创建新角色属性 RoleId:{e.RoleId}");
        }
        attrComp.Id = attr.Id;
        attrComp.Hp = attr.Hp;
        attrComp.MaxHp = attr.MaxHp;
        attrComp.Mp = attr.Mp;
        attrComp.MaxMp = attr.MaxMp;
        attrComp.PhyAttMin = attr.PhyAttMin;
        attrComp.PhyAttMax = attr.PhyAttMax;
        attrComp.MagicAttMin = attr.MagicAttMin;
        attrComp.MagicAttMax = attr.MagicAttMax;
        attrComp.PhyDef = attr.PhyDef;
        attrComp.MagicDef = attr.MagicDef;
        attrComp.MoveSpeed = attr.MoveSpeed;
        attrComp.AttackSpeed = attr.AttackSpeed;
        attrComp.CriticalRate = attr.CriticalRate;
        attrComp.CriticalDamage = attr.CriticalDamage;
        attrComp.Exp = attr.Exp;
        attrComp.Level = attr.Level;
        attrComp.Gold = attr.Gold;
    }
}
