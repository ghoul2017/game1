using System;
using System.Collections.Generic;
using System.Linq;
using Game.Components.Bag;
using Game.Components.Mail;
using Game.Components.GameTask;
using Shared.Core.ECS;

namespace Game.Components.Player;

/// <summary>
/// 玩家实体。Id = RoleId。在线玩家的运行时容器。
/// </summary>
public class PlayerEntity : Entity
{
    private readonly Dictionary<Type, long> _dirtyVersions = [];

    /// <summary>在线状态组件</summary>
    public PlayerOnlineComponent Online => GetComponent<PlayerOnlineComponent>();

    /// <summary>角色持久化数据组件</summary>
    public RoleDataComponent RoleData => GetComponent<RoleDataComponent>();

    /// <summary>角色属性组件</summary>
    public RoleAttributeComponent RoleAttribute => GetComponent<RoleAttributeComponent>();

    /// <summary>角色背包组件</summary>
    public RoleBagComponent RoleBag => GetComponent<RoleBagComponent>();

    /// <summary>角色邮件组件</summary>
    public RoleMailComponent RoleMail => GetComponent<RoleMailComponent>();

    /// <summary>角色任务组件</summary>
    public RoleTaskComponent RoleTask => GetComponent<RoleTaskComponent>();

    internal void MarkDirty(Type componentType)
    {
        _dirtyVersions[componentType] = _dirtyVersions.GetValueOrDefault(componentType) + 1;
    }

    internal List<KeyValuePair<Type, long>> GetDirtyVersions()
    {
        return _dirtyVersions.ToList();
    }

    internal void ConfirmSaved(Type componentType, long version)
    {
        if (_dirtyVersions.TryGetValue(componentType, out var currentVersion) && currentVersion == version)
            _dirtyVersions.Remove(componentType);
    }

    internal bool HasDirtyData => _dirtyVersions.Count > 0;

    /// <summary>创建玩家实体（选角入场时调用）</summary>
    public static PlayerEntity Create(long roleId, long userId)
    {
        var entity = CreateEntityById<PlayerEntity>(roleId);
        var online = entity.AddComponent<PlayerOnlineComponent>();
        online.UserId = userId;
        online.LoginTime = DateTime.UtcNow;
        online.LastActiveTime = DateTime.UtcNow;

        return entity;
    }
}
