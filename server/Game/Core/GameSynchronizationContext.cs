using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Shared.Core.Log;

namespace Game.Core;

/// <summary>
/// 游戏主线程同步上下文。所有游戏逻辑串行执行在专用线程上，
/// await 恢复后自动回到该线程，消除多线程竞态问题。
/// </summary>
public class GameSynchronizationContext : SynchronizationContext
{
    private static readonly ILog Log = LogManager.GetLogger<GameSynchronizationContext>();

    private readonly ConcurrentQueue<(SendOrPostCallback callback, object? state)> _queue = new();
    private readonly Thread _thread;
    private volatile bool _stopped;
    private int _pendingCount;

    /// <summary>当前是否在主线程上</summary>
    public static bool IsMainThread => Current is GameSynchronizationContext ctx && ctx._thread == Thread.CurrentThread;

    public GameSynchronizationContext()
    {
        _thread = new Thread(Loop)
        {
            Name = "Game-Main",
            IsBackground = true
        };
    }

    /// <summary>启动主线程循环</summary>
    public void Start()
    {
        SetSynchronizationContext(this);
        _thread.Start();
        Log.Info("游戏主线程已启动");
    }

    /// <summary>停止主线程循环（等待所有待处理消息执行完毕）</summary>
    public void Stop()
    {
        _stopped = true;
        _thread.Join();
        Log.Info("游戏主线程已停止");
    }

    /// <summary>向主线程投递一个异步任务</summary>
    public override void Post(SendOrPostCallback d, object? state)
    {
        if (_stopped) return;
        Interlocked.Increment(ref _pendingCount);
        _queue.Enqueue((d, state));
    }

    /// <summary>在主线程上同步执行（仅限从主线程调用，否则抛异常）</summary>
    public override void Send(SendOrPostCallback d, object? state)
    {
        if (_thread == Thread.CurrentThread)
        {
            d(state);
            return;
        }

        throw new InvalidOperationException("GameSynchronizationContext.Send 不支持跨线程同步调用，请使用 Post");
    }

    /// <summary>创建副本（.NET 约定：异步操作捕获当前上下文时调用）</summary>
    public override SynchronizationContext CreateCopy()
    {
        return this;
    }

    /// <summary>向主线程投递一个 Task，返回该 Task 以便 await</summary>
    public Task PostAsync(Func<Task> action)
    {
        var tcs = new TaskCompletionSource();
        Post(async _ =>
        {
            try
            {
                await action();
                tcs.SetResult();
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        }, null);
        return tcs.Task;
    }

    /// <summary>向主线程投递一个同步操作</summary>
    public Task PostAsync(Action action)
    {
        return PostAsync(() => { action(); return Task.CompletedTask; });
    }

    private void Loop()
    {
        SetSynchronizationContext(this);

        while (!_stopped || !_queue.IsEmpty)
        {
            if (_queue.TryDequeue(out var item))
            {
                try
                {
                    item.callback(item.state);
                }
                catch (Exception ex)
                {
                    Log.Error($"主线程消息处理异常: {ex.Message}");
                }
                Interlocked.Decrement(ref _pendingCount);
            }
            else
            {
                Thread.Sleep(1);
            }
        }
    }
}
