using System.Threading;
using System.Threading.Tasks;
using Game.Core;
using Shared.Core.ECS;
using Shared.Core.Log;

namespace Game.Config
{
    public class ConfigComponent : Component
    {
        private static readonly ILog Log = LogManager.GetLogger<ConfigComponent>();
        public static ConfigManager ConfigManager { get; private set; } = null!;

        public int Order => 20;

        public void Init(string directory)
        {
            ConfigManager = new ConfigManager();
            ConfigManager.LoadAll(directory);
            Log.Info("配置表模块初始化完成");
        }

        public Task StartAsync(CancellationToken ct) => Task.CompletedTask;

        public Task StopAsync()
        {
            ConfigManager.Dispose();
            return Task.CompletedTask;
        }
    }
}