using Shared.Core.ECS;

namespace Game.Components.Player;

/// <summary>
/// 保存远征带回资源、唯一设施和最近一次结算回执，作为 PlayerEntity 聚合的一部分持久化。
/// </summary>
[ComponentOf(typeof(PlayerEntity))]
public sealed class PlayerExpeditionComponent : Component
{
    public long ProgressRevision { get; set; }

    public long ResourceBalance { get; set; }

    public int FacilityLevel { get; set; }

    public long LastStartedRunIndex { get; set; }

    public long LastSettledRunId { get; set; }

    public int LastSettlementPhase { get; set; }

    public long LastSettlementGrossResource { get; set; }

    public long LastSettlementAwardedResource { get; set; }

    public long LastSettlementBalance { get; set; }

    public int LastSettlementFacilityLevel { get; set; }

    public long LastSettlementEndTick { get; set; }
}
