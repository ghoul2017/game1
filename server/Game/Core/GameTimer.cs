using System;
using System.Collections.Concurrent;
using System.Threading;
using Shared.Core.Log;

namespace Shared.Core;

/// <summary>
/// 游戏定时器。回调自动在游戏主线程执行，使用者无需关心线程安全。
/// </summary>
public static class GameTimer
{
    private static readonly ILog Log = LogManager.GetLogger("GameTimer");
    private static readonly ConcurrentDictionary<long, TimerEntry> _timers = new();
    private static long _nextId;

    /// <summary>启动一个定时器</summary>
    /// <param name="intervalMs">间隔毫秒</param>
    /// <param name="callback">回调（在主线程执行）</param>
    /// <returns>句柄，用于停止</returns>
    public static GameTimerHandle Start(int intervalMs, Action callback)
    {
        var id = Interlocked.Increment(ref _nextId);
        var entry = new TimerEntry(callback);
        entry.Timer = new Timer(
            _ => OnTick(id, entry),
            null,
            intervalMs,
            intervalMs);
        _timers[id] = entry;
        return new GameTimerHandle(id);
    }

    /// <summary>停止定时器</summary>
    public static void Stop(GameTimerHandle handle)
    {
        if (_timers.TryRemove(handle.Id, out var entry))
        {
            entry.Timer.Dispose();
        }
    }

    /// <summary>停止所有定时器</summary>
    public static void StopAll()
    {
        foreach (var kvp in _timers)
        {
            kvp.Value.Timer.Dispose();
        }
        _timers.Clear();
    }

    private static void OnTick(long id, TimerEntry entry)
    {
        // 投递到游戏主线程执行
        if (GameSynchronizationContext.Instance != null)
        {
            GameSynchronizationContext.Instance.Post(_ =>
            {
                if (_timers.ContainsKey(id))
                {
                    try
                    {
                        entry.Callback();
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"GameTimer 回调异常: {ex.Message}");
                    }
                }
            }, null);
        }
        else
        {
            // 无调度上下文，直接执行
            try
            {
                entry.Callback();
            }
            catch (Exception ex)
            {
                Log.Error($"GameTimer 回调异常: {ex.Message}");
            }
        }
    }

    private class TimerEntry
    {
        public Action Callback { get; }
        public Timer Timer { get; set; } = null!;

        public TimerEntry(Action callback)
        {
            Callback = callback;
        }
    }
}

/// <summary>定时器句柄</summary>
public readonly struct GameTimerHandle
{
    internal long Id { get; }

    internal GameTimerHandle(long id)
    {
        Id = id;
    }
}
