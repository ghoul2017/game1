using System;
using System.Collections.Generic;
using Game.Components.Player;
using Shared.Common.Protos;

namespace Game.Components.Expedition;

public enum ExpeditionInputRejectReason
{
    None = 0,
    OwnerMismatch = 1,
    RunMismatch = 2,
    RunEnded = 3,
    InvalidSequence = 4,
    StaleSequence = 5,
    InvalidDirection = 6,
    InvalidInteractionTarget = 7,
    RateLimited = 8
}

/// <summary>
/// 一局一个单写者宿主，只保存最新有效输入并顺序推进规则局。
/// </summary>
public sealed class ExpeditionRunHost
{
    private const int MaximumInputsPerWindow = 30;
    private const int MaximumPendingEvents = 256;
    private const long InputWindowMilliseconds = 1000L;
    private const long InputFreshnessMilliseconds = 500L;

    private readonly List<ExpeditionEventData> _pendingEvents = new();
    private PlayerExpeditionCheckpoint? _permanentCheckpoint;
    private int _acceptedInputsInWindow;
    private int _directionX;
    private int _directionY;
    private int _interactionTargetId;
    private long _inputWindowStartedAt;
    private long _lastAcceptedInputAt;
    private long _nextEventSequence;

    public ExpeditionRunHost(
        long userId,
        long roleId,
        long runId,
        long runIndex,
        long startRequestId,
        uint seed,
        IAuthoritativeExpeditionSimulation simulation,
        long nowMilliseconds)
        : this(
            userId,
            roleId,
            runId,
            runIndex,
            startRequestId,
            seed,
            simulation,
            nowMilliseconds,
            null)
    {
    }

    internal ExpeditionRunHost(
        long userId,
        long roleId,
        long runId,
        long runIndex,
        long startRequestId,
        uint seed,
        IAuthoritativeExpeditionSimulation simulation,
        long nowMilliseconds,
        PlayerExpeditionCheckpoint? permanentCheckpoint)
    {
        if (simulation == null)
        {
            throw new ArgumentNullException(nameof(simulation));
        }

        UserId = userId;
        RoleId = roleId;
        RunId = runId;
        RunIndex = runIndex;
        StartRequestId = startRequestId;
        Seed = seed;
        Simulation = simulation;
        _permanentCheckpoint = permanentCheckpoint;
        _inputWindowStartedAt = nowMilliseconds;
        _lastAcceptedInputAt = nowMilliseconds;
        Attached = true;
    }

    public long UserId { get; }

    public long RoleId { get; }

    public long RunId { get; }

    public long RunIndex { get; }

    public long StartRequestId { get; }

    internal uint Seed { get; }

    public IAuthoritativeExpeditionSimulation Simulation { get; }

    public long LastAcceptedInputSequence { get; private set; }

    public long LastProcessedInputSequence { get; private set; }

    public bool Attached { get; private set; }

    public long RetainUntilMilliseconds { get; private set; } = long.MaxValue;

    public ExpeditionAuthoritativeResult? TerminalResult { get; private set; }

    public ExpeditionSnapshotData? LastSnapshot { get; private set; }

    public bool ResultNotificationSent { get; private set; }

    internal ExpeditionSettlementData? SettlementReceipt { get; private set; }

    public long RejectedInputCount { get; private set; }

    public long DroppedEventCount { get; private set; }

    public ExpeditionInputRejectReason TryAcceptInput(
        long roleId,
        ExpeditionInputNtf input,
        long nowMilliseconds)
    {
        if (roleId != RoleId)
        {
            return Reject(ExpeditionInputRejectReason.OwnerMismatch);
        }

        if (input.RunId != RunId)
        {
            return Reject(ExpeditionInputRejectReason.RunMismatch);
        }

        if (Simulation.HasEnded)
        {
            return Reject(ExpeditionInputRejectReason.RunEnded);
        }

        if (input.InputSequence <= 0L)
        {
            return Reject(ExpeditionInputRejectReason.InvalidSequence);
        }

        if (input.InputSequence <= LastAcceptedInputSequence)
        {
            return Reject(ExpeditionInputRejectReason.StaleSequence);
        }

        if (!IsValidDirection(input.DirectionX, input.DirectionY))
        {
            return Reject(ExpeditionInputRejectReason.InvalidDirection);
        }

        if (input.InteractionTargetId < 0)
        {
            return Reject(ExpeditionInputRejectReason.InvalidInteractionTarget);
        }

        ResetInputWindowIfNeeded(nowMilliseconds);
        if (_acceptedInputsInWindow >= MaximumInputsPerWindow)
        {
            return Reject(ExpeditionInputRejectReason.RateLimited);
        }

        _acceptedInputsInWindow++;
        _directionX = input.DirectionX;
        _directionY = input.DirectionY;
        _interactionTargetId = input.InteractionTargetId;
        _lastAcceptedInputAt = nowMilliseconds;
        LastAcceptedInputSequence = input.InputSequence;
        return ExpeditionInputRejectReason.None;
    }

    public bool Advance(long nowMilliseconds)
    {
        if (Simulation.HasEnded)
        {
            CaptureTerminalResult();
            return false;
        }

        float directionX = 0.0f;
        float directionY = 0.0f;
        int interactionTargetId = 0;
        if (Attached && nowMilliseconds - _lastAcceptedInputAt <= InputFreshnessMilliseconds)
        {
            directionX = (float)_directionX / ExpeditionProtocolConstants.DirectionScale;
            directionY = (float)_directionY / ExpeditionProtocolConstants.DirectionScale;
            interactionTargetId = _interactionTargetId;
        }

        List<ExpeditionEventData> events = new List<ExpeditionEventData>();
        Simulation.Advance(directionX, directionY, interactionTargetId, events);
        LastProcessedInputSequence = LastAcceptedInputSequence;
        AppendEvents(events);
        CaptureTerminalResult();
        return true;
    }

    public ExpeditionSnapshotData CaptureSnapshot()
    {
        ExpeditionSnapshotData snapshot = Simulation.CaptureSnapshot(
            RunId,
            RunIndex,
            LastProcessedInputSequence,
            _pendingEvents);
        snapshot.RunId = RunId;
        snapshot.RunIndex = RunIndex;
        snapshot.ServerTick = Simulation.Tick;
        snapshot.LastProcessedInputSequence = LastProcessedInputSequence;
        _pendingEvents.Clear();
        LastSnapshot = snapshot;
        return snapshot;
    }

    /// <summary>
    /// 将本次快照携带的表现事件投影为可靠消息。序号只在存在事件时递增。
    /// </summary>
    public ExpeditionEventBatchNtf? CreateEventBatch(IReadOnlyList<ExpeditionEventData> events)
    {
        if (events == null)
        {
            throw new ArgumentNullException(nameof(events));
        }

        if (events.Count == 0)
        {
            return null;
        }

        ExpeditionEventBatchNtf batch = new ExpeditionEventBatchNtf
        {
            RunId = RunId,
            EventSequence = checked(++_nextEventSequence)
        };
        for (int index = 0; index < events.Count; index++)
        {
            batch.Events.Add(events[index]);
        }

        return batch;
    }

    public void Detach(long nowMilliseconds, long retentionMilliseconds)
    {
        Attached = false;
        _directionX = 0;
        _directionY = 0;
        _interactionTargetId = 0;
        _lastAcceptedInputAt = nowMilliseconds;
        RetainUntilMilliseconds = checked(nowMilliseconds + retentionMilliseconds);
        ResultNotificationSent = false;
    }

    public void Attach(long nowMilliseconds)
    {
        Attached = true;
        _directionX = 0;
        _directionY = 0;
        _interactionTargetId = 0;
        _lastAcceptedInputAt = nowMilliseconds;
        RetainUntilMilliseconds = long.MaxValue;
        ResultNotificationSent = false;
    }

    public bool IsExpired(long nowMilliseconds)
    {
        return !Attached && nowMilliseconds >= RetainUntilMilliseconds;
    }

    public void MarkResultNotificationSent()
    {
        ResultNotificationSent = true;
    }

    internal void RestorePermanentState(PlayerExpeditionComponent component)
    {
        _permanentCheckpoint?.Restore(component);
    }

    internal void RefreshPermanentState(PlayerExpeditionComponent component)
    {
        _permanentCheckpoint = PlayerExpeditionCheckpoint.Capture(component, component.LastStartedRunIndex);
    }

    internal void MarkSettled(
        PlayerExpeditionComponent component,
        ExpeditionSettlementData settlement,
        long nowMilliseconds,
        long retentionMilliseconds)
    {
        _permanentCheckpoint = PlayerExpeditionCheckpoint.Capture(component, component.LastStartedRunIndex);
        SettlementReceipt = settlement;
        Detach(nowMilliseconds, retentionMilliseconds);
    }

    private ExpeditionInputRejectReason Reject(ExpeditionInputRejectReason reason)
    {
        RejectedInputCount++;
        return reason;
    }

    private void ResetInputWindowIfNeeded(long nowMilliseconds)
    {
        if (nowMilliseconds - _inputWindowStartedAt < InputWindowMilliseconds)
        {
            return;
        }

        _inputWindowStartedAt = nowMilliseconds;
        _acceptedInputsInWindow = 0;
    }

    private void AppendEvents(IReadOnlyList<ExpeditionEventData> events)
    {
        for (int index = 0; index < events.Count; index++)
        {
            if (_pendingEvents.Count >= MaximumPendingEvents)
            {
                _pendingEvents.RemoveAt(0);
                DroppedEventCount++;
            }

            _pendingEvents.Add(events[index]);
        }
    }

    private void CaptureTerminalResult()
    {
        if (!Simulation.HasEnded || TerminalResult != null)
        {
            return;
        }

        TerminalResult = new ExpeditionAuthoritativeResult
        {
            RunId = RunId,
            Phase = Simulation.Phase,
            GrossResource = Simulation.GrossResource,
            EndTick = Simulation.Tick
        };
    }

    private static bool IsValidDirection(int directionX, int directionY)
    {
        int scale = ExpeditionProtocolConstants.DirectionScale;
        if (directionX < -scale || directionX > scale || directionY < -scale || directionY > scale)
        {
            return false;
        }

        long lengthSquared = ((long)directionX * directionX) + ((long)directionY * directionY);
        return lengthSquared <= (long)scale * scale;
    }
}
