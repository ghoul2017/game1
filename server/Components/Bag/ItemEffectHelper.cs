using System;
using Game.Components.Fight;
using Game.Components.Player;
using Shared.Core.Config;
using Shared.Common.Config;
using Shared.Common.Protos;
using Shared.Core.Log;

namespace Game.Components.Bag;

/// <summary>
/// 物品效果系统。处理物品使用后的实际效果。
/// </summary>
public static class ItemEffectHelper
{
    private static readonly ILog Log = LogManager.GetLogger(nameof(ItemEffectHelper));


    public const int EffectHp = 1;
    public const int EffectMp = 2;
    public const int EffectRevive = 5;


    /// <summary>
    /// 使用物品并触发效果。
    /// </summary>
    public static int ApplyItemEffect(long roleId, RoleAttributeComponent attr, int configId)
    {
        // 获取物品效果配置
        var effectConfig = ConfigManager.Get<ItemEffectConfig>(configId);
        if (effectConfig == null)
        {
            Log.Debug($"物品 {configId} 无效果配置");
            return 0;
        }

        switch (effectConfig.EffectType)
        {
            case EffectHp:
                ApplyHpEffect(roleId, attr, effectConfig.Value1);
                break;

            case EffectMp:
                ApplyMpEffect(roleId, attr, effectConfig.Value1);
                break;

            case EffectRevive:
                ApplyReviveEffect(roleId, effectConfig.Value1);
                break;
        }

        if (effectConfig.EffectType is EffectHp or EffectMp)
            attr.MarkDirty();

        Log.Info($"物品效果 RoleId:{roleId} ConfigId:{configId} EffectType:{effectConfig.EffectType} Value:{effectConfig.Value1}");
        return effectConfig.EffectType;
    }


    private static void ApplyHpEffect(long roleId, RoleAttributeComponent attr, int healAmount)
    {
        var oldHp = attr.Hp;
        attr.Hp = Math.Min(attr.MaxHp, attr.Hp + healAmount);
        var actualHeal = attr.Hp - oldHp;

        Context.Players.SendToPlayer(roleId, new HpChangedNtf
        {
            ObjectId = roleId,
            DeltaHp = actualHeal,
            Hp = attr.Hp,
            Reason = 0
        });

        Log.Info($"HP恢复 RoleId:{roleId} Amount:{actualHeal} NewHp:{attr.Hp}");
    }

    private static void ApplyMpEffect(long roleId, RoleAttributeComponent attr, int recoverAmount)
    {
        attr.Mp = Math.Min(attr.MaxMp, attr.Mp + recoverAmount);
        Log.Debug($"MP恢复 RoleId:{roleId} Amount:{recoverAmount} CurrentMp:{attr.Mp}");
    }

    private static void ApplyReviveEffect(long roleId, int hpPercent)
    {
        Log.Info($"复活请求 RoleId:{roleId} HpPercent:{hpPercent}");
        var fightUnit = FightHelper.GetFightUnit(roleId);
        if (fightUnit != null)
        {
            fightUnit.Attr.Hp = fightUnit.Attr.MaxHp * hpPercent / 100;
            SendHpChangedNtf(roleId, roleId, fightUnit.Attr.Hp, fightUnit.Attr.Hp, HpChangeReason.Revive);
        }
    }



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

}
