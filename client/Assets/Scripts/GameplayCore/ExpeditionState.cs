using System;

namespace GameplayCore
{
    /// <summary>
    /// 保存远征会话对外只读的 Tick 与队伍根位置。
    /// </summary>
    public sealed class ExpeditionState
    {
        private long _tick;
        private float _squadX;
        private float _squadY;

        public ExpeditionState(long tick, float squadX, float squadY)
        {
            if (tick < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tick), "Tick 不能为负数");
            }

            if (!IsFinite(squadX))
            {
                throw new ArgumentOutOfRangeException(nameof(squadX), "队伍根坐标 X 必须是有限数值");
            }

            if (!IsFinite(squadY))
            {
                throw new ArgumentOutOfRangeException(nameof(squadY), "队伍根坐标 Y 必须是有限数值");
            }

            _tick = tick;
            _squadX = squadX;
            _squadY = squadY;
        }

        public long Tick => _tick;

        public float SquadX => _squadX;

        public float SquadY => _squadY;

        internal ExpeditionState Copy()
        {
            return new ExpeditionState(_tick, _squadX, _squadY);
        }

        internal void Advance(float deltaX, float deltaY)
        {
            long nextTick = checked(_tick + 1);
            float nextSquadX = _squadX + deltaX;
            float nextSquadY = _squadY + deltaY;

            if (!IsFinite(nextSquadX) || !IsFinite(nextSquadY))
            {
                throw new InvalidOperationException("移动结果超出有限浮点数范围");
            }

            _tick = nextTick;
            _squadX = nextSquadX;
            _squadY = nextSquadY;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
