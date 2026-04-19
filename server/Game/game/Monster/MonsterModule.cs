using System.Threading;
using System.Threading.Tasks;
using Game.Core;
using Game.Game.Monster;
using Shared.Core.Log;

namespace Game.Game;

/// <summary>
/// 怪物模块。管理怪物系统的生命周期。
/// </summary>
public class MonsterModule : IModule
{
    private static readonly ILog Log = LogManager.GetLogger<MonsterModule>();

    public int Order => 60; // 业务模块，在 Config(20) 之后启动

    public Task InitAsync()
    {
        Log.Info("怪物模块初始化");
        return Task.CompletedTask;
    }

    public Task StartAsync(CancellationToken ct)
    {
        MonsterManager.Instance.Start();
        Log.Info("怪物模块启动");
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        MonsterManager.Instance.Stop();
        Log.Info("怪物模块停止");
        return Task.CompletedTask;
    }
}
