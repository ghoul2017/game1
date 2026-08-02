using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Shared.Common.Protos;

namespace Client.Logic
{
    /// <summary>
    /// 在线远征的客户端远端宿主。完整规则状态以服务端快照为准，本地只预测队伍根节点移动。
    /// </summary>
    public sealed class ExpeditionController
    {
        private const int MaximumPendingInputs = 64;
        private const int MaximumInputsPerRollingWindow = 29;
        private const int MaximumInputsBeforeReservedStop = MaximumInputsPerRollingWindow - 1;
        private const float HardCorrectionDistance = 2.0f;
        private const float MaximumExpectedSnapshotLag = HardCorrectionDistance * 0.75f;
        private const float SoftCorrectionRatio = 0.35f;
        private static readonly long ChangedInputIntervalTimestampTicks =
            Math.Max(1L, Stopwatch.Frequency * 50L / 1000L);
        private static readonly long InputKeepAliveTimestampTicks =
            Math.Max(1L, Stopwatch.Frequency * 200L / 1000L);
        private static readonly long InputBudgetWindowTimestampTicks = Stopwatch.Frequency;

        private readonly List<PendingExpeditionInput> _pendingInputs = new List<PendingExpeditionInput>();
        private readonly Queue<long> _inputSendTimestamps = new Queue<long>();
        private int _currentDirectionX;
        private int _currentDirectionY;
        private bool _hasCurrentIntent;
        private bool _hasSentInput;
        private long _eventRunId;
        private long _lastEventSequence;
        private long _roleId;
        private int _lastSentDirectionX;
        private int _lastSentDirectionY;
        private int _lastSentInteractionTargetId;
        private long _lastInputSentAtTimestamp;
        private long _lastStartRequestId = DateTime.UtcNow.Ticks;
        private long _nextInputSequence;
        private long _pendingStartRequestId;
        private long _roleLifecycleGeneration;

        public static ExpeditionController Instance { get; } = new ExpeditionController();

        private ExpeditionController()
        {
        }

        public ExpeditionSnapshotData LatestSnapshot { get; private set; }

        public ExpeditionProgressData Progress { get; private set; } = new ExpeditionProgressData();

        public ExpeditionSettlementData LatestSettlement { get; private set; }

        public ExpeditionResultNtf LatestResult { get; private set; }

        public float PredictedSquadX { get; private set; }

        public float PredictedSquadY { get; private set; }

        public int PendingInputCount => _pendingInputs.Count;

        public long LastEventSequence => _lastEventSequence;

        public long RoleId => _roleId;

        public event Action<ExpeditionSnapshotData> SnapshotChanged;

        public event Action<ExpeditionProgressData> ProgressChanged;

        public event Action<ExpeditionSettlementData> SettlementChanged;

        public event Action<ExpeditionResultNtf> ResultReceived;

        public event Action<ExpeditionEventBatchNtf> EventBatchReceived;

        public async Task<ExpeditionStartRes> StartAsync()
        {
            long lifecycleGeneration = Interlocked.Read(ref _roleLifecycleGeneration);
            long requestId = Interlocked.Read(ref _pendingStartRequestId);
            if (requestId <= 0L)
            {
                long candidate = Interlocked.Increment(ref _lastStartRequestId);
                long previous = Interlocked.CompareExchange(ref _pendingStartRequestId, candidate, 0L);
                requestId = previous == 0L ? candidate : previous;
            }

            ExpeditionStartRes response = await NetClient.Instance.CallAsync<ExpeditionStartRes>(
                new ExpeditionStartReq { RequestId = requestId });
            if (IsCurrentRoleLifecycle(lifecycleGeneration) && string.IsNullOrEmpty(response.ErrorMessage))
            {
                Interlocked.Exchange(ref _pendingStartRequestId, 0L);
                ApplyProgress(response.Progress);
                ApplySnapshot(response.Snapshot);
                LatestSettlement = null;
                LatestResult = null;
            }

            return response;
        }

        public void SendInput(float directionX, float directionY, int interactionTargetId = 0)
        {
            NetClient netClient = NetClient.Instance;
            if (netClient == null || LatestSnapshot == null || LatestSnapshot.Phase != 0)
            {
                return;
            }

            if (interactionTargetId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(interactionTargetId));
            }

            ExpeditionProtocolConstants.QuantizeDirection(
                directionX,
                directionY,
                out int quantizedX,
                out int quantizedY);
            _currentDirectionX = quantizedX;
            _currentDirectionY = quantizedY;
            _hasCurrentIntent = true;
            long nowTimestamp = Stopwatch.GetTimestamp();
            bool inputChanged = !_hasSentInput || quantizedX != _lastSentDirectionX ||
                quantizedY != _lastSentDirectionY || interactionTargetId != _lastSentInteractionTargetId;
            long elapsedSinceLastInput = nowTimestamp - _lastInputSentAtTimestamp;
            bool startedMoving = _hasSentInput && _lastSentDirectionX == 0 && _lastSentDirectionY == 0 &&
                (quantizedX != 0 || quantizedY != 0);
            bool stoppedMoving = _hasSentInput && (quantizedX == 0 && quantizedY == 0) &&
                (_lastSentDirectionX != 0 || _lastSentDirectionY != 0);
            bool interactionChanged = _hasSentInput && interactionTargetId != _lastSentInteractionTargetId;
            bool immediateChange = !_hasSentInput || startedMoving || stoppedMoving || interactionChanged;
            if (inputChanged && !immediateChange &&
                elapsedSinceLastInput < ChangedInputIntervalTimestampTicks)
            {
                return;
            }

            if (!inputChanged && elapsedSinceLastInput < InputKeepAliveTimestampTicks)
            {
                return;
            }

            if (!TryConsumeInputBudget(nowTimestamp, stoppedMoving))
            {
                return;
            }

            long sequence = checked(++_nextInputSequence);
            ExpeditionInputNtf input = new ExpeditionInputNtf
            {
                RunId = LatestSnapshot.RunId,
                InputSequence = sequence,
                DirectionX = quantizedX,
                DirectionY = quantizedY,
                InteractionTargetId = interactionTargetId
            };

            _pendingInputs.Add(new PendingExpeditionInput(sequence, quantizedX, quantizedY));
            if (_pendingInputs.Count > MaximumPendingInputs)
            {
                _pendingInputs.RemoveAt(0);
            }

            _hasSentInput = true;
            _lastSentDirectionX = quantizedX;
            _lastSentDirectionY = quantizedY;
            _lastSentInteractionTargetId = interactionTargetId;
            _lastInputSentAtTimestamp = nowTimestamp;
            netClient.Send(input);
        }

        /// <summary>
        /// 由客户端帧循环调用，仅预测队伍根节点；敌人、资源、伤害和收益不在客户端推进。
        /// </summary>
        public void AdvanceRootPrediction(float deltaSeconds)
        {
            ExpeditionSnapshotData snapshot = LatestSnapshot;
            if (deltaSeconds <= 0.0f || snapshot == null || snapshot.Phase != 0 || !_hasCurrentIntent)
            {
                return;
            }

            if (!HasValidPredictionRules(snapshot))
            {
                return;
            }

            float directionX = (float)_currentDirectionX / ExpeditionProtocolConstants.DirectionScale;
            float directionY = (float)_currentDirectionY / ExpeditionProtocolConstants.DirectionScale;
            PredictedSquadX = Clamp(
                PredictedSquadX + (directionX * snapshot.MoveSpeedPerSecond * deltaSeconds),
                snapshot.MinimumX,
                snapshot.MaximumX);
            PredictedSquadY = Clamp(
                PredictedSquadY + (directionY * snapshot.MoveSpeedPerSecond * deltaSeconds),
                snapshot.MinimumY,
                snapshot.MaximumY);
        }

        public async Task<ExpeditionResumeRes> ResumeAsync(long runId = 0L)
        {
            long lifecycleGeneration = Interlocked.Read(ref _roleLifecycleGeneration);
            ExpeditionResumeRes response = await NetClient.Instance.CallAsync<ExpeditionResumeRes>(
                new ExpeditionResumeReq { RunId = runId });
            if (IsCurrentRoleLifecycle(lifecycleGeneration))
            {
                ApplyResume(response);
            }

            return response;
        }

        public void ApplyResume(ExpeditionResumeRes response)
        {
            if (response == null || !string.IsNullOrEmpty(response.ErrorMessage))
            {
                return;
            }

            Interlocked.Exchange(ref _pendingStartRequestId, 0L);
            ApplyProgress(response.Progress);
            if (response.HasActiveRun)
            {
                ApplySnapshot(response.Snapshot);
            }
            else
            {
                ClearActiveRun();
            }

            if (response.Settlement != null && response.Settlement.RunId > 0L)
            {
                ApplySettlement(response.Settlement);
            }
        }

        public async Task<ExpeditionSettlementRes> SettleAsync(long runId)
        {
            long lifecycleGeneration = Interlocked.Read(ref _roleLifecycleGeneration);
            ExpeditionSettlementRes response = await NetClient.Instance.CallAsync<ExpeditionSettlementRes>(
                new ExpeditionSettlementReq { RunId = runId });
            if (IsCurrentRoleLifecycle(lifecycleGeneration) && string.IsNullOrEmpty(response.ErrorMessage))
            {
                ApplyProgress(response.Progress);
                ApplySettlement(response.Settlement);
                ResetInputState(false);
            }

            return response;
        }

        public async Task<ExpeditionQueryProgressRes> QueryProgressAsync()
        {
            long lifecycleGeneration = Interlocked.Read(ref _roleLifecycleGeneration);
            ExpeditionQueryProgressRes response = await NetClient.Instance.CallAsync<ExpeditionQueryProgressRes>(
                new ExpeditionQueryProgressReq());
            if (IsCurrentRoleLifecycle(lifecycleGeneration) && string.IsNullOrEmpty(response.ErrorMessage))
            {
                ApplyProgress(response.Progress);
            }

            return response;
        }

        public async Task<ExpeditionUpgradeRes> UpgradeAsync(
            int facilityId = ExpeditionProtocolConstants.FacilityId)
        {
            long lifecycleGeneration = Interlocked.Read(ref _roleLifecycleGeneration);
            ExpeditionUpgradeRes response = await NetClient.Instance.CallAsync<ExpeditionUpgradeRes>(
                new ExpeditionUpgradeReq { FacilityId = facilityId });
            if (IsCurrentRoleLifecycle(lifecycleGeneration) && string.IsNullOrEmpty(response.ErrorMessage))
            {
                ApplyProgress(response.Progress);
            }

            return response;
        }

        public void ApplySnapshot(ExpeditionSnapshotData snapshot)
        {
            if (snapshot == null || snapshot.RunId <= 0L)
            {
                return;
            }

            ExpeditionSnapshotData previous = LatestSnapshot;
            if (previous != null && previous.RunId == snapshot.RunId && previous.ServerTick >= snapshot.ServerTick)
            {
                return;
            }

            bool newRun = previous == null || previous.RunId != snapshot.RunId;
            bool hardInvariantChanged = newRun || previous.CurrentRoomId != snapshot.CurrentRoomId ||
                previous.Phase != snapshot.Phase;
            LatestSnapshot = snapshot;
            _nextInputSequence = Math.Max(_nextInputSequence, snapshot.LastProcessedInputSequence);
            RemoveAcknowledgedInputs(snapshot.RunId, snapshot.LastProcessedInputSequence, newRun);
            if (snapshot.Phase != 0)
            {
                ResetInputState(false);
            }

            if (newRun)
            {
                _eventRunId = snapshot.RunId;
                _lastEventSequence = 0L;
            }

            CorrectPrediction(snapshot, previous, hardInvariantChanged);
            SnapshotChanged?.Invoke(snapshot);
        }

        public void ApplyEventBatch(ExpeditionEventBatchNtf batch)
        {
            if (batch == null || batch.RunId <= 0L || batch.EventSequence <= 0L)
            {
                return;
            }

            if (LatestSnapshot == null || batch.RunId != LatestSnapshot.RunId)
            {
                return;
            }

            if (_eventRunId != batch.RunId)
            {
                _eventRunId = batch.RunId;
                _lastEventSequence = 0L;
            }

            if (batch.EventSequence <= _lastEventSequence)
            {
                return;
            }

            _lastEventSequence = batch.EventSequence;
            if (batch.Events != null && batch.Events.Count > 0)
            {
                EventBatchReceived?.Invoke(batch);
            }
        }

        public void ApplyResult(ExpeditionResultNtf result)
        {
            if (result == null || result.RunId <= 0L)
            {
                return;
            }

            if (LatestSnapshot != null && result.RunId != LatestSnapshot.RunId)
            {
                return;
            }

            LatestResult = result;
            ResultReceived?.Invoke(result);
        }

        public void ResetForRole(long roleId = 0L)
        {
            Interlocked.Increment(ref _roleLifecycleGeneration);
            long normalizedRoleId = roleId > 0L ? roleId : 0L;
            if (normalizedRoleId == 0L || _roleId != normalizedRoleId)
            {
                Interlocked.Exchange(ref _pendingStartRequestId, 0L);
            }

            _roleId = normalizedRoleId;
            ClearActiveRun();
            Progress = new ExpeditionProgressData();
            LatestSettlement = null;
            _eventRunId = 0L;
            _lastEventSequence = 0L;
            ProgressChanged?.Invoke(Progress);
            SettlementChanged?.Invoke(null);
        }

        private void ApplyProgress(ExpeditionProgressData progress)
        {
            Progress = progress ?? new ExpeditionProgressData();
            ProgressChanged?.Invoke(Progress);
        }

        private void ApplySettlement(ExpeditionSettlementData settlement)
        {
            LatestSettlement = settlement;
            SettlementChanged?.Invoke(settlement);
        }

        private void ClearActiveRun()
        {
            LatestSnapshot = null;
            LatestResult = null;
            PredictedSquadX = 0.0f;
            PredictedSquadY = 0.0f;
            ResetInputState(true);
            _nextInputSequence = 0L;
            SnapshotChanged?.Invoke(null);
        }

        private void RemoveAcknowledgedInputs(long runId, long acknowledgedSequence, bool newRun)
        {
            if (newRun)
            {
                ResetInputState(false);
                return;
            }

            int acknowledgedCount = 0;
            while (acknowledgedCount < _pendingInputs.Count &&
                   _pendingInputs[acknowledgedCount].Sequence <= acknowledgedSequence)
            {
                acknowledgedCount++;
            }

            if (acknowledgedCount > 0)
            {
                _pendingInputs.RemoveRange(0, acknowledgedCount);
            }
        }

        private void CorrectPrediction(
            ExpeditionSnapshotData snapshot,
            ExpeditionSnapshotData previous,
            bool hardInvariantChanged)
        {
            float deltaX = snapshot.SquadX - PredictedSquadX;
            float deltaY = snapshot.SquadY - PredictedSquadY;
            float distanceSquared = (deltaX * deltaX) + (deltaY * deltaY);
            if (hardInvariantChanged || distanceSquared >= HardCorrectionDistance * HardCorrectionDistance)
            {
                PredictedSquadX = snapshot.SquadX;
                PredictedSquadY = snapshot.SquadY;
                return;
            }

            RemoveExpectedSnapshotLag(snapshot, previous, ref deltaX, ref deltaY);
            PredictedSquadX += deltaX * SoftCorrectionRatio;
            PredictedSquadY += deltaY * SoftCorrectionRatio;
        }

        /// <summary>
        /// 持续移动时，快照采样时刻天然早于当前渲染时刻；只豁免相邻权威快照已经证明的同向位移。
        /// 这样既不会把正常快照时龄表现为回退，也不会掩盖边界停步、服务端拒绝或累计的大偏差。
        /// </summary>
        private void RemoveExpectedSnapshotLag(
            ExpeditionSnapshotData snapshot,
            ExpeditionSnapshotData previous,
            ref float deltaX,
            ref float deltaY)
        {
            if (previous == null || !_hasCurrentIntent || (_currentDirectionX == 0 && _currentDirectionY == 0) ||
                previous.RunId != snapshot.RunId || previous.CurrentRoomId != snapshot.CurrentRoomId ||
                previous.Phase != snapshot.Phase || previous.ServerTick >= snapshot.ServerTick)
            {
                return;
            }

            float directionX = (float)_currentDirectionX / ExpeditionProtocolConstants.DirectionScale;
            float directionY = (float)_currentDirectionY / ExpeditionProtocolConstants.DirectionScale;
            float directionLengthSquared = (directionX * directionX) + (directionY * directionY);
            if (directionLengthSquared <= 0.0f)
            {
                return;
            }

            float directionLength = (float)Math.Sqrt(directionLengthSquared);
            float unitDirectionX = directionX / directionLength;
            float unitDirectionY = directionY / directionLength;
            float longitudinalDelta = (deltaX * unitDirectionX) + (deltaY * unitDirectionY);
            float authorityDeltaX = snapshot.SquadX - previous.SquadX;
            float authorityDeltaY = snapshot.SquadY - previous.SquadY;
            float authorityProgress = (authorityDeltaX * unitDirectionX) + (authorityDeltaY * unitDirectionY);
            if (longitudinalDelta >= 0.0f || authorityProgress <= 0.0f)
            {
                return;
            }

            float removableLag = Math.Min(
                Math.Min(-longitudinalDelta, authorityProgress),
                MaximumExpectedSnapshotLag);
            deltaX += unitDirectionX * removableLag;
            deltaY += unitDirectionY * removableLag;
        }

        private void ResetInputState(bool resetSequence)
        {
            _pendingInputs.Clear();
            _inputSendTimestamps.Clear();
            _currentDirectionX = 0;
            _currentDirectionY = 0;
            _hasCurrentIntent = false;
            _hasSentInput = false;
            _lastSentDirectionX = 0;
            _lastSentDirectionY = 0;
            _lastSentInteractionTargetId = 0;
            _lastInputSentAtTimestamp = 0L;
            if (resetSequence)
            {
                _nextInputSequence = 0L;
            }
        }

        private bool TryConsumeInputBudget(long nowTimestamp, bool stoppedMoving)
        {
            while (_inputSendTimestamps.Count > 0 &&
                   nowTimestamp - _inputSendTimestamps.Peek() >= InputBudgetWindowTimestampTicks)
            {
                _inputSendTimestamps.Dequeue();
            }

            // 非停步输入最多使用 28 个名额，保证正常移动状态始终留有一个即时停步名额。
            int maximumInputs = stoppedMoving
                ? MaximumInputsPerRollingWindow
                : MaximumInputsBeforeReservedStop;
            if (_inputSendTimestamps.Count >= maximumInputs)
            {
                return false;
            }

            _inputSendTimestamps.Enqueue(nowTimestamp);
            return true;
        }

        private bool IsCurrentRoleLifecycle(long lifecycleGeneration)
        {
            return Interlocked.Read(ref _roleLifecycleGeneration) == lifecycleGeneration;
        }

        private static bool HasValidPredictionRules(ExpeditionSnapshotData snapshot)
        {
            return snapshot.MoveSpeedPerSecond > 0.0f &&
                !float.IsNaN(snapshot.MoveSpeedPerSecond) &&
                !float.IsInfinity(snapshot.MoveSpeedPerSecond) &&
                snapshot.MinimumX < snapshot.MaximumX &&
                snapshot.MinimumY < snapshot.MaximumY &&
                !float.IsNaN(snapshot.MinimumX) &&
                !float.IsNaN(snapshot.MaximumX) &&
                !float.IsNaN(snapshot.MinimumY) &&
                !float.IsNaN(snapshot.MaximumY) &&
                !float.IsInfinity(snapshot.MinimumX) &&
                !float.IsInfinity(snapshot.MaximumX) &&
                !float.IsInfinity(snapshot.MinimumY) &&
                !float.IsInfinity(snapshot.MaximumY);
        }

        private static float Clamp(float value, float minimum, float maximum)
        {
            return Math.Min(maximum, Math.Max(minimum, value));
        }

        private readonly struct PendingExpeditionInput
        {
            public PendingExpeditionInput(long sequence, int directionX, int directionY)
            {
                Sequence = sequence;
                DirectionX = directionX;
                DirectionY = directionY;
            }

            public long Sequence { get; }

            public int DirectionX { get; }

            public int DirectionY { get; }
        }
    }
}
