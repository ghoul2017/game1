using Server.Config;
using Server.Core;
using Server.Core.Log;

namespace Server.Game.Fight;

/// <summary>
/// 战斗模块。注册到 ModuleRegistry，管理 FightManager 生命周期。
/// </summary>
public class FightModel : IModule
{
    private static readonly ILog Log = LogManager.GetLogger<FightModel>();

    public int Order => 60;

    public Task InitAsync()
    {
        var configModule = ModuleRegistry.Instance.GetModule<ConfigModule>()!;
        FightManager.Instance.SetConfigManager(configModule.ConfigManager);
        Log.Info("战斗模块初始化完成");
        return Task.CompletedTask;
    }

    public Task StartAsync(CancellationToken ct)
    {
        Log.Info("战斗模块启动");
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        FightManager.Instance.RemoveAll();
        Log.Info("战斗模块停止");
        return Task.CompletedTask;
    }
}
