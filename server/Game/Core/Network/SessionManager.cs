using System;
using System.Collections.Generic;
using System.Linq;
using Shared.Core.Network;
using Shared.Common.Protos;
using Shared.Core.Log;
using System.Timers;
using Game.Components.User;
using Timer = System.Timers.Timer;

namespace Game.Network;

/// <summary>
/// 会话管理器。维护会话与玩家ID的映射关系，提供断线清理和踢人功能。
/// 所有操作在游戏主线程上执行，无需加锁。
/// </summary>
public class SessionManager
{
    private static readonly ILog Log = LogManager.GetLogger<SessionManager>();

    public static SessionManager Instance { get; } = new();

    private readonly Dictionary<long, TcpSession> _userSessionMap = new(); // userId -> session
    private readonly Dictionary<long, TcpSession> _roleSessionMap = new(); // roleId -> session

    private SessionManager()
    {
    }

    /// <summary>移除会话（连接断开时调用）</summary>
    public void Remove(TcpSession session)
    {
        if (session.UserId != 0)
        {
            _userSessionMap.Remove(session.UserId);
        }

        if (session.RoleId != 0)
        {
            _roleSessionMap.Remove(session.RoleId);
        }
    }

    /// <summary>绑定角色ID（选角完成后调用）</summary>
    public void BindRole(TcpSession session, long roleId)
    {
        session.RoleId = roleId;
        _roleSessionMap[roleId] = session;
    }

    /// <summary>按用户ID获取</summary>
    public TcpSession? GetByUserId(long userId)
    {
        return _userSessionMap.TryGetValue(userId, out var s) ? s : null;
    }

    /// <summary>按角色ID获取</summary>
    public TcpSession? GetByRoleId(long roleId)
    {
        return _roleSessionMap.TryGetValue(roleId, out var s) ? s : null;
    }

    /// <summary>角色是否在线</summary>
    public bool IsOnline(long roleId)
    {
        return _roleSessionMap.ContainsKey(roleId);
    }

    /// <summary>获取当前在线角色数</summary>
    public int OnlineCount => _roleSessionMap.Count;

    /// <summary>踢人（发送断线通知后断开连接）</summary>
    public void Kick(TcpSession session, LogoutReason reason)
    {
        Log.Info($"踢人 SessionId:{session.SessionId} UserId:{session.UserId} Reason:{reason}");

        // 发送踢人通知
        try
        {
            var ntf = new KickNtf { Reason = (int)reason };
            session.Send(ntf);
        }
        catch (Exception)
        {
        }

        // 移除映射并断开连接
        Remove(session);
        session.Disconnect();
    }

    /// <summary>按角色ID踢人</summary>
    public void KickByRoleId(long roleId, LogoutReason reason)
    {
        var session = GetByRoleId(roleId);
        if (session != null)
        {
            Kick(session, reason);
        }
    }

    /// <summary>广播消息给所有在线角色</summary>
    public void Broadcast(IMessage msg)
    {
        foreach (var session in _roleSessionMap.Values)
        {
            session.Send(msg);
        }
    }

    /// <summary>停止心跳检测定时器</summary>
    public void StopHeartbeat()
    {
    }
}
