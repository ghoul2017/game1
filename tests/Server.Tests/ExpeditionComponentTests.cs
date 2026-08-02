using System.Reflection;
using Game.Components.Expedition;
using Game.Components.Player;
using Shared.Common.Protos;
using Shared.Core.ECS;
using Xunit;

namespace Server.Tests;

public sealed class ExpeditionComponentTests
{
    [Fact]
    public void StartRequestIsIdempotentAndCannotCreateAnotherRunWithinRetentionWindow()
    {
        FakeExpeditionSimulationFactory factory = new FakeExpeditionSimulationFactory(1L, 50L, 1);
        FakeExpeditionClock clock = new FakeExpeditionClock { UtcNowMilliseconds = 100L };
        ExpeditionComponent expedition = CreateComponent(factory, clock);
        PlayerEntity player = CreatePlayer();

        Assert.True(
            expedition.TryStart(
                player,
                17L,
                out ExpeditionSnapshotData first,
                out ExpeditionProgressData firstProgress,
                out string firstError));
        Assert.Equal(string.Empty, firstError);
        Assert.Equal(1L, firstProgress.LastStartedRunIndex);

        Assert.True(
            expedition.TryStart(
                player,
                17L,
                out ExpeditionSnapshotData repeated,
                out ExpeditionProgressData repeatedProgress,
                out string repeatedError));
        Assert.Equal(string.Empty, repeatedError);
        Assert.Equal(first.RunId, repeated.RunId);
        Assert.Equal(1L, repeatedProgress.LastStartedRunIndex);

        Assert.False(
            expedition.TryStart(
                player,
                18L,
                out ExpeditionSnapshotData ignoredActiveSnapshot,
                out ExpeditionProgressData activeProgress,
                out string activeError));
        Assert.Equal("该账号已有远征，请先恢复当前局", activeError);
        Assert.Equal(1L, activeProgress.LastStartedRunIndex);

        expedition.DetachRole(player.Id);
        InvokeTick(expedition);
        Assert.True(
            expedition.TrySettle(
                player,
                first.RunId,
                out ExpeditionSettlementData ignoredSettlement,
                out ExpeditionProgressData settledProgress,
                out string settleError));
        Assert.Equal(string.Empty, settleError);
        Assert.Equal(1L, settledProgress.LastStartedRunIndex);

        Assert.False(
            expedition.TryStart(
                player,
                17L,
                out ExpeditionSnapshotData ignoredSettledSnapshot,
                out ExpeditionProgressData retainedProgress,
                out string retainedError));
        Assert.Equal("该远征启动请求已完成", retainedError);
        Assert.Equal(1L, retainedProgress.LastStartedRunIndex);

        Assert.True(
            expedition.TryStart(
                player,
                19L,
                out ExpeditionSnapshotData second,
                out ExpeditionProgressData secondProgress,
                out string secondError));
        Assert.Equal(string.Empty, secondError);
        Assert.NotEqual(first.RunId, second.RunId);
        Assert.Equal(2L, secondProgress.LastStartedRunIndex);
    }

    [Fact]
    public void ResumeRestoresActiveRunCheckpointWhenDatabaseLoadWasStale()
    {
        FakeExpeditionSimulationFactory factory = new FakeExpeditionSimulationFactory();
        FakeExpeditionClock clock = new FakeExpeditionClock();
        ExpeditionComponent expedition = CreateComponent(factory, clock);
        PlayerEntity original = CreatePlayer();
        original.GetComponent<PlayerExpeditionComponent>().ResourceBalance = 25L;

        Assert.True(
            expedition.TryStart(
                original,
                1L,
                out ExpeditionSnapshotData started,
                out ExpeditionProgressData startedProgress,
                out string startError));
        Assert.Equal(string.Empty, startError);
        Assert.Equal(1L, startedProgress.LastStartedRunIndex);

        PlayerEntity staleReload = CreatePlayer();
        Assert.True(
            expedition.TryResume(
                staleReload,
                started.RunId,
                out bool hasActiveRun,
                out ExpeditionSnapshotData resumed,
                out ExpeditionSettlementData ignoredSettlement,
                out ExpeditionProgressData resumedProgress,
                out string resumeError));
        Assert.Equal(string.Empty, resumeError);
        Assert.True(hasActiveRun);
        Assert.Equal(started.RunId, resumed.RunId);
        Assert.Equal(25L, resumedProgress.ResourceBalance);
        Assert.Equal(1L, resumedProgress.LastStartedRunIndex);
    }

    [Fact]
    public void SettledReceiptSurvivesImmediateReconnectBeforeDetachedSaveCompletes()
    {
        FakeExpeditionSimulationFactory factory = new FakeExpeditionSimulationFactory(1L, 75L, 1);
        FakeExpeditionClock clock = new FakeExpeditionClock { UtcNowMilliseconds = 100L };
        ExpeditionComponent expedition = CreateComponent(factory, clock);
        PlayerEntity original = CreatePlayer();
        original.GetComponent<PlayerExpeditionComponent>().ResourceBalance = 25L;
        Assert.True(
            expedition.TryStart(
                original,
                1L,
                out ExpeditionSnapshotData started,
                out ExpeditionProgressData ignoredProgress,
                out string startError));
        Assert.Equal(string.Empty, startError);

        expedition.DetachRole(original.Id);
        InvokeTick(expedition);
        Assert.True(
            expedition.TrySettle(
                original,
                started.RunId,
                out ExpeditionSettlementData first,
                out ExpeditionProgressData firstProgress,
                out string settleError));
        Assert.Equal(string.Empty, settleError);
        Assert.Equal(100L, firstProgress.ResourceBalance);
        Assert.Equal(0, expedition.ActiveRunCount);

        Assert.True(
            expedition.TryUpgrade(
                original,
                ExpeditionProtocolConstants.FacilityId,
                out ExpeditionUpgradeRes upgraded,
                out string upgradeError));
        Assert.Equal(string.Empty, upgradeError);
        Assert.Equal(70L, upgraded.Progress.ResourceBalance);
        Assert.Equal(1, upgraded.Progress.FacilityLevel);

        PlayerEntity staleReload = CreatePlayer();
        Assert.True(
            expedition.TrySettle(
                staleReload,
                started.RunId,
                out ExpeditionSettlementData repeated,
                out ExpeditionProgressData repeatedProgress,
                out string repeatError));
        Assert.Equal(string.Empty, repeatError);
        Assert.Equal(first.ResourceBalance, repeated.ResourceBalance);
        Assert.Equal(first.FacilityLevel, repeated.FacilityLevel);
        Assert.Equal(70L, repeatedProgress.ResourceBalance);
        Assert.Equal(1, repeatedProgress.FacilityLevel);
        Assert.Equal(started.RunId, repeatedProgress.LastSettledRunId);
    }

    [Fact]
    public void ResumeWithoutRunIdRestoresLatestPermanentStateBeforeStartingNextRun()
    {
        FakeExpeditionSimulationFactory factory = new FakeExpeditionSimulationFactory(1L, 75L, 1);
        FakeExpeditionClock clock = new FakeExpeditionClock { UtcNowMilliseconds = 100L };
        ExpeditionComponent expedition = CreateComponent(factory, clock);
        PlayerEntity original = CreatePlayer();
        original.GetComponent<PlayerExpeditionComponent>().ResourceBalance = 25L;

        Assert.True(
            expedition.TryStart(
                original,
                1L,
                out ExpeditionSnapshotData started,
                out ExpeditionProgressData ignoredProgress,
                out string startError));
        Assert.Equal(string.Empty, startError);
        expedition.DetachRole(original.Id);
        InvokeTick(expedition);
        Assert.True(
            expedition.TrySettle(
                original,
                started.RunId,
                out ExpeditionSettlementData ignoredSettlement,
                out ExpeditionProgressData settledProgress,
                out string settleError));
        Assert.Equal(string.Empty, settleError);
        Assert.Equal(100L, settledProgress.ResourceBalance);
        Assert.True(
            expedition.TryUpgrade(
                original,
                ExpeditionProtocolConstants.FacilityId,
                out ExpeditionUpgradeRes upgraded,
                out string upgradeError));
        Assert.Equal(string.Empty, upgradeError);
        Assert.Equal(70L, upgraded.Progress.ResourceBalance);
        Assert.Equal(1, upgraded.Progress.FacilityLevel);

        PlayerEntity staleReload = CreatePlayer();
        Assert.True(
            expedition.TryResume(
                staleReload,
                0L,
                out bool hasActiveRun,
                out ExpeditionSnapshotData ignoredSnapshot,
                out ExpeditionSettlementData ignoredReceipt,
                out ExpeditionProgressData resumedProgress,
                out string resumeError));
        Assert.Equal(string.Empty, resumeError);
        Assert.False(hasActiveRun);
        Assert.Equal(70L, resumedProgress.ResourceBalance);
        Assert.Equal(1, resumedProgress.FacilityLevel);
        Assert.Equal(1L, resumedProgress.LastStartedRunIndex);
        Assert.Equal(started.RunId, resumedProgress.LastSettledRunId);

        Assert.True(
            expedition.TryStart(
                staleReload,
                2L,
                out ExpeditionSnapshotData nextRun,
                out ExpeditionProgressData nextProgress,
                out string nextStartError));
        Assert.Equal(string.Empty, nextStartError);
        Assert.Equal(2L, nextRun.RunIndex);
        Assert.Equal(70L, nextProgress.ResourceBalance);
        Assert.Equal(1, nextProgress.FacilityLevel);
        Assert.Equal(2L, nextProgress.LastStartedRunIndex);
        Assert.Equal(started.RunId, nextProgress.LastSettledRunId);
    }

    [Fact]
    public void OlderRetainedReceiptCannotRollBackNewerSettlementThroughResumeOrSettle()
    {
        FakeExpeditionSimulationFactory factory = new FakeExpeditionSimulationFactory(1L, 50L, 1);
        FakeExpeditionClock clock = new FakeExpeditionClock { UtcNowMilliseconds = 100L };
        ExpeditionComponent expedition = CreateComponent(factory, clock);
        PlayerEntity player = CreatePlayer();

        ExpeditionSnapshotData firstRun = StartFinishAndSettle(expedition, player, 1L);
        ExpeditionSnapshotData secondRun = StartFinishAndSettle(expedition, player, 2L);
        PlayerExpeditionComponent permanent = player.GetComponent<PlayerExpeditionComponent>();
        long expectedRevision = permanent.ProgressRevision;
        long expectedBalance = permanent.ResourceBalance;
        Assert.Equal(secondRun.RunId, permanent.LastSettledRunId);

        Assert.True(
            expedition.TryResume(
                player,
                firstRun.RunId,
                out bool hasActiveRun,
                out ExpeditionSnapshotData ignoredSnapshot,
                out ExpeditionSettlementData resumedReceipt,
                out ExpeditionProgressData resumeProgress,
                out string resumeError));
        Assert.Equal(string.Empty, resumeError);
        Assert.False(hasActiveRun);
        Assert.Equal(firstRun.RunId, resumedReceipt.RunId);
        Assert.Equal(secondRun.RunId, resumeProgress.LastSettledRunId);
        Assert.Equal(expectedBalance, resumeProgress.ResourceBalance);
        Assert.Equal(expectedRevision, permanent.ProgressRevision);

        Assert.True(
            expedition.TrySettle(
                player,
                firstRun.RunId,
                out ExpeditionSettlementData settledReceipt,
                out ExpeditionProgressData settleProgress,
                out string settleError));
        Assert.Equal(string.Empty, settleError);
        Assert.Equal(firstRun.RunId, settledReceipt.RunId);
        Assert.Equal(secondRun.RunId, settleProgress.LastSettledRunId);
        Assert.Equal(expectedBalance, settleProgress.ResourceBalance);
        Assert.Equal(expectedRevision, permanent.ProgressRevision);
        Assert.Equal(secondRun.RunId, permanent.LastSettledRunId);
    }

    private static ExpeditionComponent CreateComponent(
        IExpeditionSimulationFactory factory,
        IExpeditionClock clock)
    {
        Shared.Core.Entities.Module module = Entity.CreateEntity<Shared.Core.Entities.Module>();
        ExpeditionComponent expedition = module.AddComponent<ExpeditionComponent>();
        expedition.Configure(factory, clock, retentionMilliseconds: 500L);
        return expedition;
    }

    private static PlayerEntity CreatePlayer()
    {
        return PlayerEntity.Create(20L, 10L, "test", 1, 1);
    }

    private static ExpeditionSnapshotData StartFinishAndSettle(
        ExpeditionComponent expedition,
        PlayerEntity player,
        long requestId)
    {
        Assert.True(
            expedition.TryStart(
                player,
                requestId,
                out ExpeditionSnapshotData started,
                out ExpeditionProgressData ignoredProgress,
                out string startError));
        Assert.Equal(string.Empty, startError);
        expedition.DetachRole(player.Id);
        InvokeTick(expedition);
        Assert.True(
            expedition.TrySettle(
                player,
                started.RunId,
                out ExpeditionSettlementData settlement,
                out ExpeditionProgressData settledProgress,
                out string settleError));
        Assert.Equal(string.Empty, settleError);
        Assert.Equal(started.RunId, settlement.RunId);
        Assert.Equal(started.RunId, settledProgress.LastSettledRunId);
        return started;
    }

    private static void InvokeTick(ExpeditionComponent expedition)
    {
        MethodInfo method = typeof(ExpeditionComponent).GetMethod(
            "TickAll",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        method.Invoke(expedition, null);
    }
}
