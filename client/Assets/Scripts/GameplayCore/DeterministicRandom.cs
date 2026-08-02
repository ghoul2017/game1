using System;

namespace GameplayCore
{
    /// <summary>
    /// 使用显式种子的轻量 32 位确定性随机数生成器。
    /// </summary>
    public sealed class DeterministicRandom
    {
        private const uint ZeroSeedFallback = 0x6D2B79F5u;

        private uint _state;

        public DeterministicRandom(uint seed)
        {
            _state = seed == 0u ? ZeroSeedFallback : seed;
        }

        public uint NextUInt()
        {
            uint value = _state;
            value ^= value << 13;
            value ^= value >> 17;
            value ^= value << 5;
            _state = value;
            return value;
        }

        public int NextInt(int exclusiveMaximum)
        {
            if (exclusiveMaximum <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(exclusiveMaximum), "随机上界必须是正数");
            }

            return (int)(NextUInt() % (uint)exclusiveMaximum);
        }

        public static uint Mix(uint seed, uint value)
        {
            uint mixed = seed == 0u ? ZeroSeedFallback : seed;
            mixed ^= value + 0x9E3779B9u + (mixed << 6) + (mixed >> 2);
            mixed ^= mixed >> 16;
            mixed *= 0x7FEB352Du;
            mixed ^= mixed >> 15;
            mixed *= 0x846CA68Bu;
            mixed ^= mixed >> 16;
            return mixed == 0u ? ZeroSeedFallback : mixed;
        }
    }
}
