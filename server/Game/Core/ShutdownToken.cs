using System;
using System.Threading;
using System.Threading.Tasks;

namespace Game.Core;

/// <summary>
/// 服务器关闭信号。封装 CancellationTokenSource + Ctrl+C 注册，
/// 供各模块通过 <see cref="Token"/> 监听关闭信号。
/// </summary>
public sealed class ShutdownToken
{
    public static readonly ShutdownToken Instance = new();
    private readonly CancellationTokenSource _cts = new();

    /// <summary>关闭令牌，模块可监听此 token 实现优雅退出</summary>
    public CancellationToken Token => _cts.Token;

    /// <summary>是否已请求关闭</summary>
    public bool IsCancellationRequested => _cts.IsCancellationRequested;

    /// <summary>注册 Console.CancelKeyPress，按 Ctrl+C 时通过主线程投递取消</summary>
    public void RegisterConsoleCancel()
    {
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            GameSynchronizationContext.Instance.Post(_ => Cancel(), null);
        };
    }

    /// <summary>触发关闭信号</summary>
    public void Cancel() => _cts.Cancel();

    /// <summary>等待关闭信号</summary>
    public Task WaitForShutdownAsync()
    {
        return Task.Delay(Timeout.Infinite, _cts.Token).ContinueWith(_ => { });
    }
}
