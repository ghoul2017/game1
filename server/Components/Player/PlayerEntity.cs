using System;
using Game.Components.Bag;
using Game.Components.GameTask;
using Game.Components.Mail;
using Shared.Core.Database;
using Shared.Core.ECS;

namespace Game.Components.Player;

/// <summary>
/// 玩家聚合根。Id = RoleId，持久化到 player_entity 集合。
/// </summary>
public class PlayerEntity : Entity, IEntity
{
    private long _dirtyVersion;
    private bool _hasDirtyData;

    internal void MarkDirty()
    {
        _dirtyVersion++;
        _hasDirtyData = true;
    }

    internal bool TryGetDirtyVersion(out long version)
    {
        version = _dirtyVersion;
        return _hasDirtyData;
    }

    internal void ConfirmSaved(long version)
    {
        if (_dirtyVersion == version)
            _hasDirtyData = false;
    }

    internal bool HasDirtyData => _hasDirtyData;

    internal void RestoreComponentParents()
    {
        foreach (Component component in GetAllComponents())
            component.SetParent(this);
    }

    public static PlayerEntity Create(long roleId, long userId, string name, int level, int job)
    {
        PlayerEntity entity = CreateEntityById<PlayerEntity>(roleId);
        entity.AddComponent(new RoleDataComponent
        {
            UserId = userId,
            Name = name,
            Level = level,
            Job = job,
            CreateTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        });
        entity.AddComponent<RoleAttributeComponent>();
        entity.AddComponent(new RoleBagComponent { Capacity = 100 });
        entity.AddComponent<RoleMailComponent>();
        entity.AddComponent<RoleTaskComponent>();
        entity.AddComponent<PlayerExpeditionComponent>();

        PlayerOnlineComponent online = entity.AddComponent<PlayerOnlineComponent>();
        online.UserId = userId;
        online.LoginTime = DateTime.UtcNow;
        online.LastActiveTime = DateTime.UtcNow;
        return entity;
    }

    internal bool EnsureExpeditionComponent()
    {
        if (TryGetComponent<PlayerExpeditionComponent>() != null)
        {
            return false;
        }

        AddComponent<PlayerExpeditionComponent>();
        return true;
    }
}
