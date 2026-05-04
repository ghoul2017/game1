using System;
using System.IO;
using System.Text.Json;
using System.Threading;
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

    /// <summary>游戏主线程同步上下文</summary>
    private static GameSynchronizationContext? _gameCtx;

    static async Task<int> Main(string[] args)
    {
        // 安装游戏主线程同步上下文，确保所有 await 恢复后回到主线程
        _gameCtx = new GameSynchronizationContext();
        _gameCtx.Start();

        // 将主逻辑投递到游戏主线程执行
        try
        {
            await _gameCtx.PostAsync(() => RunServerAsync(args));
        }
        finally
        {
            _gameCtx.Stop();
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

        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            // 从非主线程触发关闭，投递到主线程执行
            _gameCtx!.Post(_ => cts.Cancel(), null);
        };

        try
        {
            await registry.InitAllAsync();
            await registry.StartAllAsync(cts.Token);

            await networkComponent.Listen(config.Server.Host, config.Server.Port, cts.Token);
        }
        finally
        {
            await registry.StopAllAsync();
        }
    }
}