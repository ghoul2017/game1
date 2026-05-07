using System;
using System.Threading.Tasks;
using Game.Components.Fight;
using Game.Components.Player;
using Game.Config;
using Game.Database;
using Shared.Common.Config;
using Shared.Common.Protos;
using Shared.Core.Log;

namespace Game.Components.Bag;

/// <summary>
/// 物品效果系统。处理物品使用后的实际效果。
/// </summary>
public static class ItemEffectSystem
{
    private static readonly ILog Log = LogManager.GetLogger(nameof(ItemEffectSystem));

    #region 物品效果类型

    /// <summary>效果类型枚举</summary>
    public const int EffectHp = 1;      // 恢复HP
    public const int EffectMp = 2;      // 恢复MP
    public const int EffectBuff = 3;    // 增益BUFF
    public const int EffectTeleport = 4; // 传送
    public const int EffectRevive = 5;   // 复活

    #endregion

    /// <summary>
    /// 使用物品并触发效果。
    /// </summary>
    /// <param name="roleId">角色ID</param>
    /// <param name="configId">物品配置ID</param>
    /// <returns>使用结果</returns>
    public static async Task<ItemUseResult> ApplyItemEffectAsync(long roleId, int configId)
    {
        var result = new ItemUseResult();

        // 获取物品效果配置
        var effectConfig = ConfigManager.Get<ItemEffectConfig>(configId);
        if (effectConfig == null)
        {
            // 没有效果配置的物品，可能是普通物品（如装备、材料）
            result.Code = 0;
            result.EffectType = 0;
            Log.Debug($"物品 {configId} 无效果配置");
            return result;
        }

        result.EffectType = effectConfig.EffectType;
        result.Value = effectConfig.Value1;

        // 根据效果类型处理
        switch (effectConfig.EffectType)
        {
            case EffectHp:
                await ApplyHpEffectAsync(roleId, effectConfig.Value1);
                break;

            case EffectMp:
                ApplyMpEffect(roleId, effectConfig.Value1);
                break;

            case EffectBuff:
                await ApplyBuffEffectAsync(roleId, effectConfig.Value1, effectConfig.Value2);
                break;

            case EffectTeleport:
                ApplyTeleportEffect(roleId, effectConfig.Value1, effectConfig.Value2);
                break;

            case EffectRevive:
                ApplyReviveEffect(roleId, effectConfig.Value1);
                break;
        }

        result.Code = 0;
        Log.Info($"物品效果 RoleId:{roleId} ConfigId:{configId} EffectType:{effectConfig.EffectType} Value:{effectConfig.Value1}");
        return result;
    }

    #region 效果处理方法

    /// <summary>
    /// 恢复HP效果
    /// </summary>
    private static async Task ApplyHpEffectAsync(long roleId, int healAmount)
    {
        var roleAttr = await LoadRoleAttributeAsync(roleId);
        if (roleAttr == null) return;

        var oldHp = roleAttr.Hp;
        roleAttr.Hp = Math.Min(roleAttr.MaxHp, roleAttr.Hp + healAmount);
        var actualHeal = roleAttr.Hp - oldHp;

        await SaveRoleAttributeAsync(roleAttr);

        // 发送HP变化通知
        Context.Players.SendToPlayer(roleId, new HpChangedNtf
        {
            ObjectId = roleId,
            DeltaHp = actualHeal,
            Hp = roleAttr.Hp,
            Reason = 0 // 0 表示恢复
        });

        Log.Info($"HP恢复 RoleId:{roleId} Amount:{actualHeal} NewHp:{roleAttr.Hp}");
    }

    /// <summary>
    /// 恢复MP效果
    /// </summary>
    private static void ApplyMpEffect(long roleId, int recoverAmount)
    {
        var roleAttr = LoadRoleAttributeSync(roleId);
        if (roleAttr == null) return;

        roleAttr.Mp = Math.Min(roleAttr.MaxMp, roleAttr.Mp + recoverAmount);
        SaveRoleAttributeSync(roleAttr);

        Log.Debug($"MP恢复 RoleId:{roleId} Amount:{recoverAmount} CurrentMp:{roleAttr.Mp}");
    }

    /// <summary>
    /// 增益BUFF效果
    /// </summary>
    private static async Task ApplyBuffEffectAsync(long roleId, int buffId, int duration)
    {
        // TODO: 实现BUFF系统
        Log.Warn($"BUFF效果未实现 RoleId:{roleId} BuffId:{buffId} Duration:{duration}");
        await Task.CompletedTask;
    }

    /// <summary>
    /// 传送效果
    /// </summary>
    private static void ApplyTeleportEffect(long roleId, int mapId, int posX)
    {
        // TODO: 实现传送系统
        Log.Warn($"传送效果未实现 RoleId:{roleId} MapId:{mapId} PosX:{posX}");
    }

    /// <summary>
    /// 复活效果
    /// </summary>
    private static void ApplyReviveEffect(long roleId, int hpPercent)
    {
        Log.Info($"复活请求 RoleId:{roleId} HpPercent:{hpPercent}");
        var fightUnit = Context.Fight.GetUnit(roleId);
        if (fightUnit != null)
        {
            fightUnit.Attr.Hp = fightUnit.Attr.MaxHp * hpPercent / 100;
            SendHpChangedNtf(roleId, roleId, fightUnit.Attr.Hp, fightUnit.Attr.Hp, HpChangeReason.Revive);
        }
    }

    /// <summary>加载角色属性</summary>
    public static async Task<RoleAttribute?> LoadRoleAttributeAsync(long roleId)
    {
        var attr = await DatabaseManager.Instance.EntityRepo.LoadAsync<RoleAttribute>(roleId);
        if (attr == null)
        {
            attr = new RoleAttribute { Id = roleId };
            await DatabaseManager.Instance.EntityRepo.InsertAsync(attr);
            Log.Info($"创建新角色属性 RoleId:{roleId}");
        }
        return attr;
    }

    private static RoleAttribute? LoadRoleAttributeSync(long roleId)
    {
        var task = LoadRoleAttributeAsync(roleId);
        task.Wait();
        return task.Result;
    }

    /// <summary>保存角色属性</summary>
    public static async Task SaveRoleAttributeAsync(RoleAttribute attr)
    {
        await DatabaseManager.Instance.EntityRepo.UpdateAsync(attr);
    }

    private static void SaveRoleAttributeSync(RoleAttribute attr)
    {
        var task = SaveRoleAttributeAsync(attr);
        task.Wait();
    }

    #endregion

    #region 消息发送

    private static void SendHpChangedNtf(long roleId, long targetId, int deltaHp, int currentHp, int reason)
    {
        var ntf = new HpChangedNtf
        {
            ObjectId = targetId,
            DeltaHp = deltaHp,
            Hp = currentHp,
            Reason = reason,
        };
        Context.Players.SendToPlayer(roleId, ntf);
    }

    #endregion
}

/// <summary>
/// 物品使用结果
/// </summary>
public class ItemUseResult
{
    public int Code { get; set; }
    public int EffectType { get; set; }
    public int Value { get; set; }
}
