using System;

namespace GameplayCore
{
    /// <summary>
    /// 持有远征状态，并通过固定 Tick 的 Step 入口推进状态。
    /// </summary>
    public sealed class ExpeditionSession
    {
        private readonly float _movePerTick;
        private readonly ExpeditionState _state;

        public ExpeditionSession(ExpeditionState initialState, float movePerTick)
        {
            if (initialState == null)
            {
                throw new ArgumentNullException(nameof(initialState));
            }

            if (!IsFinite(movePerTick) || movePerTick <= 0.0f)
            {
                throw new ArgumentOutOfRangeException(nameof(movePerTick), "每 Tick 移动距离必须是有限正数");
            }

            _state = initialState.Copy();
            _movePerTick = movePerTick;
        }

        public ExpeditionState State => _state;

        public void Step(MoveCommand command)
        {
            float directionX = command.DirectionX;
            float directionY = command.DirectionY;
            double lengthSquared = (double)directionX * directionX + (double)directionY * directionY;

            if (lengthSquared > 1.0d)
            {
                double inverseLength = 1.0d / Math.Sqrt(lengthSquared);
                directionX = (float)(directionX * inverseLength);
                directionY = (float)(directionY * inverseLength);
            }

            _state.Advance(directionX * _movePerTick, directionY * _movePerTick);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
