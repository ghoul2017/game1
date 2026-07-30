using System;

namespace GameplayCore
{
    /// <summary>
    /// 表示单个固定 Tick 的队伍根移动方向输入。
    /// </summary>
    public readonly struct MoveCommand
    {
        public MoveCommand(float directionX, float directionY)
        {
            if (!IsFinite(directionX))
            {
                throw new ArgumentOutOfRangeException(nameof(directionX), "移动方向 X 必须是有限数值");
            }

            if (!IsFinite(directionY))
            {
                throw new ArgumentOutOfRangeException(nameof(directionY), "移动方向 Y 必须是有限数值");
            }

            DirectionX = directionX;
            DirectionY = directionY;
        }

        public float DirectionX { get; }

        public float DirectionY { get; }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
