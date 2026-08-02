using Game.Components.Expedition;
using Game.Components.Player;
using Shared.Common.Protos;
using Xunit;

namespace Server.Tests;

public sealed class PlayerExpeditionSystemTests
{
    [Fact]
    public void SettlementConsumesRunBeforeAddingResourceAndIsIdempotent()
    {
        PlayerExpeditionComponent progress = CreateProgress();
        progress.ResourceBalance = 25L;
        ExpeditionAuthoritativeResult result = CreateResult(100L, 1, 75L);

        Assert.True(progress.TryCommitSettlement(result, out ExpeditionSettlementData first, out string firstError));
        Assert.Equal(string.Empty, firstError);
        Assert.Equal(100L, progress.LastSettledRunId);
        Assert.Equal(100L, progress.ResourceBalance);
        Assert.Equal(75L, first.AwardedResource);

        Assert.True(
            progress.TryCommitSettlement(
                result,
                out ExpeditionSettlementData repeated,
                out string repeatError));
        Assert.Equal(string.Empty, repeatError);
        Assert.Equal(first.ResourceBalance, repeated.ResourceBalance);
        Assert.Equal(100L, progress.ResourceBalance);
    }

    [Fact]
    public void InvalidOrOverflowingSettlementLeavesPermanentStateUnchanged()
    {
        PlayerExpeditionComponent progress = CreateProgress();
        progress.ResourceBalance = long.MaxValue;

        Assert.False(
            progress.TryCommitSettlement(
                CreateResult(100L, 1, 1L),
                out ExpeditionSettlementData settlement,
                out string errorMessage));
        Assert.Equal(0L, settlement.RunId);
        Assert.NotEmpty(errorMessage);
        Assert.Equal(0L, progress.LastSettledRunId);
        Assert.Equal(long.MaxValue, progress.ResourceBalance);
    }

    [Fact]
    public void UpgradePrechecksThenSubtractsBeforeIncreasingLevel()
    {
        PlayerExpeditionComponent progress = CreateProgress();
        progress.ResourceBalance = 100L;

        Assert.False(
            progress.TryUpgradeFacility(
                ExpeditionProtocolConstants.FacilityId,
                100L,
                true,
                out ExpeditionUpgradeRes blocked,
                out string blockedError));
        Assert.NotEmpty(blockedError);
        Assert.Equal(100L, progress.ResourceBalance);
        Assert.Equal(0, progress.FacilityLevel);

        Assert.True(
            progress.TryUpgradeFacility(
                ExpeditionProtocolConstants.FacilityId,
                100L,
                false,
                out ExpeditionUpgradeRes upgraded,
                out string upgradedError));
        Assert.Equal(string.Empty, upgradedError);
        Assert.Equal(100L, upgraded.SpentResource);
        Assert.Equal(0L, progress.ResourceBalance);
        Assert.Equal(1, progress.FacilityLevel);
        Assert.Equal(100L, upgraded.Progress.UpgradeCost);
        Assert.Equal(PlayerExpeditionSystem.MaximumFacilityLevel, upgraded.Progress.MaximumFacilityLevel);
        Assert.Equal(
            GameplayCoreExpeditionSimulationFactory.GetRepresentativeDamage(0),
            upgraded.OldRepresentativeDamage);
        Assert.Equal(
            GameplayCoreExpeditionSimulationFactory.GetRepresentativeDamage(1),
            upgraded.NewRepresentativeDamage);
        Assert.True(upgraded.NewRepresentativeDamage > upgraded.OldRepresentativeDamage);
        Assert.Equal(upgraded.NewRepresentativeDamage, upgraded.Progress.RepresentativeDamage);
    }

    [Fact]
    public void SettlementReceiptDoesNotDriftAfterFacilityUpgrade()
    {
        PlayerExpeditionComponent progress = CreateProgress();
        progress.ResourceBalance = 100L;
        ExpeditionAuthoritativeResult result = CreateResult(100L, 1, 75L);

        Assert.True(
            progress.TryCommitSettlement(
                result,
                out ExpeditionSettlementData first,
                out string firstError));
        Assert.Equal(string.Empty, firstError);
        Assert.Equal(175L, first.ResourceBalance);
        Assert.Equal(0, first.FacilityLevel);

        Assert.True(
            progress.TryUpgradeFacility(
                ExpeditionProtocolConstants.FacilityId,
                100L,
                false,
                out ExpeditionUpgradeRes upgraded,
                out string upgradeError));
        Assert.Equal(string.Empty, upgradeError);
        Assert.Equal(1, upgraded.NewFacilityLevel);
        Assert.Equal(75L, progress.ResourceBalance);

        Assert.True(
            progress.TryCommitSettlement(
                result,
                out ExpeditionSettlementData repeated,
                out string repeatError));
        Assert.Equal(string.Empty, repeatError);
        Assert.Equal(first.ResourceBalance, repeated.ResourceBalance);
        Assert.Equal(first.FacilityLevel, repeated.FacilityLevel);
        Assert.Equal(0, repeated.FacilityLevel);
        Assert.Equal(1, progress.FacilityLevel);
        Assert.Equal(75L, progress.ResourceBalance);
    }

    [Fact]
    public void OlderRunCannotReplaceLatestSettlement()
    {
        PlayerExpeditionComponent progress = CreateProgress();
        Assert.True(
            progress.TryCommitSettlement(
                CreateResult(200L, 1, 10L),
                out ExpeditionSettlementData ignored,
                out string ignoredError));

        Assert.False(
            progress.TryCommitSettlement(
                CreateResult(199L, 1, 1000L),
                out ExpeditionSettlementData rejected,
                out string errorMessage));
        Assert.NotEmpty(errorMessage);
        Assert.Equal(0L, rejected.RunId);
        Assert.Equal(10L, progress.ResourceBalance);
    }

    private static PlayerExpeditionComponent CreateProgress()
    {
        PlayerEntity player = PlayerEntity.Create(10L, 20L, "test", 1, 1);
        return player.GetComponent<PlayerExpeditionComponent>();
    }

    private static ExpeditionAuthoritativeResult CreateResult(long runId, int phase, long grossResource)
    {
        return new ExpeditionAuthoritativeResult
        {
            RunId = runId,
            Phase = phase,
            GrossResource = grossResource,
            EndTick = 10L
        };
    }
}
