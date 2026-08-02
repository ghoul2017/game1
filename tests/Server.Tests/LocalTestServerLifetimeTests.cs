using System;
using System.Collections.Generic;
using Game;
using Xunit;

namespace Server.Tests;

public sealed class LocalTestServerLifetimeTests
{
    private const string Token = "0123456789abcdef0123456789abcdef";

    [Fact]
    public void NoDedicatedEnvironmentVariablesLeavesNormalServerUnchanged()
    {
        Dictionary<string, string?> environment = new();

        bool enabled = LocalTestServerLifetime.TryReadConfiguration(environment, out _);

        Assert.False(enabled);
    }

    [Fact]
    public void PartialDedicatedEnvironmentVariablesAreRejected()
    {
        Dictionary<string, string?> environment = new()
        {
            [LocalTestServerLifetime.ParentProcessIdEnvironmentVariable] = "123"
        };

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => LocalTestServerLifetime.TryReadConfiguration(environment, out _));

        Assert.Contains("必须全部提供", exception.Message);
    }

    [Fact]
    public void MatchingUnityParentAndControlFileKeepServerRunning()
    {
        LocalTestServerConfiguration configuration = CreateConfiguration();
        string controlJson = CreateControlJson("run", Token);

        LocalTestServerStopReason reason = LocalTestServerLifetime.Evaluate(
            configuration,
            _ => new LocalTestServerParentProcess("Unity", 456L),
            _ => controlJson);

        Assert.Equal(LocalTestServerStopReason.None, reason);
    }

    [Theory]
    [InlineData("stop", "0123456789abcdef0123456789abcdef", (int)LocalTestServerStopReason.StopRequested)]
    [InlineData("run", "abcdef0123456789abcdef0123456789", (int)LocalTestServerStopReason.ControlTokenMismatch)]
    public void StopOrTokenChangeEndsOwnership(
        string requestedState,
        string controlToken,
        int expectedReasonValue)
    {
        LocalTestServerConfiguration configuration = CreateConfiguration();
        string controlJson = CreateControlJson(requestedState, controlToken);

        LocalTestServerStopReason reason = LocalTestServerLifetime.Evaluate(
            configuration,
            _ => new LocalTestServerParentProcess("Unity", 456L),
            _ => controlJson);

        Assert.Equal((LocalTestServerStopReason)expectedReasonValue, reason);
    }

    [Theory]
    [InlineData("dotnet", 456L, (int)LocalTestServerStopReason.ParentIsNotUnity)]
    [InlineData("Unity", 789L, (int)LocalTestServerStopReason.ParentProcessReused)]
    public void ParentIdentityMustMatchUnityProcess(
        string processName,
        long startTimeUtcTicks,
        int expectedReasonValue)
    {
        LocalTestServerConfiguration configuration = CreateConfiguration();

        LocalTestServerStopReason reason = LocalTestServerLifetime.Evaluate(
            configuration,
            _ => new LocalTestServerParentProcess(processName, startTimeUtcTicks),
            _ => CreateControlJson("run", Token));

        Assert.Equal((LocalTestServerStopReason)expectedReasonValue, reason);
    }

    private static LocalTestServerConfiguration CreateConfiguration()
    {
        return new LocalTestServerConfiguration(123, 456L, Token, "C:\\temp\\game1-control.json");
    }

    private static string CreateControlJson(string requestedState, string token)
    {
        return
            "{" +
            "\"Version\":1," +
            "\"ParentProcessId\":123," +
            "\"ParentStartTimeUtcTicks\":456," +
            $"\"Token\":\"{token}\"," +
            $"\"RequestedState\":\"{requestedState}\"," +
            "\"UpdatedAtUtcTicks\":1" +
            "}";
    }
}
