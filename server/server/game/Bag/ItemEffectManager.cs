using System;
using System.Threading.Tasks;
using Shared.Common.Config;
using Shared.Common.Protos;
using Server.Config;
using Server.Core;
using Server.Core.Entity;
using Server.Core.Log;
using Server.Game.Bag;
using Server.Game.Fight;
using Server.Game.Player;
using Server.Network;
using Shared.Core.Network;

namespace Server.Game.Bag;

/// <summary>
/// 物品效果管理器。处理物品使用后的实际效果。
/// </summary>
public class ItemEffectManager
{
    private static readonly ILog Log = LogManager.GetLogger<ItemEffectManager>();

    public static ItemEffectManager Instance { get; } = new();

    private ItemEffectManager() { }

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
    public async Task<ItemUseResult> ApplyItemEffectAsync(long roleId, int configId)
    {
        var result = new ItemUseResult();

        // 获取物品效果配置
        var configModule = ModuleRegistry.Instance.GetModule<ConfigModule>();
        var effectConfig = configModule?.ConfigManager.Get<ItemEffectConfig>(configId);
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

    #region 效果处理

    /// <summary>
    /// 恢复HP效果
    /// </summary>
    private async Task ApplyHpEffectAsync(long roleId, int healAmount)
    {
        var roleAttr = await LoadRoleAttributeAsync(roleId);
        if (roleAttr == null) return;

        var oldHp = roleAttr.Hp;
        roleAttr.Hp = Math.Min(roleAttr.MaxHp, roleAttr.Hp + healAmount);
        var actualHeal = roleAttr.Hp - oldHp;

        await SaveRoleAttributeAsync(roleAttr);

        // 发送HP变化通知
        SendHpChangedNtf(roleId, roleId, actualHeal, roleAttr.Hp, HpChangeReason.Recover);
    }

    /// <summary>
    /// 恢复MP效果
    /// </summary>
    private void ApplyMpEffect(long roleId, int recoverAmount)
    {
        var roleAttr = LoadRoleAttributeSync(roleId);
        if (roleAttr == null) return;

        roleAttr.Mp = Math.Min(roleAttr.MaxMp, roleAttr.Mp + recoverAmount);
        SaveRoleAttributeSync(roleAttr);

        // 发送MP变化通知（需要添加 MP 变化通知）
        Log.Debug($"MP恢复 RoleId:{roleId} Amount:{recoverAmount} CurrentMp:{roleAttr.Mp}");
    }

    /// <summary>
    /// BUFF效果（待实现：需要BUFF系统）
    /// </summary>
    private async Task ApplyBuffEffectAsync(long roleId, int buffId, int duration)
    {
        Log.Info($"获得BUFF RoleId:{roleId} BuffId:{buffId} Duration:{duration}s");
        // TODO: 实现BUFF系统
        // BuffManager.Instance.AddBuff(roleId, buffId, duration);
        await Task.CompletedTask;
    }

    /// <summary>
    /// 传送效果
    /// </summary>
    private void ApplyTeleportEffect(long roleId, int mapId, int targetId)
    {
        Log.Info($"传送请求 RoleId:{roleId} MapId:{mapId} Target:{targetId}");
        // TODO: 实现传送逻辑
        // 需要发送传送确认给客户端，客户端播放动画后再执行
    }

    /// <summary>
    /// 复活效果
    /// </summary>
    private void ApplyReviveEffect(long roleId, int hpPercent)
    {
        // 复活效果由客户端触发，服务器验证
        Log.Info($"复活请求 RoleId:{roleId} HpPercent:{hpPercent}");
        var fightUnit = FightManager.Instance.GetUnit(roleId);
        if (fightUnit != null)
        {
            fightUnit.Attr.Hp = fightUnit.Attr.MaxHp * hpPercent / 100;
            SendHpChangedNtf(roleId, roleId, fightUnit.Attr.Hp, fightUnit.Attr.Hp, HpChangeReason.Revive);
        }
    }

    #endregion

    #region 角色属性管理

    /// <summary>加载角色属性（非异步版本）</summary>
    private RoleAttribute? LoadRoleAttributeSync(long roleId)
    {
        var task = LoadRoleAttributeAsync(roleId);
        task.Wait();
        return task.Result;
    }

    /// <summary>加载角色属性</summary>
    public async Task<RoleAttribute?> LoadRoleAttributeAsync(long roleId)
    {
        var attr = await EntityFactory.Instance.GetAsync<RoleAttribute>(roleId);
        if (attr == null)
        {
            attr = new RoleAttribute { Id = roleId };
            await EntityFactory.Instance.InsertAsync(attr);
            Log.Info($"创建新角色属性 RoleId:{roleId}");
        }
        return attr;
    }

    /// <summary>保存角色属性（同步版本）</summary>
    private void SaveRoleAttributeSync(RoleAttribute attr)
    {
        var task = SaveRoleAttributeAsync(attr);
        task.Wait();
    }

    /// <summary>保存角色属性</summary>
    public async Task SaveRoleAttributeAsync(RoleAttribute attr)
    {
        await EntityFactory.Instance.UpdateAsync(attr);
    }

    #endregion

    #region 消息发送

    private void SendHpChangedNtf(long roleId, long targetId, int deltaHp, int currentHp, int reason)
    {
        var ntf = new HpChangedNtf
        {
            ObjectId = targetId,
            DeltaHp = deltaHp,
            Hp = currentHp,
            Reason = reason,
        };
        PlayerManager.Instance.SendToPlayer(roleId, ntf);
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
