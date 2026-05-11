using System;
using Shared.Core.Log;

namespace Shared.Core
{
    /// <summary>
    /// 雪花算法 ID 生成器。单机模式，workerId 固定为 1。
    /// 结构：1bit符号 + 41bit时间戳(ms) + 10bit机器ID + 12bit序列号。
    /// 每毫秒可生成 4096 个 ID，可用约 69 年。
    /// </summary>
    public class IdGenerator
    {
        private static readonly ILog Log = LogManager.GetLogger<IdGenerator>();

        public static IdGenerator Instance { get; } = new();

        private const long Epoch = 1704067200000L; // 2024-01-01 00:00:00 UTC
        private const int WorkerIdBits = 10;
        private const int SequenceBits = 12;
        private const long MaxWorkerId = (1L << WorkerIdBits) - 1;
        private const long MaxSequence = (1L << SequenceBits) - 1;
        private const int WorkerIdShift = SequenceBits;
        private const int TimestampShift = WorkerIdBits + SequenceBits;

        private readonly long _workerId;
        private long _lastTimestamp = -1L;
        private long _sequence;

        public IdGenerator(long workerId = 1)
        {
            if (workerId < 0 || workerId > MaxWorkerId)
                throw new ArgumentException($"workerId must be 0-{MaxWorkerId}");
            _workerId = workerId;
            Log.Info($"IdGenerator 初始化 workerId={workerId}");
        }

        public long NextId()
        {
            lock (this)
            {
                var timestamp = CurrentMillis();

                if (timestamp < _lastTimestamp)
                {
                    // 时钟回拨：等待追上
                    Log.Warn($"时钟回拨 {_lastTimestamp - timestamp}ms，等待恢复");
                    timestamp = WaitNextMillis(_lastTimestamp);
                }

                if (timestamp == _lastTimestamp)
                {
                    _sequence = (_sequence + 1) & MaxSequence;
                    if (_sequence == 0)
                        timestamp = WaitNextMillis(_lastTimestamp);
                }
                else
                {
                    _sequence = 0;
                }

                _lastTimestamp = timestamp;

                return ((timestamp - Epoch) << TimestampShift)
                       | (_workerId << WorkerIdShift)
                       | _sequence;
            }
        }

        private static long CurrentMillis() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        private long WaitNextMillis(long lastTs)
        {
            var ts = CurrentMillis();
            while (ts <= lastTs) ts = CurrentMillis();
            return ts;
        }
    }
}