using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Game.Core.Network;
using Game.Database;
using Game.Network;
using Shared.Core.Log;
using Shared.Core.Network;

namespace Game.Game.Player;

/// <summary>
/// 玩家管理器。管理在线玩家列表，提供查找、广播、统计等功能。
/// 所有操作在游戏主线程上执行，无需加锁。
/// </summary>
public class PlayerManager
{
    private static readonly ILog Log = LogManager.GetLogger<PlayerManager>();

    public static PlayerManager Instance { get; } = new();

    private readonly Dictionary<long, PlayerEntity> _players = new();           // roleId → PlayerEntity
    private readonly Dictionary<long, PlayerEntity> _playersByUserId = new();   // userId → PlayerEntity
    private readonly Dictionary<string, PlayerEntity> _playersByName = new();   // roleName → PlayerEntity

    private PlayerManager() { }

    /// <summary>玩家上线（登录+选角完成后调用）</summary>
    public void AddPlayer(PlayerEntity player)
    {
        var online = player.Online;
        var role = player.RoleData.Role;

        // 如果已在线，先移除旧的
        if (_players.TryGetValue(player.Id, out var oldPlayer))
        {
            RemovePlayerInternal(oldPlayer);
            Log.Warn($"玩家重复上线，替换旧数据 RoleId:{player.Id}");
        }

        _players[player.Id] = player;
        _playersByUserId[online.UserId] = player;
        if (role != null && !string.IsNullOrEmpty(role.Name))
        {
            _playersByName[role.Name] = player;
        }
        Log.Info($"玩家上线 RoleId:{player.Id}，在线人数: {OnlineCount}");
    }

    /// <summary>玩家下线（断线或主动退出时调用）</summary>
    public void RemovePlayer(long roleId)
    {
        if (_players.TryGetValue(roleId, out var player))
        {
            RemovePlayerInternal(player);
            Log.Info($"玩家下线 RoleId:{roleId}，在线人数: {OnlineCount}");
        }
    }

    private void RemovePlayerInternal(PlayerEntity player)
    {
        var online = player.Online;
        var role = player.RoleData.Role;

        _players.Remove(player.Id);
        _playersByUserId.Remove(online.UserId);
        if (role != null && !string.IsNullOrEmpty(role.Name))
        {
            _playersByName.Remove(role.Name);
        }
    }

    /// <summary>按角色ID获取在线玩家</summary>
    public PlayerEntity? GetByRoleId(long roleId)
    {
        return _players.TryGetValue(roleId, out var p) ? p : null;
    }

    /// <summary>按用户ID获取在线玩家</summary>
    public PlayerEntity? GetByUserId(long userId)
    {
        return _playersByUserId.TryGetValue(userId, out var p) ? p : null;
    }

    /// <summary>按角色名获取在线玩家</summary>
    public PlayerEntity? GetByRoleName(string name)
    {
        return _playersByName.TryGetValue(name, out var p) ? p : null;
    }

    /// <summary>角色是否在线</summary>
    public bool IsOnline(long roleId) => _players.ContainsKey(roleId);

    /// <summary>当前在线玩家数</summary>
    public int OnlineCount => _players.Count;

    /// <summary>获取所有在线角色ID</summary>
    public List<long> GetOnlineRoleIds() => new(_players.Keys);

    /// <summary>遍历所有在线玩家</summary>
    public void ForEachPlayer(Action<PlayerEntity> action)
    {
        foreach (var player in _players.Values)
        {
            action(player);
        }
    }

    /// <summary>广播消息给所有在线玩家</summary>
    public void Broadcast(IMessage msg)
    {
        foreach (var player in _players.Values)
        {
            var session = SessionManager.Instance.GetByUserId(player.Online.UserId);
            session?.Send(msg);
        }
    }

    /// <summary>向指定角色发送消息</summary>
    public void SendToPlayer(long roleId, IMessage msg)
    {
        var session = SessionManager.Instance.GetByRoleId(roleId);
        session?.Send(msg);
    }

    /// <summary>
    /// 加载角色数据（从数据库）。登录时调用。
    /// </summary>
    public async Task<Role?> LoadRoleAsync(long roleId)
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

    /// <summary>
    /// 保存角色数据。下线或定时保存时调用。
    /// </summary>
    public async Task SaveRoleAsync(PlayerEntity player)
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

    /// <summary>保存所有在线角色</summary>
    public async Task SaveAllRolesAsync()
    {
        foreach (var player in _players.Values)
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
        Log.Info($"保存所有在线角色完成，共 {_players.Count} 个");
    }

    /// <summary>移除所有在线玩家（服务器关闭时调用）</summary>
    public void RemoveAll()
    {
        var count = _players.Count;
        _players.Clear();
        _playersByUserId.Clear();
        _playersByName.Clear();
        Log.Info($"清空所有在线玩家，共 {count} 人");
    }
}
