using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Game.Core.Entity;
using Game.Core.Network;
using Game.Network;
using Shared.Core.Log;
using Shared.Core.Network;

namespace Game.Game.Player;

/// <summary>
/// 玩家管理器。管理在线玩家列表，提供查找、广播、统计等功能。
/// 在 SessionManager 之上提供游戏层面的玩家管理。
/// 参考: Java 项目的 UserManager + ServerPlayerManager。
/// </summary>
public class PlayerManager
{
    private static readonly ILog Log = LogManager.GetLogger<PlayerManager>();

    public static PlayerManager Instance { get; } = new();

    private readonly Dictionary<long, Player> _players = new();           // roleId → Player
    private readonly Dictionary<long, Player> _playersByUserId = new();   // userId → Player
    private readonly Dictionary<string, Player> _playersByName = new();   // roleName → Player
    private readonly object _lock = new();

    private PlayerManager() { }

    /// <summary>玩家上线（登录+选角完成后调用）</summary>
    public void AddPlayer(Player player)
    {
        lock (_lock)
        {
            // 如果已在线，先移除旧的
            if (_players.TryGetValue(player.RoleId, out var oldPlayer))
            {
                RemovePlayerLocked(oldPlayer);
                Log.Warn($"玩家重复上线，替换旧数据 RoleId:{player.RoleId}");
            }

            _players[player.RoleId] = player;
            _playersByUserId[player.UserId] = player;
            if (!string.IsNullOrEmpty(player.RoleName))
            {
                _playersByName[player.RoleName] = player;
            }
        }
        Log.Info($"玩家上线 RoleId:{player.RoleId} Name:{player.RoleName}，在线人数: {OnlineCount}");
    }

    /// <summary>玩家下线（断线或主动退出时调用）</summary>
    public void RemovePlayer(long roleId)
    {
        lock (_lock)
        {
            if (_players.TryGetValue(roleId, out var player))
            {
                RemovePlayerLocked(player);
                Log.Info($"玩家下线 RoleId:{roleId}，在线人数: {OnlineCount}");
            }
        }
    }

    private void RemovePlayerLocked(Player player)
    {
        _players.Remove(player.RoleId);
        _playersByUserId.Remove(player.UserId);
        if (!string.IsNullOrEmpty(player.RoleName))
        {
            _playersByName.Remove(player.RoleName);
        }
    }

    /// <summary>按角色ID获取在线玩家</summary>
    public Player? GetByRoleId(long roleId)
    {
        lock (_lock)
        {
            return _players.TryGetValue(roleId, out var p) ? p : null;
        }
    }

    /// <summary>按用户ID获取在线玩家</summary>
    public Player? GetByUserId(long userId)
    {
        lock (_lock)
        {
            return _playersByUserId.TryGetValue(userId, out var p) ? p : null;
        }
    }

    /// <summary>按角色名获取在线玩家</summary>
    public Player? GetByRoleName(string name)
    {
        lock (_lock)
        {
            return _playersByName.TryGetValue(name, out var p) ? p : null;
        }
    }

    /// <summary>角色是否在线</summary>
    public bool IsOnline(long roleId)
    {
        lock (_lock) { return _players.ContainsKey(roleId); }
    }

    /// <summary>当前在线玩家数</summary>
    public int OnlineCount
    {
        get { lock (_lock) { return _players.Count; } }
    }

    /// <summary>获取所有在线角色ID</summary>
    public List<long> GetOnlineRoleIds()
    {
        lock (_lock) { return new List<long>(_players.Keys); }
    }

    /// <summary>遍历所有在线玩家（线程安全快照）</summary>
    public void ForEachPlayer(Action<Player> action)
    {
        List<Player> snapshot;
        lock (_lock) { snapshot = new List<Player>(_players.Values); }
        snapshot.ForEach(action);
    }

    /// <summary>广播消息给所有在线玩家</summary>
    public void Broadcast(IMessage msg)
    {
        foreach (Player player in _players.Values)
        {
           
            var session = SessionManager.Instance.GetByUserId( player.UserId);
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
    /// 加载角色数据（从实体缓存或数据库）。登录时调用。
    /// </summary>
    public async Task<Role?> LoadRoleAsync(long roleId)
    {
        try
        {
            return await EntityFactory.Instance.GetAsync<Role>(roleId);
        }
        catch (Exception ex)
        {
            Log.Error($"加载角色数据失败 RoleId:{roleId}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 保存角色数据（Write-Behind）。下线或定时保存时调用。
    /// </summary>
    public async Task SaveRoleAsync(Player player)
    {
        if (player.RoleData == null) return;
        try
        {
            await EntityFactory.Instance.UpdateAsync(player.RoleData);
        }
        catch (Exception ex)
        {
            Log.Error($"保存角色数据失败 RoleId:{player.RoleId}: {ex.Message}");
        }
    }

    /// <summary>保存所有在线角色（Write-Behind，停服时用 immediately=true）</summary>
    public async Task SaveAllRolesAsync(bool immediately = false)
    {
        List<Player> snapshot;
        lock (_lock) { snapshot = new List<Player>(_players.Values); }

        foreach (var player in snapshot)
        {
            if (player.RoleData != null)
            {
                try
                {
                    await EntityFactory.Instance.UpdateAsync(player.RoleData, immediately);
                }
                catch (Exception ex)
                {
                    Log.Error($"保存角色数据失败 RoleId:{player.RoleId}: {ex.Message}");
                }
            }
        }
        Log.Info($"保存所有在线角色完成，共 {snapshot.Count} 个");
    }

    /// <summary>移除所有在线玩家（服务器关闭时调用）</summary>
    public void RemoveAll()
    {
        lock (_lock)
        {
            var count = _players.Count;
            _players.Clear();
            _playersByUserId.Clear();
            _playersByName.Clear();
            Log.Info($"清空所有在线玩家，共 {count} 人");
        }
    }
}
