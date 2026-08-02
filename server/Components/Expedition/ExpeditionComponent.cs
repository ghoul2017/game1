using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Cryptography;
using Game.Components.Player;
using ProductExpedition;
using Shared.Common.Protos;
using Shared.Core;
using Shared.Core.ECS;
using Shared.Core.Entities;
using Shared.Core.Log;
using Shared.Core.Network;

namespace Game.Components.Expedition;

/// <summary>
/// 管理当前进程中的远征局；每个账号最多一个活动局，全部规则 Tick 在游戏主线程顺序推进。
/// </summary>
[ComponentOf(typeof(Module))]
public sealed class ExpeditionComponent : Component
{
    public const int TickIntervalMilliseconds = 100;
    public const int SnapshotIntervalTicks = 2;
    public const long DefaultRetentionMilliseconds = 5L * 60L * 1000L;
    public const long DefaultUpgradeCost = ProductExpeditionFactory.DefaultUpgradeCost;

    private static readonly ILog Log = LogManager.GetLogger(nameof(ExpeditionComponent));
    private readonly Dictionary<long, ExpeditionRunHost> _hostsByRunId = new();
    private readonly Dictionary<long, ExpeditionRunHost> _hostsByUserId = new();
    private GameTimerHandle _scheduler;
    private bool _schedulerStarted;

    public IExpeditionSimulationFactory? SimulationFactory { get; private set; }

    public IExpeditionClock Clock { get; private set; } = SystemExpeditionClock.Instance;

    public long RetentionMilliseconds { get; private set; } = DefaultRetentionMilliseconds;

    public long UpgradeCost { get; private set; } = DefaultUpgradeCost;

    public ExpeditionMetrics Metrics { get; } = new ExpeditionMetrics();

    public int ActiveRunCount => _hostsByUserId.Count;

    public void Configure(
        IExpeditionSimulationFactory simulationFactory,
        IExpeditionClock? clock = null,
        long retentionMilliseconds = DefaultRetentionMilliseconds,
        long upgradeCost = DefaultUpgradeCost)
    {
        if (simulationFactory == null)
        {
            throw new ArgumentNullException(nameof(simulationFactory));
        }

        if (retentionMilliseconds <= 0L)
        {
            throw new ArgumentOutOfRangeException(nameof(retentionMilliseconds));
        }

        if (upgradeCost <= 0L)
        {
            throw new ArgumentOutOfRangeException(nameof(upgradeCost));
        }

        if (_schedulerStarted || _hostsByRunId.Count > 0)
        {
            throw new InvalidOperationException("远征模块运行后不能重新配置");
        }

        SimulationFactory = simulationFactory;
        Clock = clock ?? SystemExpeditionClock.Instance;
        RetentionMilliseconds = retentionMilliseconds;
        UpgradeCost = upgradeCost;
    }

    public void StartScheduler()
    {
        EnsureConfigured();
        if (_schedulerStarted)
        {
            return;
        }

        _scheduler = GameTimer.Start(TickIntervalMilliseconds, TickAll);
        _schedulerStarted = true;
        Log.Info($"[Expedition] 权威调度器已启动 TickMs:{TickIntervalMilliseconds}");
    }

    public void StopScheduler()
    {
        if (!_schedulerStarted)
        {
            return;
        }

        GameTimer.Stop(_scheduler);
        _schedulerStarted = false;
        Log.Info("[Expedition] 权威调度器已停止");
    }

    public bool TryStart(
        PlayerEntity player,
        long requestId,
        out ExpeditionSnapshotData snapshot,
        out ExpeditionProgressData progress,
        out string errorMessage)
    {
        snapshot = new ExpeditionSnapshotData();
        PlayerExpeditionComponent progressComponent = player.GetComponent<PlayerExpeditionComponent>();
        progress = progressComponent.ToProtocol(UpgradeCost);
        errorMessage = string.Empty;

        if (requestId <= 0L)
        {
            errorMessage = "远征启动请求编号非法";
            return false;
        }

        EnsureConfigured();
        RoleDataComponent roleData = player.GetComponent<RoleDataComponent>();
        if (_hostsByUserId.TryGetValue(roleData.UserId, out ExpeditionRunHost? existingHost))
        {
            if (existingHost.RoleId != player.Id)
            {
                errorMessage = "该账号已有其他角色正在远征";
                return false;
            }

            if (existingHost.StartRequestId != requestId)
            {
                errorMessage = "该账号已有远征，请先恢复当前局";
                return false;
            }

            if (existingHost.SettlementReceipt != null)
            {
                errorMessage = "该远征启动请求已完成";
                return false;
            }

            if (existingHost.TerminalResult != null)
            {
                // 同一请求的响应可能丢失；重新附着并返回终态快照，让可靠结果通知继续驱动结算。
                existingHost.RestorePermanentState(progressComponent);
                existingHost.Attach(Clock.UtcNowMilliseconds);
                snapshot = existingHost.CaptureSnapshot();
                progress = progressComponent.ToProtocol(UpgradeCost);
                return true;
            }

            existingHost.RestorePermanentState(progressComponent);
            existingHost.Attach(Clock.UtcNowMilliseconds);
            snapshot = existingHost.CaptureSnapshot();
            progress = progressComponent.ToProtocol(UpgradeCost);
            return true;
        }

        foreach (ExpeditionRunHost retainedHost in _hostsByRunId.Values)
        {
            if (retainedHost.UserId == roleData.UserId && retainedHost.StartRequestId == requestId)
            {
                retainedHost.RestorePermanentState(progressComponent);
                progress = progressComponent.ToProtocol(UpgradeCost);
                errorMessage = "该远征启动请求已完成";
                return false;
            }
        }

        long runIndex;
        long progressRevision;
        try
        {
            runIndex = checked(progressComponent.LastStartedRunIndex + 1L);
            progressRevision = checked(progressComponent.ProgressRevision + 1L);
        }
        catch (OverflowException)
        {
            errorMessage = "远征局序号超过可保存范围";
            return false;
        }

        long runId = IdGenerator.Instance.NextId();
        uint seed = (uint)RandomNumberGenerator.GetInt32(1, int.MaxValue);
        ExpeditionSimulationCreateContext createContext = new ExpeditionSimulationCreateContext
        {
            UserId = roleData.UserId,
            RoleId = player.Id,
            RunId = runId,
            RunIndex = runIndex,
            Seed = seed,
            FacilityLevel = progressComponent.FacilityLevel
        };

        IAuthoritativeExpeditionSimulation simulation;
        try
        {
            simulation = SimulationFactory!.Create(createContext);
        }
        catch (Exception exception)
        {
            Log.Error($"[Expedition] 创建权威局失败 RoleId:{player.Id}", exception);
            errorMessage = "远征创建失败";
            return false;
        }

        long nowMilliseconds = Clock.UtcNowMilliseconds;
        PlayerExpeditionCheckpoint checkpoint = PlayerExpeditionCheckpoint.Capture(progressComponent, runIndex);
        ExpeditionRunHost host = new ExpeditionRunHost(
            roleData.UserId,
            player.Id,
            runId,
            runIndex,
            requestId,
            seed,
            simulation,
            nowMilliseconds,
            checkpoint);

        progressComponent.LastStartedRunIndex = runIndex;
        progressComponent.ProgressRevision = progressRevision;
        progressComponent.MarkDirty();
        _hostsByRunId.Add(runId, host);
        _hostsByUserId.Add(roleData.UserId, host);
        RefreshPermanentCheckpoints(player, progressComponent);
        UpdateActiveRunMetrics();

        snapshot = host.CaptureSnapshot();
        progress = progressComponent.ToProtocol(UpgradeCost);
        Log.Info($"[Expedition] 创建权威局 UserId:{roleData.UserId} RoleId:{player.Id} RunId:{runId}");
        return true;
    }

    public ExpeditionInputRejectReason AcceptInput(PlayerEntity player, ExpeditionInputNtf input)
    {
        RoleDataComponent roleData = player.GetComponent<RoleDataComponent>();
        if (!_hostsByUserId.TryGetValue(roleData.UserId, out ExpeditionRunHost? host))
        {
            Metrics.RejectedInputCount++;
            return ExpeditionInputRejectReason.RunMismatch;
        }

        ExpeditionInputRejectReason reason = host.TryAcceptInput(player.Id, input, Clock.UtcNowMilliseconds);
        if (reason != ExpeditionInputRejectReason.None)
        {
            Metrics.RejectedInputCount++;
        }

        return reason;
    }

    public bool TryResume(
        PlayerEntity player,
        long requestedRunId,
        out bool hasActiveRun,
        out ExpeditionSnapshotData snapshot,
        out ExpeditionSettlementData settlement,
        out ExpeditionProgressData progress,
        out string errorMessage)
    {
        hasActiveRun = false;
        snapshot = new ExpeditionSnapshotData();
        settlement = new ExpeditionSettlementData();
        PlayerExpeditionComponent progressComponent = player.GetComponent<PlayerExpeditionComponent>();
        progress = progressComponent.ToProtocol(UpgradeCost);
        errorMessage = string.Empty;

        if (requestedRunId > 0L && progressComponent.LastSettledRunId == requestedRunId)
        {
            settlement = progressComponent.GetLastSettlement();
            return true;
        }

        RoleDataComponent roleData = player.GetComponent<RoleDataComponent>();
        if (requestedRunId > 0L &&
            _hostsByRunId.TryGetValue(requestedRunId, out ExpeditionRunHost? settledHost) &&
            settledHost.RoleId == player.Id &&
            settledHost.SettlementReceipt != null)
        {
            settledHost.RestorePermanentState(progressComponent);
            settlement = settledHost.SettlementReceipt;
            progress = progressComponent.ToProtocol(UpgradeCost);
            return true;
        }

        if (!_hostsByUserId.TryGetValue(roleData.UserId, out ExpeditionRunHost? host))
        {
            if (requestedRunId == 0L)
            {
                RestorePermanentCheckpoints(player, progressComponent);
                progress = progressComponent.ToProtocol(UpgradeCost);
            }

            return true;
        }

        if (host.RoleId != player.Id || (requestedRunId > 0L && requestedRunId != host.RunId))
        {
            errorMessage = "远征局不属于当前角色";
            return false;
        }

        host.Attach(Clock.UtcNowMilliseconds);
        host.RestorePermanentState(progressComponent);
        hasActiveRun = true;
        snapshot = host.CaptureSnapshot();
        progress = progressComponent.ToProtocol(UpgradeCost);
        return true;
    }

    public bool TrySettle(
        PlayerEntity player,
        long runId,
        out ExpeditionSettlementData settlement,
        out ExpeditionProgressData progress,
        out string errorMessage)
    {
        settlement = new ExpeditionSettlementData();
        PlayerExpeditionComponent progressComponent = player.GetComponent<PlayerExpeditionComponent>();
        progress = progressComponent.ToProtocol(UpgradeCost);
        errorMessage = string.Empty;

        if (runId <= 0L)
        {
            errorMessage = "远征局编号非法";
            return false;
        }

        if (progressComponent.LastSettledRunId == runId)
        {
            settlement = progressComponent.GetLastSettlement();
            return true;
        }

        if (!_hostsByRunId.TryGetValue(runId, out ExpeditionRunHost? host) || host.RoleId != player.Id)
        {
            errorMessage = "远征局不存在或不属于当前角色";
            return false;
        }

        host.RestorePermanentState(progressComponent);
        if (host.SettlementReceipt != null)
        {
            settlement = host.SettlementReceipt;
            progress = progressComponent.ToProtocol(UpgradeCost);
            return true;
        }

        if (host.TerminalResult == null)
        {
            errorMessage = "远征尚未结束";
            return false;
        }

        if (!progressComponent.TryCommitSettlement(host.TerminalResult, out settlement, out errorMessage))
        {
            return false;
        }

        host.MarkSettled(progressComponent, settlement, Clock.UtcNowMilliseconds, RetentionMilliseconds);
        RefreshPermanentCheckpoints(player, progressComponent);
        ReleaseActiveHost(host);
        progress = progressComponent.ToProtocol(UpgradeCost);
        Log.Info($"[Expedition] 结算完成 RoleId:{player.Id} RunId:{runId} Award:{settlement.AwardedResource}");
        return true;
    }

    public bool TryUpgrade(
        PlayerEntity player,
        int facilityId,
        out ExpeditionUpgradeRes response,
        out string errorMessage)
    {
        PlayerExpeditionComponent progressComponent = player.GetComponent<PlayerExpeditionComponent>();
        RoleDataComponent roleData = player.GetComponent<RoleDataComponent>();
        bool hasActiveRun = _hostsByUserId.ContainsKey(roleData.UserId);
        bool upgraded = progressComponent.TryUpgradeFacility(
            facilityId,
            UpgradeCost,
            hasActiveRun,
            out response,
            out errorMessage);
        if (upgraded)
        {
            RefreshPermanentCheckpoints(player, progressComponent);
        }

        return upgraded;
    }

    public ExpeditionProgressData QueryProgress(PlayerEntity player)
    {
        PlayerExpeditionComponent progress = player.GetComponent<PlayerExpeditionComponent>();
        RoleDataComponent roleData = player.GetComponent<RoleDataComponent>();
        if (_hostsByUserId.TryGetValue(roleData.UserId, out ExpeditionRunHost? host) && host.RoleId == player.Id)
        {
            host.RestorePermanentState(progress);
        }

        return progress.ToProtocol(UpgradeCost);
    }

    public void DetachRole(long roleId)
    {
        ExpeditionRunHost? host = FindByRoleId(roleId);
        host?.Detach(Clock.UtcNowMilliseconds, RetentionMilliseconds);
    }

    public ExpeditionMetricsSnapshot CaptureMetrics()
    {
        return Metrics.Capture();
    }

    internal void TickAll()
    {
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        long timestampBefore = Stopwatch.GetTimestamp();
        long nowMilliseconds = Clock.UtcNowMilliseconds;
        List<ExpeditionRunHost>? expiredHosts = null;

        foreach (ExpeditionRunHost host in _hostsByRunId.Values)
        {
            if (host.IsExpired(nowMilliseconds))
            {
                expiredHosts ??= new List<ExpeditionRunHost>();
                expiredHosts.Add(host);
                continue;
            }

            bool advanced = host.Advance(nowMilliseconds);
            if (advanced)
            {
                Metrics.AdvancedRuns++;
                bool shouldSendSnapshot = host.Attached &&
                    (host.Simulation.Tick % SnapshotIntervalTicks == 0L || host.TerminalResult != null);
                if (shouldSendSnapshot)
                {
                    ExpeditionSnapshotData snapshot = host.CaptureSnapshot();
                    SendSnapshot(host, snapshot);
                    Metrics.SnapshotCount++;
                }
            }

            // 断线期间已进入终态时，重连附着后仍需补发可靠结果通知。
            SendResultIfNeeded(host);
        }

        if (expiredHosts != null)
        {
            foreach (ExpeditionRunHost host in expiredHosts)
            {
                RemoveHost(host);
                Metrics.ExpiredRunCount++;
                Log.Info($"[Expedition] 断线保留到期 RunId:{host.RunId} RoleId:{host.RoleId}");
            }
        }

        long allocatedAfter = GC.GetAllocatedBytesForCurrentThread();
        Metrics.AllocatedBytes += Math.Max(0L, allocatedAfter - allocatedBefore);
        Metrics.RecordTick(Stopwatch.GetElapsedTime(timestampBefore).TotalMilliseconds);
    }

    private void SendSnapshot(ExpeditionRunHost host, ExpeditionSnapshotData snapshot)
    {
        TcpSession? session = GetAttachedSession(host);
        if (session == null)
        {
            return;
        }

        ExpeditionEventBatchNtf? eventBatch = host.CreateEventBatch(snapshot.Events);
        if (eventBatch != null)
        {
            session.Send(eventBatch);
        }

        session.SendLatest(new ExpeditionSnapshotNtf { Snapshot = snapshot });
    }

    private void SendResultIfNeeded(ExpeditionRunHost host)
    {
        if (host.TerminalResult == null || host.ResultNotificationSent)
        {
            return;
        }

        TcpSession? session = GetAttachedSession(host);
        if (session == null)
        {
            return;
        }

        session.Send(new ExpeditionResultNtf
        {
            RunId = host.RunId,
            Phase = host.TerminalResult.Phase,
            ServerTick = host.TerminalResult.EndTick,
            SettlementReady = true
        });
        host.MarkResultNotificationSent();
        Metrics.ResultNotificationCount++;
    }

    private static TcpSession? GetAttachedSession(ExpeditionRunHost host)
    {
        if (!host.Attached)
        {
            return null;
        }

        PlayerEntity? player = global::Game.Context.Players.GetByRoleId(host.RoleId);
        return player?.GetComponent<PlayerOnlineComponent>().Session;
    }

    private ExpeditionRunHost? FindByRoleId(long roleId)
    {
        foreach (ExpeditionRunHost host in _hostsByUserId.Values)
        {
            if (host.RoleId == roleId)
            {
                return host;
            }
        }

        return null;
    }

    private void RefreshPermanentCheckpoints(
        PlayerEntity player,
        PlayerExpeditionComponent progressComponent)
    {
        foreach (ExpeditionRunHost retainedHost in _hostsByRunId.Values)
        {
            if (retainedHost.RoleId == player.Id)
            {
                retainedHost.RefreshPermanentState(progressComponent);
            }
        }
    }

    private void RestorePermanentCheckpoints(
        PlayerEntity player,
        PlayerExpeditionComponent progressComponent)
    {
        foreach (ExpeditionRunHost retainedHost in _hostsByRunId.Values)
        {
            if (retainedHost.RoleId == player.Id)
            {
                retainedHost.RestorePermanentState(progressComponent);
            }
        }
    }

    private void RemoveHost(ExpeditionRunHost host)
    {
        _hostsByRunId.Remove(host.RunId);
        if (_hostsByUserId.TryGetValue(host.UserId, out ExpeditionRunHost? current) && current == host)
        {
            _hostsByUserId.Remove(host.UserId);
        }

        UpdateActiveRunMetrics();
    }

    private void ReleaseActiveHost(ExpeditionRunHost host)
    {
        if (_hostsByUserId.TryGetValue(host.UserId, out ExpeditionRunHost? current) && current == host)
        {
            _hostsByUserId.Remove(host.UserId);
        }

        UpdateActiveRunMetrics();
    }

    private void UpdateActiveRunMetrics()
    {
        Metrics.ActiveRuns = _hostsByUserId.Count;
        Metrics.PeakActiveRuns = Math.Max(Metrics.PeakActiveRuns, Metrics.ActiveRuns);
    }

    private void EnsureConfigured()
    {
        if (SimulationFactory == null)
        {
            throw new InvalidOperationException("远征模块尚未配置 GameplayCore 适配器");
        }
    }
}
