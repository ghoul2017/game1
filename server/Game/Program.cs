using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Game.Components.User;
using Game.Config;
using Game.Core;
using Game.Core.Network;
using Game.Database;
using Shared.Core.Log;

namespace Game;

public class Program
{
    private static readonly ILog Log = LogManager.GetLogger<Program>();

    static async Task<int> Main(string[] args)
    {
        // 安装游戏主线程同步上下文，确保所有 await 恢复后回到主线程
        GameSynchronizationContext.Instance.Start();

        // 注册 Ctrl+C 关闭信号
        ShutdownToken.Instance.RegisterConsoleCancel();
        // 将主逻辑投递到游戏主线程执行
        try
        {
            await GameSynchronizationContext.Instance.PostAsync(() => RunServerAsync(args));
        }
        finally
        {
            GameSynchronizationContext.Instance.Stop();
        }

        return 0;
    }

    /// <summary>服务端主逻辑（在游戏主线程上执行）</summary>
    private static async Task RunServerAsync(string[] args)
    {
        var json = await File.ReadAllTextAsync("appsettings.json");
        var config =
            JsonSerializer.Deserialize<AppConfig>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new Exception("appsettings.json 解析失败");

        Context.Config = config;
        LogManager.Init(config.Log);
        await DatabaseManager.Instance.Init(config);
        ConfigManager.Instance.Init(config.Tables.Directory);

        var networkComponent = Context.Server.AddComponent<NetworkComponent>();
        Context.Server.AddComponent<UserComponent>();

        var registry = ModuleRegistry.Create(config);


        try
        {
            await registry.InitAllAsync();
            await registry.StartAllAsync();

            await networkComponent.Listen(config.Server.Host, config.Server.Port);
        }
        finally
        {
            await registry.StopAllAsync();
        }
    }
}