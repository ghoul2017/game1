using System;
using Game.Components.User;
using Xunit;

namespace Server.Tests;

public sealed class ReconnectTokenRegistryTests
{
    [Fact]
    public void SuccessfulConsumeChecksRoleAndRotatesSingleUseToken()
    {
        DateTime now = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        ReconnectTokenRegistry registry = new ReconnectTokenRegistry(TimeSpan.FromMinutes(5.0), () => now);
        string token = registry.Issue(10L, 20L);
        Assert.True(registry.BeginRecoveryWindow(10L, 20L, token));

        Assert.False(
            registry.TryConsumeAndRotate(
                token,
                21L,
                out long ignoredUserId,
                out long ignoredRoleId,
                out string ignoredRotatedToken,
                out string roleError));
        Assert.Contains("角色", roleError);

        Assert.True(
            registry.TryConsumeAndRotate(
                token,
                20L,
                out long userId,
                out long roleId,
                out string rotatedToken,
                out string errorMessage));
        Assert.Equal(string.Empty, errorMessage);
        Assert.Equal(10L, userId);
        Assert.Equal(20L, roleId);
        Assert.NotEqual(token, rotatedToken);

        Assert.True(
            registry.TryConsumeAndRotate(
                token,
                20L,
                out ignoredUserId,
                out ignoredRoleId,
                out string replayedRotatedToken,
                out bool replayed,
                out string replayError));
        Assert.Equal(string.Empty, replayError);
        Assert.True(replayed);
        Assert.Equal(rotatedToken, replayedRotatedToken);

        Assert.Equal(rotatedToken, registry.IssueOrReuse(userId, roleId, rotatedToken));
        Assert.False(TryConsume(registry, token, roleId));
    }

    [Fact]
    public void OnlineTokenCannotBeConsumedBeforeAuthoritativeDisconnect()
    {
        DateTime now = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        ReconnectTokenRegistry registry = new ReconnectTokenRegistry(TimeSpan.FromMinutes(5.0), () => now);
        string token = registry.Issue(10L, 20L);
        now = now.AddHours(2.0);

        Assert.False(
            registry.TryConsumeAndRotate(
                token,
                20L,
                out long userId,
                out long roleId,
                out string rotatedToken,
                out string errorMessage));
        Assert.Equal(0L, userId);
        Assert.Equal(0L, roleId);
        Assert.Equal(string.Empty, rotatedToken);
        Assert.Contains("断线恢复", errorMessage);
    }

    [Fact]
    public void OnlineTokenOlderThanLifetimeCanRecoverWithinWindowAfterDisconnect()
    {
        DateTime now = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        ReconnectTokenRegistry registry = new ReconnectTokenRegistry(TimeSpan.FromMinutes(5.0), () => now);
        string token = registry.Issue(10L, 20L);

        now = now.AddHours(2.0);
        Assert.True(registry.BeginRecoveryWindow(10L, 20L, token));
        now = now.AddMinutes(4.0);

        Assert.True(
            registry.TryConsumeAndRotate(
                token,
                20L,
                out long userId,
                out long roleId,
                out string rotatedToken,
                out string errorMessage));
        Assert.Equal(string.Empty, errorMessage);
        Assert.Equal(10L, userId);
        Assert.Equal(20L, roleId);
        Assert.NotEqual(token, rotatedToken);
    }

    [Fact]
    public void RecoveryWindowRejectsTokenAtDeadlineAndCannotBeExtendedTwice()
    {
        DateTime now = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        ReconnectTokenRegistry registry = new ReconnectTokenRegistry(TimeSpan.FromSeconds(30.0), () => now);
        string token = registry.Issue(10L, 20L);

        Assert.True(registry.BeginRecoveryWindow(10L, 20L, token));
        now = now.AddSeconds(20.0);
        Assert.False(registry.BeginRecoveryWindow(10L, 20L, token));
        now = now.AddSeconds(10.0);

        Assert.False(
            registry.TryConsumeAndRotate(
                token,
                20L,
                out long userId,
                out long roleId,
                out string rotatedToken,
                out string errorMessage));
        Assert.Equal(0L, userId);
        Assert.Equal(0L, roleId);
        Assert.Equal(string.Empty, rotatedToken);
        Assert.Contains("过期", errorMessage);
    }

    [Fact]
    public void StaleSessionTokenCannotExpireOrReplaceTheRotatedOnlineToken()
    {
        DateTime now = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        ReconnectTokenRegistry registry = new ReconnectTokenRegistry(TimeSpan.FromMinutes(5.0), () => now);
        string disconnectedToken = registry.Issue(10L, 20L);
        Assert.True(registry.BeginRecoveryWindow(10L, 20L, disconnectedToken));
        now = now.AddMinutes(1.0);
        Assert.True(
            registry.TryConsumeAndRotate(
                disconnectedToken,
                20L,
                out long userId,
                out long roleId,
                out string onlineToken,
                out string consumeError));
        Assert.Equal(string.Empty, consumeError);

        Assert.False(registry.BeginRecoveryWindow(userId, roleId, disconnectedToken));
        now = now.AddHours(1.0);

        Assert.False(TryConsume(registry, onlineToken, roleId));
        Assert.False(TryConsume(registry, disconnectedToken, roleId));
        Assert.True(registry.BeginRecoveryWindow(userId, roleId, onlineToken));
        Assert.True(TryConsume(registry, onlineToken, roleId));
    }

    [Fact]
    public void NewLoginCanInvalidateEveryRoleTokenForUser()
    {
        DateTime now = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        ReconnectTokenRegistry registry = new ReconnectTokenRegistry(TimeSpan.FromMinutes(5.0), () => now);
        string firstRoleToken = registry.Issue(10L, 20L);
        string secondRoleToken = registry.Issue(10L, 21L);
        string otherUserToken = registry.Issue(11L, 30L);

        registry.InvalidateUser(10L);
        Assert.True(registry.BeginRecoveryWindow(11L, 30L, otherUserToken));

        Assert.False(TryConsume(registry, firstRoleToken, 20L));
        Assert.False(TryConsume(registry, secondRoleToken, 21L));
        Assert.True(TryConsume(registry, otherUserToken, 30L));
    }

    [Fact]
    public void EnterGameReusesRotatedReconnectToken()
    {
        DateTime now = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        ReconnectTokenRegistry registry = new ReconnectTokenRegistry(TimeSpan.FromMinutes(5.0), () => now);
        string originalToken = registry.Issue(10L, 20L);
        Assert.True(registry.BeginRecoveryWindow(10L, 20L, originalToken));

        Assert.True(
            registry.TryConsumeAndRotate(
                originalToken,
                20L,
                out long userId,
                out long roleId,
                out string rotatedToken,
                out string errorMessage));
        Assert.Equal(string.Empty, errorMessage);

        now = now.AddHours(1.0);
        string enterGameToken = registry.IssueOrReuse(userId, roleId, rotatedToken);

        Assert.Equal(rotatedToken, enterGameToken);
        Assert.False(TryConsume(registry, enterGameToken, roleId));
        Assert.True(registry.BeginRecoveryWindow(userId, roleId, enterGameToken));
        Assert.True(TryConsume(registry, enterGameToken, roleId));
    }

    [Fact]
    public void EnterGameReplacesUnregisteredLoginToken()
    {
        DateTime now = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        ReconnectTokenRegistry registry = new ReconnectTokenRegistry(TimeSpan.FromMinutes(5.0), () => now);

        string reconnectToken = registry.IssueOrReuse(10L, 20L, "signed-login-token");

        Assert.NotEqual("signed-login-token", reconnectToken);
        Assert.True(registry.BeginRecoveryWindow(10L, 20L, reconnectToken));
        Assert.True(TryConsume(registry, reconnectToken, 20L));
    }

    private static bool TryConsume(ReconnectTokenRegistry registry, string token, long roleId)
    {
        return registry.TryConsumeAndRotate(
            token,
            roleId,
            out _,
            out _,
            out _,
            out _);
    }
}
