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
/// </summary>
public class SessionManager
{
    private static readonly ILog Log = LogManager.GetLogger<SessionManager>();

    public static SessionManager Instance { get; } = new();

    private readonly Dictionary<ulong, TcpSession> _sessionMap = new();       // sessionId -> session
    private readonly Dictionary<long, TcpSession> _userSessionMap = new();     // userId -> session
    private readonly Dictionary<long, TcpSession> _roleSessionMap = new();     // roleId -> session
    private readonly object _lock = new();

    /// <summary>心跳超时时间（秒），未登录会话超过此时间将被踢出</summary>
    private const int HeartbeatTimeoutSeconds = 60;

    /// <summary>心跳检测间隔（秒）</summary>
    private const int CheckIntervalSeconds = 20;

    private readonly Timer _heartbeatTimer;

    private SessionManager()
    {
        _heartbeatTimer = new Timer(CheckIntervalSeconds * 1000);
        _heartbeatTimer.Elapsed += OnHeartbeatCheck;
        _heartbeatTimer.AutoReset = true;
        _heartbeatTimer.Start();
    }

    /// <summary>添加会话（连接建立时调用）</summary>
    public void Add(TcpSession session)
    {
        lock (_lock)
        {
            _sessionMap[session.SessionId] = session;
        }
        Log.Info($"会话添加 SessionId:{session.SessionId}，当前连接数: {_sessionMap.Count}");
    }

    /// <summary>移除会话（连接断开时调用）</summary>
    public void Remove(TcpSession session)
    {
        lock (_lock)
        {
            _sessionMap.Remove(session.SessionId);

            if (session.UserId != 0)
            {
                _userSessionMap.Remove(session.UserId);
            }

            if (session.RoleId != 0)
            {
                _roleSessionMap.Remove(session.RoleId);
            }
        }
        Log.Info($"会话移除 SessionId:{session.SessionId}，当前连接数: {_sessionMap.Count}");
    }

    /// <summary>绑定用户ID（登录验证通过后调用）</summary>
    public void BindUser(TcpSession session, long userId)
    {
        lock (_lock)
        {
            // 顶号处理：如果该用户已有会话，踢掉旧会话
            if (_userSessionMap.TryGetValue(userId, out var oldSession) && oldSession != session)
            {
                Log.Warn($"顶号: UserId:{userId} 旧SessionId:{oldSession.SessionId} → 新SessionId:{session.SessionId}");
                Kick(oldSession, LogoutReason.AnotherSession);
            }

            session.UserId = userId;
            _userSessionMap[userId] = session;
        }
    }

    /// <summary>绑定角色ID（选角完成后调用）</summary>
    public void BindRole(TcpSession session, long roleId)
    {
        lock (_lock)
        {
            session.RoleId = roleId;
            _roleSessionMap[roleId] = session;
        }
    }

    /// <summary>按会话ID获取</summary>
    public TcpSession? GetBySessionId(ulong sessionId)
    {
        lock (_lock)
        {
            return _sessionMap.TryGetValue(sessionId, out var s) ? s : null;
        }
    }

    /// <summary>按用户ID获取</summary>
    public TcpSession? GetByUserId(long userId)
    {
        lock (_lock)
        {
            return _userSessionMap.TryGetValue(userId, out var s) ? s : null;
        }
    }

    /// <summary>按角色ID获取</summary>
    public TcpSession? GetByRoleId(long roleId)
    {
        lock (_lock)
        {
            return _roleSessionMap.TryGetValue(roleId, out var s) ? s : null;
        }
    }

    /// <summary>角色是否在线</summary>
    public bool IsOnline(long roleId)
    {
        lock (_lock)
        {
            return _roleSessionMap.ContainsKey(roleId);
        }
    }

    /// <summary>获取当前在线角色数</summary>
    public int OnlineCount
    {
        get { lock (_lock) { return _roleSessionMap.Count; } }
    }

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
        catch { }

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
        List<TcpSession> sessions;
        lock (_lock)
        {
            sessions = new List<TcpSession>(_roleSessionMap.Values);
        }

        foreach (var session in sessions)
        {
            session.Send(msg);
        }
    }

    /// <summary>服务器关闭时踢出所有连接</summary>
    public void KickAll(LogoutReason reason)
    {
        List<TcpSession> sessions;
        lock (_lock)
        {
            sessions = new List<TcpSession>(_sessionMap.Values);
            _sessionMap.Clear();
            _userSessionMap.Clear();
            _roleSessionMap.Clear();
        }

        foreach (var session in sessions)
        {
            session.Disconnect();
        }

        Log.Info($"全部断开，共 {sessions.Count} 个连接");
    }

    /// <summary>停止心跳检测定时器</summary>
    public void StopHeartbeat()
    {
        _heartbeatTimer.Stop();
        _heartbeatTimer.Dispose();
    }

    private void OnHeartbeatCheck(object? sender, ElapsedEventArgs e)
    {
        List<TcpSession> timeoutSessions;
        lock (_lock)
        {
            var now = DateTime.UtcNow;
            timeoutSessions = _sessionMap.Values
                .Where(s => s.IsConnected && (now - s.LastActiveTime).TotalSeconds > HeartbeatTimeoutSeconds)
                .ToList();
        }

        foreach (var session in timeoutSessions)
        {
            Log.Warn($"心跳超时 SessionId:{session.SessionId} UserId:{session.UserId}");
            Kick(session, LogoutReason.HeartbeatTimeout);
        }

        if (timeoutSessions.Count > 0)
        {
            Log.Info($"心跳检测: 踢出 {timeoutSessions.Count} 个超时连接，当前在线: {OnlineCount}");
        }
    }
}
