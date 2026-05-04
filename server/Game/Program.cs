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
    static async Task<int> Main(string[] args)
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
            cts.Cancel();
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
        return 0;
    }
}