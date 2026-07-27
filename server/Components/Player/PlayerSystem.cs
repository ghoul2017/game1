using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Game.Components.Bag;
using Shared.Common.Protos;
using Shared.Core;
using Shared.Core.Database;
using Game.Database;
using Shared.Core.Event;
using Shared.Core.Log;
using Shared.Core.Network;

namespace Game.Components.Player;

/// <summary>
/// 玩家系统。PlayerComponent 的扩展方法，管理在线玩家增删查、广播、持久化。
/// </summary>
public static class PlayerSystem
{
    private static readonly ILog Log = LogManager.GetLogger(nameof(PlayerSystem));

    /// <summary>玩家上线</summary>
    public static void AddPlayer(this PlayerComponent self, PlayerEntity player)
    {
        var online = player.GetComponent<PlayerOnlineComponent>();
        var roleData = player.GetComponent<RoleDataComponent>();

        if (self.Players.TryGetValue(player.Id, out var oldPlayer))
        {
            self.RemovePlayer(oldPlayer);
            Log.Warn($"玩家重复上线，替换旧数据 RoleId:{player.Id}");
        }

        self.Players[player.Id] = player;
        self.PlayersByUserId[online.UserId] = player;
        if (!string.IsNullOrEmpty(roleData.Name))
        {
            self.PlayersByName[roleData.Name] = player;
        }
        Log.Info($"玩家上线 RoleId:{player.Id}，在线人数: {self.Players.Count}");
    }

    /// <summary>玩家下线</summary>
    public static void RemovePlayer(this PlayerComponent self, long roleId)
    {
        if (self.Players.TryGetValue(roleId, out var player))
        {
            self.RemovePlayer(player);
            Log.Info($"玩家下线 RoleId:{roleId}，在线人数: {self.Players.Count}");
        }
    }

    /// <summary>玩家下线（内部）</summary>
    public static void RemovePlayer(this PlayerComponent self, PlayerEntity player)
    {
        var online = player.GetComponent<PlayerOnlineComponent>();
        var roleData = player.GetComponent<RoleDataComponent>();

        self.Players.Remove(player.Id);
        self.PlayersByUserId.Remove(online.UserId);
        if (!string.IsNullOrEmpty(roleData.Name))
        {
            self.PlayersByName.Remove(roleData.Name);
        }
    }

    public static PlayerEntity? GetByRoleId(this PlayerComponent self, long roleId)
    {
        return self.Players.TryGetValue(roleId, out var p) ? p : null;
    }

    public static PlayerEntity? GetByUserId(this PlayerComponent self, long userId)
    {
        return self.PlayersByUserId.TryGetValue(userId, out var p) ? p : null;
    }

    public static PlayerEntity? GetByRoleName(this PlayerComponent self, string name)
    {
        return self.PlayersByName.TryGetValue(name, out var p) ? p : null;
    }

    public static bool IsOnline(this PlayerComponent self, long roleId) => self.Players.ContainsKey(roleId);

    public static int OnlineCount(this PlayerComponent self) => self.Players.Count;

    public static List<long> GetOnlineRoleIds(this PlayerComponent self) => new(self.Players.Keys);

    public static void ForEachPlayer(this PlayerComponent self, Action<PlayerEntity> action)
    {
        foreach (var player in self.Players.Values)
        {
            action(player);
        }
    }

    public static void Broadcast(this PlayerComponent self, IMessage msg)
    {
        foreach (var player in self.Players.Values)
        {
            player.GetComponent<PlayerOnlineComponent>().Session?.Send(msg);
        }
    }

    public static void SendToPlayer(this PlayerComponent self, long roleId, IMessage msg)
    {
        var player = self.GetByRoleId(roleId);
        player?.GetComponent<PlayerOnlineComponent>().Session?.Send(msg);
    }

    /// <summary>进入游戏：创建 PlayerEntity + 加载角色数据 + 绑定会话 + 生成 Token + 发布上线事件</summary>
    public static async Task<(PlayerEntity player, string token)?> EnterGame(
        this PlayerComponent self, long roleId, long userId, TcpSession session, string name, int level, int job)
    {
        var player = PlayerEntity.Create(roleId, userId);

        // 加载角色持久化数据，不存在则用 UserRole 数据创建
        var roleData = await self.LoadRoleDataAsync(roleId);
        if (roleData == null)
        {
            // 首次进入游戏，按需创建 RoleDataComponent
            roleData = new RoleDataComponent
            {
                Id = roleId,
                UserId = userId,
                Name = name,
                Level = level,
                Job = job,
                CreateTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
            await MongoDb.Instance.InsertAsync(roleData);
            Log.Info($"首次进入游戏，创建角色数据 RoleId:{roleId}");
        }
        player.AddComponent(roleData);

        // 绑定会话
        session.RoleId = roleId;
        player.GetComponent<PlayerOnlineComponent>().Session = session;

        // 生成重连Token（含roleId）并写入角色
        var token = TokenSigner.Instance.Generate(userId, roleId);
        session.ReconnectToken = token;
        roleData.LastToken = token;

        self.AddPlayer(player);

        // 先完成各模块上线加载，避免客户端收到 EnterGameNtf 后立即进图时访问未挂载组件。
        await EventBus.Instance.PublishAsync(new EventType.PlayerOnlineEvent { Player = player });

        // 通知客户端进入游戏准备就绪
        session.Send(new Shared.Common.Protos.EnterGameNtf
        {
            RoleId = roleId,
            Name = roleData.Name,
            Level = roleData.Level,
            Job = roleData.Job
        });

        Log.Info($"进入游戏 UserId:{userId} RoleId:{roleId} Name:{roleData.Name}");
        return (player, token);
    }

    public static async Task<RoleDataComponent?> LoadRoleDataAsync(this PlayerComponent self, long roleId)
    {
        try
        {
            return await MongoDb.Instance.LoadAsync<RoleDataComponent>(roleId);
        }
        catch (Exception ex)
        {
            Log.Error($"加载角色数据失败 RoleId:{roleId}: {ex.Message}");
            return null;
        }
    }

    public static void RemoveAll(this PlayerComponent self)
    {
        var count = self.Players.Count;
        self.Players.Clear();
        self.PlayersByUserId.Clear();
        self.PlayersByName.Clear();
        Log.Info($"清空所有在线玩家，共 {count} 人");
    }


    /// <summary>发放经验给玩家</summary>
    public static void GrantExp(this PlayerComponent self, long roleId, int exp)
    {
        if (exp <= 0)
            return;

        var player = self.GetByRoleId(roleId);
        if (player == null)
            return;

        var roleAttr = player.GetComponent<RoleAttributeComponent>();
        roleAttr.Exp += exp;

        int newLevel = PlayerHelper.CalculateLevel(roleAttr.Exp);
        var leveledUp = newLevel > roleAttr.Level;
        if (leveledUp)
        {
            roleAttr.Level = newLevel;
            roleAttr.MaxHp += 10;
            roleAttr.MaxMp += 5;
            roleAttr.Hp = roleAttr.MaxHp;
            roleAttr.Mp = roleAttr.MaxMp;
        }

        roleAttr.MarkDirty();

        if (leveledUp)
        {
            self.SendToPlayer(roleId, new LevelUpNtf { Level = newLevel });
            EventBus.Instance.Publish(new EventType.PlayerLevelUpEvent { RoleId = roleId, NewLevel = newLevel });
            Log.Info($"玩家升级 RoleId:{roleId} Level:{newLevel}");
        }

        self.SendToPlayer(roleId, new ExpChangedNtf { Exp = roleAttr.Exp, Level = roleAttr.Level });
        EventBus.Instance.Publish(new EventType.PlayerExpChangedEvent { RoleId = roleId, Exp = roleAttr.Exp, Level = roleAttr.Level });
    }

    /// <summary>发放金币给玩家</summary>
    public static void GrantGold(this PlayerComponent self, long roleId, int gold)
    {
        if (gold <= 0)
            return;

        var player = self.GetByRoleId(roleId);
        if (player == null)
            return;

        var roleAttr = player.GetComponent<RoleAttributeComponent>();
        roleAttr.Gold += gold;
        roleAttr.MarkDirty();

        self.SendToPlayer(roleId, new GoldChangedNtf { Gold = roleAttr.Gold });
        EventBus.Instance.Publish(new EventType.PlayerGoldChangedEvent { RoleId = roleId, Gold = roleAttr.Gold });
    }

    /// <summary>发放物品给玩家</summary>
    public static void GrantItem(this PlayerComponent self, long roleId, int configId, long count)
    {
        PlayerEntity? player = self.GetByRoleId(roleId);
        if (player == null)
            return;

        RoleBagComponent bagComponent = player.GetComponent<RoleBagComponent>();
        List<BagItemAddData> normalizedItems = BagItemAddDataGenerator.Generate(configId, count);
        List<BagItemAddData> gainedItems = bagComponent.AddItems(normalizedItems);

        foreach (var gainedItem in gainedItems)
        {
            self.SendToPlayer(roleId, new ItemGainNtf
            {
                ConfigId = gainedItem.ConfigId,
                Count = gainedItem.Count,
            });
            EventBus.Instance.Publish(new EventType.PlayerItemGainedEvent
            {
                RoleId = roleId,
                ConfigId = gainedItem.ConfigId,
                Count = gainedItem.Count,
            });
            Log.Info($"[背包] 发放物品 RoleId:{roleId} ConfigId:{gainedItem.ConfigId} Count:{gainedItem.Count}");
        }
    }

}
