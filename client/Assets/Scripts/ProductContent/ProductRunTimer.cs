using System;

namespace ProductContent
{
    /// <summary>
    /// 以规则 Tick 执行产品时限；首作配置为无限，第二占位产品配置为 180 秒。
    /// </summary>
    public sealed class ProductRunTimer
    {
        private readonly long _maximumTicks;
        private long _currentTick;

        public ProductRunTimer(ProductRuleProfile rules, int ticksPerSecond)
        {
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            if (ticksPerSecond <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(ticksPerSecond));
            }

            HasTimeLimit = rules.HasTimeLimit;
            TicksPerSecond = ticksPerSecond;
            _maximumTicks = HasTimeLimit
                ? checked((long)rules.MaximumRunSeconds * ticksPerSecond)
                : 0L;
        }

        public bool HasTimeLimit { get; }

        public int TicksPerSecond { get; }

        public long CurrentTick => _currentTick;

        public long MaximumTicks => _maximumTicks;

        public long RemainingTicks => HasTimeLimit ? _maximumTicks - _currentTick : -1L;

        public bool HasExpired => HasTimeLimit && _currentTick >= _maximumTicks;

        public bool Advance(int tickCount)
        {
            if (tickCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tickCount));
            }

            bool wasExpired = HasExpired;
            if (HasTimeLimit)
            {
                _currentTick = Math.Min(_maximumTicks, checked(_currentTick + tickCount));
            }
            else
            {
                _currentTick = checked(_currentTick + tickCount);
            }

            return !wasExpired && HasExpired;
        }
    }
}
