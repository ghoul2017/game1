using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace Game.Components.User;

/// <summary>
/// 管理仅在当前服务进程内有效的单次重连令牌。在线令牌不计时且不可消费，权威会话断线后才启动恢复窗口；
/// 每次成功消费都会立即轮换旧令牌，并在原恢复窗内保留到同一新令牌的幂等重放映射。
/// </summary>
public sealed class ReconnectTokenRegistry
{
    private const int TokenByteCount = 32;
    private static readonly TimeSpan DefaultLifetime = TimeSpan.FromMinutes(5.0);

    private readonly Dictionary<(long UserId, long RoleId), string> _tokensByOwner = new();
    private readonly Dictionary<string, ReconnectTokenEntry> _entriesByToken = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ReconnectReplayEntry> _replaysByToken = new(StringComparer.Ordinal);
    private readonly object _syncRoot = new();
    private readonly TimeSpan _recoveryWindow;
    private readonly Func<DateTime> _utcNow;

    public ReconnectTokenRegistry()
        : this(DefaultLifetime, static () => DateTime.UtcNow)
    {
    }

    public ReconnectTokenRegistry(TimeSpan recoveryWindow, Func<DateTime> utcNow)
    {
        if (recoveryWindow <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(recoveryWindow), "断线恢复窗口必须大于零");
        }

        _recoveryWindow = recoveryWindow;
        _utcNow = utcNow ?? throw new ArgumentNullException(nameof(utcNow));
    }

    public static ReconnectTokenRegistry Instance { get; } = new ReconnectTokenRegistry();

    public string Issue(long userId, long roleId)
    {
        ValidateOwner(userId, roleId);

        lock (_syncRoot)
        {
            RemoveOwnerToken(userId, roleId);
            return AddOnlineToken(userId, roleId);
        }
    }

    /// <summary>
    /// 重连后的 EnterGame 复用刚轮换出的有效令牌；普通登录或无效候选仍签发新令牌。
    /// 这样 EnterGame 响应丢失时，客户端已经保存的重连令牌不会被服务端提前废除。
    /// </summary>
    public string IssueOrReuse(long userId, long roleId, string? candidateToken)
    {
        ValidateOwner(userId, roleId);

        lock (_syncRoot)
        {
            DateTime now = _utcNow();
            if (!string.IsNullOrEmpty(candidateToken) &&
                _entriesByToken.TryGetValue(candidateToken, out ReconnectTokenEntry? entry) &&
                entry.UserId == userId &&
                entry.RoleId == roleId &&
                !IsRecoveryExpired(entry, now) &&
                _tokensByOwner.TryGetValue((userId, roleId), out string? ownerToken) &&
                string.Equals(ownerToken, candidateToken, StringComparison.Ordinal))
            {
                _entriesByToken[candidateToken] = entry with { RecoveryExpiresAtUtc = null };
                RemoveOwnerReplays(userId, roleId);
                return candidateToken;
            }

            RemoveOwnerToken(userId, roleId);
            return AddOnlineToken(userId, roleId);
        }
    }

    /// <summary>
    /// 仅当前所有者令牌可在权威会话断线时启动恢复窗口；已轮换或已离线的令牌不能续期。
    /// </summary>
    public bool BeginRecoveryWindow(long userId, long roleId, string token)
    {
        ValidateOwner(userId, roleId);
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        lock (_syncRoot)
        {
            if (!_entriesByToken.TryGetValue(token, out ReconnectTokenEntry? entry) ||
                entry.UserId != userId ||
                entry.RoleId != roleId ||
                entry.RecoveryExpiresAtUtc != null ||
                !_tokensByOwner.TryGetValue((userId, roleId), out string? ownerToken) ||
                !string.Equals(ownerToken, token, StringComparison.Ordinal))
            {
                return false;
            }

            DateTime expiresAtUtc = _utcNow().Add(_recoveryWindow);
            _entriesByToken[token] = entry with { RecoveryExpiresAtUtc = expiresAtUtc };
            return true;
        }
    }

    public bool TryConsumeAndRotate(
        string token,
        long requestedRoleId,
        out long userId,
        out long roleId,
        out string rotatedToken,
        out string errorMessage)
    {
        return TryConsumeAndRotate(
            token,
            requestedRoleId,
            out userId,
            out roleId,
            out rotatedToken,
            out bool ignoredReplay,
            out errorMessage);
    }

    public bool TryConsumeAndRotate(
        string token,
        long requestedRoleId,
        out long userId,
        out long roleId,
        out string rotatedToken,
        out bool replayed,
        out string errorMessage)
    {
        userId = 0L;
        roleId = 0L;
        rotatedToken = string.Empty;
        replayed = false;
        errorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(token))
        {
            errorMessage = "Token无效或已过期";
            return false;
        }

        lock (_syncRoot)
        {
            if (!_entriesByToken.TryGetValue(token, out ReconnectTokenEntry? entry))
            {
                return TryReplay(
                    token,
                    requestedRoleId,
                    _utcNow(),
                    out userId,
                    out roleId,
                    out rotatedToken,
                    out replayed,
                    out errorMessage);
            }

            DateTime now = _utcNow();
            if (entry.RecoveryExpiresAtUtc == null)
            {
                errorMessage = "Token无效或未进入断线恢复状态";
                return false;
            }

            if (IsRecoveryExpired(entry, now))
            {
                RemoveToken(token, entry);
                errorMessage = "Token无效或已过期";
                return false;
            }

            if (entry.RoleId != requestedRoleId)
            {
                errorMessage = "Token角色不匹配";
                return false;
            }

            DateTime replayExpiresAtUtc = entry.RecoveryExpiresAtUtc.Value;
            RemoveOwnerReplays(entry.UserId, entry.RoleId);
            RemoveToken(token, entry);
            userId = entry.UserId;
            roleId = entry.RoleId;
            rotatedToken = AddOnlineToken(userId, roleId);
            _replaysByToken[token] = new ReconnectReplayEntry(
                userId,
                roleId,
                rotatedToken,
                replayExpiresAtUtc);
            return true;
        }
    }

    public void Invalidate(long userId, long roleId)
    {
        lock (_syncRoot)
        {
            RemoveOwnerToken(userId, roleId);
        }
    }

    public void InvalidateUser(long userId)
    {
        if (userId <= 0L)
        {
            throw new ArgumentOutOfRangeException(nameof(userId), "用户 ID 必须为正数");
        }

        lock (_syncRoot)
        {
            List<(long UserId, long RoleId)> owners = new List<(long UserId, long RoleId)>();
            foreach ((long UserId, long RoleId) owner in _tokensByOwner.Keys)
            {
                if (owner.UserId == userId)
                {
                    owners.Add(owner);
                }
            }

            for (int index = 0; index < owners.Count; index++)
            {
                (long ownerUserId, long ownerRoleId) = owners[index];
                RemoveOwnerToken(ownerUserId, ownerRoleId);
            }
        }
    }

    private string AddOnlineToken(long userId, long roleId)
    {
        string token;
        do
        {
            byte[] bytes = RandomNumberGenerator.GetBytes(TokenByteCount);
            token = Convert.ToBase64String(bytes);
        }
        while (_entriesByToken.ContainsKey(token) || _replaysByToken.ContainsKey(token));

        ReconnectTokenEntry entry = new ReconnectTokenEntry(userId, roleId, null);
        _entriesByToken.Add(token, entry);
        _tokensByOwner[(userId, roleId)] = token;
        return token;
    }

    private void RemoveOwnerToken(long userId, long roleId)
    {
        if (_tokensByOwner.Remove((userId, roleId), out string? existingToken))
        {
            _entriesByToken.Remove(existingToken);
        }

        RemoveOwnerReplays(userId, roleId);
    }

    private void RemoveToken(string token, ReconnectTokenEntry entry)
    {
        _entriesByToken.Remove(token);
        if (_tokensByOwner.TryGetValue((entry.UserId, entry.RoleId), out string? ownerToken) &&
            string.Equals(ownerToken, token, StringComparison.Ordinal))
        {
            _tokensByOwner.Remove((entry.UserId, entry.RoleId));
        }
    }

    private bool TryReplay(
        string token,
        long requestedRoleId,
        DateTime now,
        out long userId,
        out long roleId,
        out string rotatedToken,
        out bool replayed,
        out string errorMessage)
    {
        userId = 0L;
        roleId = 0L;
        rotatedToken = string.Empty;
        replayed = false;
        errorMessage = string.Empty;

        if (!_replaysByToken.TryGetValue(token, out ReconnectReplayEntry? replay) ||
            now >= replay.ExpiresAtUtc)
        {
            _replaysByToken.Remove(token);
            errorMessage = "Token无效或已过期";
            return false;
        }

        if (replay.RoleId != requestedRoleId)
        {
            errorMessage = "Token角色不匹配";
            return false;
        }

        if (!_tokensByOwner.TryGetValue((replay.UserId, replay.RoleId), out string? ownerToken) ||
            !string.Equals(ownerToken, replay.RotatedToken, StringComparison.Ordinal) ||
            !_entriesByToken.TryGetValue(replay.RotatedToken, out ReconnectTokenEntry? rotatedEntry))
        {
            _replaysByToken.Remove(token);
            errorMessage = "Token无效或已过期";
            return false;
        }

        _entriesByToken[replay.RotatedToken] = rotatedEntry with { RecoveryExpiresAtUtc = null };
        userId = replay.UserId;
        roleId = replay.RoleId;
        rotatedToken = replay.RotatedToken;
        replayed = true;
        return true;
    }

    private void RemoveOwnerReplays(long userId, long roleId)
    {
        List<string>? staleTokens = null;
        foreach ((string token, ReconnectReplayEntry replay) in _replaysByToken)
        {
            if (replay.UserId == userId && replay.RoleId == roleId)
            {
                staleTokens ??= new List<string>();
                staleTokens.Add(token);
            }
        }

        if (staleTokens == null)
        {
            return;
        }

        for (int index = 0; index < staleTokens.Count; index++)
        {
            _replaysByToken.Remove(staleTokens[index]);
        }
    }

    private static void ValidateOwner(long userId, long roleId)
    {
        if (userId <= 0L)
        {
            throw new ArgumentOutOfRangeException(nameof(userId), "用户 ID 必须为正数");
        }

        if (roleId <= 0L)
        {
            throw new ArgumentOutOfRangeException(nameof(roleId), "角色 ID 必须为正数");
        }
    }

    private static bool IsRecoveryExpired(ReconnectTokenEntry entry, DateTime now)
    {
        return entry.RecoveryExpiresAtUtc is DateTime expiresAtUtc && now >= expiresAtUtc;
    }

    private sealed record ReconnectTokenEntry(
        long UserId,
        long RoleId,
        DateTime? RecoveryExpiresAtUtc);

    private sealed record ReconnectReplayEntry(
        long UserId,
        long RoleId,
        string RotatedToken,
        DateTime ExpiresAtUtc);
}
