using System;
using System.Threading.Tasks;
using Game.Components.Fight;
using Game.Components.Player;
using Game.Config;
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

    public const int EffectHp = 1;
    public const int EffectMp = 2;
    public const int EffectBuff = 3;
    public const int EffectTeleport = 4;
    public const int EffectRevive = 5;

    #endregion

    /// <summary>
    /// 使用物品并触发效果。
    /// </summary>
    public static async Task<ItemUseResult> ApplyItemEffectAsync(long roleId, int configId)
    {
        await Task.CompletedTask;
        var result = new ItemUseResult();

        var player = Context.Players.GetByRoleId(roleId);
        if (player == null)
        {
            result.Code = -1;
            return result;
        }

        // 获取物品效果配置
        var effectConfig = ConfigManager.Get<ItemEffectConfig>(configId);
        if (effectConfig == null)
        {
            result.Code = 0;
            result.EffectType = 0;
            Log.Debug($"物品 {configId} 无效果配置");
            return result;
        }

        result.EffectType = effectConfig.EffectType;
        result.Value = effectConfig.Value1;

        var attr = player.RoleAttribute;

        switch (effectConfig.EffectType)
        {
            case EffectHp:
                ApplyHpEffect(roleId, attr, effectConfig.Value1);
                break;

            case EffectMp:
                ApplyMpEffect(roleId, attr, effectConfig.Value1);
                break;

            case EffectBuff:
                Log.Warn($"BUFF效果未实现 RoleId:{roleId} BuffId:{effectConfig.Value1} Duration:{effectConfig.Value2}");
                break;

            case EffectTeleport:
                Log.Warn($"传送效果未实现 RoleId:{roleId} MapId:{effectConfig.Value1} PosX:{effectConfig.Value2}");
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
        var fightUnit = Context.Fight.GetUnit(roleId);
        if (fightUnit != null)
        {
            fightUnit.Attr.Hp = fightUnit.Attr.MaxHp * hpPercent / 100;
            SendHpChangedNtf(roleId, roleId, fightUnit.Attr.Hp, fightUnit.Attr.Hp, HpChangeReason.Revive);
        }
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
