using Server.Core;
using Server.Core.Log;

namespace Server.Config;

public class ConfigModule : IModule
{
    private static readonly ILog Log = LogManager.GetLogger<ConfigModule>();

    public ConfigManager ConfigManager { get; private set; } = null!;

    public int Order => 20;

    public Task InitAsync()
    {
        ConfigManager = new ConfigManager();
        ConfigManager.LoadAll(ModuleRegistry.Config.Tables.Directory);
        Log.Info("配置表模块初始化完成");
        return Task.CompletedTask;
    }

    public Task StartAsync(CancellationToken ct) => Task.CompletedTask;

    public Task StopAsync()
    {
        ConfigManager.Dispose();
        return Task.CompletedTask;
    }
}
