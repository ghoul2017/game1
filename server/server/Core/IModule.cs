using System.Threading;
using System.Threading.Tasks;

namespace Server.Core;

/// <summary>
/// 模块接口。所有子系统通过实现此接口接入统一的生命周期管理。
/// </summary>
public interface IModule
{
    /// <summary>初始化顺序，值越小越先初始化</summary>
    int Order { get; }

    /// <summary>初始化（按 Order 升序执行）。可访问 ModuleRegistry 获取其他模块。</summary>
    Task InitAsync();

    /// <summary>启动（所有 Init 完成后，按 Order 升序执行）</summary>
    Task StartAsync(CancellationToken ct);

    /// <summary>停止（按 Order 降序执行）</summary>
    Task StopAsync();
}
