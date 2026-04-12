using Server.Core;
using Server.Core.Log;

namespace Server.Game.Map;

/// <summary>
/// 地图模块。注册到 ModuleRegistry，管理地图实例的生命周期。
/// </summary>
public class MapModel : IModule
{
    private static readonly ILog Log = LogManager.GetLogger<MapModel>();

    public int Order => 60;

    public Task InitAsync()
    {
        Log.Info("地图模块初始化");
        return Task.CompletedTask;
    }

    public Task StartAsync(CancellationToken ct)
    {
        // 启动时创建默认地图
        MapManager.Instance.InitDefaultMaps();
        Log.Info("地图模块启动 — 默认地图已创建");
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        // 停服时清理所有地图
        MapManager.Instance.RemoveAll();
        Log.Info("地图模块停止");
        return Task.CompletedTask;
    }
}
