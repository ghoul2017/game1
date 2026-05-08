using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Game.Core.Network;
using Game.Database;
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
        var online = player.Online;
        var role = player.RoleData.Role;

        if (self.Players.TryGetValue(player.Id, out var oldPlayer))
        {
            self.RemovePlayer(oldPlayer);
            Log.Warn($"玩家重复上线，替换旧数据 RoleId:{player.Id}");
        }

        self.Players[player.Id] = player;
        self.PlayersByUserId[online.UserId] = player;
        if (role != null && !string.IsNullOrEmpty(role.Name))
        {
            self.PlayersByName[role.Name] = player;
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
        var online = player.Online;
        var role = player.RoleData.Role;

        self.Players.Remove(player.Id);
        self.PlayersByUserId.Remove(online.UserId);
        if (role != null && !string.IsNullOrEmpty(role.Name))
        {
            self.PlayersByName.Remove(role.Name);
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
            var session = Context.Network.GetByUserId(player.Online.UserId);
            session?.Send(msg);
        }
    }

    public static void SendToPlayer(this PlayerComponent self, long roleId, IMessage msg)
    {
        var session = Context.Network.GetByRoleId(roleId);
        session?.Send(msg);
    }

    public static async Task<Role?> LoadRoleAsync(this PlayerComponent self, long roleId)
    {
        try
        {
            return await DatabaseManager.Instance.EntityRepo.LoadAsync<Role>(roleId);
        }
        catch (Exception ex)
        {
            Log.Error($"加载角色数据失败 RoleId:{roleId}: {ex.Message}");
            return null;
        }
    }

    public static async Task SaveRoleAsync(this PlayerComponent self, PlayerEntity player)
    {
        var role = player.RoleData.Role;
        if (role == null) return;
        try
        {
            await DatabaseManager.Instance.EntityRepo.UpdateAsync(role);
        }
        catch (Exception ex)
        {
            Log.Error($"保存角色数据失败 RoleId:{player.Id}: {ex.Message}");
        }
    }

    public static async Task SaveAllRolesAsync(this PlayerComponent self)
    {
        foreach (var player in self.Players.Values)
        {
            var role = player.RoleData.Role;
            if (role != null)
            {
                try
                {
                    await DatabaseManager.Instance.EntityRepo.UpdateAsync(role);
                }
                catch (Exception ex)
                {
                    Log.Error($"保存角色数据失败 RoleId:{player.Id}: {ex.Message}");
                }
            }
        }
        Log.Info($"保存所有在线角色完成，共 {self.Players.Count} 个");
    }

    public static void RemoveAll(this PlayerComponent self)
    {
        var count = self.Players.Count;
        self.Players.Clear();
        self.PlayersByUserId.Clear();
        self.PlayersByName.Clear();
        Log.Info($"清空所有在线玩家，共 {count} 人");
    }
}
