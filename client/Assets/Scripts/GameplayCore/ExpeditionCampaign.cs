using System;

namespace GameplayCore
{
    public sealed class ExpeditionCampaignDefinition
    {
        private readonly ExpeditionSetup _template;

        public ExpeditionCampaignDefinition(
            uint campaignSeed,
            int roomCount,
            int upgradeCost,
            int failureRetentionPercent,
            int upgradeDamagePercent,
            ExpeditionSetup template)
        {
            if (template == null)
            {
                throw new ArgumentNullException(nameof(template));
            }

            ExpeditionSetupValidator.Validate(template);
            if (template.RoomGraph != null)
            {
                throw new ArgumentException("Campaign 模板不能预先包含房间图", nameof(template));
            }

            if (roomCount < 6 || roomCount > 10)
            {
                throw new ArgumentOutOfRangeException(nameof(roomCount), "房间数必须在 6 到 10 之间");
            }

            if (upgradeCost <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(upgradeCost), "升级价格必须是正数");
            }

            if (failureRetentionPercent < 0 || failureRetentionPercent > 100)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(failureRetentionPercent),
                    "失败保留比例必须在 0 到 100 之间");
            }

            if (upgradeDamagePercent <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(upgradeDamagePercent), "升级伤害比例必须是正数");
            }

            CampaignSeed = campaignSeed == 0u ? DeterministicRandom.Mix(0u, 0u) : campaignSeed;
            RoomCount = roomCount;
            UpgradeCost = upgradeCost;
            FailureRetentionPercent = failureRetentionPercent;
            UpgradeDamagePercent = upgradeDamagePercent;
            _template = CloneSetup(template, 0L, 0u, null, 0);
        }

        public uint CampaignSeed { get; }

        public int RoomCount { get; }

        public int UpgradeCost { get; }

        public int FailureRetentionPercent { get; }

        public int UpgradeDamagePercent { get; }

        public ExpeditionRun CreateRun(long runId, uint seed, int facilityLevel)
        {
            if (runId <= 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(runId), "RunId 必须是正数");
            }

            ExpeditionRoomGraph graph = CreateRoomGraph(seed);
            ExpeditionSetup setup = CreateRunSetup(runId, graph.Seed, graph, facilityLevel);
            return ExpeditionRun.Start(setup);
        }

        internal ExpeditionSetup CreateRunSetup(long runId, uint seed, ExpeditionRoomGraph graph, int facilityLevel)
        {
            return CloneSetup(_template, runId, seed, graph, facilityLevel);
        }

        internal ExpeditionRoomGraph CreateRoomGraph(uint seed)
        {
            return ExpeditionRoomGraphGenerator.Generate(seed, RoomCount, UpgradeCost, _template);
        }

        public int GetRepresentativeDamage(int facilityLevel)
        {
            return ApplyDamageUpgrade(_template.Members[0].AttackDamage, facilityLevel);
        }

        private ExpeditionSetup CloneSetup(
            ExpeditionSetup source,
            long runId,
            uint seed,
            ExpeditionRoomGraph graph,
            int facilityLevel)
        {
            ExpeditionMemberSetup[] members = new ExpeditionMemberSetup[source.Members.Length];
            for (int index = 0; index < source.Members.Length; index++)
            {
                ExpeditionMemberSetup member = source.Members[index];
                members[index] = new ExpeditionMemberSetup
                {
                    Id = member.Id,
                    MaxHealth = member.MaxHealth,
                    AttackDamage = ApplyDamageUpgrade(member.AttackDamage, facilityLevel),
                    AttackRange = member.AttackRange,
                    AttackIntervalTicks = member.AttackIntervalTicks
                };
            }

            return new ExpeditionSetup
            {
                RunId = runId,
                Seed = seed,
                RoomGraph = graph,
                TicksPerSecond = source.TicksPerSecond,
                MoveSpeedPerSecond = source.MoveSpeedPerSecond,
                MinimumX = source.MinimumX,
                MaximumX = source.MaximumX,
                MinimumY = source.MinimumY,
                MaximumY = source.MaximumY,
                InitialSquadX = source.InitialSquadX,
                InitialSquadY = source.InitialSquadY,
                TargetAcquireRadius = source.TargetAcquireRadius,
                TargetReleaseMargin = source.TargetReleaseMargin,
                EnemyThreatRadius = source.EnemyThreatRadius,
                ExitX = source.ExitX,
                ExitY = source.ExitY,
                ExitRadius = source.ExitRadius,
                Members = members,
                Enemies = CloneEnemies(source.Enemies),
                Resources = CloneResources(source.Resources)
            };
        }

        private int ApplyDamageUpgrade(int damage, int facilityLevel)
        {
            if (facilityLevel == 0)
            {
                return damage;
            }

            if (facilityLevel != 1)
            {
                throw new ArgumentOutOfRangeException(nameof(facilityLevel), "T2 设施等级只能是 0 或 1");
            }

            int scaled = checked((damage * (100 + UpgradeDamagePercent) + 50) / 100);
            return Math.Max(damage + 1, scaled);
        }

        private static ExpeditionEnemySetup[] CloneEnemies(ExpeditionEnemySetup[] source)
        {
            ExpeditionEnemySetup[] result = new ExpeditionEnemySetup[source.Length];
            for (int index = 0; index < source.Length; index++)
            {
                ExpeditionEnemySetup value = source[index];
                result[index] = new ExpeditionEnemySetup
                {
                    Id = value.Id,
                    Kind = value.Kind,
                    X = value.X,
                    Y = value.Y,
                    MaxHealth = value.MaxHealth,
                    AttackDamage = value.AttackDamage,
                    AttackRange = value.AttackRange,
                    ActionIntervalTicks = value.ActionIntervalTicks,
                    BossWarningDelayTicks = value.BossWarningDelayTicks,
                    BossWarningRadius = value.BossWarningRadius
                };
            }

            return result;
        }

        private static ExpeditionResourceSetup[] CloneResources(ExpeditionResourceSetup[] source)
        {
            ExpeditionResourceSetup[] result = new ExpeditionResourceSetup[source.Length];
            for (int index = 0; index < source.Length; index++)
            {
                ExpeditionResourceSetup value = source[index];
                result[index] = new ExpeditionResourceSetup
                {
                    Id = value.Id,
                    X = value.X,
                    Y = value.Y,
                    Amount = value.Amount,
                    CollectionAmount = value.CollectionAmount,
                    InteractionRange = value.InteractionRange,
                    CollectionIntervalTicks = value.CollectionIntervalTicks
                };
            }

            return result;
        }
    }

    public sealed class ExpeditionCampaignState
    {
        internal ExpeditionCampaignState(
            int runIndex,
            long bankedResource,
            int facilityLevel,
            long activeRunId,
            SettlementReceipt latestSettlement)
        {
            RunIndex = runIndex;
            BankedResource = bankedResource;
            FacilityLevel = facilityLevel;
            ActiveRunId = activeRunId;
            LatestSettlement = latestSettlement;
        }

        public int RunIndex { get; }

        public long BankedResource { get; }

        public int FacilityLevel { get; }

        public long ActiveRunId { get; }

        public bool HasActiveRun => ActiveRunId != 0L;

        public SettlementReceipt LatestSettlement { get; }
    }

    public sealed class SettlementReceipt
    {
        internal SettlementReceipt(
            long runId,
            ExpeditionPhase phase,
            long collectedResource,
            long bankedResource,
            long balanceAfter)
        {
            RunId = runId;
            Phase = phase;
            CollectedResource = collectedResource;
            BankedResource = bankedResource;
            BalanceAfter = balanceAfter;
        }

        public long RunId { get; }

        public ExpeditionPhase Phase { get; }

        public long CollectedResource { get; }

        public long BankedResource { get; }

        public long BalanceAfter { get; }
    }

    public sealed class FacilityUpgradeReceipt
    {
        internal FacilityUpgradeReceipt(
            int previousLevel,
            int currentLevel,
            long cost,
            long balanceAfter,
            int previousDamage,
            int currentDamage)
        {
            PreviousLevel = previousLevel;
            CurrentLevel = currentLevel;
            Cost = cost;
            BalanceAfter = balanceAfter;
            PreviousDamage = previousDamage;
            CurrentDamage = currentDamage;
        }

        public int PreviousLevel { get; }

        public int CurrentLevel { get; }

        public long Cost { get; }

        public long BalanceAfter { get; }

        public int PreviousDamage { get; }

        public int CurrentDamage { get; }
    }

    public sealed class ExpeditionCampaignSession
    {
        private readonly ExpeditionCampaignDefinition _definition;
        private ExpeditionResult _lastConsumedResult;
        private SettlementReceipt _lastReceipt;

        public ExpeditionCampaignSession(ExpeditionCampaignDefinition definition)
        {
            _definition = definition ?? throw new ArgumentNullException(nameof(definition));
            State = new ExpeditionCampaignState(0, 0L, 0, 0L, null);
        }

        public ExpeditionCampaignState State { get; private set; }

        public ExpeditionRun ActiveRun { get; private set; }

        public int CurrentRepresentativeDamage => _definition.GetRepresentativeDamage(State.FacilityLevel);

        public int UpgradeCost => _definition.UpgradeCost;

        public ExpeditionRun StartRun()
        {
            if (ActiveRun != null)
            {
                throw new InvalidOperationException("已有进行中的远征");
            }

            int runIndex = checked(State.RunIndex + 1);
            long runId = runIndex;
            uint seed = DeterministicRandom.Mix(_definition.CampaignSeed, (uint)runIndex);
            ExpeditionRoomGraph graph = _definition.CreateRoomGraph(seed);
            ExpeditionSetup setup = _definition.CreateRunSetup(runId, seed, graph, State.FacilityLevel);
            ActiveRun = ExpeditionRun.Start(setup);
            State = new ExpeditionCampaignState(
                runIndex,
                State.BankedResource,
                State.FacilityLevel,
                runId,
                State.LatestSettlement);
            return ActiveRun;
        }

        public SettlementReceipt ConsumeResult(ExpeditionRun run)
        {
            if (run == null)
            {
                throw new ArgumentNullException(nameof(run));
            }

            ExpeditionResult result = run.Result ??
                throw new InvalidOperationException("运行中的远征不能结算");
            if (_lastConsumedResult != null && result.RunId == _lastConsumedResult.RunId)
            {
                if (!_lastConsumedResult.Equals(result))
                {
                    throw new InvalidOperationException("相同 RunId 的结果内容冲突");
                }

                return _lastReceipt;
            }

            if (_lastConsumedResult != null && result.RunId < _lastConsumedResult.RunId)
            {
                throw new InvalidOperationException("不能结算早于最近回执的结果");
            }

            if (!ReferenceEquals(ActiveRun, run) || State.ActiveRunId != result.RunId)
            {
                throw new InvalidOperationException("结果不属于当前活动远征");
            }

            long banked = result.Phase == ExpeditionPhase.Completed
                ? result.CollectedResource
                : checked(result.CollectedResource * _definition.FailureRetentionPercent / 100L);
            long balanceAfter = checked(State.BankedResource + banked);

            ActiveRun = null;
            _lastConsumedResult = result;
            _lastReceipt = new SettlementReceipt(
                result.RunId,
                result.Phase,
                result.CollectedResource,
                banked,
                balanceAfter);
            State = new ExpeditionCampaignState(
                State.RunIndex,
                balanceAfter,
                State.FacilityLevel,
                0L,
                _lastReceipt);
            return _lastReceipt;
        }

        public bool TryUpgradeFacility(out FacilityUpgradeReceipt receipt)
        {
            receipt = null;
            if (ActiveRun != null || State.FacilityLevel != 0 || State.BankedResource < _definition.UpgradeCost)
            {
                return false;
            }

            long balanceAfter = State.BankedResource - _definition.UpgradeCost;
            int previousDamage = _definition.GetRepresentativeDamage(0);
            int currentDamage = _definition.GetRepresentativeDamage(1);

            State = new ExpeditionCampaignState(
                State.RunIndex,
                balanceAfter,
                1,
                0L,
                State.LatestSettlement);
            receipt = new FacilityUpgradeReceipt(
                0,
                1,
                _definition.UpgradeCost,
                balanceAfter,
                previousDamage,
                currentDamage);
            return true;
        }
    }
}
