using System;
using System.Collections.Generic;
using Shared.Common.Protos;
using UnityEngine;

namespace Client.View.Arena
{
    /// <summary>
    /// 只推进服务端录像游标并投影生命；倍速和跳过不会重新运行战斗规则。
    /// </summary>
    public sealed class ArenaReplayPlayer : MonoBehaviour
    {
        private readonly Dictionary<long, int> _healthByUnit = new Dictionary<long, int>();
        private float _accumulatedSeconds;
        private ArenaReplayAck _replay;
        private int _speed = 1;

        public long CurrentTick { get; private set; }

        public bool HasReplay => _replay != null;

        public bool HasEnded => HasReplay && CurrentTick == _replay.FinalTick;

        public int VisibleOutcome => HasEnded ? _replay.Outcome : 0;

        public void Load(ArenaReplayAck replay)
        {
            if (replay == null)
            {
                throw new ArgumentNullException(nameof(replay));
            }

            if (!string.IsNullOrEmpty(replay.ErrorMessage))
            {
                throw new ArgumentException(replay.ErrorMessage, nameof(replay));
            }

            if (replay.TicksPerSecond <= 0 || replay.FinalTick <= 0L || replay.KeyFrames.Count < 2)
            {
                throw new ArgumentException("竞技场录像结构无效", nameof(replay));
            }

            _replay = replay;
            _accumulatedSeconds = 0.0f;
            Seek(0L);
        }

        public void SetSpeed(int speed)
        {
            if (speed != 1 && speed != 4)
            {
                throw new ArgumentOutOfRangeException(nameof(speed), "竞技场录像只支持 1 倍或 4 倍");
            }

            _speed = speed;
        }

        public void SkipToEnd()
        {
            RequireReplay();
            Seek(_replay.FinalTick);
        }

        public void Seek(long targetTick)
        {
            RequireReplay();
            if (targetTick < 0L || targetTick > _replay.FinalTick)
            {
                throw new ArgumentOutOfRangeException(nameof(targetTick));
            }

            ArenaReplayKeyFrameInfo baseline = FindBaseline(targetTick);
            _healthByUnit.Clear();
            for (int index = 0; index < baseline.Units.Count; index++)
            {
                ArenaReplayUnitFrameInfo unit = baseline.Units[index];
                _healthByUnit[CreateUnitKey(unit.Side, unit.UnitId)] = unit.Health;
            }

            for (int index = 0; index < _replay.Events.Count; index++)
            {
                ArenaReplayEventInfo current = _replay.Events[index];
                if (current.Tick <= baseline.Tick || current.Tick > targetTick || current.Kind != 2)
                {
                    continue;
                }

                _healthByUnit[CreateUnitKey(current.TargetSide, current.TargetUnitId)] = current.RemainingHealth;
            }

            CurrentTick = targetTick;
        }

        public int GetHealth(int side, int unitId)
        {
            RequireReplay();
            if (!_healthByUnit.TryGetValue(CreateUnitKey(side, unitId), out int health))
            {
                throw new ArgumentOutOfRangeException(nameof(unitId), "录像中不存在该单位");
            }

            return health;
        }

        private void Update()
        {
            if (!HasReplay || HasEnded)
            {
                return;
            }

            _accumulatedSeconds += Time.unscaledDeltaTime * _speed;
            float secondsPerTick = 1.0f / _replay.TicksPerSecond;
            int pendingTicks = (int)(_accumulatedSeconds / secondsPerTick);
            if (pendingTicks <= 0)
            {
                return;
            }

            _accumulatedSeconds -= pendingTicks * secondsPerTick;
            Seek(Math.Min(_replay.FinalTick, CurrentTick + pendingTicks));
        }

        private ArenaReplayKeyFrameInfo FindBaseline(long targetTick)
        {
            ArenaReplayKeyFrameInfo result = _replay.KeyFrames[0];
            for (int index = 1; index < _replay.KeyFrames.Count; index++)
            {
                ArenaReplayKeyFrameInfo candidate = _replay.KeyFrames[index];
                if (candidate.Tick > targetTick)
                {
                    break;
                }

                result = candidate;
            }

            return result;
        }

        private void RequireReplay()
        {
            if (_replay == null)
            {
                throw new InvalidOperationException("竞技场录像尚未加载");
            }
        }

        private static long CreateUnitKey(int side, int unitId)
        {
            return ((long)side << 32) | (uint)unitId;
        }
    }
}
