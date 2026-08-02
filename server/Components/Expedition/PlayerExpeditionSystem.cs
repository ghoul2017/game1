using System;
using Game.Components.Player;
using Shared.Common.Protos;

namespace Game.Components.Expedition;

/// <summary>
/// 玩家远征永久状态的唯一结算和升级写入口。
/// </summary>
public static class PlayerExpeditionSystem
{
    public const int MaximumFacilityLevel = 1;

    public static ExpeditionProgressData ToProtocol(this PlayerExpeditionComponent self, long upgradeCost)
    {
        if (upgradeCost <= 0L)
        {
            throw new ArgumentOutOfRangeException(nameof(upgradeCost));
        }

        return new ExpeditionProgressData
        {
            ResourceBalance = self.ResourceBalance,
            FacilityLevel = self.FacilityLevel,
            LastStartedRunIndex = self.LastStartedRunIndex,
            LastSettledRunId = self.LastSettledRunId,
            UpgradeCost = upgradeCost,
            MaximumFacilityLevel = MaximumFacilityLevel,
            RepresentativeDamage = GameplayCoreExpeditionSimulationFactory.GetRepresentativeDamage(
                self.FacilityLevel)
        };
    }

    public static ExpeditionSettlementData GetLastSettlement(this PlayerExpeditionComponent self)
    {
        if (self.LastSettledRunId == 0L)
        {
            return new ExpeditionSettlementData();
        }

        return new ExpeditionSettlementData
        {
            RunId = self.LastSettledRunId,
            Phase = self.LastSettlementPhase,
            GrossResource = self.LastSettlementGrossResource,
            AwardedResource = self.LastSettlementAwardedResource,
            ResourceBalance = self.LastSettlementBalance,
            FacilityLevel = self.LastSettlementFacilityLevel,
            EndTick = self.LastSettlementEndTick
        };
    }

    public static bool TryCommitSettlement(
        this PlayerExpeditionComponent self,
        ExpeditionAuthoritativeResult result,
        out ExpeditionSettlementData settlement,
        out string errorMessage)
    {
        settlement = new ExpeditionSettlementData();
        errorMessage = string.Empty;

        if (result == null)
        {
            errorMessage = "远征结果不存在";
            return false;
        }

        if (self.LastSettledRunId == result.RunId)
        {
            settlement = self.GetLastSettlement();
            return true;
        }

        if (self.LastSettledRunId != 0L && result.RunId < self.LastSettledRunId)
        {
            errorMessage = "远征结果早于最近结算";
            return false;
        }

        if (result.RunId <= 0L || result.EndTick < 0L || result.GrossResource < 0L)
        {
            errorMessage = "远征结果非法";
            return false;
        }

        if (result.Phase != 1 && result.Phase != 2)
        {
            errorMessage = "远征尚未进入终态";
            return false;
        }

        // 技术竖切的失败保留率按 T2 临时规则为 100%；正式经济另行配置。
        long awardedResource = result.GrossResource;
        long expectedBalance;
        long expectedRevision;
        try
        {
            expectedBalance = checked(self.ResourceBalance + awardedResource);
            expectedRevision = checked(self.ProgressRevision + 1L);
        }
        catch (OverflowException)
        {
            errorMessage = "远征永久状态超过可保存范围";
            return false;
        }

        // 先消费本局结算权，再增加永久资源。所有可预期失败均已在写入前排除。
        self.LastSettledRunId = result.RunId;
        self.LastSettlementPhase = result.Phase;
        self.LastSettlementGrossResource = result.GrossResource;
        self.LastSettlementAwardedResource = awardedResource;
        self.LastSettlementEndTick = result.EndTick;
        self.ResourceBalance = expectedBalance;
        self.LastSettlementBalance = expectedBalance;
        self.LastSettlementFacilityLevel = self.FacilityLevel;
        self.ProgressRevision = expectedRevision;
        self.MarkDirty();

        settlement = self.GetLastSettlement();
        return true;
    }

    public static bool TryUpgradeFacility(
        this PlayerExpeditionComponent self,
        int facilityId,
        long upgradeCost,
        bool hasActiveRun,
        out ExpeditionUpgradeRes response,
        out string errorMessage)
    {
        response = new ExpeditionUpgradeRes();
        errorMessage = string.Empty;

        if (facilityId != ExpeditionProtocolConstants.FacilityId)
        {
            errorMessage = "基地设施不存在";
            return false;
        }

        if (upgradeCost <= 0L)
        {
            errorMessage = "基地升级价格非法";
            return false;
        }

        if (hasActiveRun)
        {
            errorMessage = "远征进行中不能升级基地";
            return false;
        }

        if (self.FacilityLevel >= MaximumFacilityLevel)
        {
            errorMessage = "基地设施已达到当前最高等级";
            return false;
        }

        if (self.ResourceBalance < upgradeCost)
        {
            errorMessage = "远征资源不足";
            return false;
        }

        int oldLevel = self.FacilityLevel;
        long expectedBalance = self.ResourceBalance - upgradeCost;
        int expectedLevel = oldLevel + 1;
        int oldRepresentativeDamage = GameplayCoreExpeditionSimulationFactory.GetRepresentativeDamage(oldLevel);
        int newRepresentativeDamage = GameplayCoreExpeditionSimulationFactory.GetRepresentativeDamage(expectedLevel);
        long expectedRevision;
        try
        {
            expectedRevision = checked(self.ProgressRevision + 1L);
        }
        catch (OverflowException)
        {
            errorMessage = "远征永久状态超过可保存范围";
            return false;
        }

        // 先扣资源，再增加设施等级。
        self.ResourceBalance = expectedBalance;
        self.FacilityLevel = expectedLevel;
        self.ProgressRevision = expectedRevision;
        self.MarkDirty();

        response = new ExpeditionUpgradeRes
        {
            Progress = self.ToProtocol(upgradeCost),
            SpentResource = upgradeCost,
            OldFacilityLevel = oldLevel,
            NewFacilityLevel = expectedLevel,
            OldRepresentativeDamage = oldRepresentativeDamage,
            NewRepresentativeDamage = newRepresentativeDamage
        };
        return true;
    }
}

/// <summary>
/// 活动局保留期内的永久远征状态检查点，避免断线重入在旧 DB 保存完成前读到更早版本。
/// </summary>
internal sealed class PlayerExpeditionCheckpoint
{
    private PlayerExpeditionCheckpoint(PlayerExpeditionComponent component, long lastStartedRunIndex)
    {
        ProgressRevision = component.ProgressRevision;
        ResourceBalance = component.ResourceBalance;
        FacilityLevel = component.FacilityLevel;
        LastStartedRunIndex = lastStartedRunIndex;
        LastSettledRunId = component.LastSettledRunId;
        LastSettlementPhase = component.LastSettlementPhase;
        LastSettlementGrossResource = component.LastSettlementGrossResource;
        LastSettlementAwardedResource = component.LastSettlementAwardedResource;
        LastSettlementBalance = component.LastSettlementBalance;
        LastSettlementFacilityLevel = component.LastSettlementFacilityLevel;
        LastSettlementEndTick = component.LastSettlementEndTick;
    }

    private long ProgressRevision { get; }

    private long ResourceBalance { get; }

    private int FacilityLevel { get; }

    private long LastStartedRunIndex { get; }

    private long LastSettledRunId { get; }

    private int LastSettlementPhase { get; }

    private long LastSettlementGrossResource { get; }

    private long LastSettlementAwardedResource { get; }

    private long LastSettlementBalance { get; }

    private int LastSettlementFacilityLevel { get; }

    private long LastSettlementEndTick { get; }

    internal static PlayerExpeditionCheckpoint Capture(
        PlayerExpeditionComponent component,
        long lastStartedRunIndex)
    {
        return new PlayerExpeditionCheckpoint(component, lastStartedRunIndex);
    }

    internal void Restore(PlayerExpeditionComponent component)
    {
        if (component.ProgressRevision >= ProgressRevision)
        {
            return;
        }

        bool changed = component.ProgressRevision != ProgressRevision ||
            component.ResourceBalance != ResourceBalance ||
            component.FacilityLevel != FacilityLevel ||
            component.LastStartedRunIndex != LastStartedRunIndex ||
            component.LastSettledRunId != LastSettledRunId ||
            component.LastSettlementPhase != LastSettlementPhase ||
            component.LastSettlementGrossResource != LastSettlementGrossResource ||
            component.LastSettlementAwardedResource != LastSettlementAwardedResource ||
            component.LastSettlementBalance != LastSettlementBalance ||
            component.LastSettlementFacilityLevel != LastSettlementFacilityLevel ||
            component.LastSettlementEndTick != LastSettlementEndTick;
        if (!changed)
        {
            return;
        }

        component.ResourceBalance = ResourceBalance;
        component.ProgressRevision = ProgressRevision;
        component.FacilityLevel = FacilityLevel;
        component.LastStartedRunIndex = LastStartedRunIndex;
        component.LastSettledRunId = LastSettledRunId;
        component.LastSettlementPhase = LastSettlementPhase;
        component.LastSettlementGrossResource = LastSettlementGrossResource;
        component.LastSettlementAwardedResource = LastSettlementAwardedResource;
        component.LastSettlementBalance = LastSettlementBalance;
        component.LastSettlementFacilityLevel = LastSettlementFacilityLevel;
        component.LastSettlementEndTick = LastSettlementEndTick;
        component.MarkDirty();
    }
}
